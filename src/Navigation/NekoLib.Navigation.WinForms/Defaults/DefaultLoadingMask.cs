// FILE: NekoLib.Navigation.WinForms/Defaults/DefaultLoadingMask.cs
using NekoLib.Navigation.Contracts.Pages;
using NekoLib.Navigation.Metadata;
using NekoLib.Navigation.Metadata.Attributes;
using NekoLib.Navigation.WinForms.Hosting;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace NekoLib.Navigation.WinForms.Defaults
{
    [PageMetadata(Name = "DefaultLoadingMask", Presentation = PagePresentationMode.ModalOverlay)]
    public class DefaultLoadingMask : Form, IGlobalLoadingMask
    {
        // Fulfill the IPageView contract manually since we aren't using the PageView base
        public object NativeView => this;
        public new bool IsDisposed => base.IsDisposed;

        private Label _lblMessage;

        public DefaultLoadingMask()
        {
            // WinForms Magic for true transparency
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            BackColor = Color.Black;
            Opacity = 0.7; // This dims the page behind it!

            _lblMessage = new Label
            {
                ForeColor = Color.White,
                Font = new Font("Verdana", 14, FontStyle.Bold),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            };
            Controls.Add(_lblMessage);
        }

        public Task OnOverlayOpenedAsync(object payload)
        {
            _lblMessage.Text = payload?.ToString() ?? "Carregando...";
            return Task.CompletedTask;
        }

        public Task OnOverlayClosingAsync() => Task.CompletedTask;
    }
}