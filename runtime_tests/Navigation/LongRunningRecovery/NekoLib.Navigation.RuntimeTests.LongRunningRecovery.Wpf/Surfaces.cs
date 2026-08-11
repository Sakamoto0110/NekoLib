#nullable enable
using System;
using System.Threading.Tasks;
using System.Windows.Controls;
using NekoLib.Navigation.Contracts.Pages;
using NekoLib.Navigation.Wpf.Hosting;

namespace NekoLib.Navigation.RuntimeTests.LongRunningRecovery.Wpf
{
    public sealed class ScenarioDialog : DialogViewBase
    {
        protected override Task OnShownAsync(object payload)
        {
            SurfaceDirective directive = (SurfaceDirective)payload;
            if (directive.ThrowOnShow)
                throw new ScenarioInjectedException("Injected dialog show failure.");
            if (directive.Complete)
            {
                if (directive.BooleanResult) Confirm(); else Cancel();
            }
            return Task.CompletedTask;
        }
    }

    public sealed class ScenarioPrompt : PromptViewBase<string?>
    {
        protected override Task OnShownAsync(object payload)
        {
            SurfaceDirective directive = (SurfaceDirective)payload;
            if (directive.ThrowOnShow)
                throw new ScenarioInjectedException("Injected prompt show failure.");
            if (directive.Complete) CompletePrompt(directive.TextResult);
            return Task.CompletedTask;
        }
    }

    public sealed class ScenarioPopover : PopoverViewBase
    {
        protected override Task OnShownAsync(object payload)
        {
            SurfaceDirective directive = (SurfaceDirective)payload;
            if (directive.ThrowOnShow)
                throw new ScenarioInjectedException("Injected popover show failure.");
            if (directive.Complete) Complete(directive.BooleanResult);
            return Task.CompletedTask;
        }
    }

    public sealed class ScenarioToast : ToastViewBase
    {
        protected override void OnShown(object payload)
        {
            SurfaceDirective directive = (SurfaceDirective)payload;
            if (directive.ThrowOnShow)
                throw new ScenarioInjectedException("Injected toast show failure.");
            if (directive.Complete) Dismiss();
        }
    }

    public sealed class BindingFailureDialog : UserControl, IDialogView
    {
        public object NativeView => this;
        public bool IsDisposed { get; private set; }
        public void BindCompletion(Action<bool> completionCallback) =>
            throw new ScenarioInjectedException("Injected surface binding failure.");
        public Task OnShownAsync(object payload) => Task.CompletedTask;
        public void Dispose() { IsDisposed = true; }
    }
}
