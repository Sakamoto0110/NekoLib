using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NekoLib.Core.Observability;
using NekoLib.DebugUtils;
using NekoLib.Navigation.Diagnostics;
using NekoLib.Navigation.Metadata;
using NekoLib.Navigation.Runtime;
using Xunit;

namespace NekoLib.Navigation.Tests.Unit
{
    /// <summary>
    /// Verifies the opt-in observability bridge: navigation hub events are forwarded
    /// to an <see cref="IDebugUtils"/> sink as recorded operations + a pull-based
    /// "current" state, and that disable / dispose are honoured. Uses the real
    /// <see cref="DebugUtilsRuntime"/> so the wiring is exercised end-to-end.
    /// </summary>
    public sealed class DebugUtilsNavigationObserverTests
    {
        [Fact]
        public void SuccessfulNavigation_RecordsNavigatedOperation()
        {
            var hub = new NavigationEventHub();
            var debug = new DebugUtilsRuntime();

            using (DebugUtilsNavigationObserver.Attach(hub, debug))
            {
                hub.Publish(SuccessEntry());

                var op = Assert.Single(debug.GetOperations());
                Assert.Equal(DebugUtilsNavigationObserver.Module, op.Module);
                Assert.Equal("Navigated", op.Operation);
            }
        }

        [Fact]
        public void FailedNavigation_RecordsNavigationFailedOperation()
        {
            var hub = new NavigationEventHub();
            var debug = new DebugUtilsRuntime();

            using (DebugUtilsNavigationObserver.Attach(hub, debug))
            {
                hub.Publish(FailureEntry());

                Assert.Equal("NavigationFailed", Assert.Single(debug.GetOperations()).Operation);
            }
        }

        [Fact]
        public void GuardDenied_RecordsGuardDeniedOperation()
        {
            var hub = new NavigationEventHub();
            var debug = new DebugUtilsRuntime();

            using (DebugUtilsNavigationObserver.Attach(hub, debug))
            {
                hub.Publish(new GuardDeniedEvent(null, typeof(int), typeof(string), "denied"));

                Assert.Equal("GuardDenied", Assert.Single(debug.GetOperations()).Operation);
            }
        }

        [Fact]
        public void LastNavigation_IsExposedSeparatelyFromCurrentPage()
        {
            var hub = new NavigationEventHub();
            var debug = new DebugUtilsRuntime();

            using (DebugUtilsNavigationObserver.Attach(hub, debug))
            {
                hub.Publish(SuccessEntry());

                var state = debug.CaptureState();
                var current = state["Navigation::current"];
                var last = state["Navigation::lastNavigation"];

                Assert.Equal("To", Prop(current, "name"));
                Assert.Equal("To", Prop(last, "to"));
                Assert.True((bool)Prop(last, "success"));
            }
        }

        [Fact]
        public void Dispose_DetachesFromHub()
        {
            var hub = new NavigationEventHub();
            var debug = new DebugUtilsRuntime();

            var handle = DebugUtilsNavigationObserver.Attach(hub, debug);
            handle.Dispose();

            hub.Publish(SuccessEntry());

            Assert.Empty(debug.GetOperations());
        }

        [Fact]
        public void DisabledDebugUtils_AttachIsNoOpAndDoesNotThrow()
        {
            var hub = new NavigationEventHub();

            using (DebugUtilsNavigationObserver.Attach(hub, NullDebugUtils.Instance))
            {
                // No subscriber is wired for a disabled sink, so this is harmless.
                hub.Publish(SuccessEntry());
            }
        }

        [Fact]
        public void Stats_CountEveryOutcomeKind()
        {
            var hub = new NavigationEventHub();
            var debug = new DebugUtilsRuntime();

            using (DebugUtilsNavigationObserver.Attach(hub, debug))
            {
                hub.Publish(SuccessEntry());
                hub.Publish(SuccessEntry(isBackNavigation: true));
                hub.Publish(FailureEntry());
                hub.Publish(FailureEntry(isTimeout: true));
                hub.Publish(new GuardDeniedEvent(null, typeof(int), typeof(string), "denied"));

                var stats = Stats(debug);
                Assert.Equal(2, Prop(stats, "navigated"));
                Assert.Equal(2, Prop(stats, "failed"));
                Assert.Equal(1, Prop(stats, "backNavigations"));
                Assert.Equal(1, Prop(stats, "timeouts"));
                Assert.Equal(1, Prop(stats, "guardDenied"));
            }
        }

