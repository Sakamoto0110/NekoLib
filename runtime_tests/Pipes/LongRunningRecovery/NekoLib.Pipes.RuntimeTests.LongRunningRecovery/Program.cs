#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NekoLib.RuntimeTests.Harness;
using NekoLib.RuntimeTests.Harness.Faults;
using NekoLib.RuntimeTests.Harness.Reporting;

namespace NekoLib.Pipes.RuntimeTests.LongRunningRecovery
{
    /// <summary>
    /// E3-PIPE: <c>NekoLib.Pipes</c> driven across real process boundaries.
    /// <para/>
    /// The controller owns the run: it allocates a per-run endpoint, starts a
    /// server child and client children, asserts against them as an ordinary
    /// client would, dispatches the seeded faults, and reconciles. The children
    /// assert nothing — they are traffic, and their outcome is an exit code and
    /// a small result document the controller reads.
    /// <para/>
    /// That division is also the answer to the harness question this scenario
    /// raised: <b>the harness gained nothing</b>. One process owns the
    /// <c>RunArtifacts</c> and writes the single <c>result.json</c> the suite
    /// specifies; the children are workload, not workers. Multi-process support
    /// in the harness would have been speculative generality for a shape one
    /// scenario needs.
    /// </summary>
    internal static class Program
    {
        private static int Main(string[] args)
        {
            ScenarioOptions options = new ScenarioOptions();
            if (!options.TryParse(args, out string diagnostic))
            {
                Console.Error.WriteLine("E3-PIPE: " + diagnostic);
                Console.Error.WriteLine();
                Console.Error.WriteLine(ScenarioOptions.UsageText());
                return ExitCodes.Usage;
            }

            using (CancellationTokenSource cancellation = new CancellationTokenSource())
            {
                bool interrupted = false;

                Console.CancelKeyPress += (_, e) =>
                {
                    if (interrupted) return;

                    interrupted = true;
                    e.Cancel = true;
                    Console.Error.WriteLine();
                    Console.Error.WriteLine("interrupt received; winding down and writing a partial summary");
                    cancellation.Cancel();
                };

                try
                {
                    switch (options.Role)
                    {
                        case ScenarioRole.Server: return ServerRole.Run(options, cancellation.Token);
                        case ScenarioRole.Client: return ClientRole.Run(options, cancellation.Token);
                        default:
                            return new ScenarioRun(options, cancellation.Token)
                                .ExecuteAsync().GetAwaiter().GetResult();
                    }
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine("E3-PIPE failed outside any check: " + ex);
                    return ExitCodes.Unexpected;
                }
            }
        }
    }

    /// <summary>One controller execution, from preflight to the exit code.</summary>
    internal sealed class ScenarioRun : IProcessControl
    {
        private readonly ScenarioOptions _options;
        private readonly CancellationToken _ct;
        private readonly List<string> _setupGaps = new List<string>();
        private readonly List<string> _cleanupProblems = new List<string>();
        private readonly List<ChildProcess> _children = new List<ChildProcess>();

        private RunArtifacts? _artifacts;
        private CheckRunner? _runner;
        private DateTime _startedUtc = DateTime.UtcNow;
        private bool _interrupted;
        private string _pipeName = string.Empty;

        public ScenarioRun(ScenarioOptions options, CancellationToken ct)
        {
            _options = options;
            _ct = ct;
        }

        public async Task<int> ExecuteAsync()
        {
            _pipeName = Endpoint.ForCampaign(_options.CampaignId);

            if (_options.PrintScheduleOnly)
            {
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
                ScenarioSamples.ColumnNamesForHeader))
            {
                _artifacts = artifacts;

                CheckRetention retention = CheckRetention.ForMode(_options.Mode);
                _runner = new CheckRunner(artifacts.Out, _ct, retention, artifacts.AppendCheck);

                artifacts.Out("E3-PIPE  " + _options.CampaignId);
                artifacts.Out("target   " + RuntimeFacts.TargetFrameworkMoniker +
                              "  " + RuntimeFacts.RuntimeDescription);
                artifacts.Out("endpoint " + _pipeName);
                artifacts.Out("run      " + artifacts.CampaignDirectory);
                artifacts.Out(string.Empty);

                return await ExecuteWithArtifactsAsync(artifacts).ConfigureAwait(false);
            }
        }

