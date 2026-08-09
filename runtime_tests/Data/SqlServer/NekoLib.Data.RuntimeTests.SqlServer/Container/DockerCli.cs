#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using NekoLib.RuntimeTests.Harness;

namespace NekoLib.Data.RuntimeTests.SqlServer.Container
{
    /// <summary>
    /// Locates and drives the container CLI.
    /// <para/>
    /// The path is resolved rather than assumed because Docker Desktop installs
    /// per user and does not always put <c>docker.exe</c> on the machine
    /// <c>PATH</c>; a scenario that reported "no container engine" on a machine
    /// where the engine was running would be a false environment failure.
    /// </summary>
    internal sealed class DockerCli
    {
        /// <summary>Overrides discovery when the CLI lives somewhere unusual.</summary>
        public const string PathVariable = "NEKOLIB_DOCKER_CLI";

        private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(60);

        private DockerCli(string executablePath)
        {
            ExecutablePath = executablePath;
        }

        public string ExecutablePath { get; }

        public static bool TryLocate(out DockerCli? cli, out string diagnostic)
        {
            foreach (string candidate in Candidates())
            {
                if (candidate.Length == 0 || !File.Exists(candidate)) continue;

                DockerCli found = new DockerCli(candidate);
                ProcessResult probe = found.Run("version --format {{.Server.Version}}", TimeSpan.FromSeconds(30));
                if (probe.Succeeded && probe.Trimmed.Length > 0)
                {
                    cli = found;
                    diagnostic = probe.Trimmed;
                    return true;
                }

                // Found the CLI but the engine did not answer. That is a
                // different failure from "not installed" and must say so.
                cli = null;
                diagnostic = "found " + candidate + " but the engine did not answer (" + probe.Diagnostic + ")";
                return false;
            }

            cli = null;
            diagnostic = "no container CLI found; set " + PathVariable + " to an absolute docker.exe path";
            return false;
        }

        public ProcessResult Run(string arguments) => Run(arguments, DefaultTimeout);

        public ProcessResult Run(string arguments, TimeSpan timeout) =>
            ProcessRunner.Run(ExecutablePath, arguments, timeout);

        private static IEnumerable<string> Candidates()
        {
            string? configured = Environment.GetEnvironmentVariable(PathVariable);
            if (!string.IsNullOrWhiteSpace(configured))
            {
                yield return configured!;
                yield break;
            }

            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);

            yield return Path.Combine(localAppData, @"Programs\DockerDesktop\resources\bin\docker.exe");
            yield return Path.Combine(programFiles, @"Docker\Docker\resources\bin\docker.exe");
            yield return Path.Combine(localAppData, @"Programs\Docker\Docker\resources\bin\docker.exe");

            foreach (string entry in (Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(';'))
            {
                string trimmed = entry.Trim();
                if (trimmed.Length == 0) continue;

                string candidate;
                try { candidate = Path.Combine(trimmed, "docker.exe"); }
                catch (ArgumentException) { continue; }

                yield return candidate;
            }
        }
    }
}
