using System;
using System.Diagnostics;
using System.IO;
using NekoLib.Pipes;
using Xunit;

namespace NekoLib.Watchdog.Tests.Unit
{
    public sealed class WatchdogAutoBootstrapTests
    {
        public WatchdogAutoBootstrapTests()
        {
            // The RPC client and real runtime share one test process. Framework
            // pipe accept/read adapters use pool workers; reserve capacity for
            // the synchronous client independently of fixture execution order.
            System.Threading.ThreadPool.GetMinThreads(out var workers, out var completions);
            System.Threading.ThreadPool.SetMinThreads(Math.Max(64, workers), completions);
        }

        [Fact]
        public void PortableConfiguration_IsCopiedAndRejectsBootstrapIdentity()
        {
            var options = new WatchdogAutoBootstrapOptions();
            options.RuntimeOptions.RestartDelayMs = 321;
            var copy = WatchdogSupervisorLaunch.Copy(options);
            options.RuntimeOptions.RestartDelayMs = 999;
            Assert.Equal(321, copy.RuntimeOptions.RestartDelayMs);
            Assert.Equal(WatchdogHostingMode.SameExecutable, copy.HostingMode);
            Assert.False(copy.RuntimeOptions.EnableHotkeys);
            options.RuntimeOptions.InitialProcessId = 123;
            Assert.Throws<ArgumentException>(() => WatchdogSupervisorLaunch.Copy(options));
            options.RuntimeOptions.InitialProcessId = null;
            options.RuntimeOptions.TargetArguments = "--injected";
            Assert.Throws<ArgumentException>(() => WatchdogSupervisorLaunch.Copy(options));
        }

        [Fact]
        public void PortableConfiguration_RejectsInvalidPoliciesAndProcessLocalObjects()
        {
            var options = new WatchdogAutoBootstrapOptions { HostingMode = (WatchdogHostingMode)99 };
            Assert.Throws<ArgumentOutOfRangeException>(() => WatchdogSupervisorLaunch.Copy(options));
            options.HostingMode = WatchdogHostingMode.SameExecutable;
            options.HandshakeTimeoutMs = 0;
            Assert.Throws<ArgumentOutOfRangeException>(() => WatchdogSupervisorLaunch.Copy(options));
            options.HandshakeTimeoutMs = 1000;
            options.RuntimeOptions.LogSinks = new NekoLib.Core.Logging.ILogSink[1];
            Assert.Throws<ArgumentException>(() => WatchdogSupervisorLaunch.Copy(options));
        }

        [Fact]
        public void SupervisorCannotAttachToItself_AndWarningSubscriberFailuresAreIsolated()
        {
            using var current = Process.GetCurrentProcess();
            var owned = new WatchdogSupervisorLaunch
            {
                RuntimeOptions = new WatchdogOptions { InitialProcessId = current.Id, AttachToken = "self-attach" }
            }.Write();
            string observed = null;
            Action<string> throwing = message => throw new InvalidOperationException("subscriber failure");
            Action<string> collecting = message => observed = message;
            WatchdogAutoBootstrap.Warning += throwing;
            WatchdogAutoBootstrap.Warning += collecting;
            try
            {
                Assert.True(WatchdogAutoBootstrap.TryRunSupervisor(new[] { WatchdogSupervisorLaunch.Argument, owned }, out var exit));
                Assert.Equal(1, exit);
                Assert.Contains("separate processes", observed);
                Assert.False(File.Exists(owned));
            }
            finally
            {
                WatchdogAutoBootstrap.Warning -= throwing;
                WatchdogAutoBootstrap.Warning -= collecting;
                File.Delete(owned);
            }
        }

        [Fact]
        public void ConfigurationTransfer_ConsumesOnlyOwnedGeneratedFiles()
        {
            var launch = new WatchdogSupervisorLaunch
            {
                RuntimeOptions = new WatchdogOptions { InitialProcessId = 123, AttachToken = "test-attach", RestartDelayMs = 234 }
            };
            var owned = launch.Write();
            var outside = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".json");
            try
            {
                File.WriteAllText(outside, "must remain untouched");
                Assert.Throws<InvalidOperationException>(() => WatchdogSupervisorLaunch.Read(outside));
                Assert.Equal("must remain untouched", File.ReadAllText(outside));
                var read = WatchdogSupervisorLaunch.Read(owned);
                Assert.Equal(234, read.RuntimeOptions.RestartDelayMs);
                Assert.False(File.Exists(owned));
                Assert.Throws<FileNotFoundException>(() => WatchdogSupervisorLaunch.Read(owned));
            }
            finally { File.Delete(outside); File.Delete(owned); }
        }

        [Fact]
        public void InvalidOwnedConfiguration_IsConsumedWithoutStartingRuntime()
        {
            var launch = new WatchdogSupervisorLaunch { ProtocolVersion = "unsupported" };
            var owned = launch.Write();
            try
            {
                Assert.Throws<InvalidOperationException>(() => WatchdogSupervisorLaunch.Read(owned));
                Assert.False(File.Exists(owned));
            }
            finally { File.Delete(owned); }
        }

        [Fact]
        public void Detach_PreservesLiveApplication_AndDisposeRemainsNonterminating()
        {
            var root = Path.Combine(Path.GetTempPath(), "nekolib-detach-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            Process application = null;
            try
            {
                var options = WatchdogTestUtil.NewOptions(root, "/d /c ping -n 30 127.0.0.1 > nul");
                application = Process.Start(new ProcessStartInfo(options.TargetPath, options.TargetArguments) { UseShellExecute = false, CreateNoWindow = true });
                options.InitialProcessId = application.Id;
                options.AttachToken = "detach-test";
                using var runtime = new WatchdogRuntime(options);
                runtime.Start();
                var wrong = WatchdogTestUtil.Send(runtime.PipeName, WatchdogCommands.Detach, new { pid = -1 });
                Assert.False(wrong.Ok);
                Assert.True(WatchdogTestUtil.Send(runtime.PipeName, WatchdogCommands.Ping).Ok);
                var accepted = WatchdogTestUtil.Send(runtime.PipeName, WatchdogCommands.Detach, new { pid = application.Id });
                Assert.True(accepted.Ok);
                runtime.WaitForExit();
                runtime.Dispose();
                runtime.Stop();
                Assert.False(application.HasExited);
                Assert.Throws<InvalidOperationException>(() => runtime.Start());
                // The released instance slot must permit another supervisor for the same live target.
                options.MonitorPollMs = 30000;
                using (var next = new WatchdogRuntime(options))
                {
                    next.Start();
                    System.Threading.Thread.Sleep(200); // Allow the monitor to enter its configured long wait.
                    var watch = Stopwatch.StartNew();
                    next.Detach();
                    Assert.True(watch.ElapsedMilliseconds < 2000, "A long monitor interval delayed nonterminating detachment.");
                }
                Assert.False(application.HasExited);
            }
            finally
            {
                if (application != null)
                {
                    try
                    {
                        if (!application.HasExited)
                            using (var kill = Process.Start(new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "taskkill.exe"), "/PID " + application.Id + " /T /F") { UseShellExecute = false, CreateNoWindow = true }))
                                kill.WaitForExit(5000);
                    }
                    catch { }
                    application.Dispose();
                }
                try { Directory.Delete(root, true); } catch { }
            }
        }
    }
}
