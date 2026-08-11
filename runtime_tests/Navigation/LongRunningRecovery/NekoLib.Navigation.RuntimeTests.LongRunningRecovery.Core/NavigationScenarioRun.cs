#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NekoLib.Core.Inspection;
using NekoLib.Inspection;
using NekoLib.Navigation;
using NekoLib.Navigation.Runtime.Core;
using NekoLib.RuntimeTests.Harness;
using NekoLib.RuntimeTests.Harness.Faults;
using NekoLib.RuntimeTests.Harness.Reporting;

namespace NekoLib.Navigation.RuntimeTests.LongRunningRecovery
{
    public sealed class NavigationScenarioRun
    {
        private readonly ScenarioOptions _options;
        private readonly IScenarioPlatform _platform;
        private readonly CancellationToken _ct;
        private readonly List<string> _setupGaps = new List<string>();
        private readonly List<string> _cleanupProblems = new List<string>();

        private RunArtifacts? _artifacts;
        private CheckRunner? _runner;
        private DateTime _startedUtc = DateTime.UtcNow;
        private long _startedTimestamp;
        private bool _interrupted;

        public NavigationScenarioRun(
            ScenarioOptions options,
            IScenarioPlatform platform,
            CancellationToken cancellationToken)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _platform = platform ?? throw new ArgumentNullException(nameof(platform));
            _ct = cancellationToken;
        }

        public static int PrintSchedule(ScenarioOptions options)
        {
            FaultSchedule preview = ScenarioPlan.Build(options);
            Console.WriteLine(preview.ToJson());
            Console.WriteLine();
            Console.WriteLine("normalized-hash " + preview.Hash);
            return ExitCodes.Success;
        }

        public async Task<int> ExecuteAsync()
        {
            using (RunArtifacts artifacts = RunArtifacts.Create(
                _options.ArtifactsRoot,
                _options.CampaignId,
                _options.ScenarioId,
                NavigationSamples.ColumnNamesForHeader,
                _options.WorkerId))
            {
                _artifacts = artifacts;
                CheckRetention retention = CheckRetention.ForMode(_options.Mode);
                _runner = new CheckRunner(artifacts.Out, _ct, retention, artifacts.AppendCheck);

                artifacts.Out("E3-NAV  " + _options.CampaignId);
                artifacts.Out("host     " + _platform.DisplayName);
                artifacts.Out("target   " + RuntimeFacts.TargetFrameworkMoniker +
                              "  " + RuntimeFacts.RuntimeDescription);
                artifacts.Out("run      " + artifacts.CampaignDirectory);
                artifacts.Out(string.Empty);

                return await ExecuteWithArtifactsAsync(artifacts);
            }
        }

        private async Task<int> ExecuteWithArtifactsAsync(RunArtifacts artifacts)
        {
            FaultSchedule schedule = ScenarioPlan.Build(_options);

            // The immutable plan is the first scenario document. No Navigation
            // context, page, timer or surface exists before this write succeeds.
            ScenarioPlan.Persist(artifacts.SchedulePath, schedule);
            artifacts.Out("schedule  " + schedule.Events.Count + " planned fault(s), " + schedule.Hash);
            artifacts.Out(string.Empty);

            WriteEnvironment(artifacts);

            ScenarioState state = new ScenarioState();
            WorkloadCounters counters = new WorkloadCounters();
            InspectionRuntime inspection = new InspectionRuntime(
                new NekoLib.Inspection.InspectionOptions { Capacity = 32768 });
            NavigationSamples samples = new NavigationSamples(state, _platform, inspection);
            ResourceSampler sampler = new ResourceSampler(artifacts, counters, samples);
            NavigationRunContext context = new NavigationRunContext(
                _options,
                _platform,
                state,
                inspection,
                artifacts,
                _runner!,
                counters,
                sampler,
                _ct);

            int exitCode = ExitCodes.Success;
            try
            {
                sampler.Take("preflight", "baseline");
                await context.StartAsync();
                await context.NavigateSuccessAsync(_platform.Pages.Idle);
                sampler.Take("preflight", "post-warm-up");

                _startedUtc = DateTime.UtcNow;
                _startedTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();
                await RunModeAsync(context, schedule);
                sampler.Take(_options.Mode.ToString().ToLowerInvariant(), "final");
                await NavigationWorkload.RunResourceAssertionsAsync(context);
            }
            catch (OperationCanceledException)
            {
                _interrupted = true;
                artifacts.Error("interrupted before the selected mode finished");
            }
            catch (Exception ex)
            {
                artifacts.Error("E3-NAV failed outside any check: " + ex);
                counters.UnexpectedFailure();
                exitCode = ExitCodes.Unexpected;
            }
            finally
            {
                await CleanupAsync(context);
                try { sampler.Take("cleanup", "post-cleanup"); }
                catch (Exception ex) { _cleanupProblems.Add("post-cleanup sampling failed: " + ex.Message); }
                _platform.ReleaseScenarioGlobals();
                inspection.Dispose();
                ScenarioStateSlot.Clear();
            }

            return Finish(artifacts, schedule, counters, exitCode);
        }

