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
                TargetPath = CmdPath,
                TargetArguments = args,
                WorkingDirectory = workingDirectory,
                // Unique name per test instance so parallel TFM runs (net481 / net9.0-windows)
                // don't compete for the same named OS kernel object.
                PipeName = "NekoLib.Watchdog.Test." + Guid.NewGuid().ToString("N"),
                RestartDelayMs = 500,
                MonitorPollMs = 50,
                HeartbeatIntervalMs = 0,
                GracefulKillTimeoutMs = 100,
                ForceKillTimeoutMs = 1000
            };
        }

        public static PipeMessage Send(string pipeName, string command, object payload = null)
        {
            using (var client = new PipeClient(new PipeClientOptions
            {
                PipeName = pipeName,
                ConnectTimeout = TimeSpan.FromSeconds(3),
                RequestTimeout = TimeSpan.FromSeconds(5)
            }))
            {
                return client.SendAsync(command, payload)
                    .GetAwaiter()
                    .GetResult();
            }
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
