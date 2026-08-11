#nullable enable
using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using NekoLib.Navigation.Contracts.Pages;
using NekoLib.Navigation.WinForms.Hosting;

namespace NekoLib.Navigation.RuntimeTests.LongRunningRecovery.WinForms
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
            return Task.FromResult(0);
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
            return Task.FromResult(0);
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
            return Task.FromResult(0);
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
        public BindingFailureDialog() { Size = new Size(120, 80); }
        public object NativeView => this;
        public new bool IsDisposed => base.IsDisposed;
        public void BindCompletion(Action<bool> completionCallback) =>
            throw new ScenarioInjectedException("Injected surface binding failure.");
        public Task OnShownAsync(object payload) => Task.FromResult(0);
    }
}
