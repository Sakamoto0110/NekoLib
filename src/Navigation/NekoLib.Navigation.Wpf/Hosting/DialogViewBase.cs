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
    /// <see cref="Cancel"/> to resolve the awaiting service task. Centers itself
    /// within the host using the designer-defined Width/Height (or default
    /// UserControl sizing).
    /// </summary>
    public class DialogViewBase : UserControl, IDialogView
    {
        private Action<bool> _completionCallback;

        /// <inheritdoc />
        public object NativeView => this;
        /// <inheritdoc />
        public bool IsDisposed { get; private set; }

        /// <summary>Gets whether the control is running inside the WPF designer.</summary>
        public bool DesignMode =>
            base.GetValue(DesignerProperties.IsInDesignModeProperty) is bool b && b;

        /// <summary>Initializes a designer-safe centered dialog surface.</summary>
        protected DialogViewBase()
        {
            Name = GetType().Name;
            // Center within the host; explicit designer Width/Height (if any) size it.
            HorizontalAlignment = HorizontalAlignment.Center;
            VerticalAlignment = VerticalAlignment.Center;
        }

        void IDialogView.BindCompletion(Action<bool> completionCallback)
            => _completionCallback = completionCallback;

        Task IDialogView.OnShownAsync(object? payload) => OnShownAsync(payload);

        /// <summary>Override to react to the dialog becoming visible (apply payload, focus).</summary>
        protected virtual Task OnShownAsync(object? payload) => Task.CompletedTask;

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

        /// <summary>
        /// NAV-009(b): virtual so a subclass can extend disposal, the way the
        /// WinForms bases allow through <c>Dispose(bool)</c>. An override must call
        /// <c>base.Dispose()</c> — that is what clears the completion callback and
        /// sets <see cref="IsDisposed"/>.
        /// </summary>
        public virtual void Dispose()
        {
            if (IsDisposed) return;
            _completionCallback = null;
            IsDisposed = true;
        }
    }
}
