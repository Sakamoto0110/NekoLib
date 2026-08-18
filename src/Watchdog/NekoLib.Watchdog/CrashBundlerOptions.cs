using System;

namespace NekoLib.Watchdog
{
    internal sealed class CrashBundlerOptions
    {
        public string PendingCrashRoot { get; set; } = "";
        public string BundleRoot { get; set; } = "";
        public int MaxBundles { get; set; } = 10;
        public bool EnableManifests { get; set; } = true;
        public bool EnableChecksums { get; set; } = true;
        public bool CopyWatchdogLogTail { get; set; } = true;
        public string? WatchdogLogPath { get; set; }
        public int TailLines { get; set; } = 600;
        public Func<string?>? GetWatchdogStatus { get; set; }
        public Func<string?>? GetAppVersion { get; set; }
        public Func<string?>? GetWatchdogVersion { get; set; }
    }
}
