using System;
using System.IO;

namespace NekoLib.Watchdog
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
        public string TargetArguments { get; set; } = "";

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

        /// <summary>
        /// Time to wait for the forced <c>taskkill /T /F</c> tree-kill to complete
        /// after a graceful close times out. Default preserves the historical 5 s wait.
        /// </summary>
        public int ForceKillTimeoutMs { get; set; } = 5000;

        /// <summary>
        /// 0 disables heartbeat logging.
        /// </summary>
        public int HeartbeatIntervalMs { get; set; } = 5000;

        public bool BringToFrontOnStartIfRunning { get; set; } = true;

        // ============================================================
        // Control Hotkeys (global)
        // ============================================================

        /// <summary>Master switch for the global control hotkeys.</summary>
        public bool EnableHotkeys { get; set; } = true;

        /// <summary>Pauses supervision (default Ctrl+Alt+P).</summary>
        public WatchdogHotkey PauseHotkey { get; set; } = WatchdogHotkey.CtrlAlt(WatchdogHotkeys.VK_P);

        /// <summary>Resumes supervision (default Ctrl+Alt+R).</summary>
        public WatchdogHotkey ResumeHotkey { get; set; } = WatchdogHotkey.CtrlAlt(WatchdogHotkeys.VK_R);

        /// <summary>Stops the watchdog (default Ctrl+Alt+Q).</summary>
        public WatchdogHotkey StopHotkey { get; set; } = WatchdogHotkey.CtrlAlt(WatchdogHotkeys.VK_Q);

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

            var full = Path.GetFullPath(TargetPath);
            TargetPath = full;

            var targetId = ComputeTargetId(full);

            PipeName = $"NekoLib.Watchdog.{targetId}";
            if (!File.Exists(TargetPath))
                throw new FileNotFoundException("Target executable not found.", TargetPath);

            if (string.IsNullOrWhiteSpace(WorkingDirectory))
                WorkingDirectory = Path.GetDirectoryName(TargetPath)
                    ?? AppDomain.CurrentDomain.BaseDirectory;

            WorkingDirectory = Path.GetFullPath(WorkingDirectory);

            if (string.IsNullOrWhiteSpace(PipeName))
            {
                var exeName = Path.GetFileNameWithoutExtension(TargetPath);
                PipeName = $"NekoLib.Watchdog.{exeName}";
            }

            if (RestartDelayMs < 200)
                RestartDelayMs = 200;

            // Logging
            if (EnableFileLogging && string.IsNullOrWhiteSpace(LogPath))
                LogPath = Path.Combine(WorkingDirectory, "watchdog.log");

            if (!string.IsNullOrWhiteSpace(LogPath))
            {
                LogPath = Path.GetFullPath(LogPath);
                var logDir = Path.GetDirectoryName(LogPath);
                if (!string.IsNullOrWhiteSpace(logDir))
                    TryCreateDirectory(logDir);
            }

            // Crash bundling defaults
            if (string.IsNullOrWhiteSpace(PendingCrashRoot))
                PendingCrashRoot = Path.Combine(WorkingDirectory, "crash", "pending");

            if (string.IsNullOrWhiteSpace(BundleRoot))
                BundleRoot = Path.Combine(WorkingDirectory, "crash", "bundles");

            if (string.IsNullOrWhiteSpace(UpdateStagingRoot))
                UpdateStagingRoot = Path.Combine(WorkingDirectory, "updates");

            PendingCrashRoot = Path.GetFullPath(PendingCrashRoot);
            BundleRoot = Path.GetFullPath(BundleRoot);
            UpdateStagingRoot = Path.GetFullPath(UpdateStagingRoot);

            TryCreateDirectory(PendingCrashRoot);
            TryCreateDirectory(BundleRoot);
            TryCreateDirectory(UpdateStagingRoot);
        }
        private static string ComputeTargetId(string fullPath)
        {
            using (var sha1 = System.Security.Cryptography.SHA1.Create())
            {
                var bytes = System.Text.Encoding.UTF8.GetBytes(fullPath.ToLowerInvariant());
                var hash = sha1.ComputeHash(bytes);

                return BitConverter.ToString(hash)
                    .Replace("-", "")
                    .Substring(0, 16); // shorter but still safe}
                ;
            }
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
