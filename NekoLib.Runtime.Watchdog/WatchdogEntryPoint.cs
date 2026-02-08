using System;
using System.Diagnostics;
using System.Linq;

namespace NekoLib.Runtime.Watchdog
{
    public static class WatchdogEntrypoint
    {
        public static WatchdogMode ResolveMode(string[] args)
        {
            if (args == null)
                return WatchdogMode.Bootstrap;

            if (args.Any(a => a == "--watchdog-host"))
                return WatchdogMode.WatchdogHost;

            if (args.Any(a => a == "--under-watchdog"))
                return WatchdogMode.UnderWatchdog;

            return WatchdogMode.Bootstrap;
        }

        public static void BootstrapIfNeeded(
            WatchdogMode mode,
            string exePath,
            string workingDirectory)
        {
            if (mode != WatchdogMode.Bootstrap)
                return;

            Process.Start(new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = "--watchdog-host",
                WorkingDirectory = workingDirectory,
                UseShellExecute = true
            });

            Environment.Exit(0);
        }
    }

    public enum WatchdogMode
    {
        Bootstrap,
        WatchdogHost,
        UnderWatchdog
    }
}
