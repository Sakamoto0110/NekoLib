using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using NekoLib.Navigation.Contracts.Pages;
using NekoLib.Navigation.Toolkit.Abstractions;
using NekoLib.Navigation.Toolkit.Models;
using NekoLib.Navigation.WinForms.Toolkit;

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
    public class ToastViewBase : UserControl, IToastView
    {
        private Action _dismissCallback;
        private Size _designSize = Size.Empty;

        /// <inheritdoc />
        public object NativeView => this;
        /// <inheritdoc />
        public new bool IsDisposed { get; private set; }

        /// <summary>Gets whether the control is running inside the WinForms designer.</summary>
        public new bool DesignMode =>
            base.DesignMode ||
            LicenseManager.UsageMode == LicenseUsageMode.Designtime;

        /// <summary>Initializes a designer-safe anchored toast surface.</summary>
        protected ToastViewBase()
        {
            Name = GetType().Name; // NAV-008(g): aligned with the WPF surface bases.
            this.Click += OnViewClicked;
        }

        /// <summary>
        /// Distance in device-independent pixels between the toast and the two host
        /// edges it is parked against. Defaults to 20, matching the margin the WPF
        /// toast base sets declaratively. Scaled by the surface DPI factor.
        /// </summary>
        protected virtual int AnchorInset => 20;

        void IToastView.BindDismiss(Action dismissCallback)
        {
            _dismissCallback = dismissCallback;
        }

        void IToastView.OnShown(object? payload)
        {
            OnShown(payload);

            // After OnShown, so a subclass that resizes itself from the payload is
            // placed at its final size.
            ApplyDefaultAnchor();
        }

        /// <inheritdoc />
        protected override void OnParentChanged(EventArgs e)
        {
            base.OnParentChanged(e);

            // IViewHost.AddView adds the control and only then docks it to Fill, so
            // this runs while the designed size is still intact. Capture it once.
            if (Parent != null && _designSize.IsEmpty)
                _designSize = Size;
        }

        /// <summary>
        /// Parks the toast at the host's <see cref="SurfaceAnchor.BottomRight"/> anchor,
        /// inset by <see cref="AnchorInset"/> scaled for DPI.
        /// <para>
        /// NAV-010: <c>WinFormsLayeredPageHostBase.AddView</c> docks every added view to
        /// <c>Fill</c>, and this base was the only surface base that never undocked —
        /// so a stock WinForms toast covered the whole navigation host, while the WPF
        /// base parked itself bottom-right. The geometry comes from the Toolkit
        /// contract, <see cref="INavigationSurface.ResolveAnchor"/> plus
        /// <see cref="INavigationSurface.Scale"/>, read off the toast's own parent, so
        /// it works whether or not an <see cref="INavigationToolkit"/> was registered.
        /// </para>
        /// <para>
        /// Override to place the toast somewhere else; call nothing and the toast keeps
        /// whatever the host gave it.
        /// </para>
        /// </summary>
        protected virtual void ApplyDefaultAnchor()
        {
            var parent = Parent;
            if (parent == null)
                return;

            Dock = DockStyle.None;

            // The host stretched us to Fill on AddView; go back to the designed size
            // instead of staying host-sized.
            if (!_designSize.IsEmpty)
                Size = _designSize;

            // Stay parked when the host resizes, the way the WPF base's alignment does.
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right;

            var surface = new WinFormsNavigationSurface(parent);
            var corner = surface.ResolveAnchor(SurfaceAnchor.BottomRight);
            var inset = (int)Math.Round(AnchorInset * surface.Scale);

            Location = new System.Drawing.Point(
                Math.Max(0, corner.X - Width - inset),
                Math.Max(0, corner.Y - Height - inset));

            BringToFront();
        }

        /// <summary>
        /// Override to react to the toast becoming visible (e.g. apply payload to labels).
        /// </summary>
        protected virtual void OnShown(object? payload) { }

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

        /// <inheritdoc />
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
