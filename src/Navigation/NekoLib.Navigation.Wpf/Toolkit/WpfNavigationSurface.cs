using System;
using System.Windows;
using System.Windows.Media;
using NekoLib.Navigation.Toolkit.Abstractions;
using NekoLib.Navigation.Toolkit.Models;
using DrawingPoint = System.Drawing.Point;
using DrawingRectangle = System.Drawing.Rectangle;

namespace NekoLib.Navigation.Wpf.Toolkit
{
    /// <summary>
    /// Read-only view of a WPF <see cref="FrameworkElement"/> as the navigation
    /// surface. Reports its render-size bounds, DPI scale, visibility/enabled
    /// state, and resolves named anchor points used to position overlays.
    /// </summary>
    public sealed class WpfNavigationSurface : INavigationSurface
    {
        private readonly FrameworkElement _host;

        public WpfNavigationSurface(FrameworkElement host)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));
        }

        public DrawingRectangle ClientBounds => new DrawingRectangle(
            0,
            0,
            (int)_host.ActualWidth,
            (int)_host.ActualHeight);

        /// <summary>
        /// DPI factor of the host, where 1.0 is 96 DPI. Reading it never has a side
        /// effect. Before the element is connected to a presentation source there is
        /// no device transform to read, so it degrades to <c>1f</c>; see NAV-009(a)
        /// for the matching WinForms rule.
        /// </summary>
        public float Scale
        {
            get
            {
                var src = PresentationSource.FromVisual(_host);
                return (float?)src?.CompositionTarget?.TransformToDevice.M11 ?? 1f;
            }
        }

        public bool IsActive => _host.IsVisible && _host.IsEnabled;

        public DrawingPoint ResolveAnchor(SurfaceAnchor anchor)
        {
            int w = (int)_host.ActualWidth;
            int h = (int)_host.ActualHeight;
            int midX = w / 2;
            int midY = h / 2;

            return anchor switch
            {
                SurfaceAnchor.TopLeft => new DrawingPoint(0, 0),
                SurfaceAnchor.TopCenter => new DrawingPoint(midX, 0),
                SurfaceAnchor.TopRight => new DrawingPoint(w, 0),
                SurfaceAnchor.CenterLeft => new DrawingPoint(0, midY),
                SurfaceAnchor.Center => new DrawingPoint(midX, midY),
                SurfaceAnchor.CenterRight => new DrawingPoint(w, midY),
                SurfaceAnchor.BottomLeft => new DrawingPoint(0, h),
                SurfaceAnchor.BottomCenter => new DrawingPoint(midX, h),
                SurfaceAnchor.BottomRight => new DrawingPoint(w, h),
                _ => new DrawingPoint(midX, midY),
            };
        }
    }
}
