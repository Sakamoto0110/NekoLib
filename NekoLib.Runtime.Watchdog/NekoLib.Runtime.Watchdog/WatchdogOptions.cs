using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace NekoLib.Runtime.Watchdog
{
    /// <summary>
    /// Options for <see cref="WatchdogRuntime"/>.
    /// Designed for .NET Framework 4.8.1 compatibility.
    /// </summary>
    public sealed class WatchdogOptions
    {
        /// <summary>Full path of the target executable to monitor.</summary>
        public string TargetPath = "";

        /// <summary>Extra arguments passed to the target executable (watchdog will append --under-watchdog).</summary>
        public string TargetArgs = "";

        /// <summary>Working directory for the target process. If empty, defaults to target directory.</summary>
        public string WorkDir = "";

        /// <summary>Base delay between restarts (ms).</summary>
        public int RestartDelayMs = 3000;

        /// <summary>Backoff increment between repeated start failures (ms).</summary>
        public int BackoffStepMs = 60000;

        /// <summary>Maximum backoff (ms).</summary>
        public int MaxBackoffMs = 300000;

        /// <summary>
        /// Instance tag used for mutex and derived resources. If empty, computed from TargetPath.
        /// Example: WDG::MyApp::A1B2C3D4
        /// </summary>
        public string InstanceTag = "";

        /// <summary>Optional PID to kill on start (attach/claim behavior).</summary>
        public int AttachPid = 0;

        /// <summary>
        /// Directory used to store watchdog flag files + watchdog.log.
        /// If empty, defaults to the current process executable directory.
        /// </summary>
        public string FlagsDirectory = "";

        /// <summary>
        /// Registers global hotkeys. If false, no hotkeys are registered and no message loop thread is started.
        /// </summary>
        public bool EnableHotkeys = true;

        /// <summary>
        /// Validates and fills defaults (mutates this instance) for convenience.
        /// Throws if TargetPath is missing.
        /// </summary>
        public void NormalizeAndFillDefaults()
        {
            if(string.IsNullOrWhiteSpace(TargetPath))
                throw new ArgumentException("TargetPath is required.", nameof(TargetPath));

            TargetPath = Unquote(TargetPath);
            TargetArgs = Unquote(TargetArgs);
            WorkDir = Unquote(WorkDir);
            InstanceTag = Unquote(InstanceTag);
            FlagsDirectory = Unquote(FlagsDirectory);

            if(string.IsNullOrWhiteSpace(WorkDir))
                WorkDir = Path.GetDirectoryName(TargetPath) ?? GetProcessDir();

            if(string.IsNullOrWhiteSpace(FlagsDirectory))
                FlagsDirectory = GetProcessDir();

            if(string.IsNullOrWhiteSpace(InstanceTag))
                InstanceTag = ComputeInstanceTag(TargetPath);

            RestartDelayMs = Math.Max(0, RestartDelayMs);
            BackoffStepMs = Math.Max(0, BackoffStepMs);
            MaxBackoffMs = Math.Max(0, MaxBackoffMs);
        }

        /// <summary>Computes a deterministic instance tag based on the target path.</summary>
        public static string ComputeInstanceTag(string targetPath)
        {
            try
            {
                var full = Path.GetFullPath(targetPath ?? "");
                var name = Path.GetFileName(full) ?? "unknown";
                using(var md5 = MD5.Create())
                {
                    var bytes = Encoding.UTF8.GetBytes(full.ToLowerInvariant());
                    var hash = md5.ComputeHash(bytes);
                    var shortHash = BitConverter.ToString(hash, 0, 4).Replace("-", "");
                    return "WDG::" + name + "::" + shortHash;
                }
            }
            catch
            {
                return "WDG::unknown";
            }
        }

        internal static string GetProcessDir()
        {
            // AppDomain base dir is safer under restricted contexts.
            var dir = AppDomain.CurrentDomain.BaseDirectory;
            if(string.IsNullOrWhiteSpace(dir)) return Environment.CurrentDirectory;
            return dir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        internal static string Unquote(string s)
        {
            if(string.IsNullOrEmpty(s)) return s;
            if(s.Length >= 2 && s[0] == '"' && s[s.Length - 1] == '"')
                return s.Substring(1, s.Length - 2).Replace("\\\"", "\"");
            return s;
        }
    }
}
