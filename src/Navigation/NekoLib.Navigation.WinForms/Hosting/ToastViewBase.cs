using System;
using System.ComponentModel;
using System.Windows.Forms;
using NekoLib.Navigation.Contracts.Pages;

namespace NekoLib.Navigation.WinForms.Hosting
{
    /// <summary>
    /// WinForms base class for toast views. Subscribes to <see cref="Control.Click"/>
    /// so that a user click forwards an early-dismissal request to the
    /// <see cref="Contracts.Runtime.IToastService"/>.
    /// </summary>
    public abstract class ToastViewBase : UserControl, IToastView
    {
        private Action _dismissCallback;

        public object NativeView => this;
        public new bool IsDisposed { get; private set; }

        public new bool DesignMode =>
            base.DesignMode ||
            LicenseManager.UsageMode == LicenseUsageMode.Designtime;

        protected ToastViewBase()
        {
            Name = GetType().FullName;
            this.Click += OnViewClicked;
        }

        void IToastView.BindDismiss(Action dismissCallback)
        {
            _dismissCallback = dismissCallback;
        }

        void IToastView.OnShown(object payload) => OnShown(payload);

        /// <summary>
        /// Override to react to the toast becoming visible (e.g. apply payload to labels).
        /// </summary>
        protected virtual void OnShown(object payload) { }

        /// <summary>
        /// Programmatic early dismiss. Equivalent to the user clicking the toast.
        /// </summary>
        protected void Dismiss()
        {
            _dismissCallback?.Invoke();
        }

        private void OnViewClicked(object sender, EventArgs e)
        {
            _dismissCallback?.Invoke();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.Click -= OnViewClicked;
                _dismissCallback = null;
                IsDisposed = true;
            }
            base.Dispose(disposing);
        }
    }
}
