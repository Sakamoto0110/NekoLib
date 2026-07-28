using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NekoLib.Navigation.Contracts.Platform;
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

        [Fact]
        public async Task CloseAll_DisposeInvokesCompletion_CancellationStillWins()
        {
            var host = new FakePageHost();
            var factory = new PageFactory();
            factory.Register(
                typeof(DisposeCompletingDialogView),
                () => new DisposeCompletingDialogView());
            var service = new DialogService(host, factory);

            var task =
                service.ShowDialogAsync<DisposeCompletingDialogView>();
            var view = Assert.IsType<DisposeCompletingDialogView>(
                Assert.Single(host.AddedViews));

            service.CloseAll();

            Assert.False(await task);
            Assert.True(view.IsDisposed);
            Assert.Single(host.RemovedViews);
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
        public async Task ShowDialog_BlockReentersShow_AcquiresOneReference()
        {
            var host = new FakePageHost();
            var factory = new PageFactory();
            factory.Register(
                typeof(StubDialogView),
                () => new StubDialogView());
            var blocker = new ReentrantInteractionBlocker();
            DialogService service = null;
            Task<bool> nestedTask = null;
            blocker.OnBlock = () =>
                nestedTask = service.ShowDialogAsync<StubDialogView>();
            service = new DialogService(host, factory, blocker);

            var outerTask =
                service.ShowDialogAsync<StubDialogView>();

            Assert.NotNull(nestedTask);
            Assert.Equal(1, blocker.BlockCalls);
            Assert.Equal(1, blocker.Depth);
            Assert.Equal(2, host.AddedViews.Count);

            var nested = Assert.IsType<StubDialogView>(
                host.AddedViews[0]);
            var outer = Assert.IsType<StubDialogView>(
                host.AddedViews[1]);
            nested.CompletionCallback(true);
            outer.CompletionCallback(false);

            Assert.True(await nestedTask);
            Assert.False(await outerTask);
            Assert.Equal(1, blocker.UnblockCalls);
            Assert.Equal(0, blocker.Depth);
        }

        [Fact]
        public async Task ShowDialog_BlockThrows_DoesNotReleaseUnacquiredReference()
        {
            var host = new FakePageHost();
            var factory = new PageFactory();
            factory.Register(
                typeof(StubDialogView),
                () => new StubDialogView());
            var blocker = new ReentrantInteractionBlocker
            {
                ThrowOnBlock = true
            };
            var service = new DialogService(host, factory, blocker);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.ShowDialogAsync<StubDialogView>());

            Assert.Equal(1, blocker.BlockCalls);
            Assert.Equal(0, blocker.UnblockCalls);
            Assert.Equal(0, blocker.Depth);
        }

        [Fact]
        public async Task TwoStackedDialogs_PageAwareBlockerTracksModalStack()
        {
            var host = new FakePageHost();
            var factory = new PageFactory();
            factory.Register(
                typeof(StubDialogView),
                () => new StubDialogView());
            var blocker = new CountingInteractionBlocker();
            var service = new DialogService(host, factory, blocker);

            var firstTask =
                service.ShowDialogAsync<StubDialogView>();
            var secondTask =
                service.ShowDialogAsync<StubDialogView>();
            var first = Assert.IsType<StubDialogView>(
                host.AddedViews[0]);
            var second = Assert.IsType<StubDialogView>(
                host.AddedViews[1]);

            Assert.Equal(
                new object[] { first.NativeView, second.NativeView },
                blocker.ModalViews.ToArray());

            second.CompletionCallback(true);
            first.CompletionCallback(false);

            Assert.True(await secondTask);
            Assert.False(await firstTask);
            Assert.Equal(
                new object[] { second.NativeView, first.NativeView },
                blocker.RemovedViews.ToArray());
        }

        [Fact]
        public void CloseAll_WhenNoDialogs_IsNoOp()
        {
            var (svc, host) = Build();

            svc.CloseAll();

            Assert.Empty(host.AddedViews);
            Assert.Empty(host.RemovedViews);
        }

        [Fact]
        public async Task ShowDialog_OnShownFailure_RollsBackViewAndBlocker()
        {
            var host = new FakePageHost();
            var factory = new PageFactory();
            factory.Register(typeof(ThrowingDialogView), () => new ThrowingDialogView());
            var blocker = new CountingInteractionBlocker();
            var svc = new DialogService(host, factory, blocker);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => svc.ShowDialogAsync<ThrowingDialogView>());

            var view = Assert.IsType<ThrowingDialogView>(Assert.Single(host.AddedViews));
            Assert.Same(view, Assert.Single(host.RemovedViews));
            Assert.True(view.IsDisposed);
            Assert.Equal(0, blocker.Depth);
            Assert.Equal(1, blocker.BlockCalls);
            Assert.Equal(1, blocker.UnblockCalls);
        }

        [Fact]
        public async Task ShowDialog_AddViewFailure_DisposesViewAndReleasesBlocker()
        {
            var host = new FakePageHost
            {
                AddViewException = new InvalidOperationException("add failed")
            };
            var factory = new PageFactory();
            StubDialogView created = null;
            factory.Register(
                typeof(StubDialogView),
                () => created = new StubDialogView());
            var blocker = new CountingInteractionBlocker();
            var svc = new DialogService(host, factory, blocker);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => svc.ShowDialogAsync<StubDialogView>());

            Assert.NotNull(created);
            Assert.True(created.IsDisposed);
            Assert.Same(created, Assert.Single(host.RemovedViews));
            Assert.Equal(0, blocker.Depth);
        }

        [Fact]
        public async Task CloseAll_CleanupFailures_RunBestEffortAndThrowFirst()
        {
            var host = new FakePageHost
            {
                RemoveViewException =
                    new InvalidOperationException("remove failed")
            };
            var factory = new PageFactory();
            factory.Register(
                typeof(ThrowingDisposeDialogView),
                () => new ThrowingDisposeDialogView());
            var blocker = new CountingInteractionBlocker
            {
                ThrowOnUnblock = true
            };
            var service = new DialogService(host, factory, blocker);

            var task =
                service.ShowDialogAsync<ThrowingDisposeDialogView>();
            var view = Assert.IsType<ThrowingDisposeDialogView>(
                Assert.Single(host.AddedViews));

            var error = Assert.Throws<InvalidOperationException>(
                service.CloseAll);

            Assert.Equal("remove failed", error.Message);
            Assert.False(await task);
            Assert.True(view.IsDisposed);
            Assert.Equal(1, blocker.UnblockCalls);
        }

        [Fact]
        public async Task Completion_CleanupFails_CallbackDoesNotThrowAndAwaiterFaults()
        {
            var host = new FakePageHost();
            var factory = new PageFactory();
            factory.Register(
                typeof(ThrowingDisposeDialogView),
                () => new ThrowingDisposeDialogView());
            var service = new DialogService(host, factory);

            var task =
                service.ShowDialogAsync<ThrowingDisposeDialogView>();
            var view = Assert.IsType<ThrowingDisposeDialogView>(
                Assert.Single(host.AddedViews));

            var callbackError = Record.Exception(
                () => view.CompletionCallback(true));
            var taskError = await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await task);

            Assert.Null(callbackError);
            Assert.Equal("dialog dispose failed", taskError.Message);
            Assert.True(view.IsDisposed);
        }

        internal sealed class CountingInteractionBlocker :
            IPageAwareInteractionBlocker
        {
            public int Depth { get; private set; }
            public int BlockCalls { get; private set; }
            public int UnblockCalls { get; private set; }
            public bool ThrowOnUnblock { get; set; }
            public List<object> ModalViews { get; } =
                new List<object>();
            public List<object> BackgroundViews { get; } =
                new List<object>();
            public List<object> RemovedViews { get; } =
                new List<object>();

            public void Block()
            {
                BlockCalls++;
                Depth++;
            }

            public void Unblock()
            {
                UnblockCalls++;
                if (Depth > 0)
                    Depth--;
                if (ThrowOnUnblock)
                    throw new InvalidOperationException("unblock failed");
            }

            public void OnViewAdded(
                object view,
                bool isModalSurface)
            {
                if (isModalSurface)
                    ModalViews.Add(view);
                else
                    BackgroundViews.Add(view);
            }

            public void OnViewRemoved(object view)
                => RemovedViews.Add(view);
        }

        internal sealed class ReentrantInteractionBlocker :
            IInteractionBlocker
        {
            public int Depth { get; private set; }
            public int BlockCalls { get; private set; }
            public int UnblockCalls { get; private set; }
            public bool ThrowOnBlock { get; set; }
            public Action OnBlock { get; set; }

            public void Block()
            {
                BlockCalls++;
                var callback = OnBlock;
                OnBlock = null;
                callback?.Invoke();

                if (ThrowOnBlock)
                    throw new InvalidOperationException("block failed");

                Depth++;
            }

            public void Unblock()
            {
                UnblockCalls++;
                if (Depth > 0)
                    Depth--;
            }
        }
    }
}
