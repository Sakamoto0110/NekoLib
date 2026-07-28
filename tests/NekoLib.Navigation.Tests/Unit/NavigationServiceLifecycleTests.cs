using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NekoLib.Navigation.Contracts.Platform;
using NekoLib.Navigation.Contracts.Runtime;
using NekoLib.Navigation.Diagnostics;
using NekoLib.Navigation.Runtime.Core;
using NekoLib.Navigation.Runtime.Factories;
using NekoLib.Navigation.Runtime.Registry;
using NekoLib.Navigation.Runtime.Services;
using NekoLib.Navigation.Tests.Unit.Fakes;
using Xunit;

namespace NekoLib.Navigation.Tests.Unit
{
    [Collection("NavigationServiceFacade")]
    public sealed class NavigationServiceLifecycleTests
    {
        [Fact]
        public async Task Shutdown_ConcurrentCalls_ShareTeardownAndAllowRemountAfterCompletion()
        {
            await NavigationService.Shutdown();
            var mounted = RuntimeTestFixture.Build<StubIdle>(
                typeof(StubBlockingShutdownPage));
            var replacement = RuntimeTestFixture.Build<StubIdle>(
                typeof(StubA));
            StubBlockingShutdownPage page = null;

            try
            {
                NavigationService.UseContext(mounted.Context);
                await NavigationService.SwitchPage<StubBlockingShutdownPage>();
                page = Assert.IsType<StubBlockingShutdownPage>(
                    NavigationService.Current);

                var firstShutdown = NavigationService.Shutdown();
                await AwaitSignal(page.LeaveStarted);
                var concurrentShutdown = NavigationService.Shutdown();

                Assert.Same(firstShutdown, concurrentShutdown);
                Assert.Throws<InvalidOperationException>(
                    () => NavigationService.UseContext(replacement.Context));

                page.ReleaseLeave();
                await Task.WhenAll(firstShutdown, concurrentShutdown);

                Assert.Equal(1, page.OnNavigatedFromCount);
                Assert.Same(page, Assert.Single(mounted.Host.Detached));
                Assert.True(page.IsDisposed);

                NavigationService.UseContext(replacement.Context);
                await NavigationService.SwitchPage<StubA>();
                Assert.IsType<StubA>(NavigationService.Current);
            }
            finally
            {
                page?.ReleaseLeave();
                await NavigationService.Shutdown();
            }
        }

        [Fact]
        public async Task Shutdown_BlockedBackgroundLoad_DiscardsAndRemovesMaskBeforeCompletion()
        {
            await NavigationService.Shutdown();
            var fixture = RuntimeTestFixture.Build<StubIdle>(
                typeof(StubControllableBackground),
                typeof(StubLoadingMask));
            var traces = new ConcurrentQueue<NavigationTraceEvent>();
            var discarded = new TaskCompletionSource<NavigationTraceEvent>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            StubControllableBackground page = null;

            fixture.Context.Events.NavigationTrace += trace =>
            {
                traces.Enqueue(trace);
                if (trace.Kind == NavigationTraceKind.BackgroundLoadDiscarded)
                    discarded.TrySetResult(trace);
            };

            try
            {
                NavigationService.UseContext(fixture.Context);
                await NavigationService.SwitchPage<StubControllableBackground>();
                page = Assert.IsType<StubControllableBackground>(
                    NavigationService.Current);
                await AwaitSignal(page.Started);
                var mask = Assert.Single(
                    fixture.CreatedPages.OfType<StubLoadingMask>());

                var shutdown = NavigationService.Shutdown();
                await AwaitSignal(shutdown);

                Assert.True(discarded.Task.IsCompleted);
                var terminal = await discarded.Task;
                Assert.Equal(
                    NavigationTraceCloseReasons.RuntimeTeardown,
                    terminal.Decision);
                Assert.Equal(0, terminal.BackgroundLoadCount);
                Assert.Equal(1, mask.CloseCount);
                Assert.Contains(mask.NativeView, fixture.Host.RemovedViews);
                Assert.True(mask.IsDisposed);
                Assert.True(page.IsDisposed);
                Assert.Equal(0, page.ApplyCount);
                Assert.Null(NavigationService.Current);

                page.FailLoad(new InvalidOperationException("late failure"));
                await Task.Yield();
                Assert.Single(traces.Where(trace =>
                    trace.Kind == NavigationTraceKind.BackgroundLoadDiscarded));
            }
            finally
            {
                page?.CompleteLoad();
                await NavigationService.Shutdown();
            }
        }

        [Fact]
        public async Task Shutdown_OperationAdmittedBeforeCutoff_DrainsBeforeRuntimeDisposal()
        {
            await NavigationService.Shutdown();
            using (var dispatcher = new BlockingFirstBeginInvokeDispatcher())
            {
                var fixture = RuntimeTestFixture.BuildWithDispatcher<StubIdle>(
                    dispatcher,
                    typeof(StubA));

                try
                {
                    NavigationService.UseContext(fixture.Context);

                    var navigation = Task.Run(
                        () => NavigationService.SwitchPage<StubA>());
                    await AwaitSignal(dispatcher.FirstBeginInvokeEntered);

                    var shutdown = NavigationService.Shutdown();

                    Assert.False(shutdown.IsCompleted);
                    Assert.Equal(1, dispatcher.BeginInvokeCount);
                    Assert.Throws<InvalidOperationException>(
                        () =>
                        {
                            _ = NavigationService.SwitchPage<StubA>();
                        });

                    dispatcher.ReleaseFirst();
                    await AwaitSignal(dispatcher.FirstBeginInvokeQueued);
                    dispatcher.RunNext();
                    await AwaitSignal(navigation);

                    var page = Assert.IsType<StubA>(NavigationService.Current);
                    await AwaitSignal(dispatcher.SecondBeginInvokeQueued);

                    Assert.False(shutdown.IsCompleted);
                    Assert.Equal(2, dispatcher.BeginInvokeCount);

                    dispatcher.RunNext();
                    await AwaitSignal(shutdown);

                    Assert.True(page.IsDisposed);
                    Assert.Null(NavigationService.Current);
                }
                finally
                {
                    dispatcher.ReleaseFirst();
                    await PumpDispatcherUntilComplete(
                        dispatcher,
                        NavigationService.Shutdown());
                }
            }
        }

