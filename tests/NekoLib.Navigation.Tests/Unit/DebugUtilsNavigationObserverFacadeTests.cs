using System.Linq;
using System.Threading.Tasks;
using NekoLib.DebugUtils;
using NekoLib.Navigation.Diagnostics;
using NekoLib.Navigation.Tests.Unit.Fakes;
using Xunit;

namespace NekoLib.Navigation.Tests.Unit
{
    /// <summary>
    /// Covers the full-fidelity observability bridge: the signals that only exist on
    /// the static <see cref="NavigationService"/> facade (navigation intent,
    /// attach/visibility) plus the context-backed history/session snapshots.
    ///
    /// <para>
    /// These tests mount the static facade, which is process-wide state — hence the
    /// shared collection (xunit serializes tests within one collection) and the
    /// mandatory <c>Shutdown()</c> in a finally. Any future test that mounts
    /// <see cref="NavigationService"/> belongs in this collection too. Tests that
    /// only need hub events should use the hub-only overload instead and stay
    /// parallel — see <see cref="DebugUtilsNavigationObserverTests"/>.
    /// </para>
    /// </summary>
    [Collection("NavigationServiceFacade")]
    public sealed class DebugUtilsNavigationObserverFacadeTests
    {
        [Fact]
        public async Task NavigationIntent_IsRecordedBeforeTheOutcome()
        {
            var fixture = RuntimeTestFixture.Build<StubIdle>(typeof(StubA));
            var debug = new DebugUtilsRuntime();

            NavigationService.UseContext(fixture.Context);
            try
            {
                using (DebugUtilsNavigationObserver.Attach(fixture.Context, debug))
                {
                    await NavigationService.SwitchPage<StubA>();

                    var ops = debug.GetOperations().Select(o => o.Operation).ToList();

                    // The intent hook is the whole point of the static subscription:
                    // the hub only speaks once navigation has resolved.
                    Assert.Contains("NavigationStarted", ops);
                    Assert.Contains("Navigated", ops);
                    Assert.True(
                        ops.IndexOf("NavigationStarted") < ops.IndexOf("Navigated"),
                        "intent must be recorded before the outcome");
                }
            }
            finally
            {
                await NavigationService.Shutdown();
            }
        }

        [Fact]
        public async Task ContextAttach_ExposesCurrentStatsHistoryAndSession()
        {
            var fixture = RuntimeTestFixture.Build<StubIdle>(typeof(StubA), typeof(StubB));
            var debug = new DebugUtilsRuntime();

            NavigationService.UseContext(fixture.Context);
            try
            {
                using (DebugUtilsNavigationObserver.Attach(fixture.Context, debug))
                {
                    Assert.Equal(
                        new[]
                        {
                            "Navigation::current",
                            "Navigation::history",
                            "Navigation::session",
                            "Navigation::stats"
                        },
                        debug.StateKeys().OrderBy(k => k).ToArray());

                    NavigationService.Session.SignIn("admin");
                    await NavigationService.SwitchPage<StubA>();
                    await NavigationService.SwitchPage<StubB>();

                    var state = debug.CaptureState();

                    // Forward navigation records the page being left, so the first
                    // navigation (from nothing) records nothing.
                    var history = state["Navigation::history"];
                    Assert.True((bool)Prop(history, "canGoBack"));
                    Assert.Contains(nameof(StubA), (System.Collections.Generic.List<string>)Prop(history, "back"));

                    var session = state["Navigation::session"];
                    Assert.True((bool)Prop(session, "authenticated"));
                    Assert.Contains("admin", (System.Collections.Generic.List<string>)Prop(session, "roles"));

                    var stats = state["Navigation::stats"];
                    Assert.Equal(2, Prop(stats, "started"));
                    Assert.Equal(2, Prop(stats, "navigated"));
                    Assert.Equal(nameof(StubB), Prop(stats, "lastStarted"));
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
            var debug = new DebugUtilsRuntime();

            NavigationService.UseContext(fixture.Context);
            try
            {
                DebugUtilsNavigationObserver.Attach(fixture.Context, debug).Dispose();

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
