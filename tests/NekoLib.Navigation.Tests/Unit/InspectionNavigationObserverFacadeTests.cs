using System.Linq;
using System.Threading.Tasks;
using NekoLib.Inspection;
using NekoLib.Navigation.Diagnostics;
using NekoLib.Navigation.Tests.Unit.Fakes;
using Xunit;

namespace NekoLib.Navigation.Tests.Unit
{
    /// <summary>
    /// Covers the full-fidelity observability bridge: the public processing and
    /// attach/visibility signals from the static
    /// <see cref="NavigationService"/> facade, the internal request/outcome
    /// signals, and the context-backed history/session snapshots.
    ///
    /// <para>
    /// These tests mount the static facade, which is process-wide state — hence the
    /// shared collection (xunit serializes tests within one collection) and the
    /// mandatory <c>Shutdown()</c> in a finally. Any future test that mounts
    /// <see cref="NavigationService"/> belongs in this collection too. Tests that
    /// only need hub events should use the hub-only overload instead and stay
    /// parallel — see <see cref="InspectionNavigationObserverTests"/>.
    /// </para>
    /// </summary>
    [Collection("NavigationServiceFacade")]
    public sealed class InspectionNavigationObserverFacadeTests
    {
        [Fact]
        public async Task NavigationIntent_IsRecordedBeforeTheOutcome()
        {
            var fixture = RuntimeTestFixture.Build<StubIdle>(typeof(StubA));
            var debug = new InspectionRuntime();

            NavigationService.UseContext(fixture.Context);
            try
            {
                using (InspectionNavigationObserver.Attach(fixture.Context, debug))
                {
                    await NavigationService.SwitchPage<StubA>();

                    var ops = debug.GetOperations().Select(o => o.Operation).ToList();

                    Assert.Contains("NavigationStarted", ops);
                    Assert.Contains("Navigating", ops);
                    Assert.Contains("Navigated", ops);
                    Assert.True(
                        ops.IndexOf("NavigationStarted") <
                        ops.IndexOf("Navigating") &&
                        ops.IndexOf("Navigating") <
                        ops.IndexOf("Navigated"),
                        "request, processing and outcome must be recorded in order");
                }
            }
            finally
            {
                await NavigationService.Shutdown();
            }
        }

        [Fact]
        public async Task GuardDenied_RecordsStartedAndNavigatingBeforeGuardOutcome()
        {
            var fixture = RuntimeTestFixture.Build<StubIdle>(typeof(StubAuthenticated));
            var debug = new InspectionRuntime();
            var publicNavigatingCalls = 0;

            NavigationService.UseContext(fixture.Context);
            try
            {
                using (InspectionNavigationObserver.Attach(fixture.Context, debug))
                {
                    NavigationService.Navigating += (_, _, _) => publicNavigatingCalls++;

                    await NavigationService.SwitchPage<StubAuthenticated>();

                    Assert.Equal(
                        new[]
                        {
                            "NavigationStarted",
                            "Navigating",
                            "GuardDenied",
                            "ShellBlankDetected"
                        },
                        debug.GetOperations().Select(o => o.Operation).ToArray());
                    Assert.Equal(1, publicNavigatingCalls);
                    Assert.Null(NavigationService.Current);

                    var stats = debug.CaptureState()["Navigation::stats"];
                    Assert.Equal(1, Prop(stats, "started"));
                    Assert.Equal(1, Prop(stats, "guardDenied"));
                    Assert.Equal(0, Prop(stats, "navigated"));
                    Assert.Equal(0, Prop(stats, "failed"));
                }
            }
            finally
            {
                await NavigationService.Shutdown();
            }
        }

        [Fact]
        public async Task NavigatingSubscriberThrows_NavigationStillCompletes()
        {
            var fixture = RuntimeTestFixture.Build<StubIdle>(typeof(StubA));
            var debug = new InspectionRuntime();

            NavigationService.UseContext(fixture.Context);
            try
            {
                using (InspectionNavigationObserver.Attach(fixture.Context, debug))
                {
                    NavigationService.Navigating += (_, _, _) =>
                        throw new System.InvalidOperationException("observer failure");

                    await NavigationService.SwitchPage<StubA>();

                    Assert.IsType<StubA>(NavigationService.Current);
                    var operations = debug.GetOperations()
                        .Select(o => o.Operation)
                        .ToArray();
                    Assert.Contains("NavigationStarted", operations);
                    Assert.Contains("Navigating", operations);
                    Assert.Contains("Navigated", operations);
                    Assert.DoesNotContain("NavigationFailed", operations);
                }
            }
            finally
            {
                await NavigationService.Shutdown();
            }
        }

        [Fact]
        public async Task UnregisteredTarget_RecordsStartedThenNavigationFailed()
        {
            var fixture = RuntimeTestFixture.Build<StubIdle>();
            var debug = new InspectionRuntime();

            NavigationService.UseContext(fixture.Context);
            try
            {
                using (InspectionNavigationObserver.Attach(fixture.Context, debug))
                {
                    await Assert.ThrowsAsync<System.InvalidOperationException>(
                        () => NavigationService.SwitchPage(typeof(string)));

                    Assert.Equal(
                        new[]
                        {
                            "NavigationStarted",
                            "StageFailed",
                            "NavigationFailed",
                            "ShellBlankDetected"
                        },
                        debug.GetOperations().Select(o => o.Operation).ToArray());
                    Assert.Null(NavigationService.Current);

                    var stats = debug.CaptureState()["Navigation::stats"];
                    Assert.Equal(1, Prop(stats, "started"));
                    Assert.Equal(1, Prop(stats, "failed"));
                }
            }
            finally
            {
                await NavigationService.Shutdown();
            }
        }

