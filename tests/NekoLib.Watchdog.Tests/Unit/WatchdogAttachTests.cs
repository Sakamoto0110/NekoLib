using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using NekoLib.Watchdog;
using Xunit;

namespace NekoLib.Watchdog.Tests.Unit
{
    public sealed class WatchdogAttachTests
    {
        [Fact]
        public void Start_ValidInitialPid_AttachesWithoutLaunchingSecondProcess()
        {
            var root = NewTempRoot();
            Process initial = null;
            try
            {
                var options = WatchdogTestUtil.NewOptions(
                    root,
                    "/d /c ping -n 30 127.0.0.1 > nul");
                initial = StartTarget(options, removeWatchdogEnvironment: true);
                options.InitialProcessId = initial.Id;
                options.AttachToken = "valid-attach";

                using (var runtime = new WatchdogRuntime(options))
                {
                    runtime.Start();

                    Assert.True(WaitForAttach(options));

                    var status = WatchdogTestUtil.Send(
                        options.PipeName,
                        WatchdogCommands.Status);
                    Assert.True(status.Ok);
                    Assert.Matches(
                        new Regex(
                            "\"?restartCount\"?\\s*:\\s*0",
                            RegexOptions.IgnoreCase),
                        status.Data.ToString());
                }
            }
            finally
            {
                TryKill(initial);
                TryDelete(root);
            }
        }

        [Fact]
        public void InitialProcessExit_RestartsTargetWithWatchdogEnvironment()
        {
            var root = NewTempRoot();
            Process initial = null;
            try
            {
                var marker = Path.Combine(root, "environment.txt");
                var options = WatchdogTestUtil.NewOptions(root, "");
                options.TargetArguments =
                    "/d /c echo [%NEKO_UNDER_WATCHDOG%]>>" +
                    WatchdogBootstrap.QuoteArgument(marker) +
                    " & ping -n 30 127.0.0.1 > nul";

                initial = StartTarget(options, removeWatchdogEnvironment: true);
                Assert.True(WatchdogTestUtil.WaitUntil(() => File.Exists(marker)));

                options.InitialProcessId = initial.Id;
                options.AttachToken = "restart-attach";

                using (var runtime = new WatchdogRuntime(options))
                {
                    runtime.Start();
                    Assert.True(WaitForAttach(options));

                    initial.Kill();
                    initial.WaitForExit(3000);

                    var restartedUnderWatchdog = WatchdogTestUtil.WaitUntil(
                        () => ContainsSupervisedMarker(marker),
                        timeoutMs: 8000);
                    Assert.True(
                        restartedUnderWatchdog,
                        "Marker: " + SafeRead(marker) +
                        Environment.NewLine +
                        "Watchdog log: " +
                        SafeRead(options.LogPath));

                    var status = WatchdogTestUtil.Send(
                        options.PipeName,
                        WatchdogCommands.Status);
                    Assert.Matches(
                        new Regex(
                            "\"?restartCount\"?\\s*:\\s*[1-9]",
                            RegexOptions.IgnoreCase),
                        status.Data.ToString());

                    var attachStatus = WatchdogTestUtil.Send(
                        options.PipeName,
                        WatchdogCommands.AttachStatus);
                    Assert.True(attachStatus.Ok);
                    var identity = attachStatus.Data.ToString().Trim('"');
                    Assert.EndsWith(
                        ":" + options.AttachToken,
                        identity,
                        StringComparison.Ordinal);
                    var identityParts = identity.Split(':');
                    Assert.Equal(3, identityParts.Length);
                    Assert.NotEqual(
                        initial.Id,
                        int.Parse(
                            identityParts[1],
                            System.Globalization.CultureInfo.InvariantCulture));
                }
            }
            finally
            {
                TryKill(initial);
                TryDelete(root);
            }
        }

        [Fact]
        public void InitialProcessExit_WithoutLauncherHandle_PreservesExitCodeInStatus()
        {
            var root = NewTempRoot();
            Process initial = null;
            try
            {
                var exitSignal = Path.Combine(root, "exit-initial.signal");
                var initialScript = Path.Combine(root, "initial.cmd");
                File.WriteAllLines(initialScript, new[]
                {
                    "@echo off",
                    ":wait",
                    "if exist " + WatchdogBootstrap.QuoteArgument(exitSignal) + " exit /b 17",
                    "ping -n 2 127.0.0.1 > nul",
                    "goto wait"
                });

                var options = WatchdogTestUtil.NewOptions(
                    root,
                    "/d /c " + WatchdogBootstrap.QuoteArgument(initialScript));
                initial = StartTarget(options, removeWatchdogEnvironment: true);
                int initialId = initial.Id;

                // A self-bootstrapping application has no external launcher
                // keeping its process handle alive after it exits.
                initial.Dispose();
                initial = null;

                options.InitialProcessId = initialId;
                options.AttachToken = "exit-code-attach";
                options.TargetArguments = "/d /c ping -n 30 127.0.0.1 > nul";

                using (var runtime = new WatchdogRuntime(options))
                {
                    runtime.Start();
                    Assert.True(WaitForAttach(options));

                    File.WriteAllText(exitSignal, "exit");

                    string statusText = "<status unavailable>";
                    bool exitCodeObserved = WatchdogTestUtil.WaitUntil(() =>
                    {
                        try
                        {
                            var status = WatchdogTestUtil.Send(
                                options.PipeName,
                                WatchdogCommands.Status);
                            statusText = status.Data.ToString();
                            return Regex.IsMatch(
                                statusText,
                                "\\\"?lastExitCode\\\"?\\s*:\\s*17",
                                RegexOptions.IgnoreCase);
                        }
                        catch
                        {
                            return false;
                        }
                    }, timeoutMs: 8000);

                    Assert.True(
                        exitCodeObserved,
                        "Status: " + statusText +
                        Environment.NewLine +
                        "Watchdog log: " +
                        SafeRead(options.LogPath));
                }
            }
            finally
            {
                TryKill(initial);
                TryDelete(root);
            }
        }

