using System.Collections.Generic;
using System.Linq;
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
        public void LastNavigation_IsExposedAsPullableState()
        {
            var hub = new NavigationEventHub();
            var debug = new DebugUtilsRuntime();

            using (DebugUtilsNavigationObserver.Attach(hub, debug))
            {
                hub.Publish(SuccessEntry());

                var state = debug.CaptureState();
                Assert.True(state.ContainsKey(DebugUtilsNavigationObserver.Module + "::current"));
                Assert.NotNull(state[DebugUtilsNavigationObserver.Module + "::current"]);
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
        public void HubOnlyAttach_ExposesCurrentAndStatsOnly()
        {
            var hub = new NavigationEventHub();
            var debug = new DebugUtilsRuntime();

            using (DebugUtilsNavigationObserver.Attach(hub, debug))
            {
                Assert.Equal(
                    new[] { "Navigation::current", "Navigation::stats" },
                    Assert.IsAssignableFrom<IEnumerable<string>>(debug.StateKeys()).OrderBy(k => k).ToArray());
            }
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
