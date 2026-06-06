using System;
using System.IO;
using System.Reflection;
using NekoLib.Watchdog;
using Xunit;

namespace NekoLib.Watchdog.Tests.Unit
{
    public sealed class WatchdogCrashBundleTests
    {
        [Fact]
        public void ChildExit_FinalizesPendingCrashBundle()
        {
            var root = NewTempRoot();
            try
            {
                var options = WatchdogTestUtil.NewOptions(root, "/c exit 7");
                options.RestartDelayMs = 1000;

                using (var runtime = new WatchdogRuntime(options))
                {
                    var pending = Path.Combine(options.PendingCrashRoot, "crash-unit");
                    Directory.CreateDirectory(pending);
                    File.WriteAllText(Path.Combine(pending, "crash.txt"), "pending crash");

                    runtime.Start();

                    Assert.True(WatchdogTestUtil.WaitUntil(
                        () => Directory.Exists(options.BundleRoot) &&
                              Directory.GetDirectories(options.BundleRoot, "bundle-*").Length > 0,
                        6000));

                    WatchdogTestUtil.Send(options.PipeName, "stop");
                }
            }
            finally
            {
                TryDelete(root);
            }
        }

        [Fact]
        public void FileLogging_RotatesWhenMaxLogBytesIsReached()
        {
            var root = NewTempRoot();
            try
            {
                var logPath = Path.Combine(root, "watchdog.log");
                File.WriteAllText(logPath, new string('x', 70 * 1024));

                var options = WatchdogTestUtil.NewOptions(root, "/c ping -n 3 127.0.0.1 > nul");
                options.LogPath = logPath;
                options.MaxLogBytes = 64 * 1024;

                using (var runtime = new WatchdogRuntime(options))
                {
                    runtime.Start();

                    Assert.True(WatchdogTestUtil.WaitUntil(
                        () => File.Exists(logPath + ".1"),
                        5000));

                    WatchdogTestUtil.Send(options.PipeName, "stop");
                }
            }
            finally
            {
                TryDelete(root);
            }
        }

        [Fact]
        public void CrashHandlerExternalNotifier_CanNotifyWatchdog_AndFinalizeBundle()
        {
            var root = NewTempRoot();
            var old = Environment.GetEnvironmentVariable("NEKO_UNDER_WATCHDOG");

            try
            {
                Environment.SetEnvironmentVariable("NEKO_UNDER_WATCHDOG", "1");

                var options = WatchdogTestUtil.NewOptions(root, "/c ping -n 3 127.0.0.1 > nul");

                using (var runtime = new WatchdogRuntime(options))
                {
                    runtime.Start();

                    Assert.True(WatchdogTestUtil.WaitUntil(() =>
                    {
                        try { return WatchdogTestUtil.Send(options.PipeName, "ping").Ok; }
                        catch { return false; }
                    }));

                    var handler = new global::NekoLib.Diagnostics.CrashHandler(
                        new global::NekoLib.Diagnostics.CrashHandlerOptions
                        {
                            CrashRootDirectory = options.PendingCrashRoot,
                            DumpLevel = global::NekoLib.Diagnostics.CrashDumpLevel.None,
                            ExternalNotifier = e => WatchdogController.NotifyExceptionForTarget(
                                options.TargetPath,
                                e.Exception?.GetType().FullName,
                                e.Exception?.Message,
                                e.Source)
                        });

                    InvokeHandleCrash(handler);

                    Assert.True(WatchdogTestUtil.WaitUntil(
                        () => Directory.Exists(options.BundleRoot) &&
                              Directory.GetDirectories(options.BundleRoot, "bundle-*").Length > 0,
                        6000));

                    WatchdogTestUtil.Send(options.PipeName, "stop");
                }
            }
            finally
            {
                Environment.SetEnvironmentVariable("NEKO_UNDER_WATCHDOG", old);
                TryDelete(root);
            }
        }

        private static void InvokeHandleCrash(global::NekoLib.Diagnostics.CrashHandler handler)
        {
            var method = typeof(global::NekoLib.Diagnostics.CrashHandler).GetMethod(
                "HandleCrash",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.NotNull(method);

            method.Invoke(handler, new object[]
            {
                "unit-test",
                new InvalidOperationException("unit-test"),
                false
            });
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