        /// <summary>
        /// The ring buffer is bounded; the counters are not. Once the buffer wraps,
        /// the totals are the only surviving evidence of what happened.
        /// </summary>
        [Fact]
        public void Stats_SurviveRingBufferEviction()
        {
            var hub = new NavigationEventHub();
            var debug = new DebugUtilsRuntime(new DebugUtilsOptions { Capacity = 1 });

            using (DebugUtilsNavigationObserver.Attach(hub, debug))
            {
                hub.Publish(SuccessEntry());
                hub.Publish(SuccessEntry());
                hub.Publish(SuccessEntry());

                Assert.Single(debug.GetOperations());
                Assert.Equal(3, Prop(Stats(debug), "navigated"));
            }
        }

        /// <summary>
        /// The hub-only overload deliberately exposes no history/session state: it has
        /// no context to read them from, and it stays out of the static facade so a
        /// test using it introduces no global state.
        /// </summary>
        [Fact]
        public void HubOnlyAttach_ExposesOnlyTraceBackedState()
        {
            var hub = new NavigationEventHub();
            var debug = new DebugUtilsRuntime();

            using (DebugUtilsNavigationObserver.Attach(hub, debug))
            {
                Assert.Equal(
                    new[]
                    {
                        "Navigation::activeAttempts",
                        "Navigation::backgroundLoads",
                        "Navigation::cache",
                        "Navigation::current",
                        "Navigation::currentPage",
                        "Navigation::idle",
                        "Navigation::inFlight",
                        "Navigation::lastNavigation",
                        "Navigation::overlays",
                        "Navigation::pages",
                        "Navigation::queue",
                        "Navigation::runtime",
                        "Navigation::stats"
                    },
                    Assert.IsAssignableFrom<IEnumerable<string>>(debug.StateKeys()).OrderBy(k => k).ToArray());
            }
        }

        [Fact]
        public void TraceTerminal_KeepsActualCurrentSeparateAndCarriesCorrelation()
        {
            var hub = new NavigationEventHub();
            var debug = new DebugUtilsRuntime();
            var timestamp = new DateTime(2026, 7, 27, 12, 0, 0, DateTimeKind.Utc);

            using (DebugUtilsNavigationObserver.Attach(hub, debug))
            {
                hub.PublishTrace(new NavigationTraceEvent(
                    NavigationTraceKind.Page,
                    "runtime-1",
                    targetPage: typeof(string).FullName,
                    decision: "CurrentChanged",
                    attachedCount: 1,
                    visibleCount: 1,
                    timestampUtc: timestamp));

                hub.PublishTrace(new NavigationTraceEvent(
                    NavigationTraceKind.RequestCompleted,
                    "runtime-1",
                    stage: NavigationTraceStage.Completed,
                    previousStage: NavigationTraceStage.GuardEvaluation,
                    outcome: NavigationTraceOutcome.Failed,
                    trigger: NavigationTraceTrigger.Navigate,
                    requestId: "request-42",
                    fromPage: typeof(string).FullName,
                    targetPage: typeof(int).FullName,
                    failureKind: NavigationFailureKind.LifecycleFailed.ToString(),
                    errorType: typeof(InvalidOperationException).FullName,
                    success: false,
                    timestampUtc: timestamp,
                    elapsedMilliseconds: 73));

                var state = debug.CaptureState();
                var current = state["Navigation::current"];
                var alias = state["Navigation::currentPage"];
                var last = state["Navigation::lastNavigation"];

                Assert.Equal(typeof(string).FullName, Prop(current, "type"));
                Assert.Equal(typeof(string).FullName, Prop(alias, "type"));
                Assert.Equal(typeof(int).FullName, Prop(last, "to"));
                Assert.Equal("request-42", Prop(last, "requestId"));
                Assert.Equal(73L, Prop(last, "durationMs"));
                Assert.False((bool)Prop(last, "success"));
            }
        }

