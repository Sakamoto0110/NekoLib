using System;
using System.Windows.Forms;
using NekoLib.Navigation.Toolkit.Abstractions;

namespace NekoLib.Navigation.WinForms.Toolkit
{
    /// <summary>
    /// WinForms implementation of <see cref="INavigationToolkit"/>. Wraps the
    /// host Control so consumers can query the navigation surface and request
    /// focus without referencing WinForms types directly.
    /// </summary>
    public sealed class WinFormsNavigationToolkit : INavigationToolkit
    {
        private readonly Control _host;

        public WinFormsNavigationToolkit(Control host)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));
            Surface = new WinFormsNavigationSurface(host);
        }

        public INavigationSurface Surface { get; }

        public void FocusSurface()
        {
            if (!_host.IsHandleCreated)
                return;

            if (_host.InvokeRequired)
                _host.BeginInvoke((Action)(() => _host.Focus()));
            else
                _host.Focus();
        }
    }
}
