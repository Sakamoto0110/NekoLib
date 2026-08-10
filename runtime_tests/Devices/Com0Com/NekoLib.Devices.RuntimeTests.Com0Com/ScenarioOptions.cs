#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using NekoLib.RuntimeTests.Harness;
using NekoLib.RuntimeTests.Harness.Faults;
using NekoLib.RuntimeTests.Harness.Reporting;

namespace NekoLib.Devices.RuntimeTests.Com0Com
{
    /// <summary>
    /// What E3-DEV adds to the suite's shared command line.
    /// <para/>
    /// Four port names rather than two: the automated modes own <b>both</b> ends
    /// of each pair, so besides the client ends the oracle path already took
    /// they name the ends the emulator would otherwise hold. Nothing is
    /// discovered by enumeration - the controller opens the names it was given
    /// and no others, which is what "adopt only explicitly configured COM pairs"
    /// means in practice.
    /// </summary>
    internal sealed class ScenarioOptions : ScenarioOptionsBase
    {
        /// <summary>Client end of the PCB-A pair: the port the transport under test opens.</summary>
        public string PcbAPort = "COM19";

        /// <summary>Client end of the PCB-B pair.</summary>
        public string PcbBPort = "COM20";

        /// <summary>Far end of the PCB-A pair: the port the scenario-owned peer opens.</summary>
        public string PeerAPort = "COM9";

        /// <summary>Far end of the PCB-B pair.</summary>
        public string PeerBPort = "COM10";

        public TimeSpan SmokeDuration = TimeSpan.FromMinutes(15);

        public static readonly TimeSpan MinimumSpecifiedSmoke = TimeSpan.FromMinutes(15);

        public override string ScenarioId => "E3-DEV";

        protected override string CampaignPrefix => "e3dev";

        /// <summary>
        /// Identifies the schedule generator and is covered by the schedule
        /// hash, so it is this scenario's own string and nobody else's.
        /// </summary>
        public const string ScheduleGeneratorVersion = "e3dev-schedule-1";

        /// <summary>
        /// Whether these arguments select an E3-DEV mode.
        /// <para/>
        /// This is the whole of the compatibility contract with the original
        /// scenario: without a mode flag the binary behaves exactly as it did on
        /// 2026-08-01, down to the option names and the exit codes, so that
        /// evidence still describes it. Adding a mode is opt-in.
        /// </summary>
        public static bool LooksAutomated(string[] args)
        {
            foreach (string arg in args)
            {
                switch (arg.ToLowerInvariant())
                {
                    case "--smoke":
                    case "--recovery-rehearsal":
                    case "--soak":
                    case "--print-schedule":
                        return true;
                }
            }

            return false;
        }

        protected override IEnumerable<string> ScenarioUsage() => new[]
        {
            "  --smoke-duration <d>        smoke window, default 15m (the suite specifies 15-30m)",
            "  --pcb-a <port>              client end of the PCB-A pair, default COM19",
            "  --pcb-b <port>              client end of the PCB-B pair, default COM20",
            "  --peer-a <port>             far end of the PCB-A pair, default COM9",
            "  --peer-b <port>             far end of the PCB-B pair, default COM10",
            string.Empty,
            "Without a mode flag this executable runs the original parity pass against the",
            "independent NekoPcbEmulator on --pcb-a / --pcb-b. The two are mutually exclusive:",
            "the automated modes need the emulator's ports for their own peer."
        };

        protected override bool TryParseScenarioOption(
            string[] args,
            ref int index,
            string option,
            out string diagnostic)
        {
            diagnostic = string.Empty;

            switch (option)
            {
                case "--pcb-a":
                    return TryTakePort(args, ref index, "--pcb-a", ref PcbAPort, out diagnostic);

                case "--pcb-b":
                    return TryTakePort(args, ref index, "--pcb-b", ref PcbBPort, out diagnostic);

                case "--peer-a":
                    return TryTakePort(args, ref index, "--peer-a", ref PeerAPort, out diagnostic);

                case "--peer-b":
                    return TryTakePort(args, ref index, "--peer-b", ref PeerBPort, out diagnostic);

                case "--smoke-duration":
                    if (!TryTakeValue(args, ref index, "--smoke-duration", out string smoke, out diagnostic))
                        return false;
                    return TryParseDuration(smoke, out SmokeDuration, out diagnostic);

                default:
                    return false;
            }
        }

