using System;
using System.IO;

namespace NekoLib.Runtime.Watchdog
{
    /// <summary> 
    /// Configuration and policy for WatchdogRuntime.
    /// 
    /// Defines:
    /// - Supervision timings
    /// - Logging behavior
    /// - Pipe control identity
    /// - Crash bundling policy
    /// - Update orchestration policy
    /// 
    /// Options are immutable at runtime and represent policy,
    /// not dynamic state.
    /// </summary>
    public sealed class WatchdogOptions
    {
        // ============================================================
        // Target Process
        // ============================================================

        public string TargetPath { get; set; }
        public string WorkingDirectory { get; set; }

        /// <summary>
        /// Named pipe identity for RPC + events.
        /// </summary>
        public string PipeName { get; set; } = "NekoLib.Watchdog";

        // ============================================================
        // Logging
        // ============================================================

        public bool EnableFileLogging { get; set; } = true;
        public string LogPath { get; set; }
        public long MaxLogBytes { get; set; } = 2 * 1024 * 1024;

        // ============================================================
        // Supervision Timings
        // ============================================================

        public int MonitorPollMs { get; set; } = 250;
        public int RestartDelayMs { get; set; } = 1000;

        public int GracefulKillTimeoutMs { get; set; } = 1000;
        public int ForceKillTimeoutMs { get; set; } = 1000;

        /// <summary>
        /// 0 disables heartbeat logging.
        /// </summary>
        public int HeartbeatIntervalMs { get; set; } = 5000;

        public bool BringToFrontOnStartIfRunning { get; set; } = true;

        // ============================================================
        // Crash Bundling
        // ============================================================

        public bool EnableCrashBundling { get; set; } = true;

        public string PendingCrashRoot { get; set; }   // default: BaseDir\crash\pending
        public string BundleRoot { get; set; }         // default: BaseDir\crash\bundles

        public int MaxBundles { get; set; } = 10;

        public bool EnableBundleChecksums { get; set; } = true;
        public bool EnableBundleManifests { get; set; } = true;

        // ============================================================
        // Update Policy (Future-Proofed)
        // ============================================================

        /// <summary>
        /// Enables update command via pipe.
        /// </summary>
        public bool EnableUpdates { get; set; } = true;

        /// <summary>
        /// Directory where staged updates are placed.
        /// If null, defaults to WorkingDirectory\updates.
        /// </summary>
        public string UpdateStagingRoot { get; set; }

        /// <summary>
        /// If true, performs atomic directory swap during update.
        /// Recommended for reliability.
        /// </summary>
        public bool UseAtomicDirectorySwap { get; set; } = true;

        /// <summary>
        /// Optional backup folder name (relative to WorkingDirectory).
        /// </summary>
        public string BackupFolderName { get; set; } = "backup";

        // ============================================================
        // Validation / Normalization
        // ============================================================

        public void Normalize()
        {
            if (string.IsNullOrWhiteSpace(TargetPath))
                throw new InvalidOperationException("TargetPath is required.");

            if (!File.Exists(TargetPath))
                throw new FileNotFoundException("Target executable not found.", TargetPath);

            if (string.IsNullOrWhiteSpace(WorkingDirectory))
                WorkingDirectory = Path.GetDirectoryName(TargetPath);

            if (string.IsNullOrWhiteSpace(PipeName))
                PipeName = "NekoLib.Watchdog";

            // Logging
            if (EnableFileLogging && string.IsNullOrWhiteSpace(LogPath))
                LogPath = Path.Combine(WorkingDirectory, "watchdog.log");

            // Crash bundling defaults
            if (string.IsNullOrWhiteSpace(PendingCrashRoot))
                PendingCrashRoot = Path.Combine(WorkingDirectory, "crash", "pending");

            if (string.IsNullOrWhiteSpace(BundleRoot))
                BundleRoot = Path.Combine(WorkingDirectory, "crash", "bundles");

            // Update defaults
            if (string.IsNullOrWhiteSpace(UpdateStagingRoot))
                UpdateStagingRoot = Path.Combine(WorkingDirectory, "updates");

            // Ensure directories exist (safe)
            TryCreateDirectory(Path.GetDirectoryName(LogPath));
            TryCreateDirectory(PendingCrashRoot);
            TryCreateDirectory(BundleRoot);
            TryCreateDirectory(UpdateStagingRoot);
        }

        private static void TryCreateDirectory(string path)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(path))
                    Directory.CreateDirectory(path);
            }
            catch
            {
                // Ignore - watchdog must not crash due to directory failure
            }
        }
    }
}
