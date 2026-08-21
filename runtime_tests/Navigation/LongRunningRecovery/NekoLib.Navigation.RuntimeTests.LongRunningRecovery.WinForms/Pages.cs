#nullable enable
using System;
using System.Threading.Tasks;
using NekoLib.Navigation.Contracts.Pages;
using NekoLib.Navigation.Metadata;
using NekoLib.Navigation.Metadata.Attributes;
using NekoLib.Navigation.WinForms.Hosting;

namespace NekoLib.Navigation.RuntimeTests.LongRunningRecovery.WinForms
{
    public abstract class ScenarioPageBase : PageView, IScenarioProbedPage,
        IPageStateful, IBackgroundLoadable, IHostAttachable
    {
        protected ScenarioPageBase()
        {
            Probe = ScenarioStateSlot.Current.CreateProbe(GetType());
        }

        public PageProbe Probe { get; }

        public override Task OnNavigatedToAsync(NavigationArgs args) => Probe.EnterAsync(args);
        public override Task OnNavigatedFromAsync() => Probe.LeaveAsync();
        public Task LoadInBackgroundAsync(object? args) => Probe.LoadAsync();
        public Task ApplyBackgroundResultAsync() => Probe.ApplyAsync();
        public object CaptureState() => Probe.CaptureState();
        public void RestoreState(object? state) => Probe.RestoreState(state);
        public void OnAttach(IPageHost host) => Probe.Attached();
        public void OnDetach() => Probe.Detached();

        public override void ShowPage()
        {
            Probe.Shown();
            base.ShowPage();
        }

        public override void HidePage()
        {
            Probe.Hidden();
            base.HidePage();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) Probe.Dispose();
            base.Dispose(disposing);
        }
    }

    [ScenarioGuard(ScenarioGuardBehavior.AllowUnlessIdleDenied)]
    public sealed class IdlePage : ScenarioPageBase { }
    public sealed class TransientPage : ScenarioPageBase { }
    public sealed class StrongPage : ScenarioPageBase { }
    public sealed class WeakPage : ScenarioPageBase { }

    [KeepAttached]
    public sealed class KeepAttachedPage : ScenarioPageBase { }

    public sealed class LoadBeforePage : ScenarioPageBase { }
    public sealed class ShowImmediatelyPage : ScenarioPageBase { }
    public sealed class BackgroundPage : ScenarioPageBase { }

    [RequireAuthenticated]
    public sealed class AuthenticatedPage : ScenarioPageBase { }

    [RequireRole("operator")]
    public sealed class RolePage : ScenarioPageBase { }

    [RequirePermission("sell", typeof(IdlePage))]
    public sealed class PermissionPage : ScenarioPageBase { }

    [RequireRole("operator", RedirectTo = typeof(IdlePage))]
    public sealed class RedirectToIdlePage : ScenarioPageBase { }

    public sealed class FaultPage : ScenarioPageBase { }

    [ScenarioGuard(ScenarioGuardBehavior.Timeout)]
    public sealed class GuardTimeoutPage : ScenarioPageBase { }

    [ScenarioGuard(ScenarioGuardBehavior.Throw)]
    public sealed class GuardThrowPage : ScenarioPageBase { }

    [ScenarioGuard(ScenarioGuardBehavior.Redirect, typeof(CycleBPage))]
    public sealed class CycleAPage : ScenarioPageBase { }

    [ScenarioGuard(ScenarioGuardBehavior.Redirect, typeof(CycleAPage))]
    public sealed class CycleBPage : ScenarioPageBase { }

    [ScenarioGuard(ScenarioGuardBehavior.Redirect, typeof(Depth2Page))]
    public sealed class Depth1Page : ScenarioPageBase { }
    [ScenarioGuard(ScenarioGuardBehavior.Redirect, typeof(Depth3Page))]
    public sealed class Depth2Page : ScenarioPageBase { }
    [ScenarioGuard(ScenarioGuardBehavior.Redirect, typeof(Depth4Page))]
    public sealed class Depth3Page : ScenarioPageBase { }
    [ScenarioGuard(ScenarioGuardBehavior.Redirect, typeof(Depth5Page))]
    public sealed class Depth4Page : ScenarioPageBase { }
    [ScenarioGuard(ScenarioGuardBehavior.Redirect, typeof(Depth6Page))]
    public sealed class Depth5Page : ScenarioPageBase { }
    [ScenarioGuard(ScenarioGuardBehavior.Redirect, typeof(Depth7Page))]
    public sealed class Depth6Page : ScenarioPageBase { }
    [ScenarioGuard(ScenarioGuardBehavior.Redirect, typeof(Depth8Page))]
    public sealed class Depth7Page : ScenarioPageBase { }
    [ScenarioGuard(ScenarioGuardBehavior.Redirect, typeof(Depth9Page))]
    public sealed class Depth8Page : ScenarioPageBase { }
    [ScenarioGuard(ScenarioGuardBehavior.Redirect, typeof(Depth10Page))]
    public sealed class Depth9Page : ScenarioPageBase { }
    [ScenarioGuard(ScenarioGuardBehavior.Redirect, typeof(IdlePage))]
    public sealed class Depth10Page : ScenarioPageBase { }
}