        private async Task<int> ExecuteWithArtifactsAsync(RunArtifacts artifacts)
        {
            // The one prerequisite the controller cannot create for itself: the
            // endpoint it allocated must be free. A bound name means an earlier
            // run left something behind, which is an environment result rather
            // than a product finding.
            if (Endpoint.IsBound(_pipeName))
            {
                artifacts.Error("E3-PIPE: '" + _pipeName + "' is already bound; an earlier run left a server behind");
                return ExitCodes.PrerequisiteMissing;
            }

            WriteEnvironment(artifacts);

            FaultSchedule schedule = BuildSchedule();
            artifacts.WriteText(artifacts.SchedulePath, schedule.ToJson());
            artifacts.Out("schedule " + schedule.Events.Count + " planned fault(s), " + schedule.Hash);
            artifacts.Out(string.Empty);

            WorkloadCounters counters = new WorkloadCounters();
            ScenarioSamples samples = new ScenarioSamples();
            ResourceSampler sampler = new ResourceSampler(artifacts, counters, samples);
            int exitCode = ExitCodes.Success;
            ChildProcess? server = null;

            PhaseContext context = new PhaseContext
            {
                Runner = _runner!,
                Artifacts = artifacts,
                Counters = counters,
                Sampler = sampler,
                Samples = samples,
                PipeName = _pipeName,
                Seed = _options.Seed,
                Ct = _ct
            };

            try
            {
                sampler.Take("preflight", "baseline");

                server = StartServer(TimeSpan.FromHours(6));
                if (!await WaitForServerAsync(artifacts).ConfigureAwait(false))
                {
                    artifacts.Error("E3-PIPE: the server child never bound '" + _pipeName + "'");
                    exitCode = ExitCodes.PrerequisiteMissing;
                }
                else
                {
                    Server = server;

                    if (_options.Mode != ScenarioMode.Smoke)
                    {
                        TimeSpan lifetime = _options.Mode == ScenarioMode.Soak
                            ? _options.SoakDuration
                            : _options.RehearsalDuration;

                        StartClients(context, lifetime + TimeSpan.FromMinutes(5));
                    }

                    RefreshChildSamples(context);
                    sampler.Take("preflight", "post-warm-up");

                    _startedUtc = DateTime.UtcNow;
                    await RunModeAsync(context, schedule).ConfigureAwait(false);
                    await ReportClientsAsync(context).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                _interrupted = true;
                artifacts.Error("interrupted before the selected mode finished");
            }
            catch (Exception ex)
            {
                artifacts.Error("E3-PIPE: the run failed outside any check: " + ex);
                exitCode = ExitCodes.Unexpected;
            }
            finally
            {
                await CleanupAsync(artifacts, context, counters).ConfigureAwait(false);
            }

            return Finish(artifacts, schedule, counters, exitCode);
        }

        private async Task RunModeAsync(PhaseContext context, FaultSchedule schedule)
        {
            switch (_options.Mode)
            {
                case ScenarioMode.Smoke:
                    await RunSmokeAsync(context).ConfigureAwait(false);
                    break;

                case ScenarioMode.RecoveryRehearsal:
                    // Warm up first: a fault dispatched before ordinary work has
                    // been shown to succeed would be measuring start-up.
                    await RunMatricesAsync(context).ConfigureAwait(false);
                    context.Sampler.Take("recovery", "post-warm-up");

                    await RecoveryMatrix.RunAsync(context, this, schedule, _startedUtc).ConfigureAwait(false);

                    context.Sampler.Take("recovery", "cool-down");
                    await RunMatricesAsync(context).ConfigureAwait(false);
                    break;

                case ScenarioMode.Soak:
                    await RunSoakAsync(context, schedule).ConfigureAwait(false);
                    break;
            }

            context.Sampler.Take(_options.Mode.ToString().ToLowerInvariant(), "final");
        }

        private static async Task RunMatricesAsync(PhaseContext context)
        {
            await RequestMatrix.RunAsync(context).ConfigureAwait(false);
            await CancellationMatrix.RunAsync(context).ConfigureAwait(false);
            await EventMatrix.RunAsync(context).ConfigureAwait(false);
            await OverflowMatrix.RunAsync(context).ConfigureAwait(false);
            await ProtocolMatrix.RunAsync(context).ConfigureAwait(false);
            await LifecycleMatrix.RunAsync(context).ConfigureAwait(false);
        }

        /// <summary>
        /// Every workload class, then the same classes repeated under the client
        /// children's traffic until the window closes.
        /// <para/>
        /// The matrices finish in well under a minute. Stopping there would give
        /// a smoke that proves the assertions and nothing about behaviour over
        /// time, which is what the suite's 15-to-30-minute window is for.
        /// </summary>
        private async Task RunSmokeAsync(PhaseContext context)
        {
            await RunMatricesAsync(context).ConfigureAwait(false);
            await SustainAsync(context, _startedUtc + _options.SmokeDuration, "smoke").ConfigureAwait(false);
        }

        private async Task RunSoakAsync(PhaseContext context, FaultSchedule schedule)
        {
            DateTime deadline = _startedUtc + _options.SoakDuration;

            // The fault task and the cycle loop run together, serialised through
            // the context's gate: an assertion made while a fault has the server
            // killed is measuring the fault.
            Task faults = RecoveryMatrix.RunAsync(context, this, schedule, _startedUtc);

            await SustainAsync(context, deadline, "soak").ConfigureAwait(false);
            await faults.ConfigureAwait(false);
        }

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
                // describes, so it can never be taken while a fault has the
                // server away.
                await context.ExclusiveAsync(async () =>
                {
                    await RunMatricesAsync(context).ConfigureAwait(false);
                    RefreshChildSamples(context);
                    context.Sampler.Take(phaseName, "periodic");
                }).ConfigureAwait(false);
            }

