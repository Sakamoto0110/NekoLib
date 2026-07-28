using System;
using System.Threading;
using System.Threading.Tasks;
using NekoLib.Navigation.Contracts.Platform;
using NekoLib.Navigation.Runtime.Factories;
using NekoLib.Navigation.Runtime.Services;
using NekoLib.Navigation.Tests.Unit.Fakes;
using Xunit;

namespace NekoLib.Navigation.Tests.Unit
{
    /// <summary>
    /// Direct tests against <see cref="ToastService"/>. Public API + small
    /// dependency surface (IViewHost, PageFactory, IEventDispatcherAdapter) means
    /// no runtime fixture is needed; each test instantiates its own service.
    /// </summary>
    public class ToastServiceTests
    {
        private static (ToastService svc, FakePageHost host) Build()
        {
            var host = new FakePageHost();
            var factory = new PageFactory();
            factory.Register(typeof(StubToastView), () => new StubToastView());
            var dispatcher = new SyncEventDispatcherAdapter();
            return (new ToastService(host, factory, dispatcher), host);
        }

        [Fact]
        public void ShowToast_AttachesViewAndPassesPayload()
        {
            var (svc, host) = Build();

            svc.ShowToast<StubToastView>("hello");

            Assert.Single(host.AddedViews);
            var view = Assert.IsType<StubToastView>(host.AddedViews[0]);
            Assert.Equal("hello", view.LastShownPayload);
        }

        [Fact]
        public void ShowToast_PageAwareBlockerTracksItAsBackgroundOverlay()
        {
            var host = new FakePageHost();
            var factory = new PageFactory();
            factory.Register(
                typeof(StubToastView),
                () => new StubToastView());
            var blocker =
                new DialogServiceTests.CountingInteractionBlocker();
            var service = new ToastService(
                host,
                factory,
                new SyncEventDispatcherAdapter());
            ((INavigationInteractionBlockerAware)service)
                .AttachInteractionBlocker(blocker);

            service.ShowToast<StubToastView>(
                durationMs: Timeout.Infinite);
            var view = Assert.IsType<StubToastView>(
                Assert.Single(host.AddedViews));

            Assert.Same(
                view.NativeView,
                Assert.Single(blocker.BackgroundViews));

            service.DismissCurrentToast();
            Assert.Same(
                view.NativeView,
                Assert.Single(blocker.RemovedViews));
        }

        [Fact]
        public void DismissCurrentToast_RemovesAndDisposesView()
        {
            var (svc, host) = Build();
            svc.ShowToast<StubToastView>("x");
            var view = (StubToastView)host.AddedViews[0];

            svc.DismissCurrentToast();

            Assert.Single(host.RemovedViews);
            Assert.True(view.IsDisposed);
        }

        [Fact]
        public void ShowToast_Supersession_RemovesPriorAndAddsNew()
        {
            var (svc, host) = Build();

            svc.ShowToast<StubToastView>("first");
            var first = (StubToastView)host.AddedViews[0];
            svc.ShowToast<StubToastView>("second");

            Assert.Equal(2, host.AddedViews.Count);
            Assert.True(first.IsDisposed);
            Assert.Single(host.RemovedViews);
            Assert.Same(first, host.RemovedViews[0]);
        }

        /// <summary>
        /// NEW-8 regression. The original `Task.Delay(ms, token)` pattern threw
        /// <c>TaskCanceledException</c> on every superseded toast. The fix uses a
        /// non-throwing cancel pattern. Synchronously firing 5 ShowToast calls in
        /// a row simulates the rapid-fire scenario; no exception should escape.
        /// </summary>
        [Fact]
        public void RapidFireSupersession_DoesNotThrow()
        {
            var (svc, _) = Build();

            for (int i = 0; i < 5; i++)
                svc.ShowToast<StubToastView>("msg " + i);

            // No exception means the supersession + cancellation path didn't blow up.
            // Also exercises that the dispatcher's BeginInvoke (sync here) is
            // tolerant of the rapid sequence.
            svc.DismissCurrentToast();
        }

        [Fact]
        public void DismissCurrentToast_WhenNoToast_IsNoOp()
        {
            var (svc, host) = Build();

            svc.DismissCurrentToast();

            Assert.Empty(host.AddedViews);
            Assert.Empty(host.RemovedViews);
        }

        [Fact]
        public void BindDismiss_CallbackInvocation_DismissesToast()
        {
            var (svc, host) = Build();
            svc.ShowToast<StubToastView>("x");
            var view = (StubToastView)host.AddedViews[0];

            // Simulate the user clicking the toast: BindDismiss callback fires.
            view.DismissCallback?.Invoke();

            Assert.True(view.IsDisposed);
            Assert.Single(host.RemovedViews);
        }

        [Fact]
        public void ShowToast_OnShownFailure_RollsBackViewAndDisposesIt()
        {
            var host = new FakePageHost();
            var factory = new PageFactory();
            factory.Register(typeof(ThrowingToastView), () => new ThrowingToastView());
            var svc = new ToastService(host, factory, new SyncEventDispatcherAdapter());

            Assert.Throws<InvalidOperationException>(
                () => svc.ShowToast<ThrowingToastView>());

            var view = Assert.IsType<ThrowingToastView>(Assert.Single(host.AddedViews));
            Assert.Same(view, Assert.Single(host.RemovedViews));
            Assert.True(view.IsDisposed);

            // The failed toast is no longer retained as the current one.
            svc.DismissCurrentToast();
            Assert.Single(host.RemovedViews);
        }

