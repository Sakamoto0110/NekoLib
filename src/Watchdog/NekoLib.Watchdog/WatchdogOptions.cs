using System;
using System.IO;
using NekoLib.Core.Logging;
using NekoLib.Core.Telemetry;

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
        /// Optional PID of an already-running first instance. The runtime attaches
        /// to it instead of launching a second process.
        /// </summary>
        public int? InitialProcessId { get; set; }

        /// <summary>
        /// One-time identity echoed by the attach handshake. Required whenever
        /// <see cref="InitialProcessId"/> is set.
        /// </summary>
        public string AttachToken { get; set; } = "";

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
        public ILogSink[] LogSinks { get; set; } = new ILogSink[0];
        public ITelemetry? Telemetry { get; set; }

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

            if (InitialProcessId.HasValue)
            {
                if (InitialProcessId.Value < 1)
                    throw new InvalidOperationException("InitialProcessId must be positive.");
                if (string.IsNullOrWhiteSpace(AttachToken))
                    throw new InvalidOperationException(
                        "AttachToken is required when InitialProcessId is set.");
            }
            else if (!string.IsNullOrWhiteSpace(AttachToken))
            {
                throw new InvalidOperationException(
                    "InitialProcessId is required when AttachToken is set.");
            }

            var full = Path.GetFullPath(TargetPath);
            TargetPath = full;

            PipeName = WatchdogController.ResolvePipeNameForTarget(full);
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

            if (MonitorPollMs < 50)
                MonitorPollMs = 50;

            if (GracefulKillTimeoutMs < 0)
                GracefulKillTimeoutMs = 0;

            if (ForceKillTimeoutMs < 100)
                ForceKillTimeoutMs = 100;

            if (MaxLogBytes < 64 * 1024)
                MaxLogBytes = 64 * 1024;

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
