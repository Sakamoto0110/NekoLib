using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NekoLib.Navigation.Contracts.Runtime;
using NekoLib.Navigation.Contracts.Platform;
using NekoLib.Navigation.Metadata;
using NekoLib.Navigation.Runtime.Services;
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

        [Fact]
        public async Task NavigateAsync_PageAwareBlocker_TracksLatePageAttachAndDetach()
        {
            var blocker = new TrackingPageAwareBlocker();
            var fx = RuntimeTestFixture.BuildWithInteractionBlocker<StubIdle>(
                blocker,
                typeof(StubA));

            await fx.Runtime.GoIdleAsync();
            var idle = fx.Runtime.Current;
            await fx.Runtime.NavigateAsync(
                typeof(StubA),
                NavigationArgs.Default());
            var target = fx.Runtime.Current;

            Assert.Contains(
                blocker.Added,
                item =>
                    ReferenceEquals(item.View, idle.NativeView) &&
                    !item.IsModal);
            Assert.Contains(
                blocker.Added,
                item =>
                    ReferenceEquals(item.View, target.NativeView) &&
                    !item.IsModal);
            Assert.Contains(idle.NativeView, blocker.Removed);
        }

        [Fact]
        public void FirstUseConcurrentSurfaceCalls_AttachRuntimeServicesOnce()
        {
            var service = new CountingDialogService();
            var fx = RuntimeTestFixture.BuildWithServices<StubIdle>(
                (services, _, __) =>
                    services.Register<IDialogService>(service));

            Parallel.For(
                0,
                100,
                _ => fx.Runtime
                    .ShowDialogAsync<StubDialogView>()
                    .GetAwaiter()
                    .GetResult());

            Assert.Equal(1, service.AttachDiagnosticsCalls);
            Assert.Equal(100, service.ShowCalls);
        }

        [Fact]
        public async Task GoBackAsync_WhenGuardDenies_PreservesBothHistoryStacks()
        {
            var fx = RuntimeTestFixture.Build<StubIdle>(
                typeof(StubAuthenticated),
                typeof(StubA));

            fx.Context.Session.SignIn();
            await fx.Runtime.GoIdleAsync();
            await fx.Runtime.NavigateAsync(typeof(StubAuthenticated), NavigationArgs.Default());
            await fx.Runtime.NavigateAsync(typeof(StubA), NavigationArgs.Default());
            fx.Context.Session.SignOut();

            var backBefore = fx.Context.History.HistoryBack.ToArray();
            var forwardBefore = fx.Context.History.HistoryForward.ToArray();

            bool went = await fx.Runtime.GoBackAsync();

            Assert.False(went);
            Assert.IsType<StubA>(fx.Runtime.Current);
            Assert.Equal(backBefore, fx.Context.History.HistoryBack);
            Assert.Equal(forwardBefore, fx.Context.History.HistoryForward);
        }

        [Fact]
        public async Task GoBackAsync_WhenGuardRedirects_DoesNotConsumeDeniedHistoryEntry()
        {
            var fx = RuntimeTestFixture.Build<StubIdle>(
                typeof(StubRoleRedirect),
                typeof(StubA));

            fx.Context.Session.SignIn("admin");
            await fx.Runtime.GoIdleAsync();
            await fx.Runtime.NavigateAsync(typeof(StubRoleRedirect), NavigationArgs.Default());
            await fx.Runtime.NavigateAsync(typeof(StubA), NavigationArgs.Default());
            fx.Context.Session.SignOut();

            var backBefore = fx.Context.History.HistoryBack.ToArray();
            var forwardBefore = fx.Context.History.HistoryForward.ToArray();

            bool went = await fx.Runtime.GoBackAsync();

            Assert.False(went);
            Assert.IsType<StubIdle>(fx.Runtime.Current);
            Assert.Equal(backBefore, fx.Context.History.HistoryBack);
            Assert.Equal(forwardBefore, fx.Context.History.HistoryForward);
        }

        [Fact]
        public async Task GoBackAsync_WhenPreShowLoadFails_PreservesBothHistoryStacks()
        {
            var fx = RuntimeTestFixture.Build<StubIdle>(
                typeof(StubConditionalLoadBefore),
                typeof(StubA));

            await fx.Runtime.GoIdleAsync();
            await fx.Runtime.NavigateAsync(
                typeof(StubConditionalLoadBefore),
                NavigationArgs.Default());

            var conditional = (StubConditionalLoadBefore)fx.Runtime.Current;
            await fx.Runtime.NavigateAsync(typeof(StubA), NavigationArgs.Default());
            conditional.FailLoad = true;

            var backBefore = fx.Context.History.HistoryBack.ToArray();
            var forwardBefore = fx.Context.History.HistoryForward.ToArray();

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => fx.Runtime.GoBackAsync());

            Assert.IsType<StubA>(fx.Runtime.Current);
            Assert.Equal(backBefore, fx.Context.History.HistoryBack);
            Assert.Equal(forwardBefore, fx.Context.History.HistoryForward);
        }

        [Fact]
        public async Task GoBackAsync_CapturesForwardStateOnlyOnce()
        {
            var fx = RuntimeTestFixture.Build<StubIdle>(
                typeof(StubB),
                typeof(StubStateful));

            await fx.Runtime.GoIdleAsync();
            await fx.Runtime.NavigateAsync(typeof(StubB), NavigationArgs.Default());
            await fx.Runtime.NavigateAsync(typeof(StubStateful), NavigationArgs.Default());

            var stateful = (StubStateful)fx.Runtime.Current;

            Assert.True(await fx.Runtime.GoBackAsync());
            Assert.Equal(1, stateful.CaptureCallCount);
        }

        [Fact]
        public async Task GoBackAsync_WhenLifecycleMutatesHistory_DoesNotPopDifferentEntry()
        {
            var fx = RuntimeTestFixture.Build<StubIdle>(
                typeof(StubA),
                typeof(StubB));

            await fx.Runtime.GoIdleAsync();
            await fx.Runtime.NavigateAsync(typeof(StubA), NavigationArgs.Default());
            await fx.Runtime.NavigateAsync(typeof(StubB), NavigationArgs.Default());

            var expected = fx.Context.History.HistoryBack.First();
            var injected = new PageHistoryEntry(
                typeof(StubB),
                "injected",
                null);
            fx.Runtime.Navigated += (_, _, args) =>
            {
                if (args.IsBackNavigation)
                    fx.Context.History.Record(injected);
            };

            Assert.True(await fx.Runtime.GoBackAsync());

            Assert.Same(injected, fx.Context.History.HistoryBack.First());
            Assert.Contains(expected, fx.Context.History.HistoryBack);
            Assert.Empty(fx.Context.History.HistoryForward);
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

        [Fact]
        public async Task NavigateAsync_WhenLoadBeforeShowFails_CleansMaskAndTransientTarget()
        {
            var fx = RuntimeTestFixture.Build<StubIdle>(
                typeof(StubFailingTransientLoadBefore),
                typeof(StubLoadingMask));

            await fx.Runtime.GoIdleAsync();

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => fx.Runtime.NavigateAsync(
                    typeof(StubFailingTransientLoadBefore),
                    NavigationArgs.Default()));

            var target = Assert.Single(
                fx.CreatedPages.OfType<StubFailingTransientLoadBefore>());
            var mask = Assert.Single(fx.CreatedPages.OfType<StubLoadingMask>());

            Assert.True(target.IsDisposed);
            Assert.DoesNotContain(target, fx.Host.Attached);
            Assert.Equal(1, mask.OpenCount);
            Assert.Equal(1, mask.CloseCount);
            Assert.True(mask.IsDisposed);
            Assert.Contains(mask.NativeView, fx.Host.AddedViews);
            Assert.Contains(mask.NativeView, fx.Host.RemovedViews);
            Assert.IsType<StubIdle>(fx.Runtime.Current);
        }

        [Fact]
        public async Task NavigateAsync_KeepAttachedWithoutVisibility_FallsBackToDetach()
        {
            var fx = RuntimeTestFixture.Build<StubIdle>(
                typeof(StubKeepAttachedWithoutVisibility),
                typeof(StubA));

            await fx.Runtime.GoIdleAsync();
            await fx.Runtime.NavigateAsync(
                typeof(StubKeepAttachedWithoutVisibility),
                NavigationArgs.Default());
            var page = (StubKeepAttachedWithoutVisibility)fx.Runtime.Current;

            await fx.Runtime.NavigateAsync(typeof(StubA), NavigationArgs.Default());

            Assert.Contains(page, fx.Host.Detached);
            Assert.False(page.IsDisposed);
        }

        [Theory]
        [InlineData(typeof(StubThrowingShow))]
        [InlineData(typeof(StubThrowingLoadAfter))]
        [InlineData(typeof(StubThrowingEnter))]
        public async Task NavigateAsync_FailureAfterAttach_RestoresPreviousPage(
            Type failingPageType)
        {
            var fx = RuntimeTestFixture.Build<StubIdle>(failingPageType);
            await fx.Runtime.GoIdleAsync();
            var previous = fx.Runtime.Current;

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => fx.Runtime.NavigateAsync(
                    failingPageType,
                    NavigationArgs.Default()));

            var failed = Assert.Single(
                fx.CreatedPages.Where(
                    page => page.GetType() == failingPageType));
            Assert.Same(previous, fx.Runtime.Current);
            Assert.False(previous.IsDisposed);
            Assert.True(failed.IsDisposed);
            Assert.Contains(failed, fx.Host.Detached);
            Assert.Same(previous, fx.Host.Fronted.Last());
        }

        [Fact]
        public async Task NavigateAsync_RollbackShowFails_DoesNotPublishFalseVisibleCurrent()
        {
            var fx = RuntimeTestFixture.Build<StubConditionalVisibility>(
                typeof(StubThrowingEnter));
            var noVisible = 0;
            fx.Runtime.OnNoPageVisible += () => noVisible++;
            await fx.Runtime.GoIdleAsync();
            var previous = Assert.IsType<StubConditionalVisibility>(
                fx.Runtime.Current);
            previous.ThrowOnShow = true;

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => fx.Runtime.NavigateAsync(
                    typeof(StubThrowingEnter),
                    NavigationArgs.Default()));

            Assert.Null(fx.Runtime.Current);
            Assert.Equal(1, noVisible);
            Assert.Equal(1, previous.HideCount);
            Assert.Equal(2, previous.ShowCount);
        }

        [Fact]
        public async Task NavigateAsync_RollbackBringToFrontFails_DoesNotRestoreCurrent()
        {
            var fx = RuntimeTestFixture.Build<StubConditionalVisibility>(
                typeof(StubThrowingEnter));
            var noVisible = 0;
            fx.Runtime.OnNoPageVisible += () => noVisible++;
            await fx.Runtime.GoIdleAsync();
            var previous = Assert.IsType<StubConditionalVisibility>(
                fx.Runtime.Current);
            fx.Host.OperationObserved = (operation, page) =>
            {
                if (operation == "front" &&
                    ReferenceEquals(page, previous))
                {
                    throw new InvalidOperationException(
                        "rollback front failed");
                }
            };

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => fx.Runtime.NavigateAsync(
                    typeof(StubThrowingEnter),
                    NavigationArgs.Default()));

            Assert.Null(fx.Runtime.Current);
            Assert.Equal(1, noVisible);
            Assert.Equal(1, previous.HideCount);
            Assert.Equal(1, previous.ShowCount);
        }

        [Fact]
        public async Task ResetAsync_WhenPageDisposeFails_ReportsFailureAfterCleanup()
        {
            var fx = RuntimeTestFixture.Build<StubThrowingDispose>();
            await fx.Runtime.GoIdleAsync();
            var page = fx.Runtime.Current;

            var failure = await Assert.ThrowsAsync<InvalidOperationException>(
                () => fx.Runtime.ResetAsync());

            Assert.Equal("dispose failed", failure.Message);
            Assert.Contains(page, fx.Host.Detached);
            Assert.Null(fx.Runtime.Current);
        }

        [Fact]
        public async Task ResetAsync_WhenSurfaceCleanupFails_ReportsFailureAfterCleanup()
        {
            DialogService dialogService = null;
            var fx = RuntimeTestFixture.BuildWithServices<StubIdle>(
                (services, host, factory) =>
                {
                    factory.Register(
                        typeof(ThrowingDisposeDialogView),
                        () => new ThrowingDisposeDialogView());
                    dialogService = new DialogService(host, factory);
                    services.Register<IDialogService>(dialogService);
                });
            await fx.Runtime.GoIdleAsync();
            var pending =
                dialogService.ShowDialogAsync<ThrowingDisposeDialogView>();

            var failure = await Assert.ThrowsAsync<InvalidOperationException>(
                () => fx.Runtime.ResetAsync());

            Assert.Equal("dialog dispose failed", failure.Message);
            Assert.False(await pending);
            Assert.Null(fx.Runtime.Current);
        }

        [Fact]
        public async Task ResetAsync_DetachesAndDisposesEveryTrackedAttachedPage()
        {
            var fx = RuntimeTestFixture.Build<StubIdle>(
                typeof(StubKeepAttached),
                typeof(StubA));

            await fx.Runtime.GoIdleAsync();
            await fx.Runtime.NavigateAsync(typeof(StubKeepAttached), NavigationArgs.Default());
            var kept = (StubKeepAttached)fx.Runtime.Current;
            await fx.Runtime.NavigateAsync(typeof(StubA), NavigationArgs.Default());
            var current = (StubA)fx.Runtime.Current;

            await fx.Runtime.ResetAsync();

            Assert.Contains(kept, fx.Host.Detached);
            Assert.Contains(current, fx.Host.Detached);
            Assert.True(kept.IsDisposed);
            Assert.True(current.IsDisposed);
            Assert.Null(fx.Runtime.Current);
        }

        [Fact]
        public async Task DisposeAsync_DetachesAndDisposesEveryTrackedAttachedPage()
        {
            var fx = RuntimeTestFixture.Build<StubIdle>(
                typeof(StubKeepAttached),
                typeof(StubA));

            await fx.Runtime.GoIdleAsync();
            await fx.Runtime.NavigateAsync(typeof(StubKeepAttached), NavigationArgs.Default());
            var kept = (StubKeepAttached)fx.Runtime.Current;
            await fx.Runtime.NavigateAsync(typeof(StubA), NavigationArgs.Default());
            var current = (StubA)fx.Runtime.Current;

            await fx.Runtime.DisposeAsync();

            Assert.Contains(kept, fx.Host.Detached);
            Assert.Contains(current, fx.Host.Detached);
            Assert.True(kept.IsDisposed);
            Assert.True(current.IsDisposed);
            Assert.Null(fx.Runtime.Current);
        }

        [Fact]
        public async Task DisposeAsync_WhenSurfaceCleanupFails_ReportsFailureAfterCleanup()
        {
            DialogService dialogService = null;
            var fx = RuntimeTestFixture.BuildWithServices<StubIdle>(
                (services, host, factory) =>
                {
                    factory.Register(
                        typeof(ThrowingDisposeDialogView),
                        () => new ThrowingDisposeDialogView());
                    dialogService = new DialogService(host, factory);
                    services.Register<IDialogService>(dialogService);
                });
            await fx.Runtime.GoIdleAsync();
            var pending =
                dialogService.ShowDialogAsync<ThrowingDisposeDialogView>();

            var failure = await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await fx.Runtime.DisposeAsync());

            Assert.Equal("dialog dispose failed", failure.Message);
            Assert.False(await pending);
            Assert.Null(fx.Runtime.Current);
        }

        [Fact]
        public async Task RuntimeEvents_WhenFirstSubscriberThrows_ContinueWithLaterSubscribers()
        {
            var fx = RuntimeTestFixture.Build<StubIdle>(typeof(StubA));
            int navigated = 0;
            int currentChanged = 0;
            int historyChanged = 0;
            int firstAttached = 0;
            int noAttached = 0;
            int noVisible = 0;

            fx.Runtime.Navigated += (_, _, _) => throw new InvalidOperationException();
            fx.Runtime.Navigated += (_, _, _) => navigated++;
            fx.Runtime.CurrentChanged += _ => throw new InvalidOperationException();
            fx.Runtime.CurrentChanged += _ => currentChanged++;
            fx.Runtime.HistoryChanged += () => throw new InvalidOperationException();
            fx.Runtime.HistoryChanged += () => historyChanged++;
            fx.Runtime.OnFirstPageAttached += _ => throw new InvalidOperationException();
            fx.Runtime.OnFirstPageAttached += _ => firstAttached++;
            fx.Runtime.OnNoPageAttached += () => throw new InvalidOperationException();
            fx.Runtime.OnNoPageAttached += () => noAttached++;
            fx.Runtime.OnNoPageVisible += () => throw new InvalidOperationException();
            fx.Runtime.OnNoPageVisible += () => noVisible++;

            await fx.Runtime.GoIdleAsync();
            await fx.Runtime.NavigateAsync(typeof(StubA), NavigationArgs.Default());
            await fx.Runtime.ResetAsync();

            Assert.Equal(2, navigated);
            Assert.Equal(3, currentChanged);
            Assert.Equal(2, historyChanged);
            Assert.Equal(1, firstAttached);
            Assert.Equal(1, noAttached);
            Assert.Equal(1, noVisible);
        }

        [Fact]
        public async Task NavigationFailed_WhenFirstSubscriberThrows_PreservesFailureAndNotifiesNext()
        {
            var fx = RuntimeTestFixture.Build<StubIdle>();
            int notified = 0;

            fx.Runtime.NavigationFailed += (_, _, _) => throw new ApplicationException();
            fx.Runtime.NavigationFailed += (_, _, _) => notified++;

            var failure = await Assert.ThrowsAsync<InvalidOperationException>(
                () => fx.Runtime.NavigateAsync(typeof(StubA), NavigationArgs.Default()));

            Assert.Contains("not a registered page", failure.Message);
            Assert.Equal(1, notified);
        }

        private sealed class TrackingPageAwareBlocker :
            IPageAwareInteractionBlocker
        {
            public System.Collections.Generic.List<TrackedView> Added
                { get; } =
                    new System.Collections.Generic.List<TrackedView>();
            public System.Collections.Generic.List<object> Removed
                { get; } =
                    new System.Collections.Generic.List<object>();

            public void Block()
            {
            }

            public void Unblock()
            {
            }

            public void OnViewAdded(
                object view,
                bool isModalSurface)
                => Added.Add(new TrackedView(view, isModalSurface));

            public void OnViewRemoved(object view)
                => Removed.Add(view);
        }

        private sealed class CountingDialogService :
            IDialogService,
            NekoLib.Navigation.Diagnostics.INavigationDiagnosticsAware
        {
            private int _attachDiagnosticsCalls;
            private int _showCalls;

            public int AttachDiagnosticsCalls =>
                Volatile.Read(ref _attachDiagnosticsCalls);
            public int ShowCalls =>
                Volatile.Read(ref _showCalls);

            public Task<bool> ShowDialogAsync<TDialog>(
                object payload = null)
                where TDialog : class, NekoLib.Navigation.Contracts.Pages.IDialogView
            {
                Interlocked.Increment(ref _showCalls);
                return Task.FromResult(false);
            }

            public void CloseAll()
            {
            }

            public void AttachDiagnostics(
                NekoLib.Navigation.Diagnostics.NavigationDiagnostics diagnostics)
                => Interlocked.Increment(
                    ref _attachDiagnosticsCalls);
        }

        private sealed class TrackedView
        {
            public TrackedView(object view, bool isModal)
            {
                View = view;
                IsModal = isModal;
            }

            public object View { get; }
            public bool IsModal { get; }
        }
    }
}
