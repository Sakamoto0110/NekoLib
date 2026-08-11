#nullable enable
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using NekoLib.Inspection;
using NekoLib.Navigation;
using NekoLib.Navigation.Bootstrap;
using NekoLib.Navigation.Contracts.Pages;
using NekoLib.Navigation.Contracts.Platform;
using NekoLib.Navigation.Contracts.Runtime;
using NekoLib.Navigation.Runtime.Core;
using NekoLib.Navigation.Wpf.Adapters;
using NekoLib.Navigation.Wpf.Hosting;

namespace NekoLib.Navigation.RuntimeTests.LongRunningRecovery.Wpf
{
    internal sealed class WpfScenarioPlatform : IScenarioPlatform
    {
        private readonly Panel _root;

        public WpfScenarioPlatform(Panel root)
        {
            _root = root ?? throw new ArgumentNullException(nameof(root));
            Controls = new ScenarioPlatformControls();
            Pages = BuildCatalog();
        }

        public string PlatformId => "wpf";
        public string DisplayName => "WPF native host";
        public Type AdapterMarkerType => typeof(WpfPlatformAdapter);
        public ScenarioPageCatalog Pages { get; }
        public ScenarioPlatformControls Controls { get; }
        public int NativeChildCount => _root.Children.Count;

        public NavigationContext Start(
            ScenarioState state,
            InspectionRuntime inspection,
            ScenarioOptions options)
        {
            ScenarioStateSlot.Install(state);
            ScenarioWpfAdapter.Controls = Controls;
            return PageNavBootstrap
                .Use<ScenarioWpfAdapter>(_root)
                .UseInspection(inspection)
                .UseIdleTimeout(options.IdleTimeoutMilliseconds)
                .ConfigurePages(builder => ScenarioPageRegistration.Register(builder, Pages))
                .Start();
        }

        public Task<bool> ShowDialogAsync(SurfaceDirective directive) =>
            NavigationService.ShowDialogAsync<ScenarioDialog>(directive);
        public Task<string?> ShowPromptAsync(SurfaceDirective directive) =>
            NavigationService.ShowPromptAsync<ScenarioPrompt, string?>(directive);
        public Task<bool> ShowPopoverAsync(SurfaceDirective directive) =>
            NavigationService.ShowPopoverAsync<ScenarioPopover>(directive);
        public void ShowToast(SurfaceDirective directive, int durationMilliseconds) =>
            NavigationService.ShowToast<ScenarioToast>(directive, durationMilliseconds);

        public async Task<Exception?> ShowBindingFailureAsync()
        {
            try
            {
                await NavigationService.ShowDialogAsync<BindingFailureDialog>();
                return null;
            }
            catch (Exception ex) { return ex; }
        }

        public Task YieldUiAsync()
        {
            TaskCompletionSource<bool> source = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            _root.Dispatcher.BeginInvoke(new Action(() => source.TrySetResult(true)));
            return source.Task;
        }

        public void ReleaseScenarioGlobals() => ScenarioWpfAdapter.Controls = null;

        private static ScenarioPageCatalog BuildCatalog()
        {
            Type[] all =
            {
                typeof(IdlePage), typeof(TransientPage), typeof(StrongPage), typeof(WeakPage),
                typeof(KeepAttachedPage), typeof(LoadBeforePage), typeof(ShowImmediatelyPage),
                typeof(BackgroundPage), typeof(AuthenticatedPage), typeof(RolePage),
                typeof(PermissionPage), typeof(RedirectToIdlePage), typeof(FaultPage),
                typeof(GuardTimeoutPage), typeof(GuardThrowPage), typeof(CycleAPage),
                typeof(CycleBPage), typeof(Depth1Page), typeof(Depth2Page), typeof(Depth3Page),
                typeof(Depth4Page), typeof(Depth5Page), typeof(Depth6Page), typeof(Depth7Page),
                typeof(Depth8Page), typeof(Depth9Page), typeof(Depth10Page)
            };

            return new ScenarioPageCatalog
            {
                Idle = typeof(IdlePage),
                ControlledIdle = typeof(IdlePage),
                Transient = typeof(TransientPage),
                Strong = typeof(StrongPage),
                Weak = typeof(WeakPage),
                KeepAttached = typeof(KeepAttachedPage),
                LoadBefore = typeof(LoadBeforePage),
                ShowImmediately = typeof(ShowImmediatelyPage),
                Background = typeof(BackgroundPage),
                Authenticated = typeof(AuthenticatedPage),
                Role = typeof(RolePage),
                Permission = typeof(PermissionPage),
                RedirectToIdle = typeof(RedirectToIdlePage),
                Fault = typeof(FaultPage),
                GuardTimeout = typeof(GuardTimeoutPage),
                GuardThrow = typeof(GuardThrowPage),
                CycleA = typeof(CycleAPage),
                DepthStart = typeof(Depth1Page),
                All = all
            };
        }
    }

    public sealed class ScenarioWpfAdapter : IPlatformAdapter
    {
        internal static ScenarioPlatformControls? Controls;
        private readonly WpfPlatformAdapter _inner = new WpfPlatformAdapter();

        private static ScenarioPlatformControls Active => Controls ??
            throw new InvalidOperationException("Scenario platform controls are not installed.");

        public bool CanHandle(object host) => _inner.CanHandle(host);
        public IPageHost CreateHost(object host) => new ScenarioWpfHost((Panel)host, Active);
        public IEventDispatcherAdapter CreateEventDispatcher(object host) =>
            Active.WrapDispatcher(_inner.CreateEventDispatcher(host));
        public IEventSubscriptionAdapter CreateEventSubscriber(object host) =>
            _inner.CreateEventSubscriber(host);
        public IInteractionBlocker CreateInteractionBlocker(object host) =>
            Active.WrapBlocker(_inner.CreateInteractionBlocker(host));
        public ITimerAdapter CreateTimerAdapter() => Active.WrapTimer(_inner.CreateTimerAdapter());
        public Type GetDefaultLoadingMaskType() => _inner.GetDefaultLoadingMaskType();
        public IInteractionObserverService CreateInteractionObserverAdapter(object host) =>
            Active.WrapObserver(_inner.CreateInteractionObserverAdapter(host));
        public IFocusObserverAdapter CreateFocusObserver(object host) =>
            _inner.CreateFocusObserver(host);
    }

    internal sealed class ScenarioWpfHost : WpfLayeredPageHostBase
    {
        private readonly ScenarioPlatformControls _controls;

        public ScenarioWpfHost(Panel root, ScenarioPlatformControls controls)
            : base(root) { _controls = controls; }

        public override void AddView(object view)
        {
            bool added = view is UIElement element && !Root.Children.Contains(element);
            base.AddView(view);
            if (added) _controls.Metrics.ViewAdded();
        }

        public override void RemoveView(object view)
        {
            bool existed = view is UIElement element && Root.Children.Contains(element);
            base.RemoveView(view);
            if (existed) _controls.Metrics.ViewRemoved();
            if (_controls.ConsumeViewRemovalFailure())
                throw new ScenarioInjectedException("Injected surface cleanup failure.");
        }
    }
}
