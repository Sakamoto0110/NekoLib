// FILE: PageNav.WinForms/Hosting/WinFormsPageHostBase.cs

using NekoLib.Navigation.Contracts.Pages;
using NekoLib.Navigation.Contracts.Platform;
using System;
using System.Windows.Forms;

namespace NekoLib.Navigation.WinForms.Hosting
{
    /// <summary>
    /// Base class for WinForms page hosts.
    /// Infrastructure only — NEVER a page.
    /// </summary>
    public abstract class WinFormsPageHostBase : IPageHost, IViewHost
    {
        protected Control Root { get; }

        protected WinFormsPageHostBase(Control root)
        {
            Root = root ?? throw new ArgumentNullException(nameof(root));
        }

        // ---------------------------------------------------------------------
        // IPageHost
        // ---------------------------------------------------------------------

        public virtual void Attach(IPageView page)
        {
            if (!(page?.NativeView is Control control))
                throw new InvalidOperationException(
                    $"Page '{page?.Name}' NativeView must be a WinForms Control.");

            if (!Root.Controls.Contains(control))
            {
                control.Dock = DockStyle.Fill;
                Root.Controls.Add(control);
            }

            control.Visible = true;

            if (page is IHostAttachable attachable)
                attachable.OnAttach(this);
        }

        public virtual void Detach(IPageView page)
        {
            if (page?.NativeView is Control control &&
                Root.Controls.Contains(control))
            {
                Root.Controls.Remove(control);
            }

            if (page is IHostAttachable attachable)
                attachable.OnDetach();
        }

        public virtual void BringToFront(IPageView page)
        {
            if (page?.NativeView is Control control)
                control.BringToFront();
        }

        // ---------------------------------------------------------------------
        // IViewHost
        // ---------------------------------------------------------------------

        public virtual void AddView(object view)
        {
            if (!(view is Control control))
                throw new InvalidOperationException(
                    "Overlay view must be a WinForms Control.");

            if (!Root.Controls.Contains(control))
            {
                control.Dock = DockStyle.Fill;
                Root.Controls.Add(control);
            }

            control.Visible = true;
            control.BringToFront();
        }

        public virtual void RemoveView(object view)
        {
            if (view is Control control &&
                Root.Controls.Contains(control))
            {
                Root.Controls.Remove(control);
            }
        }

        public virtual void BringToFront(object view)
        {
            if (view is Control control)
                control.BringToFront();
        }

        public virtual void Focus(object view)
        {
            if (view is Control control && control.CanFocus)
                control.Focus();
        }
    }
}
