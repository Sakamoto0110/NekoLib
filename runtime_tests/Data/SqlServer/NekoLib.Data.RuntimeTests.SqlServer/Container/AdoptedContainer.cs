#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using NekoLib.RuntimeTests.Harness;

namespace NekoLib.Data.RuntimeTests.SqlServer.Container
{
    /// <summary>What the container looked like when the scenario found it.</summary>
    internal sealed class ContainerFacts
    {
        public string Name = string.Empty;
        public string Status = string.Empty;
        public string ConfiguredImage = string.Empty;
        public string ImageId = string.Empty;
        public string ImageDigest = string.Empty;
        public string ImageArchitecture = string.Empty;
        public string ImageOs = string.Empty;
        public string RestartPolicy = string.Empty;

        /// <summary>Docker's own <c>HostIp</c> for the published database port; empty means every interface.</summary>
        public string PublishedHostIp = string.Empty;
        public string PublishedHostPort = string.Empty;

        public string MountsJson = "[]";
        public string NetworksSummary = string.Empty;
        public string EngineVersion = string.Empty;

        /// <summary>True when Docker publishes the port on loopback only.</summary>
        public bool IsLoopbackOnly =>
            string.Equals(PublishedHostIp, "127.0.0.1", StringComparison.Ordinal) ||
            string.Equals(PublishedHostIp, "::1", StringComparison.Ordinal);

        /// <summary>True when the container carries no volume or bind mount.</summary>
        public bool HasNoMounts =>
            MountsJson.Trim().Length == 0 ||
            string.Equals(MountsJson.Trim(), "[]", StringComparison.Ordinal) ||
            string.Equals(MountsJson.Trim(), "null", StringComparison.Ordinal);
    }

    /// <summary>
    /// The user-owned SQL Server container, explicitly adopted.
    /// <para/>
    /// Adoption is the whole point of this type. The container was created by
    /// the repository owner, not by the scenario, so the operations it exposes
    /// are deliberately only the ones the specification allows: start, stop,
    /// restart, pause and unpause, for documented setup and fault steps. There
    /// is no remove, no create, no volume or network mutation, and no way to
    /// change credentials — a scenario that recreated the container to fix a
    /// setup gap would destroy the evidence it was asked to record.
    /// <para/>
    /// The state found at startup is captured once and restored during cleanup,
    /// so a run that stops a container it found running puts it back, and a run
    /// against a container it found stopped leaves it stopped.
    /// </summary>
    internal sealed class AdoptedContainer
    {
        private readonly DockerCli _docker;
        private readonly string _name;
        private string? _initialStatus;

        public AdoptedContainer(DockerCli docker, string name)
        {
            _docker = docker;
            _name = name;
        }

        public string Name => _name;

        /// <summary>The status the scenario found, and the one cleanup must restore.</summary>
        public string InitialStatus => _initialStatus ?? "unknown";

        public bool Exists()
        {
            ProcessResult result = _docker.Run("inspect " + _name + " --format \"{{.Id}}\"");
            return result.Succeeded && result.Trimmed.Length > 0;
        }

        public string Status()
        {
            ProcessResult result = _docker.Run("inspect " + _name + " --format \"{{.State.Status}}\"");
            return result.Succeeded ? result.Trimmed : "unknown";
        }

        public bool IsRunning() => string.Equals(Status(), "running", StringComparison.Ordinal);

        /// <summary>
        /// Reads everything the evidence record needs, without ever asking for
        /// <c>.Config.Env</c> — that array holds the SA password, and a result
        /// file is exactly the wrong place for it.
        /// </summary>
        public ContainerFacts Describe(string engineVersion)
        {
            ContainerFacts facts = new ContainerFacts
            {
                Name = _name,
                EngineVersion = engineVersion,
                Status = Status(),
                ConfiguredImage = Inspect("{{.Config.Image}}"),
                ImageId = Inspect("{{.Image}}"),
                RestartPolicy = Inspect("{{.HostConfig.RestartPolicy.Name}}"),
                MountsJson = Inspect("{{json .Mounts}}"),
                NetworksSummary = Inspect("{{range $name, $_ := .NetworkSettings.Networks}}{{$name}} {{end}}")
            };

            if (_initialStatus == null) _initialStatus = facts.Status;

            ReadPublishedPort(facts);
            ReadImageFacts(facts);
            return facts;
        }

        /// <summary>
        /// Starts the container and returns whether the command was accepted.
        /// Readiness is a separate question: the SQL Server process inside takes
        /// seconds longer than the container does.
        /// </summary>
        public bool Start(out string diagnostic)
        {
            EnsureInitialStatusCaptured();
            ProcessResult result = _docker.Run("start " + _name, TimeSpan.FromSeconds(90));
            diagnostic = result.Succeeded ? "started" : result.Diagnostic;
            return result.Succeeded;
        }

        public bool Stop(out string diagnostic)
        {
            EnsureInitialStatusCaptured();
            ProcessResult result = _docker.Run("stop " + _name, TimeSpan.FromSeconds(120));
            diagnostic = result.Succeeded ? "stopped" : result.Diagnostic;
            return result.Succeeded;
        }

        public bool Restart(out string diagnostic)
        {
            EnsureInitialStatusCaptured();
            ProcessResult result = _docker.Run("restart " + _name, TimeSpan.FromSeconds(180));
            diagnostic = result.Succeeded ? "restarted" : result.Diagnostic;
            return result.Succeeded;
        }

