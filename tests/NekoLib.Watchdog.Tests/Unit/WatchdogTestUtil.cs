using System;
using System.IO;
using System.Threading;
using NekoLib.Pipes;
using NekoLib.Watchdog;

namespace NekoLib.Watchdog.Tests.Unit
{
    internal static class WatchdogTestUtil
    {
        public static string CmdPath =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe");

        public static WatchdogOptions NewOptions(string workingDirectory, string args)
        {
            return new WatchdogOptions
            {
                // The watchdog derives its pipe + Global\ single-instance semaphore name
                // from a hash of the target path (Normalize, by design, ignores any
                // explicit PipeName). Tests all driving the same cmd.exe would therefore
                // share one OS kernel object and collide — especially since net481 and
                // net9.0-windows test runs execute as parallel processes. Give each test
                // its own private copy of cmd.exe so the derived identity is unique.
                TargetPath = MakePrivateCmd(workingDirectory),
                TargetArguments = args,
                WorkingDirectory = workingDirectory,
                RestartDelayMs = 500,
                MonitorPollMs = 50,
                HeartbeatIntervalMs = 0,
                GracefulKillTimeoutMs = 100,
                ForceKillTimeoutMs = 1000
            };
        }

        // Copies cmd.exe to a uniquely-named file under the test's working directory.
        // cmd.exe is self-contained, so the copy interprets the same "/c ..." arguments.
        private static string MakePrivateCmd(string workingDirectory)
        {
            var target = Path.Combine(
                workingDirectory, "wd-" + Guid.NewGuid().ToString("N") + ".exe");
            File.Copy(CmdPath, target);
            return target;
        }

        public static PipeMessage Send(string pipeName, string command, object payload = null)
        {
            var client = new PipeClient(new PipeClientOptions
            {
                PipeName = pipeName,
                ConnectTimeout = TimeSpan.FromSeconds(3),
                RequestTimeout = TimeSpan.FromSeconds(5)
            });
            return client.SendAsync(command, payload)
                .GetAwaiter()
                .GetResult();
        }

        public static bool WaitUntil(Func<bool> condition, int timeoutMs = 5000)
        {
            var start = Environment.TickCount;
            while (Environment.TickCount - start < timeoutMs)
            {
                if (condition())
                    return true;

                Thread.Sleep(50);
            }

            return condition();
        }
    }
}
