using System;
using System.Diagnostics;
using System.IO;
using System.Text;
#if NET9
using System.Text.Json;
#else
using Newtonsoft.Json;
#endif

namespace NekoLib.Watchdog
{
    internal sealed class WatchdogSupervisorLaunch
    {
        public string ProtocolVersion { get; set; } = WatchdogBootstrap.HostProtocolVersion;
        public WatchdogOptions RuntimeOptions { get; set; } = new WatchdogOptions();

        internal const string Argument = "--nekolib-watchdog-supervisor";
        private const int MaximumConfigurationBytes = 1024 * 1024;
        internal static string ConfigurationDirectory => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "NekoLib", "Watchdog", "bootstrap");

        internal static string Serialize<T>(T value)
        {
#if NET9
            return JsonSerializer.Serialize(value);
#else
            return JsonConvert.SerializeObject(value);
#endif
        }

        internal static T Deserialize<T>(string json) where T : class
        {
#if NET9
            return JsonSerializer.Deserialize<T>(json) ?? throw new InvalidOperationException("Missing bootstrap configuration.");
#else
            return JsonConvert.DeserializeObject<T>(json) ?? throw new InvalidOperationException("Missing bootstrap configuration.");
#endif
        }

        internal static WatchdogAutoBootstrapOptions Copy(WatchdogAutoBootstrapOptions options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (options.HostingMode != WatchdogHostingMode.SameExecutable && options.HostingMode != WatchdogHostingMode.Sidecar)
                throw new ArgumentOutOfRangeException(nameof(options.HostingMode));
            if (options.HandshakeTimeoutMs < 1) throw new ArgumentOutOfRangeException(nameof(options.HandshakeTimeoutMs));
            var runtime = options.RuntimeOptions ?? throw new ArgumentException("RuntimeOptions is required.", nameof(options));
            if (runtime.TargetPath != null || !string.IsNullOrEmpty(runtime.TargetArguments) ||
                runtime.InitialProcessId.HasValue || !string.IsNullOrEmpty(runtime.AttachToken))
                throw new ArgumentException("TargetPath, TargetArguments, InitialProcessId and AttachToken are bootstrap-owned.", nameof(options));
            if ((runtime.LogSinks != null && runtime.LogSinks.Length != 0) || runtime.Telemetry != null)
                throw new ArgumentException("LogSinks and Telemetry objects cannot be transferred to a supervisor process.", nameof(options));
            return Deserialize<WatchdogAutoBootstrapOptions>(Serialize(options));
        }

        internal string Write()
        {
            var bytes = new UTF8Encoding(false).GetBytes(Serialize(this));
            if (bytes.Length > MaximumConfigurationBytes) throw new InvalidOperationException("Bootstrap configuration exceeds 1 MiB.");
            Directory.CreateDirectory(ConfigurationDirectory);
            var path = Path.Combine(ConfigurationDirectory, Guid.NewGuid().ToString("N") + ".json");
            using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                stream.Write(bytes, 0, bytes.Length);
            return path;
        }

        internal static WatchdogSupervisorLaunch Read(string path)
        {
            var absolute = Path.GetFullPath(path);
            var name = Path.GetFileNameWithoutExtension(absolute);
            if (!string.Equals(Path.GetDirectoryName(absolute), Path.GetFullPath(ConfigurationDirectory), StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(Path.GetExtension(absolute), ".json", StringComparison.OrdinalIgnoreCase) ||
                !Guid.TryParseExact(name, "N", out _))
                throw new InvalidOperationException("Supervisor configuration must be a generated file in the owned bootstrap directory.");
            try
            {
                if (new FileInfo(absolute).Length > MaximumConfigurationBytes)
                    throw new InvalidOperationException("Bootstrap configuration exceeds 1 MiB.");
                string json;
                using (var reader = new StreamReader(absolute, new UTF8Encoding(false, true)))
                {
                    var chars = new char[MaximumConfigurationBytes + 1];
                    var count = reader.ReadBlock(chars, 0, chars.Length);
                    if (count > MaximumConfigurationBytes) throw new InvalidOperationException("Bootstrap configuration exceeds 1 MiB.");
                    json = new string(chars, 0, count);
                }
                var value = Deserialize<WatchdogSupervisorLaunch>(json);
                if (value.ProtocolVersion != WatchdogBootstrap.HostProtocolVersion || value.RuntimeOptions == null ||
                    !value.RuntimeOptions.InitialProcessId.HasValue || string.IsNullOrWhiteSpace(value.RuntimeOptions.AttachToken))
                    throw new InvalidOperationException("Invalid supervisor protocol or attach configuration.");
                return value;
            }
            finally { TryDelete(absolute); }
        }

        internal static void TryDelete(string? path)
        {
            try { if (path != null) File.Delete(path); } catch { }
        }
    }
}