        private static bool TryTakePort(
            string[] args,
            ref int index,
            string name,
            ref string target,
            out string diagnostic)
        {
            string value;
            if (!TryTakeValue(args, ref index, name, out value, out diagnostic)) return false;

            if (value.Length == 0)
            {
                diagnostic = name + " expects a COM port name";
                return false;
            }

            target = value.ToUpperInvariant();
            return true;
        }

        /// <summary>The four names this run will open, in a fixed order.</summary>
        public string[] AllPorts() => new[] { PcbAPort, PcbBPort, PeerAPort, PeerBPort };

        public static string UsageText() =>
            new ScenarioOptions().Usage(
                "E3-DEV - NekoLib.Devices over paired virtual COM ports, under load, failure and recovery");
    }

    /// <summary>
    /// The fault kinds this scenario owns, one per transition the suite names:
    /// "repeated emulator delay, silence, malformed frame, disconnect, and
    /// restart".
    /// <para/>
    /// The emulator produces none of them. Every one is a switch on the
    /// scenario's own peer, which is the design decision this scenario was
    /// built around: an independent oracle that can be told to misbehave has
    /// stopped being independent, and a product module that exposes a fault
    /// switch has grown the control plane the suite forbids.
    /// </summary>
    internal static class FaultKinds
    {
        public const string PeerDelay = "peer-delays-response";
        public const string PeerSilence = "peer-falls-silent";
        public const string PeerMalformed = "peer-sends-malformed-frame";
        public const string PeerDisconnect = "peer-disconnects";
        public const string PeerRestart = "peer-restarts";

        public static readonly string[] RecoveryRehearsalSet =
        {
            PeerDelay,
            PeerSilence,
            PeerMalformed,
            PeerDisconnect,
            PeerRestart
        };
    }

    internal static class Phases
    {
        public const string Transport = "transport";
        public const string Protocol = "protocol";
        public const string Lifecycle = "lifecycle";
        public const string Recovery = "recovery";
    }

    /// <summary>
    /// Tells the harness what each fault kind targets and what recovery looks
    /// like.
    /// <para/>
    /// <b>No COM name appears here.</b> The target string is covered by the
    /// schedule hash, so naming a port would make the same seed produce a
    /// different plan on a machine whose com0com pairs are numbered differently
    /// - the same class of defect E3-PIPE hit by interpolating its per-run pipe
    /// name. A target describes the kind of resource; the instance belongs in
    /// <c>environment.json</c>, which records all four ports.
    /// </summary>
    internal sealed class DeviceFaultVocabulary : IFaultVocabulary
    {
        public string DescribeTarget(string kind)
        {
            switch (kind)
            {
                case FaultKinds.PeerDelay:
                case FaultKinds.PeerSilence:
                    return "scenario-owned serial peer on the text pair";
                case FaultKinds.PeerMalformed:
                    return "scenario-owned serial peer on the binary pair";
                default:
                    return "scenario-owned serial peer port";
            }
        }

        public string DescribeParameters(string kind)
        {
            switch (kind)
            {
                case FaultKinds.PeerDisconnect:
                case FaultKinds.PeerRestart:
                    return "3";
                case FaultKinds.PeerDelay:
                    return "2";
                default:
                    return "1";
            }
        }

        public string DescribeExpectedRecovery(string kind)
        {
            switch (kind)
            {
                case FaultKinds.PeerDelay:
                    return "the caller's finite timeout expires inside its bound rather than hanging, and after the " +
                           "port is reopened the next request receives its own response instead of the late one";
                case FaultKinds.PeerSilence:
                    return "every read returns its documented no-data result inside its bound, and an ordinary " +
                           "request succeeds as soon as the peer answers again";
                case FaultKinds.PeerMalformed:
                    return "the malformed bytes reach the caller verbatim rather than being hidden or repaired, the " +
                           "scenario's own validator rejects them, and the next well-formed exchange succeeds";
                case FaultKinds.PeerDisconnect:
                    return "the operation ends with a bounded terminal rather than hanging, and the same endpoint " +
                           "serves again once the far end comes back";
                default:
                    return "a peer that closes and reopens is served again on the same endpoint, at worst after the " +
                           "caller reopens its own port, with no leaked handle";
            }
        }
    }