        [Fact]
        public void RequestStage_ExposesQueuedAndInFlightStateUntilTerminal()
        {
            var hub = new NavigationEventHub();
            var debug = new DebugUtilsRuntime();

            using (DebugUtilsNavigationObserver.Attach(hub, debug))
            {
                hub.PublishTrace(new NavigationTraceEvent(
                    NavigationTraceKind.RequestStarted,
                    "runtime-1",
                    stage: NavigationTraceStage.Requested,
                    trigger: NavigationTraceTrigger.Navigate,
                    requestId: "request-1",
                    targetPage: "Target"));
                hub.PublishTrace(new NavigationTraceEvent(
                    NavigationTraceKind.RequestStage,
                    "runtime-1",
                    stage: NavigationTraceStage.GateWait,
                    previousStage: NavigationTraceStage.Dispatch,
                    trigger: NavigationTraceTrigger.Navigate,
                    requestId: "request-1",
                    targetPage: "Target",
                    queueDepth: 2,
                    elapsedMilliseconds: 5));

                var active = debug.CaptureState();
                Assert.Equal(1, Prop(active["Navigation::inFlight"], "count"));
                Assert.Equal(2, Prop(active["Navigation::queue"], "depth"));

                hub.PublishTrace(new NavigationTraceEvent(
                    NavigationTraceKind.RequestCompleted,
                    "runtime-1",
                    stage: NavigationTraceStage.Completed,
                    outcome: NavigationTraceOutcome.Succeeded,
                    trigger: NavigationTraceTrigger.Navigate,
                    requestId: "request-1",
                    targetPage: "Target",
                    success: true,
                    elapsedMilliseconds: 9));

                Assert.Equal(
                    0,
                    Prop(
                        debug.CaptureState()["Navigation::inFlight"],
                        "count"));
            }
        }

        [Fact]
        public async Task CaptureState_FromWorkerThread_ReadsOnlyProjection()
        {
            var hub = new NavigationEventHub();
            var debug = new DebugUtilsRuntime();

            using (DebugUtilsNavigationObserver.Attach(hub, debug))
            {
                hub.PublishTrace(new NavigationTraceEvent(
                    NavigationTraceKind.RequestStarted,
                    "runtime-1",
                    requestId: "request-1",
                    targetPage: "Target"));

                var snapshots = await Task.WhenAll(
                    Enumerable.Range(0, 16)
                        .Select(_ => Task.Run(() => debug.CaptureState())));

                Assert.All(
                    snapshots,
                    snapshot => Assert.Equal(
                        1,
                        Prop(snapshot["Navigation::inFlight"], "count")));
            }
        }

        [Fact]
        public void BackgroundFailure_IsNotReportedAsNavigationFailure()
        {
            var hub = new NavigationEventHub();
            var debug = new DebugUtilsRuntime();

            using (DebugUtilsNavigationObserver.Attach(hub, debug))
            {
                hub.PublishTrace(new NavigationTraceEvent(
                    NavigationTraceKind.BackgroundLoadStarted,
                    "runtime-1",
                    requestId: "request-1",
                    attemptId: "attempt-1",
                    backgroundOperationId: "background-1",
                    targetPage: "Target",
                    backgroundLoadCount: 1));
                hub.PublishTrace(new NavigationTraceEvent(
                    NavigationTraceKind.BackgroundLoadFailed,
                    "runtime-1",
                    outcome: NavigationTraceOutcome.Failed,
                    requestId: "request-1",
                    attemptId: "attempt-1",
                    backgroundOperationId: "background-1",
                    targetPage: "Target",
                    errorType: typeof(InvalidOperationException).FullName,
                    success: false,
                    backgroundLoadCount: 0,
                    elapsedMilliseconds: 15));

                Assert.Equal(
                    new[] { "BackgroundLoadFailed" },
                    debug.GetOperations()
                        .Select(operation => operation.Operation)
                        .ToArray());
                Assert.Equal(
                    1,
                    Prop(
                        debug.CaptureState()["Navigation::backgroundLoads"],
                        "failed"));
            }
        }

