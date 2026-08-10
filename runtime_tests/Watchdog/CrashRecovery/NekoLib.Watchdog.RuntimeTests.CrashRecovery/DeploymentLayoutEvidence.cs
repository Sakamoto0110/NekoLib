#nullable enable
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;

namespace NekoLib.Watchdog.RuntimeTests.CrashRecovery
{
    internal sealed class DeploymentLayoutEvidence
    {
        public string Kind = string.Empty;
        public string ApplicationRoot = string.Empty;
        public string ChildPath = string.Empty;
        public string HostPath = string.Empty;
        public string PackageFile = string.Empty;
        public string PackageVersion = string.Empty;
        public string PackageSha256 = string.Empty;
        public string PackagePayloadEntry = string.Empty;
        public string ChildVersion = string.Empty;
        public string HostVersion = string.Empty;
        public string ChildMachine = string.Empty;
        public string HostMachine = string.Empty;
        public bool SupportsPackageClaim;

        public static DeploymentLayoutEvidence Resolve(ScenarioOptions options)
        {
            string root = options.Layout == DeploymentLayout.Source
                ? Path.Combine(AppContext.BaseDirectory, "ScenarioApplication")
                : options.ApplicationRoot;

            if (string.IsNullOrWhiteSpace(root))
                throw new InvalidOperationException("A package-backed layout requires --application-root.");

            root = Path.GetFullPath(root);
            string child = Path.Combine(root, "NekoLib.Watchdog.RuntimeTests.CrashRecovery.Child.exe");
            string host = Path.Combine(root, "NekoLib.Watchdog.Host", "NekoLib.Watchdog.Host.exe");

            RequireFile(child, "scenario application child");
            RequireFile(host, "deployed Watchdog Host");

            DeploymentLayoutEvidence evidence = new DeploymentLayoutEvidence
            {
                Kind = Describe(options.Layout),
                ApplicationRoot = root,
                ChildPath = child,
                HostPath = host,
                ChildVersion = ReadVersion(child),
                HostVersion = ReadVersion(host),
                ChildMachine = ReadPeMachine(child),
                HostMachine = ReadPeMachine(host),
                SupportsPackageClaim = options.Layout != DeploymentLayout.Source
            };

            if (evidence.SupportsPackageClaim)
            {
                if (string.IsNullOrWhiteSpace(options.PackageFile) ||
                    string.IsNullOrWhiteSpace(options.PackageVersion))
                {
                    throw new InvalidOperationException(
                        "A package-backed layout requires --package-file and --package-version.");
                }

                RequireFile(options.PackageFile, "immutable Watchdog Host package");
                evidence.PackageFile = Path.GetFullPath(options.PackageFile);
                evidence.PackageVersion = options.PackageVersion;
                evidence.PackageSha256 = Sha256(evidence.PackageFile);
                evidence.PackagePayloadEntry = VerifyPackage(
                    evidence.PackageFile,
                    evidence.PackageVersion,
                    evidence.HostPath);
            }

            return evidence;
        }

        private static string Describe(DeploymentLayout layout)
        {
            switch (layout)
            {
                case DeploymentLayout.DisposablePackage: return "disposable-package";
                case DeploymentLayout.PublishedConsumer: return "published-consumer";
                default: return "source";
            }
        }

        private static void RequireFile(string path, string description)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException("The " + description + " was not found.", path);
        }

        private static string ReadVersion(string path)
        {
            try
            {
                string? value = FileVersionInfo.GetVersionInfo(path).FileVersion;
                if (!string.IsNullOrWhiteSpace(value)) return value;
            }
            catch { }

            try { return AssemblyName.GetAssemblyName(path).Version?.ToString() ?? "unknown"; }
            catch { return "unknown"; }
        }

        private static string Sha256(string path)
        {
            using (FileStream stream = File.OpenRead(path))
            using (SHA256 hash = SHA256.Create())
            {
                StringBuilder text = new StringBuilder(64);
                foreach (byte value in hash.ComputeHash(stream))
                    text.Append(value.ToString("x2", CultureInfo.InvariantCulture));
                return text.ToString();
            }
        }

        private static string VerifyPackage(string packagePath, string expectedVersion, string deployedHostPath)
        {
            using (FileStream stream = File.OpenRead(packagePath))
            using (ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Read, false))
            {
                ZipArchiveEntry? nuspec = archive.Entries.FirstOrDefault(entry =>
                    entry.FullName.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase));
                if (nuspec == null)
                    throw new InvalidDataException("The supplied package contains no nuspec.");

                XDocument document;
                using (Stream nuspecStream = nuspec.Open())
                    document = XDocument.Load(nuspecStream);

                XElement? metadata = document.Descendants().FirstOrDefault(element =>
                    string.Equals(element.Name.LocalName, "metadata", StringComparison.Ordinal));
                string id = metadata?.Elements().FirstOrDefault(element =>
                    string.Equals(element.Name.LocalName, "id", StringComparison.Ordinal))?.Value ?? string.Empty;
                string version = metadata?.Elements().FirstOrDefault(element =>
                    string.Equals(element.Name.LocalName, "version", StringComparison.Ordinal))?.Value ?? string.Empty;

                if (!string.Equals(id, "NekoLib.Watchdog.Host", StringComparison.Ordinal))
                    throw new InvalidDataException("The supplied package ID is '" + id +
                                                   "', expected 'NekoLib.Watchdog.Host'.");
                if (!string.Equals(version, expectedVersion, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("The supplied package version is '" + version +
                                                   "', expected '" + expectedVersion + "'.");

                string deployedHash = Sha256(deployedHostPath);
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    if (!entry.FullName.EndsWith("/NekoLib.Watchdog.Host.exe", StringComparison.OrdinalIgnoreCase) &&
                        !entry.FullName.EndsWith("\\NekoLib.Watchdog.Host.exe", StringComparison.OrdinalIgnoreCase))
                        continue;

                    using (Stream payload = entry.Open())
                    using (SHA256 hash = SHA256.Create())
                    {
                        StringBuilder text = new StringBuilder(64);
                        foreach (byte value in hash.ComputeHash(payload))
                            text.Append(value.ToString("x2", CultureInfo.InvariantCulture));
                        if (string.Equals(text.ToString(), deployedHash, StringComparison.OrdinalIgnoreCase))
                            return entry.FullName;
                    }
                }

                throw new InvalidDataException(
                    "The deployed Watchdog Host bytes do not match any Host payload in the supplied package.");
            }
        }

        private static string ReadPeMachine(string path)
        {
            try
            {
                using (FileStream stream = File.OpenRead(path))
                using (BinaryReader reader = new BinaryReader(stream))
                {
                    stream.Position = 0x3c;
                    int peOffset = reader.ReadInt32();
                    stream.Position = peOffset;
                    if (reader.ReadUInt32() != 0x00004550) return "not-pe";

                    ushort machine = reader.ReadUInt16();
                    switch (machine)
                    {
                        case 0x014c: return "x86/AnyCPU";
                        case 0x8664: return "x64";
                        case 0xaa64: return "arm64";
                        default: return "0x" + machine.ToString("x4", CultureInfo.InvariantCulture);
                    }
                }
            }
            catch (Exception ex) { return "unknown:" + ex.GetType().Name; }
        }
    }
}
