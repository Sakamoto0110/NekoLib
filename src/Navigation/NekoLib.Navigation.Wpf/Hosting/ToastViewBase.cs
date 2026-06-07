using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using NekoLib.Navigation.Contracts.Pages;

namespace NekoLib.Navigation.Wpf.Hosting
{
    /// <summary>
    /// WPF base class for toast views. Anchored to bottom-right by default and
    /// dismisses on click (early-dismiss equivalent to the WinForms toast base).
    /// </summary>
    public abstract class ToastViewBase : UserControl, IToastView
    {
        private Action _dismissCallback;

        public object NativeView => this;
        public bool IsDisposed { get; private set; }

        public bool DesignMode =>
            base.GetValue(DesignerProperties.IsInDesignModeProperty) is bool b && b;

        protected ToastViewBase()
        {
            Name = GetType().Name;
            HorizontalAlignment = HorizontalAlignment.Right;
            VerticalAlignment = VerticalAlignment.Bottom;
            Margin = new Thickness(0, 0, 20, 20);
            Focusable = false;

            MouseLeftButtonDown += OnViewClicked;
        }

        void IToastView.BindDismiss(Action dismissCallback)
            => _dismissCallback = dismissCallback;

        void IToastView.OnShown(object payload) => OnShown(payload);

        /// <summary>Override to react to the toast becoming visible.</summary>
        protected virtual void OnShown(object payload) { }

        /// <summary>Programmatic early dismiss. Equivalent to the user clicking the toast.</summary>
        protected void Dismiss() => _dismissCallback?.Invoke();

        private void OnViewClicked(object sender, MouseButtonEventArgs e)
            => _dismissCallback?.Invoke();

        public void Dispose()
        {
            if (IsDisposed) return;
            MouseLeftButtonDown -= OnViewClicked;
            _dismissCallback = null;
            IsDisposed = true;
        }
    }
}
