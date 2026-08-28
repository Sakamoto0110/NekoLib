// FILE: NekoLib.Navigation.WinForms/Defaults/DefaultLoadingMask.cs
using NekoLib.Navigation.Contracts.Pages;
using NekoLib.Navigation.Metadata;
using NekoLib.Navigation.Metadata.Attributes;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace NekoLib.Navigation.WinForms.Defaults
{
    /// <summary>
    /// Default loading mask. Renders as a borderless, dimmed UserControl so it can be
    /// safely parented to any host Panel (top-level Forms cannot be embedded that way).
    /// </summary>
    [PageMetadata(Name = "DefaultLoadingMask")]
    public class DefaultLoadingMask : UserControl, IGlobalLoadingMask
    {
        // Fulfill the IPageView contract manually.
        /// <inheritdoc />
        public object NativeView => this;
        /// <inheritdoc />
        public new bool IsDisposed => base.IsDisposed;

        private readonly Label _lblMessage;

        /// <summary>Initializes the built-in centered loading message overlay.</summary>
        public DefaultLoadingMask()
        {
            // Dimmed solid color; UserControl does not honor true transparency the
            // way Form.Opacity does, so we approximate the "scrim" with a dark tone.
            BackColor = Color.FromArgb(40, 40, 40);
            Dock = DockStyle.Fill;

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

        /// <inheritdoc />
        public Task OnOverlayOpenedAsync(object? payload)
        {
            _lblMessage.Text = payload?.ToString() ?? "Loading...";
            return Task.CompletedTask;
        }

        Task IPageOverlay.OnOverlayOpenedAsync(object? payload)
            => OnOverlayOpenedAsync(payload);

        /// <inheritdoc />
        public Task OnOverlayClosingAsync() => Task.CompletedTask;
    }
}