    /// <summary>The versions and machine facts only this scenario can name.</summary>
    internal static class ScenarioFacts
    {
        public static string DevicesVersion =>
            RuntimeFacts.DescribeAssembly(
                "NekoLib.Devices",
                typeof(global::NekoLib.Devices.Core.Transport.SerialCommTransport));

        public static string ScenarioVersion => RuntimeFacts.AssemblyVersion(typeof(ScenarioFacts));

        /// <summary>
        /// The com0com build, which the suite requires in the evidence record.
        /// <para/>
        /// Read from the installed files rather than from the driver store,
        /// because that needs no elevation, no extra package and no registry
        /// access on either target. The driver binary is tried first because it
        /// is the thing actually doing the work; <c>setupc.exe</c> carries no
        /// version resource at all, so it is only ever a location.
        /// <para/>
        /// When nothing can be read the run records a setup gap instead of
        /// guessing. A version this process did not observe has no business in
        /// an evidence record.
        /// </summary>
        public static string DescribeCom0Com(out string? gap)
        {
            gap = null;
            string? located = null;

            foreach (string candidate in Com0ComCandidates())
            {
                try
                {
                    if (!File.Exists(candidate)) continue;

                    if (located == null) located = Path.GetDirectoryName(candidate);

                    FileVersionInfo info = FileVersionInfo.GetVersionInfo(candidate);
                    if (!string.IsNullOrEmpty(info.FileVersion))
                        return "com0com " + info.FileVersion + " (" + candidate + ")";

                    string? driverVer = ReadDriverVersion(
                        Path.Combine(Path.GetDirectoryName(candidate) ?? string.Empty, "com0com.inf"));

                    if (driverVer != null)
                        return "com0com DriverVer " + driverVer + " (" + candidate + ")";
                }
                catch (Exception)
                {
                    // A probe that cannot read a candidate simply tries the next.
                }
            }

            if (located != null)
            {
                gap = "com0com is installed at '" + located + "' but none of its files carried a readable " +
                      "version. Record the version by hand before citing this run as driver evidence.";

                return "com0com installed at " + located + ", version not readable";
            }

            gap = "The com0com version was not observed: no install was found in the standard locations. The " +
                  "pairs still work or preflight would have failed; record the version by hand before citing " +
                  "this run as driver evidence.";

            return "com0com version not observed";
        }

        /// <summary>Reads the <c>DriverVer</c> line an INF always carries.</summary>
        private static string? ReadDriverVersion(string infPath)
        {
            if (infPath.Length == 0 || !File.Exists(infPath)) return null;

            foreach (string line in File.ReadAllLines(infPath))
            {
                string trimmed = line.Trim();
                if (!trimmed.StartsWith("DriverVer", StringComparison.OrdinalIgnoreCase)) continue;

                int separator = trimmed.IndexOf('=');
                if (separator < 0) continue;

                string value = trimmed.Substring(separator + 1).Trim();
                if (value.Length > 0) return value;
            }

            return null;
        }

        private static IEnumerable<string> Com0ComCandidates()
        {
            string[] roots =
            {
                Environment.GetEnvironmentVariable("ProgramFiles(x86)") ?? string.Empty,
                Environment.GetEnvironmentVariable("ProgramFiles") ?? string.Empty,
                Environment.GetEnvironmentVariable("ProgramW6432") ?? string.Empty
            };

            List<string> candidates = new List<string>();
            foreach (string root in roots)
            {
                if (root.Length == 0) continue;

                string directory = Path.Combine(root, "com0com");
                candidates.Add(Path.Combine(directory, "com0com.sys"));
                candidates.Add(Path.Combine(directory, "setupc.exe"));
                candidates.Add(Path.Combine(directory, "setupg.exe"));
            }

            return candidates;
        }
    }