        private async Task RunModeAsync(NavigationRunContext context, FaultSchedule schedule)
        {
            switch (_options.Mode)
            {
                case ScenarioMode.Smoke:
                    await NavigationWorkload.RunAllAsync(context);
                    await SustainAsync(context, _startedUtc + _options.SmokeDuration, "smoke");
                    break;

                case ScenarioMode.RecoveryRehearsal:
                    await NavigationWorkload.RunAllAsync(context);
                    context.Sampler.Take("recovery", "post-warm-up");
                    await NavigationRecovery.RunAsync(context, schedule, _startedTimestamp);
                    context.Sampler.Take("recovery", "cool-down");
                    await NavigationWorkload.RunRecoveryProbeAsync(context);
                    break;

                case ScenarioMode.Soak:
                    Task faults = NavigationRecovery.RunAsync(context, schedule, _startedTimestamp);
                    await SustainAsync(context, _startedUtc + _options.SoakDuration, "soak");
                    await faults;
                    break;
            }
        }

        private async Task SustainAsync(
            NavigationRunContext context,
            DateTime deadline,
            string phase)
        {
            int cycles = 0;
            while (DateTime.UtcNow < deadline && !_ct.IsCancellationRequested)
            {
                cycles++;
                artifactsLine(phase + " cycle " + cycles.ToString(CultureInfo.InvariantCulture));
                await context.ExclusiveAsync(() => NavigationWorkload.RunSustainedCycleAsync(context));
                context.Sampler.Take(phase, "periodic");
            }

            artifactsLine(phase + " completed " + cycles.ToString(CultureInfo.InvariantCulture) + " cycle(s)");

            void artifactsLine(string line) => context.Artifacts.Out(line);
        }

        private async Task CleanupAsync(NavigationRunContext context)
        {
            context.Artifacts.Out(string.Empty);
            context.Artifacts.Out("cleanup");
            context.State.ClearFaultsAndReleaseLoads();
            context.Platform.Controls.RejectDispatch = false;

            try
            {
                await context.ShutdownAsync();
                context.Artifacts.Out("  navigation  awaited Shutdown completed");
            }
            catch (Exception ex)
            {
                _cleanupProblems.Add("awaited NavigationService.Shutdown failed: " +
                    ex.GetType().Name + ": " + ex.Message);
            }

            context.State.ForceCollection();

            if (context.State.AttachedPageCount != 0)
                _cleanupProblems.Add(context.State.AttachedPageCount + " scenario page(s) remained attached");
            if (context.State.VisiblePageCount != 0)
                _cleanupProblems.Add(context.State.VisiblePageCount + " scenario page(s) remained visible");
            if (context.State.ActiveBackground != 0)
                _cleanupProblems.Add(context.State.ActiveBackground + " scenario background load(s) remained active");
            if (context.Platform.Controls.Metrics.ViewsLive != 0)
                _cleanupProblems.Add(context.Platform.Controls.Metrics.ViewsLive + " scenario surface(s) remained live");
            if (context.Platform.NativeChildCount != 0)
                _cleanupProblems.Add(context.Platform.NativeChildCount + " native host child(ren) remained after shutdown");

            InspectionRuntimeDiagnostics diagnostics = context.Inspection.GetDiagnostics();
            if (diagnostics.ProviderCount != 0)
                _cleanupProblems.Add(diagnostics.ProviderCount + " Navigation Inspection provider(s) remained registered");
            if (diagnostics.ActionCount != 0)
                _cleanupProblems.Add(diagnostics.ActionCount + " Inspection action(s) were registered");

            context.Artifacts.Out("  pages       attached=" + context.State.AttachedPageCount +
                                  " visible=" + context.State.VisiblePageCount +
                                  " alive=" + context.State.AlivePageCount);
            context.Artifacts.Out("  surfaces    live=" + context.Platform.Controls.Metrics.ViewsLive +
                                  " modal=" + context.Platform.Controls.Metrics.ModalViewsLive);
            context.Artifacts.Out("  inspection  providers=" + diagnostics.ProviderCount +
                                  " actions=" + diagnostics.ActionCount);
            context.Artifacts.Out("  native       children=" + context.Platform.NativeChildCount);
        }

