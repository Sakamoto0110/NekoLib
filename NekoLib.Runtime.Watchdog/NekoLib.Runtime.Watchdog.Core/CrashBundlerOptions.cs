using System;

namespace NekoLib.Runtime.Watchdog
{
    public sealed class CrashBundlerOptions
    {
        public string PendingCrashRoot { get; set; }   // e.g. BaseDir\crash\pending
        public string BundleRoot { get; set; }         // e.g. BaseDir\crash\bundles

        public int MaxBundles { get; set; } = 10;

        public bool EnableManifests { get; set; } = true;

        /// <summary>
        /// If you want checksums gone permanently: delete CrashChecksums.cs from the project.
        /// Bundler will auto-skip checksums without any other edits.
        /// </summary>
        public bool EnableChecksums { get; set; } = true;

        public bool CopyWatchdogLogTail { get; set; } = true;
        public string WatchdogLogPath { get; set; }   // watchdog.log
        public int TailLines { get; set; } = 600;

        public Func<string> GetWatchdogStatus { get; set; } // hook: returns status string
        public Func<string> GetAppVersion { get; set; }     // hook if you want it
        public Func<string> GetWatchdogVersion { get; set; } // hook if you want it
    }
}