        public bool Pause(out string diagnostic)
        {
            EnsureInitialStatusCaptured();
            ProcessResult result = _docker.Run("pause " + _name, TimeSpan.FromSeconds(60));
            diagnostic = result.Succeeded ? "paused" : result.Diagnostic;
            return result.Succeeded;
        }

        public bool Unpause(out string diagnostic)
        {
            ProcessResult result = _docker.Run("unpause " + _name, TimeSpan.FromSeconds(60));
            diagnostic = result.Succeeded ? "unpaused" : result.Diagnostic;
            return result.Succeeded;
        }

        /// <summary>
        /// Puts the container back into the running or stopped state the run
        /// found it in. A paused container is always unpaused first, because a
        /// paused container left behind looks like a hung machine to whoever
        /// runs next.
        /// </summary>
        public bool RestoreInitialState(out string diagnostic)
        {
            if (_initialStatus == null)
            {
                diagnostic = "nothing to restore; the container was never touched";
                return true;
            }

            string current = Status();
            if (string.Equals(current, "paused", StringComparison.Ordinal))
            {
                if (!Unpause(out string unpaused))
                {
                    diagnostic = "could not unpause before restoring: " + unpaused;
                    return false;
                }

                current = Status();
            }

            bool wantRunning = string.Equals(_initialStatus, "running", StringComparison.Ordinal);
            bool isRunning = string.Equals(current, "running", StringComparison.Ordinal);

            if (wantRunning == isRunning)
            {
                diagnostic = "already " + current + ", matching the initial state";
                return true;
            }

            if (wantRunning)
            {
                bool started = Start(out string startDiagnostic);
                diagnostic = started
                    ? "restarted because the run found it running"
                    : "could not restore the running state: " + startDiagnostic;
                return started;
            }

            bool stopped = Stop(out string stopDiagnostic);
            diagnostic = stopped
                ? "stopped again because the run found it stopped"
                : "could not restore the stopped state: " + stopDiagnostic;
            return stopped;
        }

        private void EnsureInitialStatusCaptured()
        {
            if (_initialStatus == null) _initialStatus = Status();
        }

        private string Inspect(string format)
        {
            ProcessResult result = _docker.Run("inspect " + _name + " --format \"" + format + "\"");
            return result.Succeeded ? result.Trimmed : string.Empty;
        }

        /// <summary>
        /// Reads the configured host binding for the database port.
        /// <para/>
        /// This is read from <c>HostConfig.PortBindings</c> rather than from the
        /// live <c>NetworkSettings.Ports</c> so it answers the same way whether
        /// or not the container happens to be running, and because the question
        /// being asked is what the container was configured to publish.
        /// </summary>
        private void ReadPublishedPort(ContainerFacts facts)
        {
            const string format =
                "{{range $port, $bindings := .HostConfig.PortBindings}}" +
                "{{range $bindings}}{{$port}}|{{.HostIp}}|{{.HostPort}};{{end}}{{end}}";

            string raw = Inspect(format);
            foreach (string entry in raw.Split(';'))
            {
                string[] parts = entry.Split('|');
                if (parts.Length != 3) continue;
                if (!parts[0].StartsWith("1433/", StringComparison.Ordinal)) continue;

                facts.PublishedHostIp = parts[1];
                facts.PublishedHostPort = parts[2];
                return;
            }
        }

        private void ReadImageFacts(ContainerFacts facts)
        {
            string reference = facts.ConfiguredImage.Length > 0 ? facts.ConfiguredImage : facts.ImageId;
            if (reference.Length == 0) return;

            ProcessResult result = _docker.Run(
                "image inspect " + reference +
                " --format \"{{index .RepoDigests 0}}|{{.Architecture}}|{{.Os}}\"");

            if (!result.Succeeded) return;

            string[] parts = result.Trimmed.Split('|');
            if (parts.Length < 3) return;

            facts.ImageDigest = parts[0];
            facts.ImageArchitecture = parts[1];
            facts.ImageOs = parts[2];
        }

        /// <summary>
        /// Compares what was found against the pinned definition and returns the
        /// differences. A mismatch is reported, never corrected: the definition
        /// describes what the evidence claims, and rebuilding the container to
        /// match it is outside this scenario's authority.
        /// </summary>
        public static IReadOnlyList<string> Reconcile(ContainerFacts facts, PinnedContainerDefinition pinned)
        {
            List<string> gaps = new List<string>();

            if (!string.Equals(facts.ConfiguredImage, pinned.Image, StringComparison.Ordinal))
            {
                gaps.Add("image is '" + facts.ConfiguredImage +
                         "' but the pinned definition names '" + pinned.Image + "'");
            }

            if (pinned.ImageDigest.Length > 0 &&
                facts.ImageDigest.Length > 0 &&
                !string.Equals(facts.ImageDigest, pinned.ImageDigest, StringComparison.Ordinal))
            {
                gaps.Add("image digest is '" + facts.ImageDigest +
                         "' but the pinned definition records '" + pinned.ImageDigest + "'");
            }

            if (!string.Equals(facts.PublishedHostPort, pinned.HostPort.ToString(CultureInfo.InvariantCulture),
                    StringComparison.Ordinal))
            {
                gaps.Add("published host port is '" + facts.PublishedHostPort +
                         "' but the pinned definition expects " + pinned.HostPort);
            }

            if (!facts.IsLoopbackOnly)
            {
                gaps.Add("the database port is published on HostIp '" +
                         (facts.PublishedHostIp.Length == 0 ? "(every interface)" : facts.PublishedHostIp) +
                         "', so local-only exposure is not established by this setup");
            }

            return gaps;
        }
    }
}