            context.Artifacts.Out(phaseName + " completed " + cycles + " cycle(s)");
        }

        private void RefreshChildSamples(PhaseContext context)
        {
            int alive = 0;
            foreach (ChildProcess child in _children)
                if (!child.HasExited) alive++;

            context.Samples.ChildProcesses.Set(alive);
        }

        // ------------------------------------------------- IProcessControl

        public ChildProcess? Server { get; private set; }

        public IReadOnlyList<ChildProcess> Clients => _clients;

        private readonly List<ChildProcess> _clients = new List<ChildProcess>();

        public ChildProcess RestartServer()
        {
            Server = StartServer(TimeSpan.FromHours(6));
            return Server;
        }

        public bool KillOneClient(out string diagnostic)
        {
            foreach (ChildProcess client in _clients)
            {
                if (client.HasExited) continue;

                bool killed = client.Kill(out diagnostic);
                diagnostic = "pid " + client.Id + " " + diagnostic;
                return killed;
            }

            diagnostic = "no live client child remained";
            return false;
        }

        public async Task<bool> WaitForServerAsync(TimeSpan timeout)
        {
            DateTime deadline = DateTime.UtcNow + timeout;

            while (DateTime.UtcNow < deadline && !_ct.IsCancellationRequested)
            {
                if (Endpoint.IsBound(_pipeName)) return true;
                await Task.Delay(100, _ct).ConfigureAwait(false);
            }

            return false;
        }

        /// <summary>
        /// Starts the client children that make this a multi-process load.
        /// <para/>
        /// Smoke runs without them: its value is the assertions, and adding
        /// three processes of background traffic would make every count in them
        /// approximate for no gain. The rehearsal and the soak need them,
        /// because "the server kept serving while a client was killed" has no
        /// meaning without clients to kill.
        /// </summary>
        private void StartClients(PhaseContext context, TimeSpan lifetime)
        {
            for (int i = 0; i < _options.Clients; i++)
            {
                string result = Path.Combine(
                    _artifacts!.ScenarioDirectory,
                    "client-" + i.ToString(CultureInfo.InvariantCulture) + "-result.json");

                string arguments =
                    "--role client --pipe " + _pipeName +
                    " --child-duration " + ((int)lifetime.TotalSeconds).ToString(CultureInfo.InvariantCulture) + "s" +
                    " --child-result \"" + result + "\" --smoke";

                ChildProcess child = ChildProcess.Start("client", arguments, result);
                _children.Add(child);
                _clients.Add(child);
            }

            _artifacts!.Out("clients  " + _clients.Count + " child process(es) started");
            RefreshChildSamples(context);
        }

        /// <summary>
        /// Turns the client children's own result documents into one check, so
        /// their work is part of the verdict rather than decoration.
        /// </summary>
        private Task ReportClientsAsync(PhaseContext context)
        {
            if (_clients.Count == 0) return PhaseContext.CompletedTask;

            return context.Runner.RunAsync(Phases.Request, "client-children-correlation",
                "every client process completed requests and none saw a response meant for another",
                check =>
                {
                    long sent = 0;
                    long ok = 0;
                    long failed = 0;
                    long mismatched = 0;
                    int reported = 0;

                    foreach (ChildProcess client in _clients)
                    {
                        Dictionary<string, object?>? result = client.ReadResult();
                        if (result == null) continue;

                        reported++;
                        sent += JsonParser.RequireInt(result, "sent");
                        ok += JsonParser.RequireInt(result, "ok");
                        failed += JsonParser.RequireInt(result, "failed");
                        mismatched += JsonParser.RequireInt(result, "mismatchedCorrelation");
                    }

                    check.That(reported > 0, "no client child wrote a result document");
                    check.Equal(0, mismatched, "responses that reached the wrong client process");
                    check.That(ok > 0, "no client process completed a single request");

                    context.Samples.RequestsSent.Add(sent);
                    context.Samples.RequestsFailed.Add(failed);

                    check.Note(reported + " client process(es) reported " + sent + " requests, " + ok +
                               " successful and " + failed + " failed. Failures are expected here: a scheduled " +
                               "fault takes the server away while these keep sending");

                    return PhaseContext.CompletedTask;
                });
        }

        private ChildProcess StartServer(TimeSpan lifetime)
        {
            string result = Path.Combine(_artifacts!.ScenarioDirectory, "server-result.json");
            string arguments =
                "--role server --pipe " + _pipeName +
                " --child-duration " + ((int)lifetime.TotalSeconds).ToString(CultureInfo.InvariantCulture) + "s" +
                " --child-result \"" + result + "\" --smoke";

            ChildProcess child = ChildProcess.Start("server", arguments, result);
            _children.Add(child);
            _artifacts.Out("server   pid " + child.Id + " started");
            return child;
        }

        private async Task<bool> WaitForServerAsync(RunArtifacts artifacts)
        {
            DateTime deadline = DateTime.UtcNow.AddSeconds(30);

            while (DateTime.UtcNow < deadline && !_ct.IsCancellationRequested)
            {
                if (Endpoint.IsBound(_pipeName))
                {
                    artifacts.Out("server   bound " + _pipeName);
                    return true;
                }

                await Task.Delay(100, _ct).ConfigureAwait(false);
            }

            return false;
        }

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
                new PipeFaultVocabulary());
        }

        /// <summary>
        /// Bounded cleanup. The endpoint is a machine-wide resource, so the last
        /// thing this asserts is that the name it took has been given back.
        /// </summary>
        private async Task CleanupAsync(RunArtifacts artifacts, PhaseContext context, WorkloadCounters counters)
        {
            artifacts.Out(string.Empty);
            artifacts.Out("cleanup");

            // Ask the server to end itself before forcing anything.
            if (Endpoint.IsBound(_pipeName))
            {
                Exception? asked = await PhaseContext.CaptureAsync(async () =>
                {
                    using (NekoLib.Pipes.PipeClient client = new NekoLib.Pipes.PipeClient(
                        new NekoLib.Pipes.PipeClientOptions
                        {
                            PipeName = _pipeName,
                            ConnectTimeout = TimeSpan.FromSeconds(3),
                            RequestTimeout = TimeSpan.FromSeconds(3)
                        }))
                    {
                        await client.SendAsync(Ops.Shutdown, null, CancellationToken.None).ConfigureAwait(false);
                    }
                }).ConfigureAwait(false);

                artifacts.Out("  server   graceful shutdown " +
                              (asked == null ? "requested" : "not accepted: " + asked.GetType().Name));
            }

            foreach (ChildProcess child in _children)
            {
                if (!child.WaitForExit(TimeSpan.FromSeconds(10)))
                {
                    string diagnostic;
                    bool killed = child.Kill(out diagnostic);
                    artifacts.Out("  " + child.Role + "   pid " + child.Id + " " + diagnostic);

                    if (!killed)
                        _cleanupProblems.Add(child.Role + " pid " + child.Id + " could not be ended: " + diagnostic);
                }
                else
                {
                    artifacts.Out("  " + child.Role + "   pid " + child.Id + " exited " +
                                  (child.ExitCode == null ? "unknown" : child.ExitCode.Value.ToString(
                                      CultureInfo.InvariantCulture)));
                }

                child.Dispose();
            }

            context.Samples.ChildProcesses.Set(0);

            // The endpoint must be free again, or the next run cannot start.
            DateTime deadline = DateTime.UtcNow.AddSeconds(15);
            while (Endpoint.IsBound(_pipeName) && DateTime.UtcNow < deadline)
                await Task.Delay(200, CancellationToken.None).ConfigureAwait(false);

            if (Endpoint.IsBound(_pipeName))
                _cleanupProblems.Add("the endpoint '" + _pipeName + "' was still bound after cleanup");
            else
                artifacts.Out("  endpoint " + _pipeName + " released");

            try { context.ExclusiveAccess.Dispose(); }
            catch (Exception ex) { _cleanupProblems.Add("disposing the gate threw " + ex.GetType().Name); }

            artifacts.Out("  counters operations=" + counters.Operations +
                          " successes=" + counters.Successes +
                          " expectedFailures=" + counters.ExpectedFailures +
                          " unexpectedFailures=" + counters.UnexpectedFailures);
        }

        private void WriteEnvironment(RunArtifacts artifacts)
        {
            RuntimeFacts.ReadRepository(out string commit, out bool dirty, out string repositoryDiagnostic);

            JsonWriter json = new JsonWriter();
            json.Object(null, () =>
            {
                json.Prop("campaignId", _options.CampaignId);
                json.Prop("scenarioId", _options.ScenarioId);
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
                    json.Prop("moduleUnderTest", ScenarioFacts.PipesVersion);
                });

                json.Object("endpoint", () =>
                {
                    json.Prop("pipeName", _pipeName);
                    json.Prop("eventsPipeName", Endpoint.EventsFor(_pipeName));
                    json.Prop("frameLimitBytes", 64 * 1024);
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
        /// What a passing run does and does not establish. The security boundary
        /// is the one worth stating loudest: everything here runs as one user on
        /// one machine, which is not an access-control test however many pipes
        /// it opens.
        /// </summary>
        private static readonly string[] ClaimBoundaries =
        {
            "Every process is local and runs as the same user. This is not evidence about pipe ACLs, remote hosts, " +
            "elevated users or an adversarial peer, and the suite forbids claiming any of them from a same-user test.",

            "Every fault is produced by a process this controller started, by a handler this project wrote, or by a " +
            "raw pipe peer it opened itself. No fault-injection or control API was added to NekoLib.Pipes.",

            "The frame limit is deliberately 64 KiB rather than the module's 1 MiB default, so the over-limit paths " +
            "are reachable in seconds. That proves the limit's mechanics, not a capacity figure.",

            "The subscriber queue capacity is deliberately 8. Overflow behaviour is what is proved; production queue " +
            "sizing is a different claim."
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
                BelowSpecifiedWindow = BelowSpecifiedWindow(finishedUtc - _startedUtc),
                ExplicitExitCode = exitCode
            };

            summary.SetupGaps.AddRange(_setupGaps);
            summary.CleanupProblems.AddRange(_cleanupProblems);

            return summary.Write(
                artifacts,
                _runner!,
                counters,
                new PipeSummary(_pipeName, ClaimBoundaries));
        }
    }
}
