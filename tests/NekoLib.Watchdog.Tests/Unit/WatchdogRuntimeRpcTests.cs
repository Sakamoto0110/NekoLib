using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NekoLib.Pipes;
using NekoLib.Watchdog;
using Xunit;

namespace NekoLib.Watchdog.Tests.Unit
{
    public sealed class WatchdogRuntimeRpcTests
    {
        [Fact]
        public async Task RequestStop_AcceptedWorkerStopsRuntime()
        {
            var root = NewTempRoot();
            try
            {
                using var runtime = new WatchdogRuntime(WatchdogTestUtil.NewOptions(root, "/c ping -n 60 127.0.0.1 > nul"));
                runtime.Start();
                Assert.Equal(WatchdogStopResult.Accepted,
                    await WatchdogController.RequestStopToPipeAsync(runtime.PipeName, WatchdogStopRequest.Budget));
                Assert.True(WatchdogTestUtil.WaitUntil(() => !runtime.IsMonitorThreadAlive));
            }
            finally { TryDelete(root); }
        }

        [Fact]
        public async Task RequestStop_WorkerObservingDeadlineFirstCannotClaim()
        {
            var receipt = WatchdogStopRequest.Parse(WatchdogStopRequest.NewToken(TimeSpan.Zero));
            Assert.False(receipt.Decision.IsCompleted);
            Assert.False(receipt.Decide(WatchdogStopResult.Accepted));
            Assert.Equal(WatchdogStopResult.Expired, await receipt.Decision);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task RequestStop_ExpiredQueuedWorkerCannotStopTarget(bool queryBeforeRelease)
        {
            var root = NewTempRoot();
            try
            {
                var work = new TaskCompletionSource<WaitCallback>(TaskCreationOptions.RunContinuationsAsynchronously);
                var options = WatchdogTestUtil.NewOptions(root, "/c ping -n 60 127.0.0.1 > nul");
                using var runtime = new WatchdogRuntime(options, callback => work.SetResult(callback));
                runtime.Start();
                Process child = null;
                Assert.True(WatchdogTestUtil.WaitUntil(() =>
                {
                    var processes = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(options.TargetPath));
                    if (processes.Length == 0) return false;
                    child = Assert.Single(processes);
                    return true;
                }));
                using var originalChild = child;
                var token = WatchdogStopRequest.NewToken(TimeSpan.FromMilliseconds(250));
                var request = runtime.RequestStop(token);
                Assert.True(work.Task.IsCompleted);
                var receipt = WatchdogStopRequest.Parse(token);
                while (Stopwatch.GetTimestamp() < receipt.Deadline)
                    await Task.Delay(20);
                if (queryBeforeRelease)
                    Assert.Equal("expired", runtime.ReadStopReceipt(token).Data.ToString());
                (await work.Task)(null);
                Assert.Equal("expired", (await request).Data.ToString());
                Assert.Equal("expired", runtime.ReadStopReceipt(token).Data.ToString());
                Assert.True(runtime.IsMonitorThreadAlive);
                Assert.False(originalChild.HasExited);
            }
            finally { TryDelete(root); }
        }

        [Fact]
        public async Task RequestStop_LostReplyQueriesSameReceiptWithoutResubmitting()
        {
            string name = "stop-test-" + Guid.NewGuid().ToString("N");
            using var rpc = new PipeServer(new PipeServerOptions { PipeName = name });
            int submissions = 0;
            string submitted = null, queried = null;
            rpc.Map(WatchdogCommands.RequestStop, (message, ct) =>
            {
                Interlocked.Increment(ref submissions);
                submitted = message.Data.ToString();
                return Task.FromResult(new PipeMessage { Ok = false, Error = new PipeError { Code = PipeErrorCodes.ConnectionClosed } });
            });
            rpc.Map(WatchdogCommands.StopReceipt, (message, ct) =>
            {
                queried = message.Data.ToString();
                return Task.FromResult(new PipeMessage {
#if NETFRAMEWORK
                    Data = Newtonsoft.Json.Linq.JToken.FromObject("accepted")
#else
                    Data = System.Text.Json.JsonSerializer.SerializeToElement("accepted")
#endif
                });
            });
            rpc.Start();
            Assert.Equal(WatchdogStopResult.Accepted,
                await WatchdogController.RequestStopToPipeAsync(name, TimeSpan.FromSeconds(10)));
            Assert.Equal(1, submissions);
            Assert.Equal(submitted, queried);
        }

        [Fact]
        public async Task RequestStop_OlderHostRejectsWithoutFallingBackToLegacyStop()
        {
            string name = "stop-test-" + Guid.NewGuid().ToString("N");
            using var rpc = new PipeServer(new PipeServerOptions { PipeName = name });
            int legacyCalls = 0;
            rpc.Map(WatchdogCommands.Stop, (_, ct) =>
            {
                Interlocked.Increment(ref legacyCalls);
                return Task.FromResult(new PipeMessage());
            });
            rpc.Start();
            Assert.Equal(WatchdogStopResult.Rejected,
                await WatchdogController.RequestStopToPipeAsync(name, TimeSpan.FromSeconds(5)));
            Assert.Equal(0, legacyCalls);
        }

