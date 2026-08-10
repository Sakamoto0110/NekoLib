#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;
using NekoLib.Devices.Core.Transport;
using NekoLib.RuntimeTests.Harness;
using NekoLib.RuntimeTests.Harness.Faults;
using NekoLib.RuntimeTests.Harness.Reporting;

namespace NekoLib.Devices.RuntimeTests.Com0Com
{
    /// <summary>
    /// The com0com scenario, in two mutually exclusive shapes.
    /// <para/>
    /// <b>Without a mode flag</b> it is the original parity run: it opens the
    /// client ends of the pairs and exchanges both wire protocols with the
    /// independent NekoPcbEmulator. That path is untouched, so its 2026-08-01
    /// interactive pass still describes this binary.
    /// <para/>
    /// <b>With a mode flag</b> it is E3-DEV. The suite asks for faults of
    /// "delay, silence, malformed frame, disconnect and restart", and the
    /// emulator can supply none of them: it is an independent oracle in another
    /// repository with no reference to NekoLib, and a control channel would make
    /// it an accomplice rather than an oracle. Adding one to
    /// <c>NekoLib.Devices</c> is forbidden outright. So the scenario opens the
    /// ports the emulator would have held and answers for itself.
    /// <para/>
    /// The two therefore cannot run at once, because both want those ports.
    /// That is the accepted cost of the design and not an oversight: the oracle
    /// proves protocol parity against an implementation nobody here wrote, and
    /// the owned peer proves transport behaviour under faults nobody can ask
    /// that implementation to produce.
    /// </summary>
    internal static class Program
    {
        private static int Main(string[] args)
        {
            if (!ScenarioOptions.LooksAutomated(args))
                return OracleParity.Run(args);

            ScenarioOptions options = new ScenarioOptions();
            if (!options.TryParse(args, out string diagnostic))
            {
                Console.Error.WriteLine("E3-DEV: " + diagnostic);
                Console.Error.WriteLine();
                Console.Error.WriteLine(ScenarioOptions.UsageText());
                return ExitCodes.Usage;
            }

            using (CancellationTokenSource cancellation = new CancellationTokenSource())
            {
                bool interrupted = false;

                Console.CancelKeyPress += (_, e) =>
                {
                    // Taking the Ctrl+C rather than letting it kill the process
                    // is what makes bounded cleanup and a partial summary
                    // possible. It matters more here than elsewhere: a killed
                    // process leaves four COM ports held until the OS reclaims
                    // them, and the next run would find them taken.
                    if (interrupted) return;

                    interrupted = true;
                    e.Cancel = true;
                    Console.Error.WriteLine();
                    Console.Error.WriteLine("interrupt received; winding down and writing a partial summary");
                    cancellation.Cancel();
                };

                try
                {
                    return new ScenarioRun(options, cancellation.Token)
                        .ExecuteAsync().GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine("E3-DEV failed outside any check: " + ex);
                    return ExitCodes.Unexpected;
                }
            }
        }
    }

    /// <summary>One controller execution, from preflight to the exit code.</summary>
    internal sealed class ScenarioRun
    {
        private readonly ScenarioOptions _options;
        private readonly CancellationToken _ct;
        private readonly List<string> _setupGaps = new List<string>();
        private readonly List<string> _cleanupProblems = new List<string>();

        private RunArtifacts? _artifacts;
        private CheckRunner? _runner;
        private OwnedPeer? _peerA;
        private OwnedPeer? _peerB;
        private string _com0com = "com0com version not observed";
        private DateTime _startedUtc = DateTime.UtcNow;
        private bool _interrupted;

        public ScenarioRun(ScenarioOptions options, CancellationToken ct)
        {
            _options = options;
            _ct = ct;
        }