        [Fact]
        public void ReplacedToast_DelayedDismissCallback_DoesNotDismissCurrentToast()
        {
            var (svc, host) = Build();
            svc.ShowToast<StubToastView>("first", Timeout.Infinite);
            var first = Assert.IsType<StubToastView>(host.AddedViews[0]);
            var delayedDismiss = first.DismissCallback;

            svc.ShowToast<StubToastView>("second", Timeout.Infinite);
            var second = Assert.IsType<StubToastView>(host.AddedViews[1]);

            delayedDismiss();

            Assert.True(first.IsDisposed);
            Assert.False(second.IsDisposed);
            Assert.Single(host.RemovedViews);

            svc.DismissCurrentToast();
        }

        [Fact]
        public async Task OnShown_SynchronousDismiss_DoesNotStartTimer()
        {
            var host = new FakePageHost();
            var factory = new PageFactory();
            factory.Register(
                typeof(SelfDismissingToastView),
                () => new SelfDismissingToastView());
            var dispatcher = new CountingEventDispatcherAdapter();
            var svc = new ToastService(host, factory, dispatcher);

            var error = Record.Exception(
                () => svc.ShowToast<SelfDismissingToastView>(
                    durationMs: 0));

            Assert.Null(error);
            var view = Assert.IsType<SelfDismissingToastView>(
                Assert.Single(host.AddedViews));
            Assert.True(view.IsDisposed);
            Assert.Single(host.RemovedViews);

            await Task.Delay(50);
            Assert.Equal(0, dispatcher.BeginInvokeCalls);
        }

        [Fact]
        public void ShowToast_DurationBelowInfinite_ThrowsSynchronously()
        {
            var (svc, host) = Build();

            Assert.Throws<ArgumentOutOfRangeException>(
                () => svc.ShowToast<StubToastView>(
                    durationMs: Timeout.Infinite - 1));
            Assert.Empty(host.AddedViews);
        }

        [Fact]
        public async Task ShowToast_InfiniteDuration_RemainsUntilExplicitDismiss()
        {
            var (svc, host) = Build();

            svc.ShowToast<StubToastView>(
                durationMs: Timeout.Infinite);
            var view = Assert.IsType<StubToastView>(
                Assert.Single(host.AddedViews));

            await Task.Delay(30);
            Assert.False(view.IsDisposed);

            svc.DismissCurrentToast();
            Assert.True(view.IsDisposed);
        }

        [Fact]
        public async Task ShowToast_ZeroDuration_AutoDismisses()
        {
            var (svc, host) = Build();

            svc.ShowToast<StubToastView>(durationMs: 0);
            var view = Assert.IsType<StubToastView>(
                Assert.Single(host.AddedViews));

            for (int i = 0; i < 100 && !view.IsDisposed; i++)
                await Task.Delay(10);

            Assert.True(view.IsDisposed);
            Assert.Single(host.RemovedViews);
        }

        [Fact]
        public void DismissCurrentToast_CleanupFails_PropagatesFirstAfterDispose()
        {
            var host = new FakePageHost
            {
                RemoveViewException =
                    new InvalidOperationException("remove failed")
            };
            var factory = new PageFactory();
            factory.Register(
                typeof(ThrowingDisposeToastView),
                () => new ThrowingDisposeToastView());
            var service = new ToastService(
                host,
                factory,
                new SyncEventDispatcherAdapter());

            service.ShowToast<ThrowingDisposeToastView>(
                durationMs: Timeout.Infinite);
            var view = Assert.IsType<ThrowingDisposeToastView>(
                Assert.Single(host.AddedViews));

            var error = Assert.Throws<InvalidOperationException>(
                service.DismissCurrentToast);

            Assert.Equal("remove failed", error.Message);
            Assert.True(view.IsDisposed);
            service.DismissCurrentToast();
        }

        [Fact]
        public void DismissCallback_CleanupFails_DoesNotThrow()
        {
            var host = new FakePageHost();
            var factory = new PageFactory();
            factory.Register(
                typeof(ThrowingDisposeToastView),
                () => new ThrowingDisposeToastView());
            var service = new ToastService(
                host,
                factory,
                new SyncEventDispatcherAdapter());

            service.ShowToast<ThrowingDisposeToastView>(
                durationMs: Timeout.Infinite);
            var view = Assert.IsType<ThrowingDisposeToastView>(
                Assert.Single(host.AddedViews));

            var callbackError = Record.Exception(
                () => view.DismissCallback());

            Assert.Null(callbackError);
            Assert.True(view.IsDisposed);
            Assert.Single(host.RemovedViews);
        }

        private sealed class CountingEventDispatcherAdapter :
            IEventDispatcherAdapter
        {
            private int _beginInvokeCalls;

            public int BeginInvokeCalls =>
                Volatile.Read(ref _beginInvokeCalls);

            public void Invoke(Action action) => action();

            public void BeginInvoke(Action action)
            {
                Interlocked.Increment(ref _beginInvokeCalls);
                action();
            }
        }
    }
}