        [Fact]
        public void SurfaceAndIdleTrace_ExposeStateWithoutRecordingInteractions()
        {
            var hub = new NavigationEventHub();
            var debug = new DebugUtilsRuntime();
            var configured = new DateTime(
                2026,
                7,
                27,
                12,
                0,
                0,
                DateTimeKind.Utc);

            using (DebugUtilsNavigationObserver.Attach(hub, debug))
            {
                hub.PublishTrace(new NavigationTraceEvent(
                    NavigationTraceKind.IdleConfigured,
                    "runtime-1",
                    decision: "Configured",
                    idleIntervalMilliseconds: 30000,
                    timestampUtc: configured));
                hub.PublishTrace(new NavigationTraceEvent(
                    NavigationTraceKind.IdleInteraction,
                    "runtime-1",
                    decision: "TimerReset",
                    idleIntervalMilliseconds: 30000,
                    timestampUtc: configured.AddSeconds(1)));
                hub.PublishTrace(new NavigationTraceEvent(
                    NavigationTraceKind.SurfaceOpening,
                    "runtime-1",
                    targetPage: "ConfirmDialog",
                    surfaceId: "surface-1",
                    surfaceKind: "Dialog",
                    surfaceDepth: 1,
                    timestampUtc: configured.AddSeconds(2)));
                hub.PublishTrace(new NavigationTraceEvent(
                    NavigationTraceKind.SurfaceOpened,
                    "runtime-1",
                    outcome: NavigationTraceOutcome.Succeeded,
                    targetPage: "ConfirmDialog",
                    success: true,
                    surfaceId: "surface-1",
                    surfaceKind: "Dialog",
                    surfaceDepth: 1,
                    timestampUtc: configured.AddSeconds(3),
                    elapsedMilliseconds: 4));

                var openState = debug.CaptureState()["Navigation::overlays"];
                Assert.Equal(1, Prop(openState, "active"));

                hub.PublishTrace(new NavigationTraceEvent(
                    NavigationTraceKind.SurfaceClosed,
                    "runtime-1",
                    outcome: NavigationTraceOutcome.Succeeded,
                    targetPage: "ConfirmDialog",
                    success: true,
                    surfaceId: "surface-1",
                    surfaceKind: "Dialog",
                    surfaceDepth: 1,
                    closeReason: "CompletedByView",
                    timestampUtc: configured.AddSeconds(4),
                    elapsedMilliseconds: 10));

                Assert.Equal(
                    new[]
                    {
                        "IdleConfigured",
                        "SurfaceOpened",
                        "SurfaceClosed"
                    },
                    debug.GetOperations()
                        .Select(operation => operation.Operation)
                        .ToArray());

                var state = debug.CaptureState();
                var overlays = state["Navigation::overlays"];
                var terminal = Prop(overlays, "lastTerminal");
                Assert.Equal(0, Prop(overlays, "active"));
                Assert.Equal(
                    "CompletedByView",
                    Prop(terminal, "closeReason"));

                var idle = state["Navigation::idle"];
                Assert.Equal(30000, Prop(idle, "intervalMs"));
                Assert.Equal(
                    configured.AddSeconds(1),
                    Prop(idle, "lastInteractionUtc"));
            }
        }

