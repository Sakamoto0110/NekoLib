using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using NekoLib.Navigation.Contracts.Pages;

namespace NekoLib.Navigation.Wpf.Hosting
{
    /// <summary>
    /// WPF base class for prompt views (typed result). Subclasses call
    /// <see cref="CompletePrompt"/> to resolve the awaiting service task.
    /// Centered, like <see cref="DialogViewBase"/>.
    /// </summary>
    public class PromptViewBase<TResult> : UserControl, IPromptView<TResult>
    {
        private Action<TResult?>? _completionCallback;

        public object NativeView => this;
        public bool IsDisposed { get; private set; }

        public bool DesignMode =>
            base.GetValue(DesignerProperties.IsInDesignModeProperty) is bool b && b;

        protected PromptViewBase()
        {
            Name = GetType().Name;
            HorizontalAlignment = HorizontalAlignment.Center;
            VerticalAlignment = VerticalAlignment.Center;
        }

        void IPromptView<TResult>.BindCompletion(Action<TResult?> completionCallback)
            => _completionCallback = completionCallback;

        Task IPromptView.OnShownAsync(object? payload) => OnShownAsync(payload);

        /// <summary>Override to react to the prompt becoming visible.</summary>
        protected virtual Task OnShownAsync(object? payload) => Task.CompletedTask;

        /// <summary>Resolves the awaiting task with the user's result. Subsequent calls are no-ops.</summary>
        protected void CompletePrompt(TResult? result)
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
