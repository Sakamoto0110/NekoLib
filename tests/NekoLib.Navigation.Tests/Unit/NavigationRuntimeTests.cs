using System.Linq;
using System.Threading.Tasks;
using NekoLib.Navigation.Metadata;
using NekoLib.Navigation.Tests.Unit.Fakes;
using Xunit;

namespace NekoLib.Navigation.Tests.Unit
{
    /// <summary>
    /// Headless integration tests for <see cref="Runtime.Core.NavigationRuntime"/>
    /// driven through fakes (sync dispatcher, recording page host, stub pages).
    /// These exercise the runtime end-to-end without WinForms, which is exactly
    /// the coverage layer that was missing when NEW-7 (history double-push on
    /// back-nav) shipped.
    /// </summary>
    public class NavigationRuntimeTests
    {
        // -----------------------------------------------------------------
        // Forward navigation: back-stack records the leaving page
        // -----------------------------------------------------------------

        [Fact]
        public async Task NavigateAsync_FromIdle_AttachesTargetAndPushesIdleOntoBackStack()
        {
            var fx = RuntimeTestFixture.Build<StubIdle>(typeof(StubA));

            await fx.Runtime.GoIdleAsync();
            await fx.Runtime.NavigateAsync(typeof(StubA), NavigationArgs.Default());

            Assert.IsType<StubA>(fx.Runtime.Current);
            Assert.Single(fx.Context.History.HistoryBack);
            Assert.Equal(typeof(StubIdle), fx.Context.History.HistoryBack.First().PageType);
        }

        [Fact]
        public async Task NavigateAsync_ChainedForward_RecordsLeavingPagesInOrder()
        {
            var fx = RuntimeTestFixture.Build<StubIdle>(typeof(StubA), typeof(StubB), typeof(StubC));

            await fx.Runtime.GoIdleAsync();                                              // current: Idle
            await fx.Runtime.NavigateAsync(typeof(StubA), NavigationArgs.Default());     // back: [Idle],     cur: A
            await fx.Runtime.NavigateAsync(typeof(StubB), NavigationArgs.Default());     // back: [Idle, A],  cur: B
            await fx.Runtime.NavigateAsync(typeof(StubC), NavigationArgs.Default());     // back: [Idle,A,B], cur: C

            var back = fx.Context.History.HistoryBack.Select(e => e.PageType).ToArray();
            // HistoryBack enumerates top-first (Stack ordering): top is the most-recent
            // pushed (= the page we just left, B), bottom is the oldest (Idle).
            Assert.Equal(new[] { typeof(StubB), typeof(StubA), typeof(StubIdle) }, back);
            Assert.IsType<StubC>(fx.Runtime.Current);
        }

        // -----------------------------------------------------------------
        // Back navigation
        // -----------------------------------------------------------------

        [Fact]
        public async Task GoBackAsync_PopsBackStackAndPushesCurrentForward()
        {
            var fx = RuntimeTestFixture.Build<StubIdle>(typeof(StubA));

            await fx.Runtime.GoIdleAsync();
            await fx.Runtime.NavigateAsync(typeof(StubA), NavigationArgs.Default());

            bool went = await fx.Runtime.GoBackAsync();

            Assert.True(went);
            Assert.IsType<StubIdle>(fx.Runtime.Current);
            Assert.Empty(fx.Context.History.HistoryBack);
            Assert.Single(fx.Context.History.HistoryForward);
            Assert.Equal(typeof(StubA), fx.Context.History.HistoryForward.First().PageType);
        }

        [Fact]
        public async Task GoBackAsync_OnEmptyHistory_ReturnsFalse()
        {
            var fx = RuntimeTestFixture.Build<StubIdle>();
            await fx.Runtime.GoIdleAsync();

            bool went = await fx.Runtime.GoBackAsync();

            Assert.False(went);
            Assert.IsType<StubIdle>(fx.Runtime.Current);
        }

