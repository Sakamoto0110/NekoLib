using System;
using System.Threading.Tasks;
using NekoLib.Navigation.Runtime.Factories;
using NekoLib.Navigation.Runtime.Services;
using NekoLib.Navigation.Tests.Unit.Fakes;
using Xunit;

namespace NekoLib.Navigation.Tests.Unit
{
    /// <summary>
    /// Pins the core PopoverService contract: shows the view without engaging
    /// any blocker, forwards completion, auto-dismisses IUnfocusAware views via
    /// the focus observer, and tears subscriptions down deterministically.
    /// </summary>
    public class PopoverServiceTests
    {
        private static (PopoverService svc, FakePageHost host, FakeFocusObserverAdapter focus) Build()
        {
            var host = new FakePageHost();
            var factory = new PageFactory();
            factory.Register(typeof(StubPopoverView), () => new StubPopoverView());
            factory.Register(typeof(StubAutoDismissPopoverView), () => new StubAutoDismissPopoverView());

            var focus = new FakeFocusObserverAdapter();
            return (new PopoverService(host, factory, focus), host, focus);
        }

        [Fact]
        public async Task ShowPopover_AttachesViewAndResolvesWithCompletion()
        {
            var (svc, host, _) = Build();

            var task = svc.ShowPopoverAsync<StubPopoverView>("payload");
            var view = (StubPopoverView)host.AddedViews[0];

            Assert.Equal("payload", view.LastShownPayload);
            Assert.NotNull(view.CompletionCallback);

            view.CompletionCallback(true);

            Assert.True(await task);
            Assert.True(view.IsDisposed);
            Assert.Single(host.RemovedViews);
        }

        [Fact]
        public void NonUnfocusAwareView_DoesNotSubscribeToFocusObserver()
        {
            var (svc, _, focus) = Build();

            _ = svc.ShowPopoverAsync<StubPopoverView>();

            // StubPopoverView does NOT implement IUnfocusAware, so the service
            // must NOT register it with the focus observer.
            Assert.Equal(0, focus.SubscriptionCount);
        }

        [Fact]
        public async Task UnfocusAwareView_AutoDismissesOnUnfocus()
        {
            var (svc, host, focus) = Build();

            var task = svc.ShowPopoverAsync<StubAutoDismissPopoverView>();
            var view = (StubAutoDismissPopoverView)host.AddedViews[0];

            Assert.Equal(1, focus.SubscriptionCount);

            focus.TriggerUnfocus(view);

            Assert.False(await task);
            Assert.Equal(1, view.UnfocusCount);
            Assert.True(view.IsDisposed);
            Assert.Equal(0, focus.SubscriptionCount); // subscription disposed on teardown
        }

        [Fact]
        public async Task CompletionViaCallback_DisposesFocusSubscription()
        {
            var (svc, host, focus) = Build();

            var task = svc.ShowPopoverAsync<StubAutoDismissPopoverView>();
            var view = (StubAutoDismissPopoverView)host.AddedViews[0];

            Assert.Equal(1, focus.SubscriptionCount);

            // User-driven completion (not unfocus) must still drop the focus
            // subscription so the observer doesn't leak past the popover.
            view.CompletionCallback(true);

            Assert.True(await task);
            Assert.Equal(0, focus.SubscriptionCount);
        }

        [Fact]
        public async Task CloseAll_ResolvesAllPendingWithFalse_AndClearsFocusSubscriptions()
        {
            var (svc, host, focus) = Build();

            var t1 = svc.ShowPopoverAsync<StubAutoDismissPopoverView>();
            var t2 = svc.ShowPopoverAsync<StubAutoDismissPopoverView>();

            Assert.Equal(2, focus.SubscriptionCount);

            svc.CloseAll();

            Assert.False(await t1);
            Assert.False(await t2);
            Assert.Equal(0, focus.SubscriptionCount);
            Assert.Equal(2, host.RemovedViews.Count);
        }

        [Fact]
        public async Task CloseAll_DisposeInvokesCompletion_CancellationStillWins()
        {
            var host = new FakePageHost();
            var factory = new PageFactory();
            factory.Register(
                typeof(DisposeCompletingPopoverView),
                () => new DisposeCompletingPopoverView());
            var service = new PopoverService(host, factory);

            var task =
                service.ShowPopoverAsync<DisposeCompletingPopoverView>();
            var view = Assert.IsType<DisposeCompletingPopoverView>(
                Assert.Single(host.AddedViews));

            service.CloseAll();

            Assert.False(await task);
            Assert.True(view.IsDisposed);
            Assert.Single(host.RemovedViews);
        }

        [Fact]
        public async Task ShowPopover_OnShownFailure_RollsBackView()
        {
            var host = new FakePageHost();
            var factory = new PageFactory();
            factory.Register(typeof(ThrowingPopoverView), () => new ThrowingPopoverView());
            var svc = new PopoverService(host, factory, new FakeFocusObserverAdapter());

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => svc.ShowPopoverAsync<ThrowingPopoverView>());

            var view = Assert.IsType<ThrowingPopoverView>(Assert.Single(host.AddedViews));
            Assert.Same(view, Assert.Single(host.RemovedViews));
            Assert.True(view.IsDisposed);
        }

        [Fact]
        public async Task CloseAll_DisposeFails_CancelsAwaiterAndPropagatesCleanup()
        {
            var host = new FakePageHost();
            var factory = new PageFactory();
            factory.Register(
                typeof(ThrowingDisposePopoverView),
                () => new ThrowingDisposePopoverView());
            var service = new PopoverService(host, factory);

            var task =
                service.ShowPopoverAsync<ThrowingDisposePopoverView>();
            var view = Assert.IsType<ThrowingDisposePopoverView>(
                Assert.Single(host.AddedViews));

            var error = Assert.Throws<InvalidOperationException>(
                service.CloseAll);

            Assert.Equal("popover dispose failed", error.Message);
            Assert.False(await task);
            Assert.True(view.IsDisposed);
        }

        [Fact]
        public async Task Completion_CleanupFails_CallbackDoesNotThrowAndAwaiterFaults()
        {
            var host = new FakePageHost();
            var factory = new PageFactory();
            factory.Register(
                typeof(ThrowingDisposePopoverView),
                () => new ThrowingDisposePopoverView());
            var service = new PopoverService(host, factory);

            var task =
                service.ShowPopoverAsync<ThrowingDisposePopoverView>();
            var view = Assert.IsType<ThrowingDisposePopoverView>(
                Assert.Single(host.AddedViews));

            var callbackError = Record.Exception(
                () => view.CompletionCallback(true));
            var taskError = await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await task);

            Assert.Null(callbackError);
            Assert.Equal("popover dispose failed", taskError.Message);
            Assert.True(view.IsDisposed);
        }
    }
}
