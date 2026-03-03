using NekoLib.Navigation.Contracts.Pages;
using NekoLib.Navigation.Contracts.Platform;
using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace NekoLib.Navigation.WinForms.Hosting
{


  

    public class WinFormsLayeredPageHostBase : IPageHost, IViewHost
    {
        protected Control Root { get; }

        protected Panel ContentLayer { get; }
        protected Panel ModalLayer { get; }
        protected Panel OverlayLayer { get; }

        public   WinFormsLayeredPageHostBase(Control root)
        {
            Root = root ?? throw new ArgumentNullException(nameof(root));

            // Create layers once
            ContentLayer = CreateLayer("ContentLayer");
             
            ModalLayer = new Panel
            {
                Name = "ModalLayer",
                Dock = DockStyle.Fill,
                Visible = false
            };

            OverlayLayer = new Panel
            {
                Name = "OverlayLayer",
                Dock = DockStyle.Fill,
                Visible = false
            };
            // Add in correct order (bottom -> top)
            Root.Controls.Add(ContentLayer);
            Root.Controls.Add(ModalLayer);
            Root.Controls.Add(OverlayLayer);

            ContentLayer.BringToFront(); // not strictly needed, but keeps intent clear
            ModalLayer.BringToFront();
            OverlayLayer.BringToFront();
        }

        private static Panel CreateLayer(string name)
            => new Panel
            {
                Name = name,
                 
                Visible = true,
                BackColor = Color.Transparent // optional, helps visually
            };

        // ---------------------------------------------------------------------
        // IPageHost
        // ---------------------------------------------------------------------

        public virtual void Attach(IPageView page)
        {
            if (!(page?.NativeView is Control control))
                throw new InvalidOperationException(
                    $"Page '{page?.Name}' NativeView must be a WinForms Control.");

            var layer = (page is IModalView) ? ModalLayer : ContentLayer;

            if (!layer.Controls.Contains(control))
            {
                control.Dock = DockStyle.Fill;
                layer.Controls.Add(control);
            }

            control.Visible = true;
            control.BringToFront();

            // Ensure top layers remain on top
          //  OverlayLayer.BringToFront();
          //  ModalLayer.BringToFront();

            if (page is IHostAttachable attachable)
                attachable.OnAttach(this);
        }

        public virtual void Detach(IPageView page)
        {
            if (page?.NativeView is Control control)
            {
                // Remove from whichever layer contains it
                if (ContentLayer.Controls.Contains(control))
                    ContentLayer.Controls.Remove(control);

                if (ModalLayer.Controls.Contains(control))
                    ModalLayer.Controls.Remove(control);
            }

            if (page is IHostAttachable attachable)
                attachable.OnDetach();
        }

        public virtual void BringToFront(IPageView page)
        {
            if (page?.NativeView is Control control)
            {
                control.BringToFront();
                OverlayLayer.BringToFront();
            }
        }

        // ---------------------------------------------------------------------
        // IViewHost (Overlays, HUD, blockers...)
        // ---------------------------------------------------------------------

        public virtual void AddView(object view)
        {
            if (!(view is Control control))
                throw new InvalidOperationException("Overlay view must be a WinForms Control.");

            if (!OverlayLayer.Controls.Contains(control))
            {
                control.Dock = DockStyle.Fill;
                //OverlayLayer.Controls.Add(control);
            }

            control.Visible = true;
            control.BringToFront();
           // OverlayLayer.BringToFront();
        }

        public virtual void RemoveView(object view)
        {
            if (view is Control control && OverlayLayer.Controls.Contains(control))
                OverlayLayer.Controls.Remove(control);
        }

        public virtual void BringToFront(object view)
        {
            if (view is Control control)
            {
                control.BringToFront();
               // OverlayLayer.BringToFront();
            }
        }

        public virtual void Focus(object view)
        {
            if (view is Control control && control.CanFocus)
                control.Focus();
        }

        // ---------------------------------------------------------------------
        // Debug helpers (makes testing easy)
        // ---------------------------------------------------------------------

        public (int content, int modals, int overlays) GetLayerCounts()
            => (ContentLayer.Controls.Count, ModalLayer.Controls.Count, OverlayLayer.Controls.Count);

        public Control[] GetModalStackTopFirst()
            => ModalLayer.Controls.Cast<Control>().Reverse().ToArray();
    }
}