        /// <summary>
        /// NEW-7 regression: walks IDLE → A → B → C → back → back, asserts we
        /// land on A (not C). Before the fix, <c>SwitchInternalAsync</c>
        /// unconditionally re-pushed the page being left back onto the back-stack
        /// during the back-step itself, so each back popped the page we just left
        /// and the user bounced between two pages forever.
        /// </summary>
        [Fact]
        public async Task GoBackAsync_TwiceFromC_LandsOnA_NotOnC()
        {
            var fx = RuntimeTestFixture.Build<StubIdle>(typeof(StubA), typeof(StubB), typeof(StubC));

            await fx.Runtime.GoIdleAsync();
            await fx.Runtime.NavigateAsync(typeof(StubA), NavigationArgs.Default());
            await fx.Runtime.NavigateAsync(typeof(StubB), NavigationArgs.Default());
            await fx.Runtime.NavigateAsync(typeof(StubC), NavigationArgs.Default());
            //                                                            current = C
            //                                                            back = [Idle, A, B]

            bool back1 = await fx.Runtime.GoBackAsync();
            //                                                            current = B
            //                                                            back = [Idle, A]
            bool back2 = await fx.Runtime.GoBackAsync();
            //                                                            current = A
            //                                                            back = [Idle]

            Assert.True(back1);
            Assert.True(back2);
            Assert.IsType<StubA>(fx.Runtime.Current);

            // Going back once more must reach Idle, not bounce.
            Assert.True(await fx.Runtime.GoBackAsync());
            Assert.IsType<StubIdle>(fx.Runtime.Current);

            Assert.False(await fx.Runtime.GoBackAsync());
        }

        // -----------------------------------------------------------------
        // Lifecycle hooks fire in order
        // -----------------------------------------------------------------

        [Fact]
        public async Task NavigateAsync_FiresFromHookOnLeaving_AndToHookOnArrival()
        {
            var fx = RuntimeTestFixture.Build<StubIdle>(typeof(StubA));

            await fx.Runtime.GoIdleAsync();
            var idle = (StubIdle)fx.Runtime.Current;

            await fx.Runtime.NavigateAsync(typeof(StubA), NavigationArgs.Default());
            var a = (StubA)fx.Runtime.Current;

            Assert.Equal(1, idle.OnNavigatedFromCount);  // idle was left
            Assert.Equal(1, a.OnNavigatedToCount);       // a was entered
            Assert.Equal(1, idle.OnNavigatedToCount);    // (initial GoIdle)
        }

        // -----------------------------------------------------------------
        // IPageStateful contract (Pass 4 / N-2)
        // -----------------------------------------------------------------

        /// <summary>
        /// Pass 4 / N-2 + Pass 5 NEW-7 + NEW-10 together: a stateful page captures
        /// its counter on leave, the runtime stores it in the history entry, and on
        /// back-navigation a fresh Transient instance has RestoreState called with
        /// the captured value before its OnNavigatedToAsync runs.
        /// </summary>
        [Fact]
        public async Task BackNavigation_ToStatefulPage_CallsRestoreStateWithCapturedValue()
        {
            var fx = RuntimeTestFixture.Build<StubIdle>(typeof(StubStateful), typeof(StubA));

            await fx.Runtime.GoIdleAsync();
            await fx.Runtime.NavigateAsync(typeof(StubStateful), NavigationArgs.Default());

            var firstInstance = (StubStateful)fx.Runtime.Current;
            firstInstance.Counter = 3;

            await fx.Runtime.NavigateAsync(typeof(StubA), NavigationArgs.Default());
            await fx.Runtime.GoBackAsync();

            var restoredInstance = (StubStateful)fx.Runtime.Current;

            // Transient policy -> back creates a fresh instance, distinct from the
            // first one. RestoreState must have been called with the captured 3.
            Assert.NotSame(firstInstance, restoredInstance);
            Assert.Equal(1, restoredInstance.RestoreCallCount);
            Assert.Equal(3, restoredInstance.LastRestoredState);
            Assert.Equal(3, restoredInstance.Counter);

            // Restored instance's NavigationArgs also carries IsBackNavigation = true.
            Assert.True(restoredInstance.LastNavArgs.IsBackNavigation);
        }

        // -----------------------------------------------------------------
        // Host attach/detach side-effects
        // -----------------------------------------------------------------

        [Fact]
        public async Task NavigateAsync_AttachesTargetAndDetachesPrevious()
        {
            var fx = RuntimeTestFixture.Build<StubIdle>(typeof(StubA));

            await fx.Runtime.GoIdleAsync();
            await fx.Runtime.NavigateAsync(typeof(StubA), NavigationArgs.Default());

            // First nav attached Idle; second attached A and detached Idle.
            Assert.Contains(fx.Host.Attached, p => p is StubIdle);
            Assert.Contains(fx.Host.Attached, p => p is StubA);
            Assert.Contains(fx.Host.Detached, p => p is StubIdle);
        }
    }
}
