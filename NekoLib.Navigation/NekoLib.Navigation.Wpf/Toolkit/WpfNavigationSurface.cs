//// FILE: PageNav.Toolkit.Wpf/WpfNavigationSurface.cs
//using System.Windows;
//using System.Windows.Media;
//using System.Windows.Shapes;

//namespace PageNav.Toolkit.Wpf
//{
//    public sealed class WpfNavigationSurface : INavigationSurface
//    {
//        private readonly FrameworkElement _host;

//        public WpfNavigationSurface(FrameworkElement host)
//        {
//            _host = host;
//        }

//        public Rectangle ClientBounds
//        {
//            get { var r = new Rectangle();
//                r.w }

//        }
            

//        public Point Anchor =>
//            new Point(
//                (int)(_host.ActualWidth / 2),
//                (int)(_host.ActualHeight / 2));

//        public float Scale
//        {
//            get
//            {
//                var src = PresentationSource.FromVisual(_host);
//                return src?.CompositionTarget?.TransformToDevice.M11 ?? 1f;
//            }
//        }

//        public bool IsActive => _host.IsVisible && _host.IsEnabled;
//    }
//}
