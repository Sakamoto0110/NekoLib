// FILE: PageNav.WinForms/PanelPageHost.cs
using System;
using System.Threading.Tasks;
 using NekoLib.Navigation.Contracts.Pages;
using NekoLib.Navigation.Metadata;
using System.Windows.Forms;
namespace NekoLib.Navigation.Hosting
{


    /// <summary>
    /// Simple WinForms host that attaches pages and overlays to a Panel.
    /// Implements both IPageHost and IViewHost.
    /// </summary>
    public partial class PanelPageHost  : UserControl, IPageView, IPageHost, IViewHost  
    {
        private Panel _root;

        public Panel View { get => _root; private set => _root = value; }

        
        object IPageView.NativeView => this;
        bool IPageView.IsDisposed => base.IsDisposed;
        bool IPageView.DesignMode => DesignMode;


        //public static explicit operator Panel(PanelPageHost host) => host._root;
         

        public PanelPageHost(Panel root)
        {
            _root = root;
            Name = _root.Name;
        }

        // ------------------------------------------------------------
        // IPageHost (page-level)
        // ------------------------------------------------------------

        public void Attach(IPageView page)
        {
            if (page == null)
                throw new ArgumentNullException(nameof(page));

            if (!(page.NativeView is Control c))
                throw new InvalidOperationException(
                    $"Page '{page.Name}' NativeView must be a WinForms Control.");

            if (!_root.Controls.Contains(c))
            {
                c.Dock = DockStyle.Fill;
                _root.Controls.Add(c);
            }

            c.Visible = true;

            if (page is IHostAttachable attachable)
                attachable.OnAttach(this);
        }

        public void Detach(IPageView page)
        {
            if (page == null)
                return;

            if (page.NativeView is Control c)
            {
                if (_root.Controls.Contains(c))
                    _root.Controls.Remove(c);
            }

            if (page is IHostAttachable attachable)
                attachable.OnDetach();
        }

        public void BringToFront(IPageView page)
        {
            if (page?.NativeView is Control c)
                c.BringToFront();
        }

        // ------------------------------------------------------------
        // IViewHost (raw view-level, overlays use this)
        // ------------------------------------------------------------

        public void AddView(object view)
        {
            if (!(view is Control c))
                throw new InvalidOperationException("Overlay view must be a Control.");

            if (!_root.Controls.Contains(c))
            {
                c.Dock = DockStyle.Fill;
                _root.Controls.Add(c);
            }

            c.Visible = true;
        }

        public void RemoveView(object view)
        {
            if (view is Control c && _root.Controls.Contains(c))
                _root.Controls.Remove(c);
        }

        public void BringToFront(object view)
        {
            if (view is Control c)
                c.BringToFront();
        }

        public void Focus(object view)
        {
            if (view is Control c && c.CanFocus)
                c.Focus();
        }

        public virtual Task OnNavigatedToAsync(NavigationArgs args) => Task.CompletedTask;

        public virtual Task OnNavigatedFromAsync() => Task.CompletedTask;
    }
}
