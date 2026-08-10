#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NekoLib.Core.Inspection;
using NekoLib.Core.Logging;
using NekoLib.Core.Telemetry;
using NekoLib.Observability.RuntimeTests.LongRunningRecovery.Faults;
using NekoLib.Observability.RuntimeTests.LongRunningRecovery.Sinks;
using NekoLib.Observability.RuntimeTests.LongRunningRecovery.Workload;
using NekoLib.RuntimeTests.Harness;
using NekoLib.RuntimeTests.Harness.Faults;
using NekoLib.RuntimeTests.Harness.Reporting;

namespace NekoLib.Observability.RuntimeTests.LongRunningRecovery
{
    /// <summary>
    /// E3-OBS: NekoLib Logging, Telemetry and passive Inspection composed the
    /// way an application composes them, then driven under load, deterministic
    /// failure and recovery.
    /// <para/>
    /// Three capabilities share one process here for the practical reason that
    /// they are opt-in halves of the same story, but they are never one claim.
    /// Each has its own phase, its own checks and its own section in the result,
    /// and one collapsing entirely does not stop the other two from being run
    /// and reported.
    /// <para/>
    /// What this cannot establish is equally important: only Navigation has
    /// Inspection hooks, so nothing here is evidence that Data, Pipes, Watchdog,
    /// Devices or Diagnostics emit anything. This scenario is an application
    /// pushing through public APIs, and it says so in its own claim boundaries.
    /// </summary>
    internal static class Program
    {
        private static int Main(string[] args)
        {
            ScenarioOptions options = new ScenarioOptions();
            if (!options.TryParse(args, out string diagnostic))
            {
                Console.Error.WriteLine("E3-OBS: " + diagnostic);
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
                    // possible; a second press still ends things immediately.
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
                    Console.Error.WriteLine("E3-OBS failed outside any check: " + ex);
                    return ExitCodes.Unexpected;
                }
            }
        }
    }

    /// <summary>One execution, from preflight to the exit code.</summary>
    internal sealed class ScenarioRun
    {
        private readonly ScenarioOptions _options;
        private readonly CancellationToken _ct;
        private readonly List<string> _setupGaps = new List<string>();
        private readonly List<string> _cleanupProblems = new List<string>();

        private RunArtifacts? _artifacts;
        private CheckRunner? _runner;
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
                _runner = new CheckRunner(artifacts.Out, _ct);

                artifacts.Out("E3-OBS  " + _options.CampaignId);
                artifacts.Out("target  " + RuntimeFacts.TargetFrameworkMoniker +
                              "  " + RuntimeFacts.RuntimeDescription);
                artifacts.Out("run     " + artifacts.CampaignDirectory);
                artifacts.Out(string.Empty);

                return await ExecuteWithArtifactsAsync(artifacts).ConfigureAwait(false);
            }
        }

        private async Task<int> ExecuteWithArtifactsAsync(RunArtifacts artifacts)
        {
            // The one prerequisite: somewhere to write. Everything else this
            // scenario needs it creates itself, which is why it has no setup
            // script and no adopted resource.
            string working = Path.Combine(artifacts.ScenarioDirectory, "work");
            try
            {
                Directory.CreateDirectory(working);
                string probe = Path.Combine(working, "probe.tmp");
                File.WriteAllText(probe, "probe");
                File.Delete(probe);
            }
            catch (Exception ex)
            {
                artifacts.Error("E3-OBS: the working directory is not usable: " + ex.Message);
                return ExitCodes.PrerequisiteMissing;
            }

            // The process-wide Inspection slot has to be free, because several
            // checks install into it. Finding it occupied is an environment
            // result, not a product finding.
            if (!_options.NoGlobalInspection && InspectionProvider.Current.IsEnabled)
            {
                artifacts.Error("E3-OBS: the process-wide Inspection slot is already occupied. " +
                                "Pass --no-global-inspection to run the rest.");
                return ExitCodes.PrerequisiteMissing;
            }

            WriteEnvironment(artifacts);

            FaultSchedule schedule = BuildSchedule();
            artifacts.WriteText(artifacts.SchedulePath, schedule.ToJson());
            artifacts.Out("schedule   " + schedule.Events.Count + " planned fault(s), " + schedule.Hash);
            artifacts.Out(string.Empty);

            WorkloadCounters counters = new WorkloadCounters();
            int exitCode = ExitCodes.Success;
            ObservabilityWorkspace? workspace = null;
            PhaseContext? context = null;

            try
            {
                workspace = new ObservabilityWorkspace(working, _options.Seed);
                ScenarioSamples samples = new ScenarioSamples(workspace);
                ResourceSampler sampler = new ResourceSampler(artifacts, counters, samples);

                context = new PhaseContext
                {
                    Workspace = workspace,
                    Runner = _runner!,
                    Counters = counters,
                    Sampler = sampler,
                    Samples = samples,
                    Artifacts = artifacts,
                    WorkingDirectory = working,
                    Seed = _options.Seed,
                    LogRate = _options.LogRate,
                    Ct = _ct
                };

                sampler.Take("preflight", "baseline");

                // A short warm-up before anything is asserted or injected: a
                // check that runs first measures start-up as much as behaviour.
                await WarmUpAsync(context).ConfigureAwait(false);
                sampler.Take("preflight", "post-warm-up");

                _startedUtc = DateTime.UtcNow;
                await RunModeAsync(context, schedule).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                _interrupted = true;
                artifacts.Error("interrupted before the selected mode finished");
            }
            catch (Exception ex)
            {
                artifacts.Error("E3-OBS: the run failed outside any check: " + ex);
                exitCode = ExitCodes.Unexpected;
            }
            finally
            {
                await CleanupAsync(artifacts, context, workspace, working, counters).ConfigureAwait(false);
            }

            return Finish(artifacts, schedule, counters, exitCode);
        }

        private static async Task WarmUpAsync(PhaseContext context)
        {
            for (int i = 1; i <= 200; i++)
            {
                context.Workspace.Logger.Log(LogLevel.Debug, TracedMessage.Compose(99, i));
                context.Samples.LogEntriesWritten.Increment();

                ITelemetryOperation operation = context.Workspace.Telemetry
                    .StartOperation("scenario", "warm-up", "warm-" + i);
                operation.Complete(TelemetryOutcome.Succeeded);
                context.Samples.TelemetryCompleted.Increment();

                context.Workspace.Inspection.Record("scenario", "warm-up");
                context.Samples.InspectionRecorded.Increment();
            }

            context.Workspace.Logger.Flush(TimeSpan.FromSeconds(10));
            await Task.Yield();
        }

        private async Task RunModeAsync(PhaseContext context, FaultSchedule schedule)
        {
            switch (_options.Mode)
            {
                case ScenarioMode.Smoke:
                    await RunSmokeAsync(context).ConfigureAwait(false);
                    break;

                case ScenarioMode.RecoveryRehearsal:
                    // Warm-up first, then faults, then the capability matrices
                    // again so the run ends by proving ordinary work still holds.
                    await RunCapabilitiesAsync(context).ConfigureAwait(false);
                    context.Sampler.Take("recovery", "post-warm-up");

                    await RecoveryMatrix.RunAsync(context, schedule, _startedUtc).ConfigureAwait(false);

                    context.Sampler.Take("recovery", "cool-down");
                    await RunCapabilitiesAsync(context).ConfigureAwait(false);
                    break;

                case ScenarioMode.Soak:
                    await RunSoakAsync(context, schedule).ConfigureAwait(false);
                    break;
            }

            context.Sampler.Take(_options.Mode.ToString().ToLowerInvariant(), "final");
            await ResourceMatrix.RunAsync(context).ConfigureAwait(false);
        }

        /// <summary>
        /// Every workload class, then the same classes repeated under steady
        /// traffic for the specified window.
        /// <para/>
        /// The matrices finish in seconds on this hardware. Stopping there would
        /// give a smoke that proves the assertions and nothing about behaviour
        /// over time, and the suite asks for 15 to 30 minutes precisely because
        /// the second part is where drift shows up.
        /// </summary>
        private async Task RunSmokeAsync(PhaseContext context)
        {
            await RunCapabilitiesAsync(context).ConfigureAwait(false);

            DateTime deadline = _startedUtc + _options.SmokeDuration;
            if (DateTime.UtcNow >= deadline)
            {
                context.Artifacts.Out("smoke      the matrices already exceeded the requested window");
                return;
            }

            Task background = SteadyTrafficAsync(context, deadline);

            await ResourceMatrix.SustainAsync(
                context, deadline, () => RunCapabilitiesAsync(context), "smoke").ConfigureAwait(false);

            await background.ConfigureAwait(false);
        }

        /// <summary>
        /// Runs the three capability matrices so that one collapsing outside its
        /// own checks cannot remove the other two from the result.
        /// </summary>
        private static async Task RunCapabilitiesAsync(PhaseContext context)
        {
            await RunCapabilityAsync(context, Phases.Logging, () => LoggingMatrix.RunAsync(context))
                .ConfigureAwait(false);
            await RunCapabilityAsync(context, Phases.Telemetry, () => TelemetryMatrix.RunAsync(context))
                .ConfigureAwait(false);
            await RunCapabilityAsync(context, Phases.Inspection, () => InspectionMatrix.RunAsync(context))
                .ConfigureAwait(false);
        }

        private static async Task RunCapabilityAsync(PhaseContext context, string capability, Func<Task> body)
        {
            try
            {
                await body().ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // An interrupt is not a capability failure; it ends the run.
                throw;
            }
            catch (Exception ex)
            {
                // A capability that fails outside its own checks would otherwise
                // vanish from the result, and a missing section reads as "not
                // run" rather than "broke". This records the collapse as a
                // failed check inside that capability's own section.
                string detail = ex.GetType().Name + ": " + PhaseContext.Flatten(ex.Message);
                await context.Runner.RunAsync(capability, capability + "-section-completed",
                    "the capability's own checks ran to completion",
                    check =>
                    {
                        check.That(false, "the section stopped early with " + detail);
                        return PhaseContext.CompletedTask;
                    }).ConfigureAwait(false);

                context.Artifacts.Error("capability " + capability + " stopped early: " + detail);
                context.Counters.UnexpectedFailure();
            }
        }

        private async Task RunSoakAsync(PhaseContext context, FaultSchedule schedule)
        {
            DateTime deadline = _startedUtc + _options.SoakDuration;

            // The fault task and the cycle loop run together, serialised through
            // the context's exclusivity gate: a matrix asserts, and an assertion
            // made while a scheduled fault is applied measures the fault rather
            // than the library. Steady traffic holds nothing and is free to fail
            // and be counted.
            Task faults = RecoveryMatrix.RunAsync(context, schedule, _startedUtc);
            Task traffic = SteadyTrafficAsync(context, deadline);

            await ResourceMatrix.SustainAsync(
                context, deadline, () => RunCapabilitiesAsync(context), "soak").ConfigureAwait(false);

            await faults.ConfigureAwait(false);
            await traffic.ConfigureAwait(false);
        }

        /// <summary>
        /// Background traffic through all three capabilities for the length of
        /// the soak. It asserts nothing: its job is to keep the bounded
        /// structures churning so the samples describe a system under load
        /// rather than an idle one.
        /// </summary>
        private async Task SteadyTrafficAsync(PhaseContext context, DateTime deadline)
        {
            long sequence = 0;
            int delayMilliseconds = _options.LogRate > 0
                ? Math.Max(1, 1000 / _options.LogRate)
                : 1;

            while (DateTime.UtcNow < deadline && !_ct.IsCancellationRequested)
            {
                sequence++;

                context.Workspace.Logger.Log(LogLevel.Info, TracedMessage.Compose(7, sequence));
                context.Samples.LogEntriesWritten.Increment();

                ITelemetryOperation operation = context.Workspace.Telemetry
                    .StartOperation("scenario", "steady", "steady-" + sequence);
                operation.Checkpoint("mid");
                operation.Complete(TelemetryOutcome.Succeeded);
                context.Samples.TelemetryCompleted.Increment();

                context.Workspace.Inspection.Record("scenario", "steady");
                context.Samples.InspectionRecorded.Increment();

                context.Counters.Success();

                if (sequence % 50 == 0)
                    await Task.Delay(delayMilliseconds, _ct).ConfigureAwait(false);
            }
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
                    kinds = Selected();
                    break;

                case ScenarioMode.Soak:
                    mode = "soak";
                    window = _options.SoakDuration;
                    kinds = Selected();
                    break;

                default:
                    // Smoke exercises every workload class without destructive
                    // fault density, so its schedule is deliberately empty and is
                    // still written, so a run directory always says what was
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
                new ObservabilityFaultVocabulary());
        }

        /// <summary>
        /// The fault kinds this run may inject. Dropping the global-slot fault
        /// changes the schedule, and therefore its hash, which is correct: a run
        /// that covers six of seven transitions is a different plan and must not
        /// claim the same determinism evidence.
        /// </summary>
        private string[] Selected()
        {
            if (!_options.NoGlobalInspection) return FaultKinds.RecoveryRehearsalSet;

            List<string> kinds = new List<string>();
            foreach (string kind in FaultKinds.RecoveryRehearsalSet)
                if (kind != FaultKinds.InspectionGlobalTeardown) kinds.Add(kind);

            return kinds.ToArray();
        }

        /// <summary>
        /// Bounded cleanup that runs whatever happened above, including after an
        /// interrupt. Deleting the working directory is the assertion, not the
        /// housekeeping: on Windows it fails outright if any sink still holds a
        /// file, which is how "no unexpected process-held file handle" is proved
        /// rather than asserted.
        /// </summary>
        private Task CleanupAsync(
            RunArtifacts artifacts,
            PhaseContext? context,
            ObservabilityWorkspace? workspace,
            string working,
            WorkloadCounters counters)
        {
            artifacts.Out(string.Empty);
            artifacts.Out("cleanup");

            if (workspace != null)
            {
                try
                {
                    workspace.Dispose();
                    artifacts.Out("  workspace  logger, pipeline and inspection runtime disposed");
                }
                catch (Exception ex)
                {
                    _cleanupProblems.Add("disposing the workspace threw " + ex.GetType().Name + ": " + ex.Message);
                }

                if (workspace.Healthy.DisposeCount != 1)
                {
                    _cleanupProblems.Add(
                        "the logger disposed its healthy sink " + workspace.Healthy.DisposeCount +
                        " time(s); exactly one was expected with DisposeSinks enabled");
                }

                if (workspace.Inspection.IsEnabled)
                    _cleanupProblems.Add("the inspection runtime is still enabled after disposal");
            }

            // Nothing may still own the process-wide slot.
            if (InspectionProvider.Current.IsEnabled)
            {
                _cleanupProblems.Add(
                    "the process-wide Inspection slot is still occupied; a global runtime was not disposed");
            }
            else
            {
                artifacts.Out("  inspection process-wide slot is back to the null recorder");
            }

            if (_options.KeepWorkingDirectory)
            {
                artifacts.Out("  work       kept at the caller's request: " + working);
            }
            else
            {
                try
                {
                    long files = Directory.Exists(working)
                        ? Directory.GetFiles(working, "*", SearchOption.AllDirectories).Length
                        : 0;

                    if (Directory.Exists(working)) Directory.Delete(working, true);

                    artifacts.Out("  work       " + files +
                                  " file(s) removed, so no sink was still holding one");
                }
                catch (Exception ex)
                {
                    // The interesting failure. An IOException here means a file
                    // handle outlived its sink.
                    _cleanupProblems.Add(
                        "the working directory could not be removed, so a file handle survived its sink: " +
                        ex.GetType().Name + ": " + ex.Message);
                }
            }

            if (context != null)
            {
                try { context.ExclusiveAccess.Dispose(); }
                catch (Exception ex)
                {
                    _cleanupProblems.Add("disposing the exclusivity gate threw " + ex.GetType().Name);
                }
            }

            artifacts.Out("  counters   operations=" + counters.Operations +
                          " successes=" + counters.Successes +
                          " expectedFailures=" + counters.ExpectedFailures +
                          " unexpectedFailures=" + counters.UnexpectedFailures +
                          " cancellations=" + counters.Cancellations);

            return PhaseContext.CompletedTask;
        }

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
                    json.Prop("installedMemoryBytes", (long)RuntimeFacts.InstalledMemoryBytes);
                    json.Prop("installedMemory", RuntimeFacts.DescribeBytes(RuntimeFacts.InstalledMemoryBytes));
                });

                json.Object("process", () =>
                {
                    json.Prop("targetFramework", RuntimeFacts.TargetFrameworkMoniker);
                    json.Prop("runtime", RuntimeFacts.RuntimeDescription);
                    json.Prop("scenarioVersion", ScenarioFacts.ScenarioVersion);
                });

                json.Object("modulesUnderTest", () =>
                {
                    json.Prop("logging", ScenarioFacts.LoggingVersion);
                    json.Prop("telemetry", ScenarioFacts.TelemetryVersion);
                    json.Prop("inspection", ScenarioFacts.InspectionVersion);
                    json.Prop("core", ScenarioFacts.CoreVersion);
                });

                json.Object("configuration", () =>
                {
                    json.Prop("logRecentCapacity", ObservabilityWorkspace.LogRecentCapacity);
                    json.Prop("telemetryCapacity", ObservabilityWorkspace.TelemetryCapacity);
                    json.Prop("inspectionCapacity", ObservabilityWorkspace.InspectionCapacity);
                    json.Prop("logRate", _options.LogRate);
                    json.Prop("globalInspectionChecks", !_options.NoGlobalInspection);
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
        /// What a passing run does and does not establish. These are written
        /// into every result because the easiest mistake this scenario could
        /// cause is someone reading it as proof that the modules are
        /// instrumented.
        /// </summary>
        private static readonly string[] ClaimBoundaries =
        {
            "This scenario composes public Logging, Telemetry and Inspection APIs as an application would. It is " +
            "not evidence that any module emits: only Navigation has Inspection hooks, and Data, Pipes, Watchdog, " +
            "Devices and Diagnostics have none.",

            "No action is registered and none is invoked. The Inspection action channel and the module " +
            "instrumentation rollout are frozen and explicitly outside this scenario.",

            "Every injected failure comes from a sink, a state provider or a file this scenario owns. No " +
            "fault-injection or test-control API was added to any product module.",

            "Rotation and retention are proved at a deliberately small maximum file size. That establishes the " +
            "mechanics and says nothing about retention capacity at a production file size.",

            "Logging is synchronous and ordered by design, so the ordering claims here are about delivery order " +
            "under one dispatch lock, not about timestamp ordering.",

            "Telemetry v1 keeps completed operations in bounded memory and does not persist them. Nothing here is " +
            "evidence about durability across a process restart."
        };

        /// <summary>
        /// Whether this run is shorter than the window the suite specifies for
        /// its mode, judged by what actually elapsed rather than by what was
        /// requested.
        /// <para/>
        /// The distinction is not academic. A rehearsal asks for 60 minutes and
        /// finishes in about 53: the schedule reserves a quiet window at each
        /// end, so the last fault lands well before the nominal deadline and the
        /// cool-down completes shortly after it. Comparing the request would
        /// have called that run compliant when its elapsed time was below the
        /// suite's lower bound. To land inside 60-90 minutes, ask for about 70.
        /// </summary>
        private bool BelowSpecifiedWindow(TimeSpan elapsed)
        {
            switch (_options.Mode)
            {
                case ScenarioMode.Smoke:
                    return elapsed < ScenarioOptions.MinimumSpecifiedSmoke;

                case ScenarioMode.RecoveryRehearsal:
                    return elapsed < ScenarioOptions.MinimumSpecifiedRehearsal;

                default:
                    return false;
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
                new ObservabilitySummary(ClaimBoundaries));
        }
    }
}
