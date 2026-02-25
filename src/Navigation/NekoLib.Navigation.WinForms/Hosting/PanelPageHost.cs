// FILE: PageNav.WinForms/Hosting/PanelPageHost.cs

using NekoLib.Navigation.Contracts.Pages;
using NekoLib.Navigation.Contracts.Platform;
using NekoLib.Navigation.Metadata;
using System;
using System.ComponentModel.Design.Serialization;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace NekoLib.Navigation.WinForms.Hosting
{
    /// <summary>
    /// WinForms host that supports modal stacking.
    /// </summary>
    public class PanelPageHost
        : WinFormsPageHostBase, IModalHost, IPageView
    {
        private readonly Panel _modalOverlay;
        private readonly Panel _modalContainer;

        public string Name => Root.Name;
        

        public object NativeView => Root;

        public bool IsDisposed => Root.IsDisposed;

        public void Dispose()
        {
            Root?.Dispose();
        }
        public PanelPageHost(Panel root) : base(root)
        {
            // Modal overlay
            _modalOverlay = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(150, 0, 0, 0),
                Visible = false
            };

            _modalContainer = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent
            };

            _modalOverlay.Controls.Add(_modalContainer);
            Root.Controls.Add(_modalOverlay);
            _modalOverlay.BringToFront();
        }

        // ---------------------------------------------------------------------
        // Modal Support
        // ---------------------------------------------------------------------

        public Task ShowModalAsync(IPageView page)
        {
            if (!(page?.NativeView is Control control))
                throw new InvalidOperationException(
                    $"Modal page '{page?.Name}' must expose a WinForms Control NativeView.");

            control.Dock = DockStyle.Fill;

            _modalContainer.Controls.Add(control);
            _modalOverlay.Visible = true;
            _modalOverlay.BringToFront();

            return Task.CompletedTask;
        }

        public Task HideModalAsync(IPageView page)
        {
            if (!(page?.NativeView is Control control))
                return Task.CompletedTask;

            _modalContainer.Controls.Remove(control);

            if (_modalContainer.Controls.Count == 0)
            {
                _modalOverlay.Visible = false;
            }
            else
            {
                _modalContainer
                    .Controls[_modalContainer.Controls.Count - 1]
                    .BringToFront();
            }

            return Task.CompletedTask;
        }

        // Ensure modal overlay stays on top
        public override void BringToFront(IPageView page)
        {
            base.BringToFront(page);

            if (_modalOverlay.Visible)
                _modalOverlay.BringToFront();
        }

        public virtual Task OnNavigatedToAsync(NavigationArgs args)
            => Task.CompletedTask;

        public virtual Task OnNavigatedFromAsync()
            => Task.CompletedTask;
    }
}
