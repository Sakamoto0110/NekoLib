using System;
using System.ComponentModel;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using NekoLib.Navigation.Contracts.Pages;

namespace NekoLib.Navigation.WinForms.Hosting
{
    /// <summary>
    /// WinForms base class for dialog views (Confirm / Cancel). Subclasses call
    /// <see cref="Confirm"/> or <see cref="Cancel"/> to signal completion; the
    /// <see cref="Contracts.Runtime.IDialogService"/> resolves its awaited task and
    /// removes the control from the host.
    ///
    /// The framework's view host docks every overlay to <see cref="DockStyle.Fill"/>
    /// when it is added. A dialog is conceptually a small centered rectangle, so this
    /// base class re-undocks itself after attach and keeps itself centered as the host
    /// resizes. Subclasses control the dialog's natural size via their designer
    /// <see cref="Control.Size"/>.
    /// </summary>
    public class DialogViewBase : UserControl, IDialogView
    {
        private Action<bool> _completionCallback;
        private Size _naturalSize;
        private Control _trackedParent;
        private Action? _pendingLayout;

        /// <inheritdoc />
        public object NativeView => this;
        /// <inheritdoc />
        public new bool IsDisposed { get; private set; }

        /// <summary>Gets whether the control is running inside the WinForms designer.</summary>
        public new bool DesignMode =>
            base.DesignMode ||
            LicenseManager.UsageMode == LicenseUsageMode.Designtime;

        /// <summary>Initializes a designer-safe centered dialog surface.</summary>
        protected DialogViewBase()
        {
            Name = GetType().Name; // NAV-008(g): aligned with the WPF surface bases.
            ParentChanged += OnParentChanged;
        }

        void IDialogView.BindCompletion(Action<bool> completionCallback)
        {
            _completionCallback = completionCallback;
        }

        Task IDialogView.OnShownAsync(object? payload) => OnShownAsync(payload);

        /// <summary>
        /// Override to react to the dialog becoming visible (e.g. apply payload, focus).
        /// </summary>
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

        // ---------------------------------------------------------------------
        // CENTERED-RECTANGLE LAYOUT
        // ---------------------------------------------------------------------

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

            if (_trackedParent != null)
            {
                _trackedParent.Resize -= OnParentResize;
                _trackedParent = null;
            }

            if (Parent == null) return;

            // Capture the designer-defined size as our "natural" dialog size
            // (the host has not yet docked us when ParentChanged first fires for the
            // initial Controls.Add, but it will immediately set Dock=Fill, so snapshot
            // the size while it is still meaningful).
            if (_naturalSize.IsEmpty || _naturalSize.Width <= 0 || _naturalSize.Height <= 0)
                _naturalSize = Size;

            // The host docks us to Fill in AddView. Undo that and center.
            // Deferred so we run after host AddView completes.
            RunWhenHandleReady(() =>
            {
                if (IsDisposed || Parent == null) return;

                Dock = DockStyle.None;
                Anchor = AnchorStyles.None;
                Size = _naturalSize;
                Recenter();

                _trackedParent = Parent;
                _trackedParent.Resize += OnParentResize;
            });
        }

        /// <summary>
        /// Queues <paramref name="action"/> onto the message loop, waiting for the
        /// window handle when it does not exist yet. <see cref="Control.BeginInvoke(Delegate)"/>
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

        /// <inheritdoc />
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);

            Action? pending = _pendingLayout;
            _pendingLayout = null;
            if (pending != null)
                BeginInvoke(pending);
        }

        private void OnParentResize(object sender, EventArgs e) => Recenter();

        private void Recenter()
        {
            if (Parent == null) return;
            Location = new Point(
                Math.Max(0, (Parent.ClientSize.Width  - Width)  / 2),
                Math.Max(0, (Parent.ClientSize.Height - Height) / 2));
        }

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ParentChanged -= OnParentChanged;
                if (_trackedParent != null)
                {
                    _trackedParent.Resize -= OnParentResize;
                    _trackedParent = null;
                }
                _completionCallback = null;
                IsDisposed = true;
            }
            base.Dispose(disposing);
        }
    }
}
