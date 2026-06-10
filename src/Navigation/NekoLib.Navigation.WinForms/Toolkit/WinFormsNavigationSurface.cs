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

        public float Scale
        {
            get
            {
                using (var g = _host.CreateGraphics())
                    return g.DpiX / 96f;
            }
        }

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
