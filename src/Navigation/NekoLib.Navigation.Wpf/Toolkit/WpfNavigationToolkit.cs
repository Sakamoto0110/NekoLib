//// FILE: PageNav.Toolkit.Wpf/WpfNavigationToolkit.cs
//using PageNav.WinForms.Services;
//using System.Windows;

//namespace PageNav.Toolkit.Wpf
//{
//    public sealed class WpfNavigationToolkit : INavigationToolkit
//    {
//        public INavigationSurface Surface { get; }

//        public WpfNavigationToolkit(FrameworkElement host)
//        {
//            Surface = new WpfNavigationSurface(host);
//        }

//        public Point ResolveAnchor(SurfaceAnchor anchor)
//        {
//            var r = Surface.ClientBounds;
//            switch(anchor)
//            {
//                case SurfaceAnchor.TopLeft: return new Point(0, 0);
//                case SurfaceAnchor.TopCenter: return new Point(r.Width / 2, 0);
//                case SurfaceAnchor.TopRight: return new Point(r.Width, 0);
//                case SurfaceAnchor.CenterLeft: return new Point(0, r.Height / 2);
//                case SurfaceAnchor.Center: return new Point(r.Width / 2, r.Height / 2);
//                case SurfaceAnchor.CenterRight: return new Point(r.Width, r.Height / 2);
//                case SurfaceAnchor.BottomLeft: return new Point(0, r.Height);
//                case SurfaceAnchor.BottomCenter: return new Point(r.Width / 2, r.Height);
//                case SurfaceAnchor.BottomRight: return new Point(r.Width, r.Height);
//            }
//            return new Point(r.Width / 2, r.Height / 2);
//        }

//        public void Focus()
//        {
//            // Optional in WPF; usually focus is managed differently
//        }
//    }
//}
