using NekoLib.Navigation.Diagnostics;
using NekoLib.Navigation.Runtime.Factories;
using NekoLib.Navigation.Runtime.Services;
using NekoLib.Navigation.Tests.Unit.Fakes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace NekoLib.Navigation.Tests.Unit
{
    public class SurfaceDiagnosticsTests
    {
        [Fact]
        public void Toast_ReplacementAndDelayedCallback_EmitOneTerminalPerSurface()
        {
            var host = new FakePageHost();
            var factory = new PageFactory();
            factory.Register(typeof(StubToastView), () => new StubToastView());
            var service = new ToastService(
                host,
                factory,
                new SyncEventDispatcherAdapter());
            var traces = Attach(service);

            service.ShowToast<StubToastView>(
                "first payload must not be retained",
                Timeout.Infinite);
            var first = Assert.IsType<StubToastView>(host.AddedViews[0]);
            var delayedDismiss = first.DismissCallback;

            service.ShowToast<StubToastView>(
                "second payload must not be retained",
                Timeout.Infinite);
            var second = Assert.IsType<StubToastView>(host.AddedViews[1]);

            delayedDismiss();
            Assert.False(second.IsDisposed);
            Assert.Equal(5, traces.Count);

            second.DismissCallback();

            Assert.Equal(
                new[]
                {
                    NavigationTraceKind.SurfaceOpening,
                    NavigationTraceKind.SurfaceOpened,
                    NavigationTraceKind.SurfaceClosed,
                    NavigationTraceKind.SurfaceOpening,
                    NavigationTraceKind.SurfaceOpened,
                    NavigationTraceKind.SurfaceClosed
                },
                traces.Select(e => e.Kind).ToArray());

            var surfaceIds = traces
                .Select(e => e.SurfaceId)
                .Distinct()
                .ToArray();
            Assert.Equal(2, surfaceIds.Length);
            Assert.All(
                surfaceIds,
                id => Assert.Single(
                    traces.Where(e =>
                        e.SurfaceId == id &&
                        IsTerminal(e.Kind))));

            Assert.Equal(
                NavigationTraceCloseReasons.Replaced,
                traces[2].CloseReason);
            Assert.Equal(
                NavigationTraceCloseReasons.DismissedByView,
                traces[5].CloseReason);
            Assert.All(
                traces,
                e =>
                {
                    Assert.Equal(
                        NavigationTraceSurfaceKinds.Toast,
                        e.SurfaceKind);
                    Assert.Equal(1, e.SurfaceDepth);
                    Assert.Equal(
                        typeof(StubToastView).FullName,
                        e.TargetPage);
                });
        }

        [Fact]
        public async Task Dialog_StackAndRuntimeTeardown_EmitDepthAndReason()
        {
            var host = new FakePageHost();
            var factory = new PageFactory();
            factory.Register(typeof(StubDialogView), () => new StubDialogView());
            var service = new DialogService(host, factory);
            var traces = Attach(service);

            var first = service.ShowDialogAsync<StubDialogView>(
                new object());
            var second = service.ShowDialogAsync<StubDialogView>(
                new object());

            ((INavigationRuntimeTeardownAware)service).TeardownForRuntime(
                NavigationTraceCloseReasons.RuntimeTeardown);

            Assert.False(await first);
            Assert.False(await second);

            var openings = traces
                .Where(e => e.Kind == NavigationTraceKind.SurfaceOpening)
                .ToArray();
            Assert.Equal(2, openings.Length);
            Assert.Equal(1, openings[0].SurfaceDepth);
            Assert.Equal(2, openings[1].SurfaceDepth);

            var terminals = traces
                .Where(e => IsTerminal(e.Kind))
                .ToArray();
            Assert.Equal(2, terminals.Length);
            Assert.All(
                terminals,
                e => Assert.Equal(
                    NavigationTraceCloseReasons.RuntimeTeardown,
                    e.CloseReason));
        }

        [Fact]
        public async Task Dialog_PublicCloseAll_EmitsClosedByService()
        {
            var host = new FakePageHost();
            var factory = new PageFactory();
            factory.Register(typeof(StubDialogView), () => new StubDialogView());
            var service = new DialogService(host, factory);
            var traces = Attach(service);

            var task = service.ShowDialogAsync<StubDialogView>();
            service.CloseAll();

            Assert.False(await task);
            var terminal = Assert.Single(
                traces.Where(e => IsTerminal(e.Kind)));
            Assert.Equal(
                NavigationTraceCloseReasons.ClosedByService,
                terminal.CloseReason);
        }

        [Fact]
        public async Task Prompt_Completion_EmitsScalarLifecycleOnly()
        {
            var host = new FakePageHost();
            var factory = new PageFactory();
            factory.Register(typeof(StubPromptView), () => new StubPromptView());
            var service = new PromptService(host, factory);
            var traces = Attach(service);

            var task = service.ShowPromptAsync<StubPromptView, string>(
                new object());
            var prompt = Assert.IsType<StubPromptView>(
                Assert.Single(host.AddedViews));
            prompt.CompletionCallback("secret result");

            Assert.Equal("secret result", await task);
            Assert.Equal(
                new[]
                {
                    NavigationTraceKind.SurfaceOpening,
                    NavigationTraceKind.SurfaceOpened,
                    NavigationTraceKind.SurfaceClosed
                },
                traces.Select(e => e.Kind).ToArray());
            Assert.Equal(
                NavigationTraceCloseReasons.CompletedByView,
                traces[2].CloseReason);
            Assert.Equal(
                typeof(StubPromptView).FullName,
                traces[2].TargetPage);
        }

        [Fact]
        public async Task Popover_FocusLoss_EmitsFocusLossTerminal()
        {
            var host = new FakePageHost();
            var factory = new PageFactory();
            factory.Register(
                typeof(StubAutoDismissPopoverView),
                () => new StubAutoDismissPopoverView());
            var focus = new FakeFocusObserverAdapter();
            var service = new PopoverService(host, factory, focus);
            var traces = Attach(service);

            var task =
                service.ShowPopoverAsync<StubAutoDismissPopoverView>();
            var popover = Assert.IsType<StubAutoDismissPopoverView>(
                Assert.Single(host.AddedViews));

            focus.TriggerUnfocus(popover);

            Assert.False(await task);
            var terminal = Assert.Single(
                traces.Where(e => IsTerminal(e.Kind)));
            Assert.Equal(
                NavigationTraceKind.SurfaceClosed,
                terminal.Kind);
            Assert.Equal(
                NavigationTraceCloseReasons.FocusLoss,
                terminal.CloseReason);
        }

        [Fact]
        public void Toast_SetupFailure_EmitsFailedWithoutClosed()
        {
            var host = new FakePageHost();
            var factory = new PageFactory();
            factory.Register(
                typeof(ThrowingToastView),
                () => new ThrowingToastView());
            var service = new ToastService(
                host,
                factory,
                new SyncEventDispatcherAdapter());
            var traces = Attach(service);

            Assert.Throws<InvalidOperationException>(
                () => service.ShowToast<ThrowingToastView>());

            Assert.Equal(
                new[]
                {
                    NavigationTraceKind.SurfaceOpening,
                    NavigationTraceKind.SurfaceFailed
                },
                traces.Select(e => e.Kind).ToArray());
            Assert.Equal(
                NavigationTraceCloseReasons.SetupFailed,
                traces[1].CloseReason);
            Assert.Equal(
                typeof(InvalidOperationException).FullName,
                traces[1].ErrorType);
            Assert.False(traces[1].Success);
        }

        [Fact]
        public async Task Dialog_CleanupFailure_EmitsFailedWithoutClosed()
        {
            var host = new FakePageHost
            {
                RemoveViewException =
                    new InvalidOperationException("remove failed")
            };
            var factory = new PageFactory();
            factory.Register(
                typeof(StubDialogView),
                () => new StubDialogView());
            var service = new DialogService(host, factory);
            var traces = Attach(service);

            var task = service.ShowDialogAsync<StubDialogView>();

            Assert.Throws<InvalidOperationException>(
                service.CloseAll);
            Assert.False(await task);

            var terminal = Assert.Single(
                traces.Where(e => IsTerminal(e.Kind)));
            Assert.Equal(
                NavigationTraceKind.SurfaceFailed,
                terminal.Kind);
            Assert.Equal(
                NavigationTraceCloseReasons.ClosedByService,
                terminal.CloseReason);
            Assert.Equal(
                typeof(InvalidOperationException).FullName,
                terminal.ErrorType);
        }

        [Fact]
        public async Task Prompt_CompletionCleanupFailure_EmitsFailedAndFaultsAwaiter()
        {
            var host = new FakePageHost();
            var factory = new PageFactory();
            factory.Register(
                typeof(ThrowingDisposePromptView),
                () => new ThrowingDisposePromptView());
            var service = new PromptService(host, factory);
            var traces = Attach(service);

            var task = service.ShowPromptAsync<
                ThrowingDisposePromptView,
                string>();
            var prompt = Assert.IsType<ThrowingDisposePromptView>(
                Assert.Single(host.AddedViews));

            var callbackError = Record.Exception(
                () => prompt.CompletionCallback("accepted"));
            Assert.Null(callbackError);
            await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await task);

            var terminal = Assert.Single(
                traces.Where(e => IsTerminal(e.Kind)));
            Assert.Equal(
                NavigationTraceKind.SurfaceFailed,
                terminal.Kind);
            Assert.Equal(
                NavigationTraceCloseReasons.CompletedByView,
                terminal.CloseReason);
        }

        private static List<NavigationTraceEvent> Attach(
            object service)
        {
            var traces = new List<NavigationTraceEvent>();
            var hub = new NavigationEventHub();
            hub.NavigationTrace += traces.Add;
            var diagnostics = new NavigationDiagnostics(hub);
            ((INavigationDiagnosticsAware)service).AttachDiagnostics(
                diagnostics);
            return traces;
        }

        private static bool IsTerminal(NavigationTraceKind kind)
            => kind == NavigationTraceKind.SurfaceClosed ||
               kind == NavigationTraceKind.SurfaceFailed;
    }
}