        public async Task<int> ExecuteAsync()
        {
            if (_options.PrintScheduleOnly)
            {
                // Opens nothing. This is the first thing to run against a new
                // scenario precisely because it costs nothing and catches a
                // schedule that is not actually deterministic.
                FaultSchedule preview = BuildSchedule();
                Console.WriteLine(preview.ToJson());
                Console.WriteLine();
                Console.WriteLine("normalized-hash " + preview.Hash);
                return ExitCodes.Success;
            }

            using (RunArtifacts artifacts = RunArtifacts.Create(
                _options.ArtifactsRoot,
                _options.CampaignId,
                _options.ScenarioId,
                ScenarioSamples.ColumnNamesForHeader,
                _options.WorkerId))
            {
                _artifacts = artifacts;

                CheckRetention retention = CheckRetention.ForMode(_options.Mode);
                _runner = new CheckRunner(artifacts.Out, _ct, retention, artifacts.AppendCheck);

                artifacts.Out("E3-DEV   " + _options.CampaignId);
                artifacts.Out("target   " + RuntimeFacts.TargetFrameworkMoniker +
                              "  " + RuntimeFacts.RuntimeDescription + "  " + RuntimeFacts.ProcessArchitecture);
                artifacts.Out("pairs    " + _options.PeerAPort + " <-> " + _options.PcbAPort +
                              ", " + _options.PeerBPort + " <-> " + _options.PcbBPort);
                artifacts.Out("run      " + artifacts.CampaignDirectory);
                artifacts.Out(string.Empty);

                return await ExecuteWithArtifactsAsync(artifacts).ConfigureAwait(false);
            }
        }

