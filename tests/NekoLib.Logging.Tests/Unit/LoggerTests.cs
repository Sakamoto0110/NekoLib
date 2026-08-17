using NekoLib.Core.Logging;
using NekoLib.Logging.Sinks;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace NekoLib.Logging.Tests.Unit
{
    public sealed class LoggerTests
    {
        [Fact]
        public void Log_BelowMinimumLevel_DoesNotDispatchOrRetain()
        {
            var sink = new RecordingSink("sink", new List<string>());
            using var logger = new Logger(LogLevel.Info, sink);

            logger.Debug("ignored");

            Assert.Empty(sink.Entries);
            Assert.Empty(logger.GetRecentEntries(10));
        }

        [Fact]
        public void Log_MultipleSinks_DispatchesInRegistrationOrder()
        {
            var order = new List<string>();
            var first = new RecordingSink("first", order);
            var second = new RecordingSink("second", order);
            using var logger = new Logger(LogLevel.Trace, first, second);

            logger.Info("accepted", "Navigation");

            Assert.Equal(new[] { "first", "second" }, order);
            Assert.Equal("Navigation", first.Entries.Single().Category);
        }

        [Fact]
        public void Log_SinkThrows_LaterSinkStillReceivesEntry()
        {
            var later = new RecordingSink("later", new List<string>());
            using var logger = new Logger(LogLevel.Trace, new ThrowingSink(), later);

            logger.Error("failure", new InvalidOperationException("boom"));

            Assert.Single(later.Entries);
        }

        [Fact]
        public void GetRecentEntries_CapacityExceeded_ReturnsNewestOldestFirst()
        {
            using var logger = new Logger(new LoggerOptions
            {
                MinimumLevel = LogLevel.Trace,
                RecentEntryCapacity = 2
            });

            logger.Info("one");
            logger.Info("two");
            logger.Info("three");

            Assert.Equal(new[] { "two", "three" },
                logger.GetRecentEntries(10).Select(x => x.Message));
        }

        [Fact]
        public void Flush_FlushableSinks_InvokesEverySink()
        {
            var first = new FlushSink();
            var second = new FlushSink();
            using var logger = new Logger(LogLevel.Trace, first, second);

            Assert.True(logger.Flush(TimeSpan.FromSeconds(1)));
            Assert.Equal(1, first.FlushCount);
            Assert.Equal(1, second.FlushCount);
        }

        [Fact]
        public void Flush_SinkExceedsBudget_ReturnsFalse()
        {
            using var sink = new BlockingFlushSink();
            using var logger = new Logger(LogLevel.Trace, sink);

            bool completedWithinBudget = logger.Flush(TimeSpan.FromMilliseconds(20));
            sink.Release();

            Assert.False(completedWithinBudget);
            Assert.True(sink.WaitForFlush(TimeSpan.FromSeconds(5)));
        }

        [Fact]
        public void RollingFileSink_Info_WritesWithoutDiagnosticsOrInspection()
        {
            var root = NewRoot();
            try
            {
                var path = Path.Combine(root, "application.log");
                var sink = CreateFileSink(path, 4096, 2);
                using var logger = new Logger(LogLevel.Info, sink);

                logger.Info("ordinary-info", "Application");

                Assert.Contains("ordinary-info", File.ReadAllText(path));
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [Fact]
        public void RollingFileSink_MaximumExceeded_RotatesAndRetainsConfiguredCount()
        {
            var root = NewRoot();
            try
            {
                var path = Path.Combine(root, "application.log");
                var sink = CreateFileSink(path, 1024, 2);
                var payload = new string('x', 700);

                for (int i = 0; i < 6; i++)
                {
                    sink.Write(new LogEntry(
                        DateTime.UtcNow,
                        LogLevel.Info,
                        i + "-" + payload));
                }

                Assert.True(File.Exists(path));
                Assert.True(File.Exists(path + ".1"));
                Assert.True(File.Exists(path + ".2"));
                Assert.False(File.Exists(path + ".3"));
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [Fact]
        public void RollingFileSink_PathIsDirectory_ThrowsWriteFailure()
        {
            var root = NewRoot();
            try
            {
                var sink = CreateFileSink(root, 4096, 2);

                Assert.ThrowsAny<Exception>(() => sink.Write(new LogEntry(
                    DateTime.UtcNow,
                    LogLevel.Info,
                    "cannot-write")));
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [Fact]
        public void Constructor_SinkArrayMutatedAfterConstruction_DoesNotRetargetPipeline()
        {
            var order = new List<string>();
            var supplied = new RecordingSink("supplied", order);
            var replacement = new RecordingSink("replacement", order);
            var sinks = new ILogSink[] { supplied };

            using var logger = new Logger(
                new LoggerOptions { MinimumLevel = LogLevel.Trace, DisposeSinks = false },
                sinks);

            sinks[0] = replacement;
            logger.Info("after the caller swapped its own array");

            Assert.Single(supplied.Entries);
            Assert.Empty(replacement.Entries);
        }

        [Fact]
        public void Constructor_NullSinkArray_AcceptsEntriesWithoutDispatching()
        {
            using var logger = new Logger(LogLevel.Trace, null);

            logger.Info("retained without any sink");

            Assert.Single(logger.GetRecentEntries(10));
        }

        [Fact]
        public void Constructor_NullSinkElements_AreIgnored()
        {
            var order = new List<string>();
            var real = new RecordingSink("real", order);

            using var logger = new Logger(
                new LoggerOptions { MinimumLevel = LogLevel.Trace },
                new ILogSink[] { null, real, null });

            logger.Info("dispatched past the null elements");

            Assert.Single(real.Entries);
            Assert.True(logger.Flush(TimeSpan.FromSeconds(1)));
        }

        [Fact]
        public void LoggerOptions_Defaults_AreTheSupportedContract()
        {
            var options = new LoggerOptions();

            Assert.Equal(LogLevel.Info, options.MinimumLevel);
            Assert.Equal(1024, options.RecentEntryCapacity);
            Assert.True(options.DisposeSinks);
        }

        [Fact]
        public void Constructor_RecentEntryCapacityBelowOne_Throws()
            => Assert.Throws<ArgumentOutOfRangeException>(
                () => new Logger(new LoggerOptions { RecentEntryCapacity = 0 }));

        [Fact]
        public void Flush_SinkThrows_LaterSinkStillFlushedAndResultIsFalse()
        {
            var failing = new ThrowingFlushSink();
            var later = new FlushSink();
            using var logger = new Logger(LogLevel.Trace, failing, later);

            bool confirmed = logger.Flush(TimeSpan.FromSeconds(5));

            Assert.False(confirmed);
            Assert.Equal(1, failing.FlushCount);
            Assert.Equal(1, later.FlushCount);
        }

        [Fact]
        public void Flush_FirstSinkExhaustsBudget_DoesNotAdmitLaterSink()
        {
            using var blocking = new BlockingFlushSink();
            var later = new FlushSink();
            using var logger = new Logger(
                new LoggerOptions { MinimumLevel = LogLevel.Trace, DisposeSinks = false },
                blocking,
                later);

            bool confirmed = logger.Flush(TimeSpan.FromMilliseconds(20));
            blocking.Release();

            Assert.False(confirmed);
            Assert.True(blocking.WaitForFlush(TimeSpan.FromSeconds(5)));
            Assert.Equal(0, later.FlushCount);
        }

        [Fact]
        public void Flush_NegativeTimeout_Throws()
        {
            using var logger = new Logger(LogLevel.Trace);

            Assert.Throws<ArgumentOutOfRangeException>(
                () => logger.Flush(Timeout.InfiniteTimeSpan));
        }

        [Fact]
        public void Flush_AfterDispose_ReturnsTrueWithoutTouchingDisposedSinks()
        {
            var sink = new OwnershipSink();
            var logger = new Logger(
                new LoggerOptions { MinimumLevel = LogLevel.Trace, DisposeSinks = true },
                sink);

            logger.Dispose();
            int flushesDuringDispose = sink.FlushCount;

            Assert.True(logger.Flush(TimeSpan.FromSeconds(1)));
            Assert.Equal(flushesDuringDispose, sink.FlushCount);
        }

        [Fact]
        public async Task Flush_DisposeInProgress_RespectsBudgetThenConfirmsCompletion()
        {
            using var sink = new CoordinatedDisposeSink();
            var logger = new Logger(
                new LoggerOptions { MinimumLevel = LogLevel.Trace, DisposeSinks = false },
                sink);
            var disposeTask = Task.Run((Action)logger.Dispose);

            try
            {
                Assert.True(sink.WaitForFlushStart(TimeSpan.FromSeconds(5)));
                Assert.False(logger.Flush(TimeSpan.FromMilliseconds(20)));
            }
            finally
            {
                sink.Release();
            }

            var completed = await Task.WhenAny(
                disposeTask,
                Task.Delay(TimeSpan.FromSeconds(5)));
            Assert.Same(disposeTask, completed);
            await disposeTask;
            Assert.True(logger.Flush(TimeSpan.FromSeconds(1)));
            Assert.Equal(1, sink.FlushCount);
        }

        [Fact]
        public void Flush_AbandonedSinkFailsAfterBudget_IsNotReportedAsUnobserved()
        {
            const string sentinel = "nekolib-abandoned-flush-sentinel";
            int captured = 0;

            EventHandler<UnobservedTaskExceptionEventArgs> handler = (sender, args) =>
            {
                if (args.Exception != null &&
                    args.Exception.ToString().IndexOf(sentinel, StringComparison.Ordinal) >= 0)
                {
                    Interlocked.Increment(ref captured);
                    args.SetObserved();
                }
            };

            TaskScheduler.UnobservedTaskException += handler;
            try
            {
                using (var sink = new BlockingThenThrowingFlushSink(sentinel))
                {
                    using (var logger = new Logger(
                        new LoggerOptions { MinimumLevel = LogLevel.Trace, DisposeSinks = false },
                        sink))
                    {
                        Assert.False(logger.Flush(TimeSpan.FromMilliseconds(50)));

                        sink.Release();
                        Assert.True(sink.WaitForFlush(TimeSpan.FromSeconds(5)));
                    }
                }

                // The abandoned task reports its failure through the finalizer, so
                // the fault has to be given a chance to surface before it is
                // declared absent.
                for (int i = 0; i < 4; i++)
                {
                    Thread.Sleep(50);
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                }

                Assert.Equal(0, Volatile.Read(ref captured));
            }
            finally
            {
                TaskScheduler.UnobservedTaskException -= handler;
            }
        }

        [Fact]
        public void GetRecentEntries_AfterDispose_StillReturnsRetainedEntries()
        {
            var logger = new Logger(LogLevel.Trace);
            logger.Info("written before shutdown");

            logger.Dispose();

            Assert.Equal("written before shutdown", logger.GetRecentEntries(10).Single().Message);
        }

        [Fact]
        public void Log_AfterDispose_IsInert()
        {
            var sink = new OwnershipSink();
            var logger = new Logger(
                new LoggerOptions { MinimumLevel = LogLevel.Trace, DisposeSinks = false },
                sink);

            logger.Dispose();
            logger.Info("written after shutdown");

            Assert.Equal(0, sink.WriteCount);
            Assert.Empty(logger.GetRecentEntries(10));
        }

        [Fact]
        public void Dispose_DisposeSinksEnabled_FlushesThenDisposesSinks()
        {
            var sink = new OwnershipSink();

            using (new Logger(
                new LoggerOptions { MinimumLevel = LogLevel.Trace, DisposeSinks = true },
                sink))
            {
            }

            Assert.Equal(1, sink.FlushCount);
            Assert.Equal(1, sink.DisposeCount);
            Assert.Equal(new[] { "flush", "dispose" }, sink.Calls);
        }

        [Fact]
        public void Dispose_DisposeSinksDisabled_FlushesButDoesNotDisposeSinks()
        {
            var sink = new OwnershipSink();

            using (new Logger(
                new LoggerOptions { MinimumLevel = LogLevel.Trace, DisposeSinks = false },
                sink))
            {
            }

            Assert.Equal(1, sink.FlushCount);
            Assert.Equal(0, sink.DisposeCount);
        }

        [Fact]
        public void Dispose_CalledTwice_FlushesAndDisposesOnce()
        {
            var sink = new ThrowingOwnershipSink();
            var logger = new Logger(
                new LoggerOptions { MinimumLevel = LogLevel.Trace, DisposeSinks = true },
                sink);

            logger.Dispose();
            logger.Dispose();

            Assert.Equal(1, sink.FlushCount);
            Assert.Equal(1, sink.DisposeCount);
        }

        [Fact]
        public void DebugLogSink_Write_ReachesTheTraceChannelInEveryConfiguration()
        {
            var listener = new CapturingTraceListener();
            Trace.Listeners.Add(listener);
            try
            {
                new DebugLogSink().Write(new LogEntry(
                    DateTime.UtcNow,
                    LogLevel.Error,
                    "debug-sink-must-not-be-compiled-out"));
            }
            finally
            {
                Trace.Listeners.Remove(listener);
            }

            Assert.Contains(
                listener.Lines,
                line => line.Contains("debug-sink-must-not-be-compiled-out"));
        }

        [Fact]
        public void DebugLogSink_NullEntry_Throws()
            => Assert.Throws<ArgumentNullException>(() => new DebugLogSink().Write(null));

        [Fact]
        public void RollingFileSink_RetainedFileCountOne_KeepsExactlyOneArchive()
        {
            var root = NewRoot();
            try
            {
                var path = Path.Combine(root, "application.log");
                var sink = CreateFileSink(path, 1024, 1);
                var payload = new string('x', 700);

                for (int i = 0; i < 6; i++)
                {
                    sink.Write(new LogEntry(
                        DateTime.UtcNow,
                        LogLevel.Info,
                        i + "-" + payload));
                }

                Assert.True(File.Exists(path));
                Assert.True(File.Exists(path + ".1"));
                Assert.False(File.Exists(path + ".2"));
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [Fact]
        public void RollingFileSinkOptions_Defaults_AreTheSupportedContract()
        {
            var options = new RollingFileLogSinkOptions();

            Assert.Equal(string.Empty, options.FilePath);
            Assert.Equal(4 * 1024 * 1024, options.MaximumFileBytes);
            Assert.Equal(4, options.RetainedFileCount);
            Assert.Empty(options.Encoding.GetPreamble());
        }

        [Fact]
        public void RollingFileSink_InvalidOptions_AreRejectedAtConstruction()
        {
            Assert.Throws<ArgumentNullException>(() => new RollingFileLogSink(null));

            Assert.Throws<ArgumentException>(() => new RollingFileLogSink(
                new RollingFileLogSinkOptions { FilePath = "   " }));

            Assert.Throws<ArgumentOutOfRangeException>(() => new RollingFileLogSink(
                new RollingFileLogSinkOptions { FilePath = "a.log", MaximumFileBytes = 1023 }));

            Assert.Throws<ArgumentOutOfRangeException>(() => new RollingFileLogSink(
                new RollingFileLogSinkOptions { FilePath = "a.log", RetainedFileCount = 0 }));

            Assert.Throws<ArgumentNullException>(() => new RollingFileLogSink(
                new RollingFileLogSinkOptions { FilePath = "a.log", Encoding = null }));
        }

        private static RollingFileLogSink CreateFileSink(
            string path,
            long maximumBytes,
            int retainedFiles)
            => new RollingFileLogSink(new RollingFileLogSinkOptions
            {
                FilePath = path,
                MaximumFileBytes = maximumBytes,
                RetainedFileCount = retainedFiles
            });

        private static string NewRoot()
        {
            var root = Path.Combine(
                Path.GetTempPath(),
                "nekolib-logging-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return root;
        }

        private static void DeleteRoot(string root)
        {
            try { if (Directory.Exists(root)) Directory.Delete(root, true); }
            catch { }
        }

        private sealed class RecordingSink : ILogSink
        {
            private readonly string _name;
            private readonly List<string> _order;

            public RecordingSink(string name, List<string> order)
            {
                _name = name;
                _order = order;
            }

            public List<LogEntry> Entries { get; } = new List<LogEntry>();

            public void Write(LogEntry entry)
            {
                _order.Add(_name);
                Entries.Add(entry);
            }
        }

        private sealed class ThrowingSink : ILogSink
        {
            public void Write(LogEntry entry) => throw new InvalidOperationException("sink");
        }

        private sealed class FlushSink : IFlushableLogSink
        {
            public int FlushCount { get; private set; }
            public void Write(LogEntry entry) { }
            public void Flush() => FlushCount++;
        }

        private sealed class ThrowingFlushSink : IFlushableLogSink
        {
            public int FlushCount { get; private set; }
            public void Write(LogEntry entry) { }
            public void Flush()
            {
                FlushCount++;
                throw new InvalidOperationException("flush");
            }
        }

        private sealed class OwnershipSink : IFlushableLogSink, IDisposable
        {
            public List<string> Calls { get; } = new List<string>();
            public int WriteCount { get; private set; }
            public int FlushCount { get; private set; }
            public int DisposeCount { get; private set; }

            public void Write(LogEntry entry) => WriteCount++;

            public void Flush()
            {
                FlushCount++;
                Calls.Add("flush");
            }

            public void Dispose()
            {
                DisposeCount++;
                Calls.Add("dispose");
            }
        }

        private sealed class ThrowingOwnershipSink : IFlushableLogSink, IDisposable
        {
            public int FlushCount { get; private set; }
            public int DisposeCount { get; private set; }

            public void Write(LogEntry entry) { }

            public void Flush()
            {
                FlushCount++;
                throw new InvalidOperationException("flush");
            }

            public void Dispose()
            {
                DisposeCount++;
                throw new InvalidOperationException("dispose");
            }
        }

        private sealed class BlockingThenThrowingFlushSink : IFlushableLogSink, IDisposable
        {
            private readonly ManualResetEventSlim _release = new ManualResetEventSlim(false);
            private readonly ManualResetEventSlim _flushReached = new ManualResetEventSlim(false);
            private readonly string _sentinel;

            public BlockingThenThrowingFlushSink(string sentinel) => _sentinel = sentinel;

            public void Write(LogEntry entry) { }

            public void Flush()
            {
                _release.Wait();
                _flushReached.Set();
                throw new InvalidOperationException(_sentinel);
            }

            public void Release() => _release.Set();

            public bool WaitForFlush(TimeSpan timeout) => _flushReached.Wait(timeout);

            public void Dispose()
            {
                _flushReached.Dispose();
                _release.Dispose();
            }
        }

        private sealed class CapturingTraceListener : TraceListener
        {
            public List<string> Lines { get; } = new List<string>();

            public override void Write(string message) => Lines.Add(message);

            public override void WriteLine(string message) => Lines.Add(message);
        }

        private sealed class CoordinatedDisposeSink : IFlushableLogSink, IDisposable
        {
            private readonly ManualResetEventSlim _flushStarted = new ManualResetEventSlim(false);
            private readonly ManualResetEventSlim _release = new ManualResetEventSlim(false);
            private int _flushCount;

            public int FlushCount => Volatile.Read(ref _flushCount);

            public void Write(LogEntry entry) { }

            public void Flush()
            {
                Interlocked.Increment(ref _flushCount);
                _flushStarted.Set();
                _release.Wait();
            }

            public bool WaitForFlushStart(TimeSpan timeout) => _flushStarted.Wait(timeout);

            public void Release() => _release.Set();

            public void Dispose()
            {
                _flushStarted.Dispose();
                _release.Dispose();
            }
        }

        private sealed class BlockingFlushSink : IFlushableLogSink, IDisposable
        {
            private readonly ManualResetEventSlim _release = new ManualResetEventSlim(false);
            private readonly ManualResetEventSlim _flushCompleted = new ManualResetEventSlim(false);

            public void Write(LogEntry entry) { }

            public void Flush()
            {
                _release.Wait();
                _flushCompleted.Set();
            }

            public void Release() => _release.Set();

            public bool WaitForFlush(TimeSpan timeout) => _flushCompleted.Wait(timeout);

            public void Dispose()
            {
                _flushCompleted.Dispose();
                _release.Dispose();
            }
        }
    }
}
