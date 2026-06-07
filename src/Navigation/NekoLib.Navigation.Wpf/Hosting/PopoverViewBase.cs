using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using NekoLib.Navigation.Contracts.Pages;

namespace NekoLib.Navigation.Wpf.Hosting
{
    /// <summary>
    /// WPF base class for popover views (non-blocking overlays). Subclasses call
    /// <see cref="Complete"/> to resolve the awaiting service task. Unlike
    /// <see cref="DialogViewBase"/>, popovers do NOT center themselves — the
    /// designer-defined alignment, margin, or attached Canvas position is
    /// preserved so subclasses keep full control of placement.
    /// <para>
    /// For auto-dismissal on focus loss, derive from
    /// <see cref="AutoDismissPopoverBase"/> instead, or implement
    /// <see cref="IUnfocusAware"/> directly.
    /// </para>
    /// </summary>
    public abstract class PopoverViewBase : UserControl, IPopoverView
    {
        private Action<bool> _completionCallback;

        public object NativeView => this;
        public bool IsDisposed { get; private set; }

        public bool DesignMode =>
            base.GetValue(DesignerProperties.IsInDesignModeProperty) is bool b && b;

        protected PopoverViewBase()
        {
            Name = GetType().Name;
            // Do NOT override alignments here — popover keeps designer placement.
        }

        void IPopoverView.BindCompletion(Action<bool> completionCallback)
            => _completionCallback = completionCallback;

        Task IPopoverView.OnShownAsync(object payload) => OnShownAsync(payload);

        /// <summary>
        /// Override to react to the popover becoming visible (e.g. focus a control,
        /// position relative to an anchor passed via <paramref name="payload"/>).
        /// </summary>
        protected virtual Task OnShownAsync(object payload) => Task.CompletedTask;

        /// <summary>Resolves the awaiting task. Subsequent calls are no-ops.</summary>
        protected void Complete(bool result)
        {
            var callback = _completionCallback;
            _completionCallback = null;
            callback?.Invoke(result);
        }

        public void Dispose()
        {
            if (IsDisposed) return;
            _completionCallback = null;
            IsDisposed = true;
        }
    }
}
