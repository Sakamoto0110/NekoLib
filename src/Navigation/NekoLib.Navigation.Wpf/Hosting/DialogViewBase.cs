using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using NekoLib.Navigation.Contracts.Pages;

namespace NekoLib.Navigation.Wpf.Hosting
{
    /// <summary>
    /// WPF base class for dialog views. Subclasses call <see cref="Confirm"/> /
    /// <see cref="Cancel"/> to resolve the awaiting service task. The host
    /// stretches every overlay; this base undoes that and centers itself using
    /// the designer-defined Width/Height (or default UserControl sizing).
    /// </summary>
    public abstract class DialogViewBase : UserControl, IDialogView
    {
        private Action<bool> _completionCallback;

        public object NativeView => this;
        public bool IsDisposed { get; private set; }

        public bool DesignMode =>
            base.GetValue(DesignerProperties.IsInDesignModeProperty) is bool b && b;

        protected DialogViewBase()
        {
            Name = GetType().Name;
            // Centered overlay: cancel the host's stretch so designer Width/Height apply.
            HorizontalAlignment = HorizontalAlignment.Center;
            VerticalAlignment = VerticalAlignment.Center;
        }

        void IDialogView.BindCompletion(Action<bool> completionCallback)
            => _completionCallback = completionCallback;

        Task IDialogView.OnShownAsync(object payload) => OnShownAsync(payload);

        /// <summary>Override to react to the dialog becoming visible (apply payload, focus).</summary>
        protected virtual Task OnShownAsync(object payload) => Task.CompletedTask;

        /// <summary>Signals user confirmation. Subsequent completion calls are no-ops.</summary>
        protected void Confirm() => Complete(true);

        /// <summary>Signals user cancellation. Subsequent completion calls are no-ops.</summary>
        protected void Cancel() => Complete(false);

        private void Complete(bool result)
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
