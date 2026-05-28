using NekoLib.Navigation.Contracts.Pages;
using NekoLib.Navigation.Contracts.Platform;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace NekoLib.Navigation.WinForms.Hosting
{
    /// <summary>
    /// Managed host that simulates layers (Content, Modal, Overlay) 
    /// without nested panels to avoid WinForms transparency bugs.
    /// </summary>
    public class WinFormsLayeredPageHostBase : IPageHost, IViewHost
    {
        protected Control Root { get; }
        private readonly Dictionary<object, (Form Host, Form Mask)> _activeOverlays = new();
        public WinFormsLayeredPageHostBase(Control root)
        {
            Root = root ?? throw new ArgumentNullException(nameof(root));
        }

        // ---------------------------------------------------------------------
        // IPageHost (Standard Pages)
        // ---------------------------------------------------------------------

        public virtual void Attach(IPageView page)
        {
            if (!(page?.NativeView is Control control))
                throw new InvalidOperationException("Page NativeView must be a WinForms Control.");

            if (!Root.Controls.Contains(control))
            {
                control.Dock = DockStyle.Fill;
                Root.Controls.Add(control);
            }

            control.Visible = true;

            // Standard pages are always the "bottom" layer.
            // Pushing to back ensures they don't cover existing modals/overlays.
            control.SendToBack();

            if (page is IHostAttachable attachable)
                attachable.OnAttach(this);
        }

        public virtual void Detach(IPageView page)
        {
            if (page?.NativeView is Control control && Root.Controls.Contains(control))
            {
                Root.Controls.Remove(control);
            }

            if (page is IHostAttachable attachable)
                attachable.OnDetach();
        }

        public virtual void BringToFront(IPageView page)
        {
            if (page?.NativeView is Control control)
            {
                // To maintain "Content" layer status, we bring it to front 
                // but then immediately re-stack any active overlays on top of it.
                control.BringToFront();
                RestackOverlays();
            }
        }

        // ---------------------------------------------------------------------
        // IViewHost (Modals and Overlays)
        // ---------------------------------------------------------------------

        private readonly Dictionary<object, (Form Mask, Form Host)> _popupWrappers = new();

        // 2. Update AddView and RemoveView
        public virtual void AddView(object view)
        {
            // 1. Standard Page Replacement (No special handling)
            if (view is Control ctrl && !(view is IPageOverlay))
            {
                if (!Root.Controls.Contains(ctrl)) Root.Controls.Add(ctrl);
                ctrl.Dock = DockStyle.Fill;
                ctrl.Visible = true;
                ctrl.BringToFront();
                return;
            }

            // 2. Overlays & Dialogs
            if (view is Control overlayControl && view is IPageOverlay)
            {
                var parentForm = Root.FindForm();
                Form mask = null;

                // ONLY create a mask if the descriptor says it's Modal
                // (You can check this via reflection or by passing a flag from the Runtime)
                 
                if (view is IModalView modal)
                {
                    mask = new Form
                    {
                        FormBorderStyle = FormBorderStyle.None,
                        StartPosition = FormStartPosition.Manual,
                        BackColor = Color.Black,
                        Opacity = 0.5,
                        Bounds = Root.RectangleToScreen(Root.ClientRectangle),
                        ShowInTaskbar = false,
                        Dock = DockStyle.Fill,
                        TopLevel = false
                    };
                    mask.Show(parentForm);
                }

                // Create the floating host Form for the UserControl
                var host = new Form
                {
                    FormBorderStyle = FormBorderStyle.None,
                    StartPosition = FormStartPosition.Manual,
                    Size = overlayControl.Size,
                    ShowInTaskbar = false,
                    BackColor = overlayControl.BackColor,
                    Dock = DockStyle.Fill,
                    
                };

                //overlayControl.Dock = DockStyle.Fill;
                Root.Controls.Add(mask);

                Root.Controls.Add(overlayControl);

                // Center it relative to the Root panel
                var screenRect = Root.RectangleToScreen(Root.ClientRectangle);
                host.Location = new Point(
                    screenRect.Left + (screenRect.Width - host.Width) / 2,
                    screenRect.Top + (screenRect.Height - host.Height) / 2
                );

                _activeOverlays[view] = (host, mask);

                // Show the host (above the mask if it exists)
                //host.Show(mask ?? parentForm);
                 //Root.Controls.Add(host);
            }
        }

        public virtual void RemoveView(object view)
        {
            if (_activeOverlays.TryGetValue(view, out var pair))
            {
                pair.Host.Close();
                pair.Host.Dispose();

                if (pair.Mask != null)
                {
                    pair.Mask.Close();
                    pair.Mask.Dispose();
                }

                _activeOverlays.Remove(view);
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

        /// <summary>
        /// Ensures that any non-page views (modals/toasts) stay above 
        /// standard pages even if a page is "brought to front".
        /// </summary>
        private void RestackOverlays()
        {
            // WinForms Z-order: Index 0 is the "top".
            // We need to ensure that anything in the 'Modal' or 'Overlay' logical layers
            // is moved back to the front of the Root.Controls collection.
            var overlays = Root.Controls.Cast<Control>()
                .Where(c => c is not PageView) // Simplified check
                .ToList();

            foreach (var overlay in overlays)
            {
                overlay.BringToFront();
            }
        }
    }
}