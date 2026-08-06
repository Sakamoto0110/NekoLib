using System;
using System.ComponentModel;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using NekoLib.Navigation.Contracts.Pages;

namespace NekoLib.Navigation.WinForms.Hosting
{
    /// <summary>
    /// WinForms base class for prompt views. Subclasses call
    /// <see cref="CompletePrompt"/> to signal completion; the
    /// <see cref="Contracts.Runtime.IPromptService"/> resolves its awaited task and
    /// removes the control from the host.
    ///
    /// As with <see cref="DialogViewBase"/>, a prompt is conceptually a centered
    /// rectangle: this base class undoes the host's <c>Dock=Fill</c> after attach
    /// and centers itself at its designer-defined size, tracking the parent's
    /// <c>Resize</c> so it stays centered.
    /// </summary>
    public class PromptViewBase<TResult> : UserControl, IPromptView<TResult>
    {
        private Action<TResult> _completionCallback;
        private Size _naturalSize;
        private Control _trackedParent;
        private Action? _pendingLayout;

        public object NativeView => this;
        public new bool IsDisposed { get; private set; }

        public new bool DesignMode =>
            base.DesignMode ||
            LicenseManager.UsageMode == LicenseUsageMode.Designtime;

        protected PromptViewBase()
        {
            Name = GetType().Name; // NAV-008(g): aligned with the WPF surface bases.
            ParentChanged += OnParentChanged;
        }

        void IPromptView<TResult>.BindCompletion(Action<TResult> completionCallback)
        {
            _completionCallback = completionCallback;
        }

        Task IPromptView.OnShownAsync(object payload) => OnShownAsync(payload);

        /// <summary>
        /// Override to react to the prompt becoming visible (e.g. focus, load payload).
        /// </summary>
        protected virtual Task OnShownAsync(object payload) => Task.CompletedTask;

        /// <summary>
        /// Signals that the user has produced a result. Resolves the awaiting service
        /// task; subsequent calls are no-ops.
        /// </summary>
        protected void CompletePrompt(TResult result)
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

            if (_naturalSize.IsEmpty || _naturalSize.Width <= 0 || _naturalSize.Height <= 0)
                _naturalSize = Size;

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

        private void OnParentResize(object sender, EventArgs e) => Recenter();

        private void Recenter()
        {
            if (Parent == null) return;
            Location = new Point(
                Math.Max(0, (Parent.ClientSize.Width  - Width)  / 2),
                Math.Max(0, (Parent.ClientSize.Height - Height) / 2));
        }

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