        [Fact]
        public void RedirectAndNoHistory_HaveDistinctOperationsAndCounters()
        {
            var hub = new NavigationEventHub();
            var debug = new DebugUtilsRuntime();

            using (DebugUtilsNavigationObserver.Attach(hub, debug))
            {
                hub.PublishTrace(new NavigationTraceEvent(
                    NavigationTraceKind.AttemptStarted,
                    "runtime-1",
                    requestId: "request-1",
                    attemptId: "attempt-1",
                    targetPage: "Protected"));
                hub.PublishTrace(new NavigationTraceEvent(
                    NavigationTraceKind.AttemptCompleted,
                    "runtime-1",
                    outcome: NavigationTraceOutcome.Redirected,
                    requestId: "request-1",
                    attemptId: "attempt-1",
                    targetPage: "Protected",
                    decision: "GuardRedirect",
                    elapsedMilliseconds: 2));
                hub.PublishTrace(new NavigationTraceEvent(
                    NavigationTraceKind.RequestCompleted,
                    "runtime-1",
                    outcome: NavigationTraceOutcome.Succeeded,
                    requestId: "request-1",
                    targetPage: "Idle",
                    decision: "Redirected",
                    success: true,
                    attachedCount: 1,
                    visibleCount: 1,
                    elapsedMilliseconds: 3));
                hub.PublishTrace(new NavigationTraceEvent(
                    NavigationTraceKind.RequestCompleted,
                    "runtime-1",
                    outcome: NavigationTraceOutcome.NoHistory,
                    trigger: NavigationTraceTrigger.Back,
                    requestId: "request-2",
                    targetPage: "<history>",
                    decision: "NoHistory",
                    success: false,
                    attachedCount: 1,
                    visibleCount: 1));

                Assert.Equal(
                    new[]
                    {
                        "GuardRedirected",
                        "Navigated",
                        "NavigationNoHistory"
                    },
                    debug.GetOperations()
                        .Select(operation => operation.Operation)
                        .ToArray());

                var stats = debug.CaptureState()["Navigation::stats"];
                Assert.Equal(1, Prop(stats, "redirects"));
                Assert.Equal(1, Prop(stats, "noHistory"));
                Assert.Equal(0, Prop(stats, "failed"));
            }
        }

        [Fact]
        public void PageMirror_NewInstanceClearsDisposedStateForSameType()
        {
            var hub = new NavigationEventHub();
            var debug = new DebugUtilsRuntime();
            const string pageType = "Example.TransientPage";

            using (DebugUtilsNavigationObserver.Attach(hub, debug))
            {
                hub.PublishTrace(new NavigationTraceEvent(
                    NavigationTraceKind.Page,
                    "runtime-1",
                    targetPage: pageType,
                    decision: "NavigationCleanupDisposed",
                    isDisposed: true,
                    attachedCount: 0,
                    visibleCount: 0));
                hub.PublishTrace(new NavigationTraceEvent(
                    NavigationTraceKind.Page,
                    "runtime-1",
                    targetPage: pageType,
                    decision: "TransientCreated",
                    isDisposed: false,
                    attachedCount: 0,
                    visibleCount: 0));
                hub.PublishTrace(new NavigationTraceEvent(
                    NavigationTraceKind.Page,
                    "runtime-1",
                    targetPage: pageType,
                    decision: "Attached",
                    isDisposed: false,
                    attachedCount: 1,
                    visibleCount: 0));
                hub.PublishTrace(new NavigationTraceEvent(
                    NavigationTraceKind.Page,
                    "runtime-1",
                    targetPage: pageType,
                    decision: "Visible",
                    isDisposed: false,
                    attachedCount: 1,
                    visibleCount: 1));

                var pages = debug.CaptureState()["Navigation::pages"];
                var tracked = ((System.Collections.IEnumerable)
                    Prop(pages, "tracked")).Cast<object>();
                var page = Assert.Single(tracked);

                Assert.Equal(pageType, Prop(page, "page"));
                Assert.True((bool)Prop(page, "attached"));
                Assert.True((bool)Prop(page, "visible"));
                Assert.False((bool)Prop(page, "disposed"));
            }
        }

