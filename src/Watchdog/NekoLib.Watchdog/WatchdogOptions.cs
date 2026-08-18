using NekoLib.Core.Logging;
using NekoLib.Core.Telemetry;

namespace NekoLib.Watchdog
{
    /// <summary>
    /// Configuration supplied when constructing a <see cref="WatchdogRuntime"/>.
    /// The runtime validates, normalizes, and snapshots these values without
    /// mutating this object. Later changes do not affect a constructed runtime.
    /// Supplied sinks and telemetry instances remain owned by the caller.
    /// </summary>
    public sealed class WatchdogOptions
    {
        public string? TargetPath { get; set; }
        public string? WorkingDirectory { get; set; }
        public string? TargetArguments { get; set; } = "";

        /// <summary>
        /// Optional PID of an already-running first instance. The runtime attaches
        /// to it instead of launching a second process.
        /// </summary>
        public int? InitialProcessId { get; set; }

        /// <summary>
        /// One-time identity echoed by the attach handshake. Required whenever
        /// <see cref="InitialProcessId"/> is set. This is a correlation value, not
        /// an authentication secret.
        /// </summary>
        public string? AttachToken { get; set; } = "";

        public bool EnableFileLogging { get; set; } = true;
        public string? LogPath { get; set; }
        public long MaxLogBytes { get; set; } = 2 * 1024 * 1024;
        public ILogSink?[]? LogSinks { get; set; } = new ILogSink?[0];
        public ITelemetry? Telemetry { get; set; }

        public int MonitorPollMs { get; set; } = 250;
        public int RestartDelayMs { get; set; } = 1000;
        public int GracefulKillTimeoutMs { get; set; } = 1000;
        public int ForceKillTimeoutMs { get; set; } = 1000;

        /// <summary>Set to 0 to disable heartbeat logging.</summary>
        public int HeartbeatIntervalMs { get; set; } = 5000;

        public bool BringToFrontOnStartIfRunning { get; set; } = true;

        /// <summary>
        /// Registers the compatibility-default Ctrl+Alt+P/R/Q global controls.
        /// Headless and custom supervisors can opt out before constructing the
        /// runtime.
        /// </summary>
        public bool EnableHotkeys { get; set; } = true;

        public bool EnableCrashBundling { get; set; } = true;
        public string? PendingCrashRoot { get; set; }
        public string? BundleRoot { get; set; }
        public int MaxBundles { get; set; } = 10;
        public bool EnableBundleChecksums { get; set; } = true;
        public bool EnableBundleManifests { get; set; } = true;
    }
}
