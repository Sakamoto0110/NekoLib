using System;
using System.Windows;
using NekoLib.Navigation.Toolkit.Abstractions;

namespace NekoLib.Navigation.Wpf.Toolkit
{
    /// <summary>
    /// WPF implementation of <see cref="INavigationToolkit"/>. Wraps the host
    /// element so consumers can query the surface and request focus without
    /// referencing WPF types directly.
    /// </summary>
    public sealed class WpfNavigationToolkit : INavigationToolkit
    {
        private readonly FrameworkElement _host;

        public WpfNavigationToolkit(FrameworkElement host)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));
            Surface = new WpfNavigationSurface(host);
        }

        public INavigationSurface Surface { get; }

        public void FocusSurface() => _host.Focus();
    }
}