        [Fact]
        public void IdleConfigurationFailure_IsReportedAsUnavailable()
        {
            var hub = new NavigationEventHub();
            var debug = new DebugUtilsRuntime();

            using (DebugUtilsNavigationObserver.Attach(hub, debug))
            {
                hub.PublishTrace(new NavigationTraceEvent(
                    NavigationTraceKind.IdleConfigured,
                    "runtime-1",
                    decision: "TimerAdapterUnavailable",
                    errorType: typeof(NotSupportedException).FullName,
                    success: false));

                var idle = debug.CaptureState()["Navigation::idle"];
                Assert.Equal("Unavailable", Prop(idle, "status"));
                Assert.Equal(
                    "TimerAdapterUnavailable",
                    Prop(idle, "decision"));
                Assert.Null(Prop(idle, "configuredUtc"));
                Assert.Equal(
                    "IdleConfigurationFailed",
                    Assert.Single(debug.GetOperations()).Operation);
            }
        }

        [Fact]
        public void TerminalTrace_WithSlowFinalStage_RecordsSlowStage()
        {
            var hub = new NavigationEventHub();
            var debug = new DebugUtilsRuntime();

            using (DebugUtilsNavigationObserver.Attach(hub, debug))
            {
                hub.PublishTrace(new NavigationTraceEvent(
                    NavigationTraceKind.AttemptCompleted,
                    "runtime-1",
                    stage: NavigationTraceStage.Completed,
                    previousStage: NavigationTraceStage.EnterPage,
                    outcome: NavigationTraceOutcome.Succeeded,
                    requestId: "request-1",
                    attemptId: "attempt-1",
                    targetPage: "Target",
                    success: true,
                    stageElapsedMilliseconds: 1500));
                hub.PublishTrace(new NavigationTraceEvent(
                    NavigationTraceKind.RequestCompleted,
                    "runtime-1",
                    stage: NavigationTraceStage.Completed,
                    previousStage: NavigationTraceStage.Processing,
                    outcome: NavigationTraceOutcome.Succeeded,
                    requestId: "request-1",
                    targetPage: "Target",
                    success: true,
                    stageElapsedMilliseconds: 1600));

                Assert.Equal(
                    2,
                    debug.GetOperations().Count(
                        operation => operation.Operation == "SlowStage"));
            }
        }

        [Fact]
        public void ProviderRegistrationFailure_RollsBackPartialObserver()
        {
            var hub = new NavigationEventHub();
            var debug = new DebugUtilsRuntime();
            var existing = debug.RegisterStateProvider(
                DebugUtilsNavigationObserver.Module,
                "current",
                () => "existing");

            try
            {
                Assert.Throws<InvalidOperationException>(
                    () => DebugUtilsNavigationObserver.Attach(hub, debug));

                hub.Publish(SuccessEntry());

                Assert.Empty(debug.GetOperations());
                Assert.Equal(
                    new[] { "Navigation::current" },
                    debug.StateKeys());
            }
            finally
            {
                existing.Dispose();
            }

            Assert.Empty(debug.StateKeys());
        }

        [Fact]
        public void Dispose_UnregistersStateProviders()
        {
            var hub = new NavigationEventHub();
            var debug = new DebugUtilsRuntime();

            DebugUtilsNavigationObserver.Attach(hub, debug).Dispose();

            Assert.Empty(debug.StateKeys());
        }

        private static object Stats(DebugUtilsRuntime debug)
            => debug.CaptureState()[DebugUtilsNavigationObserver.Module + "::stats"];

        /// <summary>
        /// Snapshots are anonymous types by design (consumers read them via
        /// ToString()/reflection, never by name), so tests read them the same way.
        /// </summary>
        private static object Prop(object snapshot, string name)
            => snapshot.GetType().GetProperty(name).GetValue(snapshot);

        private static PageLogEntry SuccessEntry(bool isBackNavigation = false)
            => new PageLogEntry(
                typeof(string), "From", typeof(int), "To",
                NavigationArgs.Default(), true,
                default(PagePresentationMode), default(NavigationLoadMode), default(PageReusePolicy),
                isBackNavigation: isBackNavigation);

        private static PageLogEntry FailureEntry(bool isTimeout = false)
            => new PageLogEntry(
                typeof(string), "From", typeof(int), "To",
                NavigationArgs.Default(), false,
                default(PagePresentationMode), default(NavigationLoadMode), default(PageReusePolicy),
                failureKind: NavigationFailureKind.None, isTimeout: isTimeout, error: "boom");
    }
}
