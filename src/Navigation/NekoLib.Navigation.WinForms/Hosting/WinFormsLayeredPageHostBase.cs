using NekoLib.Navigation.Contracts.Pages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace NekoLib.Navigation.WinForms.Hosting
{
    /// <summary>
    /// Managed host that layers content pages and transient surfaces (toasts, dialogs,
    /// prompts, the loading mask) inside a single Panel. Pages live at the bottom of the
    /// z-order; every other control is kept above them so surfaces remain visible.
    /// Nested host Forms are intentionally avoided to dodge WinForms transparency bugs.
    /// </summary>
    public class WinFormsLayeredPageHostBase : IPageHost, IViewHost
    {
        protected Control Root { get; }
        private readonly HashSet<Control> _pageControls =
            new HashSet<Control>();

        public WinFormsLayeredPageHostBase(Control root)
        {
            Root = root ?? throw new ArgumentNullException(nameof(root));
        }

        // ---------------------------------------------------------------------
        // IPageHost (content pages)
        // ---------------------------------------------------------------------

        public virtual void Attach(IPageView page)
        {
            if (!(page?.NativeView is Control control))
                throw new InvalidOperationException("Page NativeView must be a WinForms Control.");

            var added = !Root.Controls.Contains(control);
            if (added)
            {
                control.Dock = DockStyle.Fill;
                Root.Controls.Add(control);
            }

            _pageControls.Add(control);
            control.Visible = true;

            // Content pages are always the bottom layer; pushing them to the back keeps
            // any live surfaces (toasts/dialogs/prompts/mask) above them.
            control.SendToBack();

            if (added && page is IHostAttachable attachable)
                attachable.OnAttach(this);
        }

        public virtual void Detach(IPageView page)
        {
            if (page?.NativeView is not Control control)
                return;

            var wasAttached = Root.Controls.Contains(control);
            if (wasAttached)
                Root.Controls.Remove(control);

            _pageControls.Remove(control);

            if (wasAttached &&
                !Root.Controls.Contains(control) &&
                page is IHostAttachable attachable)
                attachable.OnDetach();
        }

        public virtual void BringToFront(IPageView page)
        {
            if (page?.NativeView is Control control)
            {
                // Promote the page, then re-assert surfaces so they stay on top.
                control.BringToFront();
                RestackOverlays();
            }
        }

        // ---------------------------------------------------------------------
        // IViewHost (transient surfaces: toasts, dialogs, prompts, mask)
        // ---------------------------------------------------------------------

        public virtual void AddView(object view)
        {
            if (view is Control control)
            {
                if (!Root.Controls.Contains(control))
                    Root.Controls.Add(control);

                control.Dock = DockStyle.Fill;
                control.Visible = true;
                control.BringToFront();
            }
        }

        public virtual void RemoveView(object view)
        {
            if (view is Control control && Root.Controls.Contains(control))
                Root.Controls.Remove(control);
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

        /// <summary>
        /// Re-asserts that every non-page surface stays above the content pages, even
        /// after a page has been brought to front.
        /// </summary>
        private void RestackOverlays()
        {
            // WinForms z-order: BringToFront moves a control to the visual top.
            var surfaces = Root.Controls.Cast<Control>()
                .Where(c => !_pageControls.Contains(c))
                .ToList();

            // ControlCollection enumerates front-to-back (index zero is top).
            // Reapply from the bottom up so the surface that was already top-most
            // remains top-most after promoting the content page.
            for (int i = surfaces.Count - 1; i >= 0; i--)
                surfaces[i].BringToFront();
        }
    }
}
