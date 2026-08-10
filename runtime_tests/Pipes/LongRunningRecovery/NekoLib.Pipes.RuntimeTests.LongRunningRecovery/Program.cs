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
    internal sealed class ScenarioRun
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

            // This scenario is a first pass: the workload matrices are
            // implemented and the schedule is generated, but no fault dispatcher
            // exists yet. A mode that plans faults and silently fires none would
            // exit 0 having proved nothing about recovery, which is a far worse
            // outcome than refusing to start. It refuses.
            if (_options.Mode != ScenarioMode.Smoke)
            {
                artifacts.Error(
                    "E3-PIPE: " + _options.Mode + " is not implemented yet. The fault schedule is generated and " +
                    "persisted, but nothing dispatches it, so this mode would report success without having " +
                    "injected a single fault. Use --smoke until the recovery matrix lands.");

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
                    return Finish(artifacts, schedule, counters, ExitCodes.PrerequisiteMissing);
                }

                samples.ChildProcesses.Set(_children.Count);
                sampler.Take("preflight", "post-warm-up");

                _startedUtc = DateTime.UtcNow;
                await RunModeAsync(context).ConfigureAwait(false);
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

        private async Task RunModeAsync(PhaseContext context)
        {
            // Every mode runs the same matrices; only the duration and the fault
            // schedule differ. The soak and the sustained smoke additionally
            // keep client children generating background traffic.
            await RunMatricesAsync(context).ConfigureAwait(false);

            context.Sampler.Take(_options.Mode.ToString().ToLowerInvariant(), "final");
        }

        private static async Task RunMatricesAsync(PhaseContext context)
        {
            await RequestMatrix.RunAsync(context).ConfigureAwait(false);
            await EventMatrix.RunAsync(context).ConfigureAwait(false);
            await ProtocolMatrix.RunAsync(context).ConfigureAwait(false);
            await LifecycleMatrix.RunAsync(context).ConfigureAwait(false);
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
