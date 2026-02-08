using System;
using System.IO;
namespace NekoLib.Runtime.Watchdog
{
    /// <summary>
    /// Configuration and policy for WatchdogRuntime.
    /// 
    /// This class defines:
    /// - Timing behavior (polling, restart delays, kill timeouts)
    /// - Logging behavior (enable/disable, rotation)
    /// - Control plane identity (PipeName)
    /// 
    /// Options are immutable at runtime and represent policy,
    /// not dynamic state.
    /// </summary>
    public sealed class WatchdogOptions
    {
        public string TargetPath { get; set; }
        public string WorkingDirectory { get; set; }

        public string PipeName { get; set; } = "NekoLib.Watchdog";

        // Logging
        public bool EnableFileLogging { get; set; } = true;
        public string LogPath { get; set; }
        public long MaxLogBytes { get; set; } = 2 * 1024 * 1024;

        // Timings (policy)
        public int MonitorPollMs { get; set; } = 250;
        public int RestartDelayMs { get; set; } = 1000;
        public int KillTimeoutMs { get; set; } = 1000;
        public int GracefulKillTimeoutMs { get; set; } = 1000;

        public int ForceKillTimeoutMs { get; set; } = 1000;
        public int HeartbeatIntervalMs { get; set; } = 5000;   // 0 disables
        public bool BringToFrontOnStartIfRunning { get; set; } = true;

        // Crash bundling
        public bool EnableCrashBundling { get; set; } = true;
        public string PendingCrashRoot { get; set; } = null;   // default: BaseDir\crash\pending
        public string BundleRoot { get; set; } = null;         // default: BaseDir\crash\bundles
        public int MaxBundles { get; set; } = 10;
        public bool EnableBundleChecksums { get; set; } = true;
        public bool EnableBundleManifests { get; set; } = true;
        public void Normalize()
        {
            if (string.IsNullOrWhiteSpace(TargetPath))
                throw new InvalidOperationException("TargetPath is required.");

            if (string.IsNullOrWhiteSpace(WorkingDirectory))
                WorkingDirectory = Path.GetDirectoryName(TargetPath);

            if (EnableFileLogging && string.IsNullOrWhiteSpace(LogPath))
                LogPath = Path.Combine(WorkingDirectory, "watchdog.log");
            if (string.IsNullOrWhiteSpace(PendingCrashRoot))
                PendingCrashRoot = Path.Combine(WorkingDirectory, "crash", "pending");

            if (string.IsNullOrWhiteSpace(BundleRoot))
                BundleRoot = Path.Combine(WorkingDirectory, "crash", "bundles");

        }
    }
}