        private async Task<int> ExecuteWithArtifactsAsync(RunArtifacts artifacts)
        {
            _com0com = ScenarioFacts.DescribeCom0Com(out string? probeGap);
            if (probeGap != null) _setupGaps.Add(probeGap);

            // Written before the port check, deliberately: a run that fails
            // because a name is missing is exactly the run whose environment
            // record - which lists every installed port - is worth having.
            WriteEnvironment(artifacts);

            string? unusable = CheckPortConfiguration();
            if (unusable != null)
            {
                artifacts.Error("E3-DEV: " + unusable);
                return ExitCodes.PrerequisiteMissing;
            }

            FaultSchedule schedule = BuildSchedule();
            artifacts.WriteText(artifacts.SchedulePath, schedule.ToJson());
            artifacts.Out("schedule " + schedule.Events.Count + " planned fault(s), " + schedule.Hash);
            artifacts.Out(string.Empty);

            WorkloadCounters counters = new WorkloadCounters();
            ScenarioSamples samples = new ScenarioSamples();
            ResourceSampler sampler = new ResourceSampler(artifacts, counters, samples);
            int exitCode = ExitCodes.Success;
            PhaseContext? context = null;

            try
            {
                sampler.Take("preflight", "baseline");

                // Every prerequisite failure below sets the exit code and falls
                // through rather than returning: a return inside this try would
                // run the finally as well and clean up twice, which would print
                // two cleanup blocks and probe every port for release twice.
                // Falling through also means a prerequisite failure still writes
                // its result document.
                string? peerProblem = OpenPeers(artifacts);
                if (peerProblem != null)
                {
                    artifacts.Error("E3-DEV: " + peerProblem);
                    exitCode = ExitCodes.PrerequisiteMissing;
                }
                else
                {
                    context = new PhaseContext
                    {
                        Runner = _runner!,
                        Artifacts = artifacts,
                        Counters = counters,
                        Sampler = sampler,
                        Samples = samples,
                        PeerA = _peerA!,
                        PeerB = _peerB!,
                        PcbAPort = _options.PcbAPort,
                        PcbBPort = _options.PcbBPort,
                        Seed = _options.Seed,
                        Ct = _ct
                    };

                    string? pairing = await ProbePairsAsync(artifacts, context).ConfigureAwait(false);
                    if (pairing != null)
                    {
                        artifacts.Error("E3-DEV: " + pairing);
                        exitCode = ExitCodes.PrerequisiteMissing;
                    }
                    else
                    {
                        context.RefreshPeerSamples();
                        sampler.Take("preflight", "post-warm-up");

                        _startedUtc = DateTime.UtcNow;
                        await RunModeAsync(context, schedule).ConfigureAwait(false);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _interrupted = true;
                artifacts.Error("interrupted before the selected mode finished");
            }
            catch (Exception ex)
            {
                artifacts.Error("E3-DEV: the run failed outside any check: " + ex);
                exitCode = ExitCodes.Unexpected;
            }
            finally
            {
                await CleanupAsync(artifacts, context, counters).ConfigureAwait(false);
            }

            return Finish(artifacts, schedule, counters, exitCode);
        }

        // ------------------------------------------------------- preflight

        /// <summary>
        /// The four names must be installed and distinct before anything opens
        /// one. Nothing is discovered by enumeration: the run adopts exactly the
        /// ports it was given, so it can never take a port that belongs to
        /// something else on the machine.
        /// </summary>
        private string? CheckPortConfiguration()
        {
            string[] ports = _options.AllPorts();

            HashSet<string> distinct = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string port in ports)
            {
                if (!distinct.Add(port))
                    return "'" + port + "' was configured more than once; the four ports must be distinct";
            }

            HashSet<string> installed = new HashSet<string>(
                SerialPort.GetPortNames(), StringComparer.OrdinalIgnoreCase);

            List<string> missing = new List<string>();
            foreach (string port in ports)
                if (!installed.Contains(port)) missing.Add(port);

            if (missing.Count > 0)
            {
                return "the configured port(s) " + string.Join(", ", missing.ToArray()) +
                       " are not installed. Present: " + string.Join(", ", SerialPort.GetPortNames());
            }

            return null;
        }

        /// <summary>
        /// Opens the scenario's own end of each pair, which is where the
        /// mutual exclusion with the oracle pass becomes visible.
        /// </summary>
        private string? OpenPeers(RunArtifacts artifacts)
        {
            _peerA = new OwnedPeer(_options.PeerAPort, PeerKind.TextPcbA);
            _peerB = new OwnedPeer(_options.PeerBPort, PeerKind.BinaryPcbB);

            foreach (OwnedPeer peer in new[] { _peerA, _peerB })
            {
                try
                {
                    peer.Open();
                    artifacts.Out("peer     " + peer.PortName + " opened as " + peer.Kind);
                }
                catch (Exception ex)
                {
                    return "the scenario could not open '" + peer.PortName + "': " +
                           ex.GetType().Name + ": " + ex.Message +
                           ". The automated modes need both ends of each pair, so they cannot run while the " +
                           "NekoPcbEmulator holds these ports. Stop it, or run the oracle pass instead by " +
                           "leaving out the mode flag.";
                }
            }

            return null;
        }

        /// <summary>
        /// Proves the pairs are actually cross-connected, which nothing had ever
        /// established: the documented wiring was read from a port list, and a
        /// port list says a name exists, not what it is joined to. A failure
        /// here is an environment result and not a product finding, so it is a
        /// prerequisite rather than a check.
        /// </summary>
        private async Task<string?> ProbePairsAsync(RunArtifacts artifacts, PhaseContext context)
        {
            try
            {
                using (Subject subject = await context.OpenAsync(_options.PcbAPort).ConfigureAwait(false))
                {
                    string? line = await context
                        .ExchangeLineAsync(subject.Transport, PcbA.Ping, 3000).ConfigureAwait(false);

                    if (!string.Equals(line, "OK PONG;", StringComparison.Ordinal))
                    {
                        return "'" + _options.PcbAPort + "' and '" + _options.PeerAPort +
                               "' are not cross-connected: the peer answered '" +
                               PhaseContext.Flatten(line) + "' instead of 'OK PONG;'";
                    }

                    await subject.Transport.Close().ConfigureAwait(false);
                }

                using (Subject subject = await context.OpenAsync(_options.PcbBPort).ConfigureAwait(false))
                {
                    const byte sequence = 0x01;
                    NekoLib.Devices.Core.Abstractions.HardwareResponse response = await context
                        .SendPcbBAsync(subject.Transport, sequence, 3000).ConfigureAwait(false);

                    string? problem = response.Success
                        ? PcbB.Validate(response.RawBytes, sequence)
                        : "the exchange failed: " + response.Status;

                    if (problem != null)
                    {
                        return "'" + _options.PcbBPort + "' and '" + _options.PeerBPort +
                               "' are not cross-connected: " + problem;
                    }

                    await subject.Transport.Close().ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return "the pairing probe failed: " + ex.GetType().Name + ": " + ex.Message;
            }

            artifacts.Out("pairs    both cross-connections verified by a real exchange");
            return null;
        }

        // ------------------------------------------------------------ modes

        private async Task RunModeAsync(PhaseContext context, FaultSchedule schedule)
        {
            switch (_options.Mode)
            {
                case ScenarioMode.Smoke:
                    await RunMatricesAsync(context).ConfigureAwait(false);
                    await SustainAsync(context, _startedUtc + _options.SmokeDuration, "smoke").ConfigureAwait(false);
                    break;

                case ScenarioMode.RecoveryRehearsal:
                    // Warm up first: a fault dispatched before ordinary work has
                    // been shown to succeed would be measuring start-up.
                    await RunMatricesAsync(context).ConfigureAwait(false);
                    context.Sampler.Take("recovery", "post-warm-up");

                    await RecoveryMatrix.RunAsync(context, schedule, _startedUtc).ConfigureAwait(false);

                    context.Sampler.Take("recovery", "cool-down");
                    await RunMatricesAsync(context).ConfigureAwait(false);
                    break;

                case ScenarioMode.Soak:
                    await RunSoakAsync(context, schedule).ConfigureAwait(false);
                    break;
            }

            context.RefreshPeerSamples();
            context.Sampler.Take(_options.Mode.ToString().ToLowerInvariant(), "final");
        }

        private static async Task RunMatricesAsync(PhaseContext context)
        {
            await TransportMatrix.RunAsync(context).ConfigureAwait(false);
            await ProtocolMatrix.RunAsync(context).ConfigureAwait(false);
            await LifecycleMatrix.RunAsync(context).ConfigureAwait(false);
        }

        private async Task RunSoakAsync(PhaseContext context, FaultSchedule schedule)
        {
            DateTime deadline = _startedUtc + _options.SoakDuration;

            // The fault task and the cycle loop run together, serialised through
            // the context's gate: an assertion made while a fault has the peer
            // silent is measuring the fault.
            Task faults = RecoveryMatrix.RunAsync(context, schedule, _startedUtc);

            await SustainAsync(context, deadline, "soak").ConfigureAwait(false);
            await faults.ConfigureAwait(false);
        }

        /// <summary>
        /// Repeats every workload class until the window closes.
        /// <para/>
        /// The matrices finish in well under a minute. Stopping there would give
        /// a smoke that proves the assertions and nothing about behaviour over
        /// time, which is what the suite's 15-to-30-minute window is for - and
        /// for a serial scenario it is also the only way "port, process and
        /// handle stability over the soak" gets any data at all.
        /// </summary>
        private async Task SustainAsync(PhaseContext context, DateTime deadline, string phaseName)
        {
            int cycles = 0;

            while (DateTime.UtcNow < deadline && !_ct.IsCancellationRequested)
            {
                cycles++;
                TimeSpan left = deadline - DateTime.UtcNow;
                if (left < TimeSpan.Zero) left = TimeSpan.Zero;

                context.Artifacts.Out(phaseName + " cycle " + cycles.ToString(CultureInfo.InvariantCulture) +
                                      "  " + ((int)left.TotalMinutes).ToString(CultureInfo.InvariantCulture) +
                                      "m remaining");

                // The sample is taken inside the gate, beside the work it
                // describes, so it can never be taken while a fault has a peer
                // silent or disconnected.
                await context.ExclusiveAsync(async () =>
                {
                    await RunMatricesAsync(context).ConfigureAwait(false);
                    context.RefreshPeerSamples();
                    context.Sampler.Take(phaseName, "periodic");
                }).ConfigureAwait(false);
            }

            context.Artifacts.Out(phaseName + " completed " + cycles + " cycle(s)");
        }

        // --------------------------------------------------------- schedule

        private FaultSchedule BuildSchedule()
        {
            if (_options.FaultSchedulePath != null)
                return FaultSchedule.Load(_options.FaultSchedulePath, _options.ScenarioId);

            string mode;
            TimeSpan window;
            IReadOnlyList<string> kinds;

            switch (_options.Mode)
            {
                case ScenarioMode.RecoveryRehearsal:
                    mode = "recovery-rehearsal";
                    window = _options.RehearsalDuration;
                    kinds = FaultKinds.RecoveryRehearsalSet;
                    break;

                case ScenarioMode.Soak:
                    mode = "soak";
                    window = _options.SoakDuration;
                    kinds = FaultKinds.RecoveryRehearsalSet;
                    break;

                default:
                    // Smoke exercises every workload class without destructive
                    // fault density, so its schedule is deliberately empty and
                    // still written: a run directory must always say what was
                    // planned.
                    mode = "smoke";
                    window = TimeSpan.FromMinutes(20);
                    kinds = new string[0];
                    break;
            }

            return FaultSchedule.Generate(
                _options.CampaignId,
                _options.ScenarioId,
                ScenarioOptions.ScheduleGeneratorVersion,
                mode,
                _options.Seed,
                window,
                kinds,
                new DeviceFaultVocabulary());
        }

        // ---------------------------------------------------------- cleanup

        /// <summary>
        /// Bounded cleanup. A COM port is a machine-wide resource, so the last
        /// thing this asserts is that all four names can be opened again - the
        /// serial equivalent of E3-PIPE's "the endpoint can be rebound", and the
        /// only honest way to say no handle was leaked.
        /// </summary>
        private async Task CleanupAsync(RunArtifacts artifacts, PhaseContext? context, WorkloadCounters counters)
        {
            artifacts.Out(string.Empty);
            artifacts.Out("cleanup");

            foreach (OwnedPeer? peer in new[] { _peerA, _peerB })
            {
                if (peer == null) continue;

                try
                {
                    peer.Dispose();
                    artifacts.Out("  peer     " + peer.PortName + " closed after " +
                                  peer.Responses + " response(s), " + peer.Restarts + " restart(s)");
                }
                catch (Exception ex)
                {
                    _cleanupProblems.Add("disposing the peer on " + peer.PortName + " threw " +
                                         ex.GetType().Name + ": " + ex.Message);
                }
            }

            if (context != null)
            {
                context.Samples.PeerPortsOpen.Set(0);

                long alive = context.Samples.SubjectTransportsAlive.Value;
                if (alive != 0)
                {
                    _cleanupProblems.Add(
                        alive + " transport(s) under test were created and never disposed");
                }

                try { context.ExclusiveAccess.Dispose(); }
                catch (Exception ex) { _cleanupProblems.Add("disposing the gate threw " + ex.GetType().Name); }
            }

            // Give the driver a moment: a port closed microseconds ago can still
            // refuse the next open, and reporting that as a leak would be a
            // false negative in cleanup rather than a finding.
            await Task.Delay(500, CancellationToken.None).ConfigureAwait(false);

            foreach (string port in _options.AllPorts())
            {
                string? problem = TryReopen(port);
                if (problem == null) artifacts.Out("  port     " + port + " reopened and released");
                else _cleanupProblems.Add("'" + port + "' could not be reopened after the run: " + problem);
            }

            artifacts.Out("  counters operations=" + counters.Operations +
                          " successes=" + counters.Successes +
                          " expectedFailures=" + counters.ExpectedFailures +
                          " unexpectedFailures=" + counters.UnexpectedFailures +
                          " cancellations=" + counters.Cancellations);
        }

        private static string? TryReopen(string portName)
        {
            for (int attempt = 0; attempt < 3; attempt++)
            {
                try
                {
                    using (SerialPort port = new SerialPort(portName))
                    {
                        port.ReadTimeout = 50;
                        port.WriteTimeout = 500;
                        port.Open();
                        port.Close();
                    }

                    return null;
                }
                catch (Exception ex)
                {
                    if (attempt == 2) return ex.GetType().Name + ": " + ex.Message;

                    try { Thread.Sleep(500); } catch (ThreadInterruptedException) { }
                }
            }

            return "unreachable";
        }

        // ------------------------------------------------------ the record

        private void WriteEnvironment(RunArtifacts artifacts)
        {
            RuntimeFacts.ReadRepository(out string commit, out bool dirty, out string repositoryDiagnostic);

            JsonWriter json = new JsonWriter();
            json.Object(null, () =>
            {
                json.Prop("campaignId", _options.CampaignId);
                json.Prop("scenarioId", _options.ScenarioId);
                json.Prop("artifactLayoutVersion", artifacts.ArtifactLayoutVersion);
                if (artifacts.WorkerId != null) json.Prop("workerId", artifacts.WorkerId);
                json.Prop("startedUtc", DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture));

                json.Object("repository", () =>
                {
                    json.Prop("commit", commit);
                    json.Prop("dirty", dirty);
                    if (repositoryDiagnostic.Length > 0) json.Prop("diagnostic", repositoryDiagnostic);
                });

                json.Object("host", () =>
                {
                    json.Prop("windowsVersion", RuntimeFacts.WindowsVersion);
                    json.Prop("machineArchitecture", RuntimeFacts.MachineArchitecture);
                    json.Prop("processArchitecture", RuntimeFacts.ProcessArchitecture);
                    json.Prop("logicalProcessors", RuntimeFacts.LogicalProcessorCount);
                    json.Prop("installedMemory", RuntimeFacts.DescribeBytes(RuntimeFacts.InstalledMemoryBytes));
                });

                json.Object("process", () =>
                {
                    json.Prop("targetFramework", RuntimeFacts.TargetFrameworkMoniker);
                    json.Prop("runtime", RuntimeFacts.RuntimeDescription);
                    json.Prop("scenarioVersion", ScenarioFacts.ScenarioVersion);
                    json.Prop("moduleUnderTest", ScenarioFacts.DevicesVersion);
                });

                json.Object("serial", () =>
                {
                    json.Prop("com0com", _com0com);
                    json.Prop("protocolMode", "scenario-owned peer (PCB-A text, PCB-B binary)");
                    json.Prop("emulator", "not used: the automated modes own both ends of each pair");
                    json.Prop("pcbAPort", _options.PcbAPort);
                    json.Prop("pcbBPort", _options.PcbBPort);
                    json.Prop("peerAPort", _options.PeerAPort);
                    json.Prop("peerBPort", _options.PeerBPort);
                    json.Array("installedPorts", () =>
                    {
                        foreach (string port in SerialPort.GetPortNames()) json.Item(port);
                    });
                });

                json.Array("setupGaps", () =>
                {
                    foreach (string gap in _setupGaps) json.Item(gap);
                });

                json.Array("claimBoundaries", () =>
                {
                    foreach (string boundary in ClaimBoundaries) json.Item(boundary);
                });
            });

            artifacts.WriteText(artifacts.EnvironmentPath, json.ToString());
        }

        /// <summary>
        /// What a passing run does and does not establish. The first one is the
        /// loudest because it is the easiest mistake this scenario could cause:
        /// reading an automated pass as protocol evidence, when in these modes
        /// both ends of the conversation were written here.
        /// </summary>
        private static readonly string[] ClaimBoundaries =
        {
            "In the automated modes the peer is owned by this scenario, so the protocol checks are not " +
            "independent-oracle evidence. Two halves of the same project agreeing proves framing was carried " +
            "intact, not that either half is right. The independent claim belongs to the emulator pass, which " +
            "is a separate run and is not replaced by this one.",

            "Every fault is a switch on the scenario's own peer. No fault-injection or test-control API was " +
            "added to NekoLib.Devices, and the emulator was neither modified nor given a control channel.",

            "com0com is a virtual pair. It does not emulate baud, framing, line levels or noise, so nothing " +
            "here is evidence about physical UART behaviour, wiring, USB adapters or electrical conditions. " +
            "What is proved is the real Windows serial API against a real driver.",

            "Linux serial behaviour is out of scope entirely: this scenario is Windows-only and both target " +
            "frameworks run on the same machine and the same driver.",

            "Configuration parity means the fields Configure applies and PortInfo reports. A round trip at a " +
            "given baud rate does not establish that the rate reached hardware, because on a virtual pair it " +
            "does not reach any."
        };

        private bool BelowSpecifiedWindow(TimeSpan elapsed)
        {
            switch (_options.Mode)
            {
                case ScenarioMode.Smoke: return elapsed < ScenarioOptions.MinimumSpecifiedSmoke;
                case ScenarioMode.RecoveryRehearsal: return elapsed < ScenarioOptions.MinimumSpecifiedRehearsal;
                default: return false;
            }
        }

        private int Finish(
            RunArtifacts artifacts,
            FaultSchedule schedule,
            WorkloadCounters counters,
            int exitCode)
        {
            DateTime finishedUtc = DateTime.UtcNow;

            RunSummary summary = new RunSummary
            {
                CampaignId = _options.CampaignId,
                ScenarioId = _options.ScenarioId,
                Mode = _options.Mode.ToString(),
                Seed = _options.Seed,
                ScheduleHash = schedule.Hash,
                ScheduleFaultCount = schedule.Events.Count,
                StartedUtc = _startedUtc,
                FinishedUtc = finishedUtc,
                Interrupted = _interrupted,

                // Judged on elapsed time rather than on what was requested: the
                // schedule reserves a quiet window at each end, so a rehearsal
                // asking for 60 minutes elapses about 53. Ask for about 70.
                BelowSpecifiedWindow = BelowSpecifiedWindow(finishedUtc - _startedUtc),
                ExplicitExitCode = exitCode
            };

            summary.SetupGaps.AddRange(_setupGaps);
            summary.CleanupProblems.AddRange(_cleanupProblems);

            return summary.Write(
                artifacts,
                _runner!,
                counters,
                new DeviceSummary(_options, _com0com, ClaimBoundaries));
        }
    }
}