    /// <summary>A counter that is cheap to read from the sampling path.</summary>
    internal sealed class Counter
    {
        private long _value;

        public long Value => Interlocked.Read(ref _value);
        public void Increment() => Interlocked.Increment(ref _value);
        public void Add(long amount) => Interlocked.Add(ref _value, amount);
        public void Set(long value) => Interlocked.Exchange(ref _value, value);
    }

    /// <summary>
    /// The sample columns only this scenario can fill.
    /// <para/>
    /// The suite asks every sample for "active/retained item counts for bounded
    /// components" and "queue depth or a truthful unavailable marker". A serial
    /// port has no queue this process can read, so the honest answer here is the
    /// count of ports currently held open on each side - which is also the
    /// number that would grow if the scenario ever leaked a handle, and the one
    /// thing "port/process/handle stability over the soak" really turns on.
    /// </summary>
    internal sealed class ScenarioSamples : IScenarioSamples
    {
        private static readonly string[] Columns =
        {
            "requests_sent",
            "requests_failed",
            "peer_responses",
            "peer_bytes_read",
            "peer_ports_open",
            "peer_restarts",
            "subject_transports_alive"
        };

        public static IReadOnlyList<string> ColumnNamesForHeader => Columns;

        public IReadOnlyList<string> ColumnNames => Columns;

        public readonly Counter RequestsSent = new Counter();
        public readonly Counter RequestsFailed = new Counter();
        public readonly Counter PeerResponses = new Counter();
        public readonly Counter PeerBytesRead = new Counter();
        public readonly Counter PeerPortsOpen = new Counter();
        public readonly Counter PeerRestarts = new Counter();

        /// <summary>
        /// Transports created and not yet disposed. Counted rather than read
        /// from the operating system on purpose: a transport that is closed but
        /// never disposed still owns a <c>SerialPort</c>, and that is precisely
        /// the leak a soak has to notice.
        /// </summary>
        public readonly Counter SubjectTransportsAlive = new Counter();

        public long[] Read() => new[]
        {
            RequestsSent.Value,
            RequestsFailed.Value,
            PeerResponses.Value,
            PeerBytesRead.Value,
            PeerPortsOpen.Value,
            PeerRestarts.Value,
            SubjectTransportsAlive.Value
        };
    }

    internal sealed class DeviceSummary : IScenarioSummary
    {
        private readonly ScenarioOptions _options;
        private readonly string _com0com;
        private readonly IReadOnlyList<string> _boundaries;

        public DeviceSummary(ScenarioOptions options, string com0com, IReadOnlyList<string> boundaries)
        {
            _options = options;
            _com0com = com0com;
            _boundaries = boundaries;
        }

        public IReadOnlyList<KeyValuePair<string, string>> Facts => new[]
        {
            new KeyValuePair<string, string>("Devices", ScenarioFacts.DevicesVersion),
            new KeyValuePair<string, string>("com0com", _com0com),
            new KeyValuePair<string, string>(
                "Pairs",
                _options.PeerAPort + " <-> " + _options.PcbAPort + ", " +
                _options.PeerBPort + " <-> " + _options.PcbBPort),
            new KeyValuePair<string, string>("Protocol mode", "scenario-owned peer (PCB-A text, PCB-B binary)"),
            new KeyValuePair<string, string>("Architecture", RuntimeFacts.ProcessArchitecture)
        };

        public void WriteJson(JsonWriter json)
        {
            json.Prop("devices", ScenarioFacts.DevicesVersion);
            json.Prop("com0com", _com0com);
            json.Prop("protocolMode", "scenario-owned peer (PCB-A text, PCB-B binary)");
            json.Prop("processArchitecture", RuntimeFacts.ProcessArchitecture);

            json.Object("ports", () =>
            {
                json.Prop("pcbA", _options.PcbAPort);
                json.Prop("pcbB", _options.PcbBPort);
                json.Prop("peerA", _options.PeerAPort);
                json.Prop("peerB", _options.PeerBPort);
            });

            json.Array("claimBoundaries", () =>
            {
                foreach (string boundary in _boundaries) json.Item(boundary);
            });
        }
    }
}
