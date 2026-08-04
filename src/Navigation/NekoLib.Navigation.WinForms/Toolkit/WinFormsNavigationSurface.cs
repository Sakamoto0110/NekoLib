using System;
using System.Drawing;
using System.Windows.Forms;
using NekoLib.Navigation.Toolkit.Abstractions;
using NekoLib.Navigation.Toolkit.Models;

namespace NekoLib.Navigation.WinForms.Toolkit
{
    /// <summary>
    /// Read-only view of a WinForms <see cref="Control"/> as the navigation surface:
    /// reports its client bounds, DPI scale, visibility/enabled state, and resolves
    /// named anchor points used to position overlays (dialogs, toasts, keyboards).
    /// </summary>
    public sealed class WinFormsNavigationSurface : INavigationSurface
    {
        private readonly Control _host;

        public WinFormsNavigationSurface(Control host)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));
        }

        public Rectangle ClientBounds => _host.ClientRectangle;

        /// <summary>
        /// DPI factor of the host, where 1.0 is 96 DPI.
        /// <para>
        /// NAV-009(a): this used to read <c>Control.CreateGraphics()</c>, which forces
        /// the host's window handle to be created as a side effect and throws
        /// <see cref="ObjectDisposedException"/> once the host is disposed — so merely
        /// asking a surface for its scale could realize a window, and an anchor
        /// consumer could not read it during teardown. <c>DeviceDpi</c> is a plain
        /// field read: <b>before the host is realized</b> it reports the DPI captured
        /// when the control was constructed and creates no handle, and <b>after the
        /// host is disposed</b> it keeps reporting the last known value instead of
        /// throwing. The value is therefore always safe to read.
        /// </para>
        /// </summary>
        public float Scale => _host.DeviceDpi / 96f;

        public bool IsActive => _host.Visible && _host.Enabled;

        public Point ResolveAnchor(SurfaceAnchor anchor)
        {
            var r = _host.ClientRectangle;
            int midX = r.Width / 2;
            int midY = r.Height / 2;

            switch (anchor)
            {
                case SurfaceAnchor.TopLeft: return new Point(0, 0);
                case SurfaceAnchor.TopCenter: return new Point(midX, 0);
                case SurfaceAnchor.TopRight: return new Point(r.Width, 0);
                case SurfaceAnchor.CenterLeft: return new Point(0, midY);
                case SurfaceAnchor.Center: return new Point(midX, midY);
                case SurfaceAnchor.CenterRight: return new Point(r.Width, midY);
                case SurfaceAnchor.BottomLeft: return new Point(0, r.Height);
                case SurfaceAnchor.BottomCenter: return new Point(midX, r.Height);
                case SurfaceAnchor.BottomRight: return new Point(r.Width, r.Height);
                default: return new Point(midX, midY);
            }
        }
    }
}
