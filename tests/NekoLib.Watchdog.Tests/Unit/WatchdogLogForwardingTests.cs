using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NekoLib.Core.Logging;
using NekoLib.Pipes;
using NekoLib.Watchdog;
using Xunit;

namespace NekoLib.Watchdog.Tests.Unit
{
    public sealed class WatchdogLogForwardingTests
    {
        // ------------------------------------------------------------------
        // Buffered sink (no watchdog listening): never blocks, drops on a full
        // queue, and disposes (drains + joins) without hanging.
        // ------------------------------------------------------------------
        [Fact]
        public void BufferedSink_DropsUnderSaturation_AndDisposesCleanly()
        {
            // Tiny queue + no watchdog: the flush thread stalls on connect while we
            // flood, so the bounded queue overflows deterministically.
            var sink = new WatchdogPipeLogSink(maxQueued: 2, flushIntervalMs: 10, maxBatch: 1);
            try
            {
                for (int i = 0; i < 5000; i++)
                    sink.Write(new LogEntry(
                        DateTime.UtcNow,
                        LogLevel.Info,
                        "msg-" + i,
                        category: "test"));

                Assert.True(sink.DroppedCount > 0, "expected drops under saturation");
            }
            finally
            {
                // Must return well within the Join timeout (2s) even mid-connect.
                var start = Environment.TickCount;
                sink.Dispose();
                Assert.True(Environment.TickCount - start < 5000, "Dispose hung");
            }
        }

        // ------------------------------------------------------------------
        // log_write_batch handler: a batch of external entries is parsed,
        // buffered, and replayable via log_history.
        // ------------------------------------------------------------------
        [Fact]
        public void LogWriteBatch_IsBufferedAndReplayed()
        {
            var root = NewTempRoot();
            try
            {
                var options = WatchdogTestUtil.NewOptions(root, "/c ping -n 3 127.0.0.1 > nul");

                using (var runtime = new WatchdogRuntime(options))
                {
                    runtime.Start();
                    WaitForPing(runtime.PipeName);

                    var payload = new
                    {
                        entries = new[]
                        {
                            new { level = "Info", category = "ext", message = "batch-alpha" },
                            new { level = "Warn", category = "ext", message = "batch-beta" }
                        }
                    };

                    var res = WatchdogTestUtil.Send(runtime.PipeName, "log_write_batch", payload);
                    Assert.True(res.Ok);

                    var history = WatchdogTestUtil.Send(runtime.PipeName, "log_history").Data.ToString();
                    Assert.Contains("batch-alpha", history);
                    Assert.Contains("batch-beta", history);

                    WatchdogTestUtil.Send(runtime.PipeName, "stop");
                }
            }
            finally
            {
                TryDelete(root);
            }
        }

        // ------------------------------------------------------------------
        // Loop break: an externally-received log (log_write_batch) is logged and
        // buffered, but NOT re-forwarded to LogSinks — otherwise a
        // WatchdogPipeLogSink would push it back onto the pipe and loop.
        // ------------------------------------------------------------------
        [Fact]
        public void ExternalBatchLog_IsNotReForwardedToLogSinks()
        {
            var root = NewTempRoot();
            try
            {
                var sink = new CountingSink();
                var options = WatchdogTestUtil.NewOptions(root, "/c ping -n 3 127.0.0.1 > nul");
                options.LogSinks = new ILogSink[] { sink };

                using (var runtime = new WatchdogRuntime(options))
                {
                    runtime.Start();
                    WaitForPing(runtime.PipeName);

                    // Sanity: internal logs DO reach the sink.
                    Assert.True(
                        WatchdogTestUtil.WaitUntil(() => sink.Count > 0),
                        "sink never received internal logs");

                    var payload = new
                    {
                        entries = new[]
                        {
                            new { level = "Info", category = "ext", message = "loopy-msg" }
                        }
                    };

                    Assert.True(WatchdogTestUtil.Send(runtime.PipeName, "log_write_batch", payload).Ok);

                    // It must be logged/buffered (in history) ...
                    var history = WatchdogTestUtil.Send(runtime.PipeName, "log_history").Data.ToString();
                    Assert.Contains("loopy-msg", history);

                    // ... but never re-forwarded to the sink.
                    Assert.DoesNotContain("loopy-msg", sink.Messages());

                    WatchdogTestUtil.Send(runtime.PipeName, "stop");
                }
            }
            finally
            {
                TryDelete(root);
            }
        }

        // ------------------------------------------------------------------
        // Dropped-event counter is wired into the status telemetry.
        // ------------------------------------------------------------------
        [Fact]
        public void Status_ExposesDistinctCumulativeLossCounters()
        {
            var root = NewTempRoot();
            try
            {
                var options = WatchdogTestUtil.NewOptions(root, "/c ping -n 3 127.0.0.1 > nul");

                using (var runtime = new WatchdogRuntime(options))
                {
                    runtime.Start();
                    WaitForPing(runtime.PipeName);

                    var status = WatchdogTestUtil.Send(runtime.PipeName, "status").Data.ToString();
                    Assert.Contains("eventsDropped", status);
                    Assert.Contains("eventQueueDropped", status);
                    Assert.Contains("historyEvictions", status);
                    Assert.Contains("eventPublishFailures", status);

                    WatchdogTestUtil.Send(runtime.PipeName, "stop");
                }
            }
            finally
            {
                TryDelete(root);
            }
        }

        [Fact]
        public void LogHistoryEviction_IncrementsDedicatedCounter()
        {
            var root = NewTempRoot();
            try
            {
                var options = WatchdogTestUtil.NewOptions(
                    root,
                    "/c ping -n 30 127.0.0.1 > nul");

                using (var runtime = new WatchdogRuntime(options))
                {
                    runtime.Start();
                    WaitForPing(runtime.PipeName);
                    var entries = new List<object>();
                    for (var index = 0; index < 350; index++)
                    {
                        entries.Add(new
                        {
                            level = "Info",
                            category = "eviction",
                            message = "entry-" + index
                        });
                    }

                    Assert.True(WatchdogTestUtil.Send(
                        runtime.PipeName,
                        "log_write_batch",
                        new { entries }).Ok);

                    var status = WatchdogTestUtil.Send(
                        runtime.PipeName,
                        "status").Data.ToString();
                    Assert.Matches(
                        new Regex(
                            "\\\"?historyEvictions\\\"?\\s*:\\s*[1-9]",
                            RegexOptions.IgnoreCase),
                        status);

                    WatchdogTestUtil.Send(runtime.PipeName, "stop");
                }
            }
            finally
            {
                TryDelete(root);
            }
        }

        private static void WaitForPing(string pipeName)
        {
            Assert.True(WatchdogTestUtil.WaitUntil(() =>
            {
                try { return WatchdogTestUtil.Send(pipeName, "ping").Ok; }
                catch { return false; }
            }), "watchdog never came up");
        }

        private sealed class CountingSink : ILogSink
        {
            private readonly object _lock = new object();
            private readonly List<string> _messages = new List<string>();

            public int Count
            {
                get { lock (_lock) return _messages.Count; }
            }

            public string Messages()
            {
                lock (_lock) return string.Join("\n", _messages);
            }

            public void Write(LogEntry entry)
            {
                lock (_lock) _messages.Add(entry?.Message ?? "");
            }
        }

        private static string NewTempRoot()
        {
            var root = Path.Combine(Path.GetTempPath(), "nekolib-watchdog-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return root;
        }

        private static void TryDelete(string path)
        {
            try { if (Directory.Exists(path)) Directory.Delete(path, true); } catch { }
        }
    }
}
