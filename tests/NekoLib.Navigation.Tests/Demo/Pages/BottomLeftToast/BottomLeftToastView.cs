using System;
using System.Drawing;
using System.Windows.Forms;
using NekoLib.Navigation.WinForms.Hosting;

namespace NavigationDemo.Pages.BottomLeftToast
{
    /// <summary>
    /// Toast that renders as a fixed-size rectangle pinned to the bottom-left corner
    /// of the host root, instead of the default dock-fill the host applies to overlays.
    ///
    /// The framework host calls <c>control.Dock = DockStyle.Fill</c> in <c>AddView</c>;
    /// to render as a small rectangle we react to <see cref="Control.ParentChanged"/>
    /// (fired when the host inserts us into <c>Root.Controls</c>) and re-establish
    /// our own Dock=None / Anchor=Bottom|Left positioning. We also subscribe to the
    /// parent's <c>Resize</c> to keep the rectangle pinned if the host window resizes.
    /// </summary>
    public partial class BottomLeftToastView : ToastViewBase
    {
        private const int EdgeMargin = 12;
        private const int ToastWidth = 280;
        private const int ToastHeight = 56;

        private Control _trackedParent;

        public BottomLeftToastView()
        {
            InitializeComponent();

            ParentChanged += OnParentChanged;
        }

        protected override void OnShown(object payload)
        {
            if (payload is string text && !string.IsNullOrEmpty(text))
                lblMessage.Text = text;
        }

        // ---------------------------------------------------------------------
        // Bottom-left positioning (overrides the host's Dock=Fill after attach)
        // ---------------------------------------------------------------------

        private void OnParentChanged(object sender, EventArgs e)
        {
            if (_trackedParent != null)
            {
                _trackedParent.Resize -= OnParentResize;
                _trackedParent = null;
            }

            if (Parent == null) return;

            BeginInvoke((Action)(() =>
            {
                if (IsDisposed || Parent == null) return;

                Dock = DockStyle.None;
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
                Size = new Size(ToastWidth, ToastHeight);
                Reposition();

                _trackedParent = Parent;
                _trackedParent.Resize += OnParentResize;
            }));
        }

        private void OnParentResize(object sender, EventArgs e) => Reposition();

        private void Reposition()
        {
            if (Parent == null) return;
            Location = new Point(
                EdgeMargin,
                Parent.ClientSize.Height - ToastHeight - EdgeMargin);
        }

        // ---------------------------------------------------------------------
        // Cleanup
        // ---------------------------------------------------------------------

        // Designer.cs already overrides Dispose(bool) for `components`; piggy-back
        // there to also unhook our positioning hooks.
        partial void DisposeOverrides(bool disposing)
        {
            if (disposing)
            {
                ParentChanged -= OnParentChanged;
                if (_trackedParent != null)
                {
                    _trackedParent.Resize -= OnParentResize;
                    _trackedParent = null;
                }
            }
        }
    }
}