        [Fact]
        public async Task ContextAttach_ExposesFullProjectionWithoutRawSessionClaims()
        {
            var fixture = RuntimeTestFixture.Build<StubIdle>(typeof(StubA), typeof(StubB));
            var debug = new InspectionRuntime();

            NavigationService.UseContext(fixture.Context);
            try
            {
                using (InspectionNavigationObserver.Attach(fixture.Context, debug))
                {
                    Assert.Equal(
                        new[]
                        {
                            "Navigation::activeAttempts",
                            "Navigation::backgroundLoads",
                            "Navigation::cache",
                            "Navigation::current",
                            "Navigation::currentPage",
                            "Navigation::history",
                            "Navigation::idle",
                            "Navigation::inFlight",
                            "Navigation::lastNavigation",
                            "Navigation::overlays",
                            "Navigation::pages",
                            "Navigation::queue",
                            "Navigation::registry",
                            "Navigation::runtime",
                            "Navigation::session",
                            "Navigation::stats"
                        },
                        debug.StateKeys().OrderBy(k => k).ToArray());

                    NavigationService.Session.SignIn(
                        new[] { "admin" },
                        new[] { "edit-sales" });

                    // Session.Changed refreshes the immutable mirror immediately;
                    // a navigation is not required to make authentication visible.
                    var sessionBeforeNavigation =
                        debug.CaptureState()["Navigation::session"];
                    Assert.True((bool)Prop(
                        sessionBeforeNavigation,
                        "authenticated"));
                    Assert.Equal(1, Prop(sessionBeforeNavigation, "roleCount"));
                    Assert.Equal(
                        1,
                        Prop(sessionBeforeNavigation, "permissionCount"));
                    Assert.DoesNotContain(
                        "edit-sales",
                        sessionBeforeNavigation.ToString());

                    await NavigationService.SwitchPage<StubA>();
                    await NavigationService.SwitchPage<StubB>();

                    var state = debug.CaptureState();

                    // Forward navigation records the page being left, so the first
                    // navigation (from nothing) records nothing.
                    var history = state["Navigation::history"];
                    Assert.True((bool)Prop(history, "canGoBack"));
                    Assert.Contains(
                        nameof(StubA),
                        (System.Collections.Generic.IEnumerable<string>)
                            Prop(history, "back"));

                    var session = state["Navigation::session"];
                    Assert.True((bool)Prop(session, "authenticated"));
                    Assert.Equal(1, Prop(session, "roleCount"));
                    Assert.Equal(1, Prop(session, "permissionCount"));
                    Assert.Null(session.GetType().GetProperty("roles"));
                    Assert.Null(session.GetType().GetProperty("permissions"));
                    Assert.DoesNotContain("admin", session.ToString());

                    var current = state["Navigation::current"];
                    var last = state["Navigation::lastNavigation"];
                    Assert.Equal(nameof(StubB), Prop(current, "name"));
                    Assert.Equal(nameof(StubB), Prop(last, "to"));
                    Assert.NotNull(Prop(last, "requestId"));
                    Assert.True((long)Prop(last, "durationMs") >= 0);

                    var stats = state["Navigation::stats"];
                    Assert.Equal(2, Prop(stats, "started"));
                    Assert.Equal(2, Prop(stats, "navigated"));
                    Assert.Equal(typeof(StubB).FullName, Prop(stats, "lastStarted"));
                    Assert.Equal(2, Prop(stats, "requestsCompleted"));

                    // CaptureState can run from a diagnostics worker. It returns the
                    // history mirror updated by HistoryChanged, never the live
                    // UI-thread-affine NavigationHistory object.
                    var workerState = await Task.Run(() => debug.CaptureState());
                    var workerHistory = workerState["Navigation::history"];
                    Assert.True((bool)Prop(workerHistory, "canGoBack"));
                    Assert.Equal(1, Prop(workerHistory, "backCount"));
                }
            }
            finally
            {
                await NavigationService.Shutdown();
            }
        }

        [Fact]
        public async Task Dispose_UnsubscribesFromTheStaticFacade()
        {
            var fixture = RuntimeTestFixture.Build<StubIdle>(typeof(StubA), typeof(StubB));
            var debug = new InspectionRuntime();

            NavigationService.UseContext(fixture.Context);
            try
            {
                InspectionNavigationObserver.Attach(fixture.Context, debug).Dispose();

                await NavigationService.SwitchPage<StubA>();

                Assert.Empty(debug.GetOperations());
                Assert.Empty(debug.StateKeys());
            }
            finally
            {
                await NavigationService.Shutdown();
            }
        }

        /// <summary>
        /// Snapshots are anonymous types by design (consumers read them via
        /// ToString()/reflection, never by name), so tests read them the same way.
        /// </summary>
        private static object Prop(object snapshot, string name)
            => snapshot.GetType().GetProperty(name).GetValue(snapshot);
    }
}
