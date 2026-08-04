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
    /// <remarks>
    /// <b>Reachability:</b> only a click on the toast's own background dismisses.
    /// WinForms click events do not bubble, so a click on a child control raises
    /// that child's <c>Click</c> and never the container's. A toast that contains
    /// child controls must therefore offer an explicit close affordance calling
    /// <see cref="Dismiss"/>; "tap anywhere to dismiss" does not hold across it.
    /// The WPF base differs — see the overlay section of the Navigation README.
    /// </remarks>
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
            Name = GetType().Name; // NAV-008(g): aligned with the WPF surface bases.
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