        [Fact]
        public async Task Shutdown_QueuedToastAdmission_WaitsUntilUiMutationBeforeTeardown()
        {
            await NavigationService.Shutdown();
            using (var dispatcher = new BlockingFirstBeginInvokeDispatcher())
            {
                var host = new FakePageHost();
                var factory = new PageFactory();
                factory.Register(
                    typeof(StubToastView),
                    () => new StubToastView());
                var services = new ServiceLocator();
                services.Register<IEventDispatcherAdapter>(dispatcher);
                services.Register<PageFactory>(factory);
                services.Register<IToastService>(
                    new ToastService(host, factory, dispatcher));
                services.Lock();
                var context = new NavigationContext(
                    host,
                    services,
                    PageRegistry.Create(_ => { }),
                    new FakePlatformAdapter());

                try
                {
                    NavigationService.UseContext(context);
                    var showCall = Task.Run(() =>
                        NavigationService.ShowToast<StubToastView>(
                            durationMs: Timeout.Infinite));
                    await AwaitSignal(dispatcher.FirstBeginInvokeEntered);

                    var shutdown = NavigationService.Shutdown();

                    Assert.False(shutdown.IsCompleted);
                    Assert.Empty(host.AddedViews);

                    dispatcher.ReleaseFirst();
                    await AwaitSignal(dispatcher.FirstBeginInvokeQueued);
                    await AwaitSignal(showCall);
                    dispatcher.RunNext();

                    var toast = Assert.IsType<StubToastView>(
                        Assert.Single(host.AddedViews));
                    await AwaitSignal(dispatcher.SecondBeginInvokeQueued);
                    Assert.False(shutdown.IsCompleted);

                    dispatcher.RunNext();
                    await AwaitSignal(shutdown);

                    Assert.True(toast.IsDisposed);
                    Assert.Same(
                        toast.NativeView,
                        Assert.Single(host.RemovedViews));
                }
                finally
                {
                    dispatcher.ReleaseFirst();
                    await PumpDispatcherUntilComplete(
                        dispatcher,
                        NavigationService.Shutdown());
                }
            }
        }

        [Fact]
        public async Task Shutdown_QueuedDialogAdmission_RegistersThenRuntimeTeardownCompletesAwaiter()
        {
            await NavigationService.Shutdown();
            using (var dispatcher = new BlockingFirstBeginInvokeDispatcher())
            {
                var host = new FakePageHost();
                var factory = new PageFactory();
                factory.Register(
                    typeof(StubDialogView),
                    () => new StubDialogView());
                var services = new ServiceLocator();
                services.Register<IEventDispatcherAdapter>(dispatcher);
                services.Register<PageFactory>(factory);
                services.Register<IDialogService>(
                    new DialogService(host, factory));
                services.Lock();
                var context = new NavigationContext(
                    host,
                    services,
                    PageRegistry.Create(_ => { }),
                    new FakePlatformAdapter());

                try
                {
                    NavigationService.UseContext(context);
                    var invocation = Task.Factory.StartNew(
                        () => NavigationService
                            .ShowDialogAsync<StubDialogView>(),
                        CancellationToken.None,
                        TaskCreationOptions.DenyChildAttach,
                        TaskScheduler.Default);
                    await AwaitSignal(dispatcher.FirstBeginInvokeEntered);

                    var shutdown = NavigationService.Shutdown();

                    Assert.False(shutdown.IsCompleted);
                    Assert.Empty(host.AddedViews);

                    dispatcher.ReleaseFirst();
                    await AwaitSignal(dispatcher.FirstBeginInvokeQueued);
                    var dialogTask = await invocation;
                    dispatcher.RunNext();

                    var dialog = Assert.IsType<StubDialogView>(
                        Assert.Single(host.AddedViews));
                    await AwaitSignal(dispatcher.SecondBeginInvokeQueued);
                    Assert.False(shutdown.IsCompleted);
                    Assert.False(dialogTask.IsCompleted);

                    dispatcher.RunNext();
                    await AwaitSignal(shutdown);

                    Assert.False(await dialogTask);
                    Assert.True(dialog.IsDisposed);
                    Assert.Same(
                        dialog.NativeView,
                        Assert.Single(host.RemovedViews));
                }
                finally
                {
                    dispatcher.ReleaseFirst();
                    await PumpDispatcherUntilComplete(
                        dispatcher,
                        NavigationService.Shutdown());
                }
            }
        }

        private static async Task AwaitSignal(Task signal)
        {
            var completed = await Task.WhenAny(signal, Task.Delay(5000));
            Assert.Same(signal, completed);
            await signal;
        }

        private static async Task PumpDispatcherUntilComplete(
            BlockingFirstBeginInvokeDispatcher dispatcher,
            Task task)
        {
            var timeout = Task.Delay(5000);
            while (!task.IsCompleted && !timeout.IsCompleted)
            {
                dispatcher.RunAll();
                await Task.Delay(1);
            }

            dispatcher.RunAll();
            Assert.True(task.IsCompleted);
            await task;
        }
    }
}