        [Fact]
        public void Runtime_RespondsToControlCommands_AndStopReturns()
        {
            var root = NewTempRoot();
            try
            {
                var options = WatchdogTestUtil.NewOptions(root, "/c ping -n 3 127.0.0.1 > nul");

                using (var runtime = new WatchdogRuntime(options))
                {
                    runtime.Start();

                    Assert.Equal(
                        PipeAccessPolicy.CurrentUserOnly,
                        runtime.ControlPipeAccessPolicy);

                    Assert.True(WatchdogTestUtil.WaitUntil(() =>
                    {
                        try { return WatchdogTestUtil.Send(runtime.PipeName, "ping").Data.ToString().Contains("pong"); }
                        catch { return false; }
                    }));

                    Assert.True(WatchdogTestUtil.Send(runtime.PipeName, "status").Ok);
                    Assert.True(WatchdogTestUtil.Send(runtime.PipeName, "pause").Ok);
                    Assert.True(WatchdogTestUtil.Send(runtime.PipeName, "resume").Ok);

                    var stopped = WatchdogTestUtil.Send(runtime.PipeName, "stop");
                    Assert.True(stopped.Ok);
                    Assert.Contains("stopped", stopped.Data.ToString());
                }
            }
            finally
            {
                TryDelete(root);
            }
        }

        [Fact]
        public void RuntimePipeName_MatchesControllerTargetResolution()
        {
            var root = NewTempRoot();
            try
            {
                var options = WatchdogTestUtil.NewOptions(root, "/c exit 0");
                using var runtime = new WatchdogRuntime(options);

                Assert.Equal(
                    WatchdogController.ResolvePipeNameForTarget(options.TargetPath),
                    runtime.PipeName);
            }
            finally
            {
                TryDelete(root);
            }
        }

        [Fact]
        public void Runtime_TargetTemporarilyMissing_RetriesUntilSpawnSucceeds()
        {
            var root = NewTempRoot();
            try
            {
                var marker = Path.Combine(root, "spawn-recovered.txt");
                var options = WatchdogTestUtil.NewOptions(
                    root,
                    "/d /c echo recovered>" +
                    WatchdogBootstrap.QuoteArgument(marker) +
                    " & ping -n 30 127.0.0.1 > nul");
                options.RestartDelayMs = 200;

                using (var runtime = new WatchdogRuntime(options))
                {
                    File.Delete(options.TargetPath);
                    runtime.Start();

                    Assert.True(WatchdogTestUtil.WaitUntil(
                        () => FileContains(
                            runtime.CapturedOptions.LogPath,
                            "[child_start_failed]")));

                    var restoredTarget = Path.Combine(root, "restored-cmd.exe");
                    File.Copy(WatchdogTestUtil.CmdPath, restoredTarget);
                    File.Move(restoredTarget, options.TargetPath);

                    Assert.True(
                        WatchdogTestUtil.WaitUntil(
                            () => File.Exists(marker),
                            timeoutMs: 8000),
                        File.Exists(runtime.CapturedOptions.LogPath)
                            ? File.ReadAllText(runtime.CapturedOptions.LogPath)
                            : "Watchdog log was not created.");
                }
            }
            finally
            {
                TryDelete(root);
            }
        }

        [Fact]
        public void Runtime_ReplaysLogHistory_AndPublishesLiveLogEvents()
        {
            var root = NewTempRoot();
            try
            {
                var options = WatchdogTestUtil.NewOptions(root, "/c ping -n 3 127.0.0.1 > nul");

                using (var runtime = new WatchdogRuntime(options))
                {
                    runtime.Start();

                    Assert.True(WatchdogTestUtil.WaitUntil(() =>
                    {
                        try { return WatchdogTestUtil.Send(runtime.PipeName, "ping").Ok; }
                        catch { return false; }
                    }));

                    var history = WatchdogTestUtil.Send(runtime.PipeName, "log_history");
                    Assert.True(history.Ok);
                    Assert.Contains("watchdog_start", history.Data.ToString());

                    PipeMessage live = null;
                    using (var events = new PipeEventClient(runtime.PipeName))
                    {
                        events.OnEvent += msg =>
                        {
                            if (msg.Name == "log")
                                live = msg;
                        };
                        events.Start();

                        for (int i = 0; i < 10 && live == null; i++)
                        {
                            WatchdogTestUtil.Send(runtime.PipeName, i % 2 == 0 ? "pause" : "resume");
                            WatchdogTestUtil.WaitUntil(() => live != null, 300);
                        }
                    }

                    Assert.NotNull(live);
                    WatchdogTestUtil.Send(runtime.PipeName, "stop");
                }
            }
            finally
            {
                TryDelete(root);
            }
        }

        private static string NewTempRoot()
        {
            var root = Path.Combine(Path.GetTempPath(), "nekolib-watchdog-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return root;
        }

        private static bool FileContains(string path, string value)
        {
            try
            {
                return File.Exists(path) &&
                       File.ReadAllText(path).Contains(value);
            }
            catch (IOException)
            {
                return false;
            }
        }

        private static void TryDelete(string path)
        {
            try { if (Directory.Exists(path)) Directory.Delete(path, true); } catch { }
        }
    }
}
