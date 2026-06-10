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

        private static PageLogEntry SuccessEntry()
            => new PageLogEntry(
                typeof(string), "From", typeof(int), "To",
                NavigationArgs.Default(), true,
                default(PagePresentationMode), default(NavigationLoadMode), default(PageReusePolicy));

        private static PageLogEntry FailureEntry()
            => new PageLogEntry(
                typeof(string), "From", typeof(int), "To",
                NavigationArgs.Default(), false,
                default(PagePresentationMode), default(NavigationLoadMode), default(PageReusePolicy),
                failureKind: NavigationFailureKind.None, error: "boom");
    }
}
