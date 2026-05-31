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
    public class BottomLeftToastView : ToastViewBase
    {
        private const int EdgeMargin = 12;
        private const int ToastWidth = 280;
        private const int ToastHeight = 56;

        private Control _trackedParent;
        private readonly Label _lblMessage;

        public BottomLeftToastView()
        {
            Name = nameof(BottomLeftToastView);
            BackColor = Color.FromArgb(40, 40, 40);
            Size = new Size(ToastWidth, ToastHeight);

            _lblMessage = new Label
            {
                Dock = DockStyle.Fill,
                Font = new Font("Microsoft Sans Serif", 11F, FontStyle.Bold),
                ForeColor = Color.White,
                TextAlign = ContentAlignment.MiddleCenter,
                Text = "Toast",
                BackColor = Color.Transparent,
            };
            Controls.Add(_lblMessage);

            ParentChanged += OnParentChanged;
        }

        protected override void OnShown(object payload)
        {
            if (payload is string text && !string.IsNullOrEmpty(text))
                _lblMessage.Text = text;
        }

        private void OnParentChanged(object sender, EventArgs e)
        {
            // Detach from the previous parent's resize hook.
            if (_trackedParent != null)
            {
                _trackedParent.Resize -= OnParentResize;
                _trackedParent = null;
            }

            if (Parent == null) return;

            // The host docks us to Fill in AddView. Undo that and pin bottom-left.
            // Use BeginInvoke so we run after the host's add-time configuration
            // completes (BringToFront, Visible=true, etc.).
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
            }
            base.Dispose(disposing);
        }
    }
}
