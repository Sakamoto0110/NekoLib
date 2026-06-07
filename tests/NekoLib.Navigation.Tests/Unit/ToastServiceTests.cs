using System.Threading.Tasks;
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
    }
}
