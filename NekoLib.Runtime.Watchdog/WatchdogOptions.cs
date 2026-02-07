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
        public void Normalize()
        {
            if (string.IsNullOrWhiteSpace(TargetPath))
                throw new InvalidOperationException("TargetPath is required.");

            if (string.IsNullOrWhiteSpace(WorkingDirectory))
                WorkingDirectory = Path.GetDirectoryName(TargetPath);

            if (EnableFileLogging && string.IsNullOrWhiteSpace(LogPath))
                LogPath = Path.Combine(WorkingDirectory, "watchdog.log");
        }
    }
}
