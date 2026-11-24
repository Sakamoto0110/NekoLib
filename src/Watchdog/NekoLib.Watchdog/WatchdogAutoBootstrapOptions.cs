namespace NekoLib.Watchdog
{
    /// <summary>Selects the executable that hosts automatic supervision.</summary>
    public enum WatchdogHostingMode
    {
        /// <summary>Start the application executable in its generated headless supervisor mode.</summary>
        SameExecutable = 0,
        /// <summary>Start the directly deployed, same-version NekoLib.Watchdog.Host executable.</summary>
        Sidecar = 1
    }

    /// <summary>
    /// Startup policy for package-enabled automatic desktop supervision. Values
    /// are captured at activation; optional configuration must precede activation.
    /// </summary>
    public sealed class WatchdogAutoBootstrapOptions
    {
        /// <summary>Gets or sets supervisor hosting. The default needs no separate Host executable.</summary>
        public WatchdogHostingMode HostingMode { get; set; } = WatchdogHostingMode.SameExecutable;
        /// <summary>Gets or sets the positive total startup/attachment budget, in milliseconds.</summary>
        public int HandshakeTimeoutMs { get; set; } = 5000;
        /// <summary>
        /// Gets or sets portable runtime policy. TargetPath, TargetArguments,
        /// InitialProcessId and AttachToken are bootstrap-owned. Caller-owned
        /// LogSinks and Telemetry cannot cross the process boundary. Hotkeys
        /// default to disabled for automatic application supervision.
        /// </summary>
        public WatchdogOptions RuntimeOptions { get; set; } = new WatchdogOptions { EnableHotkeys = false };
    }
}
