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

        /// <summary>Initializes toolkit services for one WinForms navigation root.</summary>
        /// <param name="host">Native navigation root.</param>
        public WinFormsNavigationToolkit(Control host)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));
            Surface = new WinFormsNavigationSurface(host);
        }

        /// <inheritdoc />
        public INavigationSurface Surface { get; }

        /// <inheritdoc />
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
