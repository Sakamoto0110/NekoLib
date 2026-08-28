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
        /// <summary>Gets or sets the required target executable path. It is normalized and must exist at runtime construction.</summary>
        public string? TargetPath { get; set; }
        /// <summary>Gets or sets the target working directory, or <c>null</c>/blank to use the target directory.</summary>
        public string? WorkingDirectory { get; set; }
        /// <summary>Gets or sets the command-line text supplied to each launched target instance.</summary>
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

        /// <summary>Gets or sets whether the runtime writes its own rolling log file. The default is <c>true</c>.</summary>
        public bool EnableFileLogging { get; set; } = true;
        /// <summary>Gets or sets the runtime log path, or <c>null</c>/blank for <c>watchdog.log</c> in the working directory.</summary>
        public string? LogPath { get; set; }
        /// <summary>Gets or sets the rolling live-file size. Values below 64 KiB are normalized to 64 KiB.</summary>
        public long MaxLogBytes { get; set; } = 2 * 1024 * 1024;
        /// <summary>Gets or sets additional caller-owned synchronous sinks. The outer array is copied; sink instances are not disposed.</summary>
        public ILogSink?[]? LogSinks { get; set; } = new ILogSink?[0];
        /// <summary>Gets or sets optional caller-owned telemetry used for Watchdog operations.</summary>
        public ITelemetry? Telemetry { get; set; }

        /// <summary>Gets or sets the monitor polling interval in milliseconds. Values below 50 are normalized to 50.</summary>
        public int MonitorPollMs { get; set; } = 250;
        /// <summary>Gets or sets the replacement-process delay in milliseconds. Values below 200 are normalized to 200.</summary>
        public int RestartDelayMs { get; set; } = 1000;
        /// <summary>Gets or sets the graceful close budget in milliseconds. Negative values are normalized to zero.</summary>
        public int GracefulKillTimeoutMs { get; set; } = 1000;
        /// <summary>Gets or sets the forced-termination helper budget in milliseconds. Values below 100 are normalized to 100.</summary>
        public int ForceKillTimeoutMs { get; set; } = 1000;

        /// <summary>Set to 0 to disable heartbeat logging.</summary>
        public int HeartbeatIntervalMs { get; set; } = 5000;

        /// <summary>Gets or sets whether a failed duplicate start best-effort activates the existing target window.</summary>
        public bool BringToFrontOnStartIfRunning { get; set; } = true;

        /// <summary>
        /// Registers the compatibility-default Ctrl+Alt+P/R/Q global controls.
        /// Headless and custom supervisors can opt out before constructing the
        /// runtime.
        /// </summary>
        public bool EnableHotkeys { get; set; } = true;

        /// <summary>Gets or sets whether pending application crash evidence is finalized into Watchdog bundles.</summary>
        public bool EnableCrashBundling { get; set; } = true;
        /// <summary>Gets or sets the pending crash root, or <c>null</c>/blank for <c>crash/pending</c> below the working directory.</summary>
        public string? PendingCrashRoot { get; set; }
        /// <summary>Gets or sets the finalized bundle root, or <c>null</c>/blank for <c>crash/bundles</c> below the working directory.</summary>
        public string? BundleRoot { get; set; }
        /// <summary>Gets or sets the retained finalized-bundle count. A non-positive value disables retention deletion.</summary>
        public int MaxBundles { get; set; } = 10;
        /// <summary>Gets or sets whether finalized bundle manifests include per-file checksums.</summary>
        public bool EnableBundleChecksums { get; set; } = true;
        /// <summary>Gets or sets whether finalization writes <c>manifest.json</c>.</summary>
        public bool EnableBundleManifests { get; set; } = true;
    }
}
