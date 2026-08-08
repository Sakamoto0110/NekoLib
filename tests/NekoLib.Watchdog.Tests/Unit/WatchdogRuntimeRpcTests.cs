using System;
using System.IO;
using NekoLib.Pipes;
using NekoLib.Watchdog;
using Xunit;

namespace NekoLib.Watchdog.Tests.Unit
{
    public sealed class WatchdogRuntimeRpcTests
    {
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
                        try { return WatchdogTestUtil.Send(options.PipeName, "ping").Data.ToString().Contains("pong"); }
                        catch { return false; }
                    }));

                    Assert.True(WatchdogTestUtil.Send(options.PipeName, "status").Ok);
                    Assert.True(WatchdogTestUtil.Send(options.PipeName, "pause").Ok);
                    Assert.True(WatchdogTestUtil.Send(options.PipeName, "resume").Ok);

                    var stopped = WatchdogTestUtil.Send(options.PipeName, "stop");
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
        public void OptionsPipeName_MatchesControllerTargetResolution()
        {
            var root = NewTempRoot();
            try
            {
                var options = WatchdogTestUtil.NewOptions(root, "/c exit 0");
                options.Normalize();

                Assert.Equal(
                    WatchdogController.ResolvePipeNameForTarget(options.TargetPath),
                    options.PipeName);
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
                            options.LogPath,
                            "[child_start_failed]")));

                    var restoredTarget = Path.Combine(root, "restored-cmd.exe");
                    File.Copy(WatchdogTestUtil.CmdPath, restoredTarget);
                    File.Move(restoredTarget, options.TargetPath);

                    Assert.True(
                        WatchdogTestUtil.WaitUntil(
                            () => File.Exists(marker),
                            timeoutMs: 8000),
                        File.Exists(options.LogPath)
                            ? File.ReadAllText(options.LogPath)
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
                        try { return WatchdogTestUtil.Send(options.PipeName, "ping").Ok; }
                        catch { return false; }
                    }));

                    var history = WatchdogTestUtil.Send(options.PipeName, "log_history");
                    Assert.True(history.Ok);
                    Assert.Contains("watchdog_start", history.Data.ToString());

                    PipeMessage live = null;
                    using (var events = new PipeEventClient(options.PipeName))
                    {
                        events.OnEvent += msg =>
                        {
                            if (msg.Name == "log")
                                live = msg;
                        };
                        events.Start();

                        for (int i = 0; i < 10 && live == null; i++)
                        {
                            WatchdogTestUtil.Send(options.PipeName, i % 2 == 0 ? "pause" : "resume");
                            WatchdogTestUtil.WaitUntil(() => live != null, 300);
                        }
                    }

                    Assert.NotNull(live);
                    WatchdogTestUtil.Send(options.PipeName, "stop");
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
