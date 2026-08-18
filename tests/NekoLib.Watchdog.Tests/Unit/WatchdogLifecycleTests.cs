using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace NekoLib.Watchdog.Tests.Unit
{
    public sealed class WatchdogLifecycleTests
    {
        [Fact]
        public void WaitForExit_BeforeStart_Throws()
        {
            var root = NewTempRoot();
            try
            {
                using var runtime = new WatchdogRuntime(
                    WatchdogTestUtil.NewOptions(root, "/c exit 0"));

                Assert.Throws<InvalidOperationException>(() => runtime.WaitForExit());
            }
            finally
            {
                TryDelete(root);
            }
        }

        [Fact]
        public void Stop_BeforeStart_IsTerminalAndIdempotent()
        {
            var root = NewTempRoot();
            try
            {
                var runtime = new WatchdogRuntime(
                    WatchdogTestUtil.NewOptions(root, "/c exit 0"));

                runtime.Stop();
                runtime.Stop();
                runtime.Dispose();

                Assert.Throws<InvalidOperationException>(() => runtime.Start());
                Assert.Throws<InvalidOperationException>(() => runtime.WaitForExit());
            }
            finally
            {
                TryDelete(root);
            }
        }

        [Fact]
        public async Task ConcurrentStart_AdmitsExactlyOneCaller()
        {
            var root = NewTempRoot();
            try
            {
                using var runtime = new WatchdogRuntime(
                    WatchdogTestUtil.NewOptions(root, "/c ping -n 30 127.0.0.1 > nul"));
                using var gate = new ManualResetEventSlim(false);
                var outcomes = new string[2];
                var tasks = Enumerable.Range(0, 2)
                    .Select(index => Task.Run(() =>
                    {
                        gate.Wait();
                        try
                        {
                            runtime.Start();
                            outcomes[index] = "started";
                        }
                        catch (InvalidOperationException)
                        {
                            outcomes[index] = "rejected";
                        }
                    }))
                    .ToArray();

                gate.Set();
                var allTasks = Task.WhenAll(tasks);
                var completed = await Task.WhenAny(allTasks, Task.Delay(10000));
                Assert.Same(allTasks, completed);
                await allTasks;
                Assert.Single(outcomes, outcome => outcome == "started");
                Assert.Single(outcomes, outcome => outcome == "rejected");

                runtime.Stop();
            }
            finally
            {
                TryDelete(root);
            }
        }

        [Fact]
        public async Task ConcurrentStopAndDispose_JoinOneTerminalCleanup()
        {
            var root = NewTempRoot();
            try
            {
                var runtime = new WatchdogRuntime(
                    WatchdogTestUtil.NewOptions(root, "/c ping -n 30 127.0.0.1 > nul"));
                runtime.Start();

                var tasks = new[]
                {
                    Task.Run(() => runtime.Stop()),
                    Task.Run(() => runtime.Dispose()),
                    Task.Run(() => runtime.Stop())
                };

                var allTasks = Task.WhenAll(tasks);
                var completed = await Task.WhenAny(allTasks, Task.Delay(10000));
                Assert.Same(allTasks, completed);
                await allTasks;
                Assert.False(runtime.IsMonitorThreadAlive);
                Assert.False(runtime.IsEventThreadAlive);
                Assert.False(runtime.IsHotkeyThreadAlive);
                runtime.Dispose();
            }
            finally
            {
                TryDelete(root);
            }
        }

        [Fact]
        public void Stop_DuringCrashLoopCooldown_InterruptsAndJoinsMonitor()
        {
            var root = NewTempRoot();
            try
            {
                using var runtime = new WatchdogRuntime(
                    WatchdogTestUtil.NewOptions(root, "/c exit 9"));
                runtime.Start();

                Assert.True(
                    WatchdogTestUtil.WaitUntil(
                        () => FileContains(
                            runtime.CapturedOptions.LogPath,
                            "[crash_loop] cooling 10s"),
                        10000),
                    SafeRead(runtime.CapturedOptions.LogPath));

                var stopwatch = Stopwatch.StartNew();
                runtime.Stop();
                stopwatch.Stop();

                Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(3));
                Assert.False(runtime.IsMonitorThreadAlive);
            }
            finally
            {
                TryDelete(root);
            }
        }

        [Fact]
        public void SystemTaskkillPath_IgnoresSearchPath()
        {
            var original = Environment.GetEnvironmentVariable("PATH");
            try
            {
                Environment.SetEnvironmentVariable("PATH", "C:\\untrusted-tools");

                Assert.Equal(
                    Path.Combine(Environment.SystemDirectory, "taskkill.exe"),
                    WatchdogRuntime.SystemTaskkillPath);
            }
            finally
            {
                Environment.SetEnvironmentVariable("PATH", original);
            }
        }

        private static bool FileContains(string path, string value)
        {
            try
            {
                return File.Exists(path) && File.ReadAllText(path).Contains(value);
            }
            catch
            {
                return false;
            }
        }

        private static string SafeRead(string path)
        {
            try
            {
                return File.Exists(path) ? File.ReadAllText(path) : "<missing>";
            }
            catch (Exception error)
            {
                return "<read failed: " + error.Message + ">";
            }
        }

        private static string NewTempRoot()
        {
            var root = Path.Combine(
                Path.GetTempPath(),
                "nekolib-watchdog-lifecycle-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return root;
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
