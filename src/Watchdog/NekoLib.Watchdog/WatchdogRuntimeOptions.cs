using System;
using System.IO;
using NekoLib.Core.Logging;
using NekoLib.Core.Telemetry;

namespace NekoLib.Watchdog
{
    internal sealed class WatchdogRuntimeOptions
    {
        private WatchdogRuntimeOptions()
        {
        }

        public string TargetPath { get; private set; } = "";
        public string WorkingDirectory { get; private set; } = "";
        public string TargetArguments { get; private set; } = "";
        public int? InitialProcessId { get; private set; }
        public string AttachToken { get; private set; } = "";
        public string PipeName { get; private set; } = "";
        public bool EnableFileLogging { get; private set; }
        public string? LogPath { get; private set; }
        public long MaxLogBytes { get; private set; }
        public ILogSink?[] LogSinks { get; private set; } = new ILogSink?[0];
        public ITelemetry? Telemetry { get; private set; }
        public int MonitorPollMs { get; private set; }
        public int RestartDelayMs { get; private set; }
        public int GracefulKillTimeoutMs { get; private set; }
        public int ForceKillTimeoutMs { get; private set; }
        public int HeartbeatIntervalMs { get; private set; }
        public bool BringToFrontOnStartIfRunning { get; private set; }
        public bool EnableHotkeys { get; private set; }
        public bool EnableCrashBundling { get; private set; }
        public string PendingCrashRoot { get; private set; } = "";
        public string BundleRoot { get; private set; } = "";
        public int MaxBundles { get; private set; }
        public bool EnableBundleChecksums { get; private set; }
        public bool EnableBundleManifests { get; private set; }

        public static WatchdogRuntimeOptions Capture(WatchdogOptions options)
        {
            if (options == null)
                throw new ArgumentNullException(nameof(options));
            if (string.IsNullOrWhiteSpace(options.TargetPath))
                throw new InvalidOperationException("TargetPath is required.");

            if (options.InitialProcessId.HasValue)
            {
                if (options.InitialProcessId.Value < 1)
                    throw new InvalidOperationException("InitialProcessId must be positive.");
                if (string.IsNullOrWhiteSpace(options.AttachToken))
                {
                    throw new InvalidOperationException(
                        "AttachToken is required when InitialProcessId is set.");
                }
            }
            else if (!string.IsNullOrWhiteSpace(options.AttachToken))
            {
                throw new InvalidOperationException(
                    "InitialProcessId is required when AttachToken is set.");
            }

            var targetPath = Path.GetFullPath(options.TargetPath);
            if (!File.Exists(targetPath))
                throw new FileNotFoundException("Target executable not found.", targetPath);

            var workingDirectory = string.IsNullOrWhiteSpace(options.WorkingDirectory)
                ? Path.GetDirectoryName(targetPath) ?? AppDomain.CurrentDomain.BaseDirectory
                : options.WorkingDirectory;
            workingDirectory = Path.GetFullPath(workingDirectory);

            string? logPath = options.LogPath;
            if (options.EnableFileLogging && string.IsNullOrWhiteSpace(logPath))
                logPath = Path.Combine(workingDirectory, "watchdog.log");
            if (!string.IsNullOrWhiteSpace(logPath))
            {
                logPath = Path.GetFullPath(logPath);
                TryCreateDirectory(Path.GetDirectoryName(logPath));
            }

            var pendingCrashRoot = string.IsNullOrWhiteSpace(options.PendingCrashRoot)
                ? Path.Combine(workingDirectory, "crash", "pending")
                : options.PendingCrashRoot;
            var bundleRoot = string.IsNullOrWhiteSpace(options.BundleRoot)
                ? Path.Combine(workingDirectory, "crash", "bundles")
                : options.BundleRoot;
            pendingCrashRoot = Path.GetFullPath(pendingCrashRoot);
            bundleRoot = Path.GetFullPath(bundleRoot);
            TryCreateDirectory(pendingCrashRoot);
            TryCreateDirectory(bundleRoot);

            var sourceSinks = options.LogSinks;
            var sinks = sourceSinks == null
                ? new ILogSink?[0]
                : (ILogSink?[])sourceSinks.Clone();

            return new WatchdogRuntimeOptions
            {
                TargetPath = targetPath,
                WorkingDirectory = workingDirectory,
                TargetArguments = options.TargetArguments ?? "",
                InitialProcessId = options.InitialProcessId,
                AttachToken = options.AttachToken ?? "",
                PipeName = WatchdogController.ResolvePipeNameForTarget(targetPath),
                EnableFileLogging = options.EnableFileLogging,
                LogPath = logPath,
                MaxLogBytes = Math.Max(64 * 1024, options.MaxLogBytes),
                LogSinks = sinks,
                Telemetry = options.Telemetry,
                MonitorPollMs = Math.Max(50, options.MonitorPollMs),
                RestartDelayMs = Math.Max(200, options.RestartDelayMs),
                GracefulKillTimeoutMs = Math.Max(0, options.GracefulKillTimeoutMs),
                ForceKillTimeoutMs = Math.Max(100, options.ForceKillTimeoutMs),
                HeartbeatIntervalMs = options.HeartbeatIntervalMs,
                BringToFrontOnStartIfRunning = options.BringToFrontOnStartIfRunning,
                EnableHotkeys = options.EnableHotkeys,
                EnableCrashBundling = options.EnableCrashBundling,
                PendingCrashRoot = pendingCrashRoot,
                BundleRoot = bundleRoot,
                MaxBundles = options.MaxBundles,
                EnableBundleChecksums = options.EnableBundleChecksums,
                EnableBundleManifests = options.EnableBundleManifests
            };
        }

        private static void TryCreateDirectory(string? path)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(path))
                    Directory.CreateDirectory(path);
            }
            catch
            {
                // Directory availability is reported when the corresponding
                // operation runs; configuration capture must remain fail-soft.
            }
        }
    }
}