        private void WriteEnvironment(RunArtifacts artifacts)
        {
            RuntimeFacts.ReadRepository(out string commit, out bool dirty, out string diagnostic);
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
                    if (diagnostic.Length > 0) json.Prop("diagnostic", diagnostic);
                });

                json.Object("host", () =>
                {
                    json.Prop("windowsVersion", RuntimeFacts.WindowsVersion);
                    json.Prop("machineArchitecture", RuntimeFacts.MachineArchitecture);
                    json.Prop("processArchitecture", RuntimeFacts.ProcessArchitecture);
                    json.Prop("logicalProcessors", RuntimeFacts.LogicalProcessorCount);
                    json.Prop("installedMemoryBytes", (long)RuntimeFacts.InstalledMemoryBytes);
                });

                json.Object("process", () =>
                {
                    json.Prop("targetFramework", RuntimeFacts.TargetFrameworkMoniker);
                    json.Prop("runtime", RuntimeFacts.RuntimeDescription);
                    json.Prop("scenarioVersion", ScenarioFacts.ScenarioVersion);
                    json.Prop("platform", _platform.PlatformId);
                    json.Prop("adapter", RuntimeFacts.DescribeAssembly("adapter", _platform.AdapterMarkerType));
                });

                json.Object("modulesUnderTest", () =>
                {
                    json.Prop("navigation", ScenarioFacts.NavigationVersion);
                    json.Prop("inspection", ScenarioFacts.InspectionVersion);
                });

                json.Object("configuration", () =>
                {
                    json.Prop("idleTimeoutMilliseconds", _options.IdleTimeoutMilliseconds);
                    json.Prop("switchesPerCycle", _options.SwitchesPerCycle);
                    json.Prop("smokeDurationSeconds", _options.SmokeDuration.TotalSeconds);
                });

                json.Array("setupGaps", () =>
                {
                    foreach (string gap in _setupGaps) json.Item(gap);
                });
            });
            artifacts.WriteText(artifacts.EnvironmentPath, json.ToString());
        }

        private bool BelowSpecifiedWindow(TimeSpan elapsed)
        {
            if (_options.Mode == ScenarioMode.Smoke)
                return elapsed < ScenarioOptions.MinimumSpecifiedSmoke;
            if (_options.Mode == ScenarioMode.RecoveryRehearsal)
                return elapsed < ScenarioOptionsBase.MinimumSpecifiedRehearsal;
            return false;
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
            return summary.Write(artifacts, _runner!, counters, new NavigationSummary(_platform));
        }
    }

    internal sealed class NavigationRunContext
    {
        private NavigationContext? _navigationContext;
        private WeakReference? _lastHandlerOwner;

        public NavigationRunContext(
            ScenarioOptions options,
            IScenarioPlatform platform,
            ScenarioState state,
            InspectionRuntime inspection,
            RunArtifacts artifacts,
            CheckRunner runner,
            WorkloadCounters counters,
            ResourceSampler sampler,
            CancellationToken cancellationToken)
        {
            Options = options;
            Platform = platform;
            State = state;
            Inspection = inspection;
            Artifacts = artifacts;
            Runner = runner;
            Counters = counters;
            Sampler = sampler;
            Ct = cancellationToken;
        }

        public ScenarioOptions Options { get; }
        public IScenarioPlatform Platform { get; }
        public ScenarioState State { get; }
        public InspectionRuntime Inspection { get; }
        public RunArtifacts Artifacts { get; }
        public CheckRunner Runner { get; }
        public WorkloadCounters Counters { get; }
        public ResourceSampler Sampler { get; }
        public CancellationToken Ct { get; }
        public readonly SemaphoreSlim Exclusive = new SemaphoreSlim(1, 1);
        public bool Mounted => _navigationContext != null;

        public async Task ExclusiveAsync(Func<Task> action)
        {
            await Exclusive.WaitAsync(Ct);
            try { await action(); }
            finally { Exclusive.Release(); }
        }

        public Task StartAsync()
        {
            if (Mounted) throw new InvalidOperationException("Navigation is already mounted.");
            ScenarioStateSlot.Install(State);
            _navigationContext = Platform.Start(State, Inspection, Options);
            AttachObservers();
            _lastHandlerOwner = AttachEphemeralStaticHandler();
            return CompletedTask;
        }

        public async Task ShutdownAsync()
        {
            if (!Mounted) return;
            try { await NavigationService.Shutdown(); }
            finally { _navigationContext = null; }
        }

        public async Task RestartAsync()
        {
            await ShutdownAsync();
            await StartAsync();
        }

        private void AttachObservers()
        {
            NavigationService.NavigationFailed += (_, __, ___) => State.NavigationFailureObserved();
            NavigationService.Events.GuardDenied += _ => State.GuardDeniedObserved();
        }

        private static WeakReference AttachEphemeralStaticHandler()
        {
            HandlerOwner owner = new HandlerOwner();
            NavigationService.CurrentChanged += owner.OnCurrentChanged;
            return new WeakReference(owner);
        }

        public bool LastHandlerOwnerCollected()
        {
            if (_lastHandlerOwner == null) return true;
            for (int i = 0; i < 3 && _lastHandlerOwner.IsAlive; i++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            }
            return !_lastHandlerOwner.IsAlive;
        }

        public async Task<Exception?> NavigateSuccessAsync(Type target)
        {
            Exception? error = await ExecuteRequestAsync(() => NavigationService.SwitchPage(target), false);
            if (error != null) throw new CheckFailure("navigation to " + target.Name + " failed: " + error.Message);
            return null;
        }

        public Task<Exception?> NavigateExpectedFailureAsync(Type target) =>
            ExecuteRequestAsync(() => NavigationService.SwitchPage(target), true);

        public async Task<Exception?> ExecuteRequestAsync(Func<Task> request, bool expectedFailure)
        {
            Ct.ThrowIfCancellationRequested();
            Inspection.ClearOperations();
            State.RequestStarted();
            Exception? error = null;
            try
            {
                await request();
            }
            catch (Exception ex)
            {
                error = ex;
            }
            finally
            {
                State.RequestCompleted();
            }

            IReadOnlyList<InspectionOperation> operations = Inspection.GetOperations();
            int started = operations.Count(item =>
                item.Module == "Navigation" && item.Operation == "NavigationStarted");
            int terminals = operations.Count(item =>
                item.Module == "Navigation" && IsRequestTerminal(item.Operation));

            if (started != 1 || terminals != 1)
            {
                Counters.UnexpectedFailure();
                throw new CheckFailure(
                    "one API request must produce one start and one terminal; observed " +
                    started + " start(s) and " + terminals + " terminal(s)");
            }

            if (expectedFailure) Counters.ExpectedFailure();
            else if (error == null) Counters.Success();
            else Counters.UnexpectedFailure();

            return error;
        }

        private static bool IsRequestTerminal(string operation) =>
            operation == "Navigated" ||
            operation == "GuardDenied" ||
            operation == "NavigationFailed" ||
            operation == "NavigationNoHistory" ||
            operation == "NavigationDiscarded";

        public static Task CompletedTask
        {
            get
            {
#if NET481
                return Task.FromResult(0);
#else
                return Task.CompletedTask;
#endif
            }
        }

        private sealed class HandlerOwner
        {
            public void OnCurrentChanged(NekoLib.Navigation.Contracts.Pages.IPageView page) { }
        }
    }
}