        [Fact]
        public void Start_InvalidInitialPid_DoesNotLaunchTargetAndReleasesSingleton()
        {
            var root = NewTempRoot();
            try
            {
                var marker = Path.Combine(root, "unexpected-launch.txt");
                var options = WatchdogTestUtil.NewOptions(
                    root,
                    "/d /c echo launched>" + WatchdogBootstrap.QuoteArgument(marker));
                options.InitialProcessId = int.MaxValue;
                options.AttachToken = "invalid-attach";

                using (var runtime = new WatchdogRuntime(options))
                {
                    Assert.Throws<InvalidOperationException>(() => runtime.Start());
                }

                Assert.False(File.Exists(marker));

                // A failed attach must release the existing per-target semaphore.
                var recovery = new WatchdogOptions
                {
                    TargetPath = options.TargetPath,
                    TargetArguments = "/d /c ping -n 30 127.0.0.1 > nul",
                    WorkingDirectory = root,
                    MonitorPollMs = 50,
                    RestartDelayMs = 500,
                    HeartbeatIntervalMs = 0
                };
                using (var runtime = new WatchdogRuntime(recovery))
                {
                    runtime.Start();
                    Assert.True(WatchdogTestUtil.WaitUntil(() =>
                    {
                        try
                        {
                            return WatchdogTestUtil.Send(
                                recovery.PipeName,
                                WatchdogCommands.Ping).Ok;
                        }
                        catch
                        {
                            return false;
                        }
                    }));
                }
            }
            finally
            {
                TryDelete(root);
            }
        }

        [Fact]
        public void Start_SecondRuntimeForSameTarget_RespectsSingleton()
        {
            var root = NewTempRoot();
            try
            {
                var firstOptions = WatchdogTestUtil.NewOptions(
                    root,
                    "/d /c ping -n 30 127.0.0.1 > nul");
                var secondOptions = new WatchdogOptions
                {
                    TargetPath = firstOptions.TargetPath,
                    TargetArguments = firstOptions.TargetArguments,
                    WorkingDirectory = root,
                    HeartbeatIntervalMs = 0
                };

                using (var first = new WatchdogRuntime(firstOptions))
                using (var second = new WatchdogRuntime(secondOptions))
                {
                    first.Start();

                    var error = Assert.Throws<InvalidOperationException>(
                        () => second.Start());
                    Assert.Contains("already running", error.Message);
                }
            }
            finally
            {
                TryDelete(root);
            }
        }

        private static Process StartTarget(
            WatchdogOptions options,
            bool removeWatchdogEnvironment)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = options.TargetPath,
                Arguments = options.TargetArguments,
                WorkingDirectory = options.WorkingDirectory,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            if (removeWatchdogEnvironment)
            {
                startInfo.EnvironmentVariables.Remove(
                    WatchdogBootstrap.UnderWatchdogEnvironmentVariable);
            }

            return Process.Start(startInfo);
        }

        private static bool WaitForAttach(WatchdogOptions options)
        {
            var expected = WatchdogBootstrap.FormatAttachmentStatus(
                options.InitialProcessId.Value,
                options.AttachToken);

            return WatchdogTestUtil.WaitUntil(() =>
            {
                try
                {
                    var response = WatchdogTestUtil.Send(
                        options.PipeName,
                        WatchdogCommands.AttachStatus);
                    return response.Ok &&
                           string.Equals(
                               response.Data.ToString().Trim('"'),
                               expected,
                               StringComparison.Ordinal);
                }
                catch
                {
                    return false;
                }
            });
        }

        private static bool ContainsSupervisedMarker(string path)
        {
            try
            {
                foreach (var line in File.ReadAllLines(path))
                {
                    if (string.Equals(line.Trim(), "[1]", StringComparison.Ordinal))
                        return true;
                }
            }
            catch
            {
            }
            return false;
        }

        private static string SafeRead(string path)
        {
            try { return File.Exists(path) ? File.ReadAllText(path) : "<missing>"; }
            catch (Exception ex) { return "<read failed: " + ex.Message + ">"; }
        }

        private static string NewTempRoot()
        {
            var root = Path.Combine(
                Path.GetTempPath(),
                "nekolib-watchdog-attach-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return root;
        }

        private static void TryKill(Process process)
        {
            try
            {
                if (process != null && !process.HasExited)
                    process.Kill();
            }
            catch
            {
            }
            finally
            {
                try { process?.Dispose(); } catch { }
            }
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (Directory.Exists(path))
                    Directory.Delete(path, true);
            }
            catch
            {
            }
        }
    }
}
