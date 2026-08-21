using System;
using System.ComponentModel;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using NekoLib.Navigation.Contracts.Pages;

namespace NekoLib.Navigation.WinForms.Hosting
{
    /// <summary>
    /// WinForms base class for popover views (non-blocking overlays). Subclasses
    /// call <see cref="Complete"/> to resolve the awaiting service task. Unlike
    /// <see cref="DialogViewBase"/>, popovers do NOT recenter — they preserve the
    /// designer-defined <see cref="Control.Location"/> so the subclass can anchor
    /// itself contextually (e.g. next to the control that triggered it).
    /// <para>
    /// To opt into auto-dismissal on focus loss, derive from
    /// <see cref="AutoDismissPopoverBase"/> instead, or implement
    /// <see cref="IUnfocusAware"/> directly.
    /// </para>
    /// </summary>
    public class PopoverViewBase : UserControl, IPopoverView
    {
        private Action<bool> _completionCallback;
        private Size _naturalSize;
        private Point _naturalLocation;
        private Action? _pendingLayout;

        public object NativeView => this;
        public new bool IsDisposed { get; private set; }

        public new bool DesignMode =>
            base.DesignMode ||
            LicenseManager.UsageMode == LicenseUsageMode.Designtime;

        protected PopoverViewBase()
        {
            Name = GetType().Name; // NAV-008(g): aligned with the WPF surface bases.
            ParentChanged += OnParentChanged;
        }

        void IPopoverView.BindCompletion(Action<bool> completionCallback)
        {
            _completionCallback = completionCallback;
        }

        Task IPopoverView.OnShownAsync(object? payload) => OnShownAsync(payload);

        /// <summary>
        /// Override to react to the popover becoming visible (e.g. focus a control,
        /// position relative to an anchor passed via <paramref name="payload"/>).
        /// </summary>
        protected virtual Task OnShownAsync(object? payload) => Task.CompletedTask;

        /// <summary>
        /// Resolves the awaiting service task with <paramref name="result"/>.
        /// Subsequent calls are no-ops.
        /// </summary>
        protected void Complete(bool result)
        {
            var callback = _completionCallback;
            _completionCallback = null;
            callback?.Invoke(result);
        }

        // ---------------------------------------------------------------------
        // PRESERVE-DESIGNER-LAYOUT
        // ---------------------------------------------------------------------
        // The host docks every overlay to Fill on AddView. Popovers want their
        // designer-defined size AND location, so snapshot both before the dock
        // takes effect, then restore after the host's AddView completes.

        private void OnParentChanged(object sender, EventArgs e)
        {
            // The WinForms designer parents the control too, and none of the layout
            // fixup below applies there: the design surface must keep the layout the
            // designer itself defined. Skipping it is also what makes the view
            // loadable at all - the deferred call further down needs a window handle,
            // and at design time there is none, so the designer would report
            // "Invoke or BeginInvoke cannot be called ... until the window handle has
            // been created" and refuse to open the view.
            if (DesignMode) return;

            if (Parent == null) return;

            if (_naturalSize.IsEmpty || _naturalSize.Width <= 0 || _naturalSize.Height <= 0)
            {
                _naturalSize = Size;
                _naturalLocation = Location;
            }

            RunWhenHandleReady(() =>
            {
                if (IsDisposed || Parent == null) return;

                Dock = DockStyle.None;
                Anchor = AnchorStyles.Top | AnchorStyles.Left;
                Size = _naturalSize;
                Location = _naturalLocation;
            });
        }

        /// <summary>
        /// Queues <paramref name="action"/> onto the message loop, waiting for the
        /// window handle when it does not exist yet. <see cref="Control.BeginInvoke"/>
        /// throws outright on a handle-less control rather than deferring, so calling
        /// it straight from a parenting notification is not safe.
        /// </summary>
        private void RunWhenHandleReady(Action action)
        {
            if (IsHandleCreated)
            {
                BeginInvoke(action);
                return;
            }

            _pendingLayout = action;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);

            Action? pending = _pendingLayout;
            _pendingLayout = null;
            if (pending != null)
                BeginInvoke(pending);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ParentChanged -= OnParentChanged;
                _completionCallback = null;
                IsDisposed = true;
            }
            base.Dispose(disposing);
        }
    }
}
