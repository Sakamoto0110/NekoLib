using System.Threading.Tasks;
using NekoLib.Navigation.Runtime.Factories;
using NekoLib.Navigation.Runtime.Services;
using NekoLib.Navigation.Tests.Unit.Fakes;
using Xunit;

namespace NekoLib.Navigation.Tests.Unit
{
    public class DialogServiceTests
    {
        private static (DialogService svc, FakePageHost host) Build()
        {
            var host = new FakePageHost();
            var factory = new PageFactory();
            factory.Register(typeof(StubDialogView), () => new StubDialogView());
            return (new DialogService(host, factory, interactionBlocker: null), host);
        }

        [Fact]
        public async Task ShowDialog_AttachesViewAndResolvesWithCompletion()
        {
            var (svc, host) = Build();

            var task = svc.ShowDialogAsync<StubDialogView>("payload");

            // OnShownAsync ran inline (it's just Task.CompletedTask in the stub);
            // the view should be attached and its completion callback bound.
            var view = (StubDialogView)host.AddedViews[0];
            Assert.Equal("payload", view.LastShownPayload);
            Assert.NotNull(view.CompletionCallback);

            view.CompletionCallback(true);

            Assert.True(await task);
            Assert.True(view.IsDisposed);
            Assert.Single(host.RemovedViews);
        }

        [Fact]
        public async Task CompletionWithFalse_ResolvesTaskWithFalse()
        {
            var (svc, host) = Build();
            var task = svc.ShowDialogAsync<StubDialogView>();
            var view = (StubDialogView)host.AddedViews[0];

            view.CompletionCallback(false);

            Assert.False(await task);
        }

        /// <summary>
        /// N-3: Two concurrent dialogs. CloseAll must resolve both pending TCSs
        /// with false, dispose both views, and remove both from the host —
        /// regardless of order, with no hang on the awaiters.
        /// </summary>
        [Fact]
        public async Task CloseAll_WithTwoPendingDialogs_ResolvesBothFalse()
        {
            var (svc, host) = Build();

            var t1 = svc.ShowDialogAsync<StubDialogView>();
            var t2 = svc.ShowDialogAsync<StubDialogView>();

            Assert.Equal(2, host.AddedViews.Count);
            Assert.False(t1.IsCompleted);
            Assert.False(t2.IsCompleted);

            svc.CloseAll();

            Assert.False(await t1);
            Assert.False(await t2);

            // Both dialogs disposed + removed.
            Assert.True(((StubDialogView)host.AddedViews[0]).IsDisposed);
            Assert.True(((StubDialogView)host.AddedViews[1]).IsDisposed);
            Assert.Equal(2, host.RemovedViews.Count);
        }

        /// <summary>
        /// N-3 sequel: open two dialogs, close the SECOND one first via its
        /// completion callback, then the first. Both awaiters resolve cleanly
        /// (no leak, no hang, no order-dependent deadlock).
        /// </summary>
        [Fact]
        public async Task TwoStackedDialogs_CompleteSecondFirst_ResolvesBoth()
        {
            var (svc, host) = Build();
            var t1 = svc.ShowDialogAsync<StubDialogView>();
            var t2 = svc.ShowDialogAsync<StubDialogView>();

            var view1 = (StubDialogView)host.AddedViews[0];
            var view2 = (StubDialogView)host.AddedViews[1];

            view2.CompletionCallback(true);
            view1.CompletionCallback(false);

            Assert.True(await t2);
            Assert.False(await t1);
        }

        [Fact]
        public void CloseAll_WhenNoDialogs_IsNoOp()
        {
            var (svc, host) = Build();

            svc.CloseAll();

            Assert.Empty(host.AddedViews);
            Assert.Empty(host.RemovedViews);
        }
    }
}
