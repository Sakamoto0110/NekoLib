using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using NekoLib.Navigation.Contracts.Pages;
using NekoLib.Navigation.Metadata.Attributes;

namespace NekoLib.Navigation.Wpf.Defaults
{
    /// <summary>
    /// Default WPF loading mask. Dimmed scrim with a centered message; participates
    /// in the IPageOverlay lifecycle so the runtime can set the message via payload.
    /// </summary>
    [PageMetadata(Name = "DefaultLoadingMask")]
    public class DefaultLoadingMask : UserControl, IGlobalLoadingMask
    {
        /// <inheritdoc />
        public object NativeView => this;
        /// <inheritdoc />
        public bool IsDisposed { get; private set; }

        private readonly TextBlock _message;

        /// <summary>Initializes the built-in centered loading message overlay.</summary>
        public DefaultLoadingMask()
        {
            Background = new SolidColorBrush(Color.FromArgb(160, 40, 40, 40));
            Focusable = false;

            _message = new TextBlock
            {
                Foreground = Brushes.White,
                FontFamily = new FontFamily("Verdana"),
                FontSize = 22,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                Text = "Loading..."
            };

            Content = _message;
        }

        /// <inheritdoc />
        public Task OnOverlayOpenedAsync(object? payload)
        {
            _message.Text = payload?.ToString() ?? "Loading...";
            return Task.CompletedTask;
        }

        Task IPageOverlay.OnOverlayOpenedAsync(object? payload)
            => OnOverlayOpenedAsync(payload);

        /// <inheritdoc />
        public Task OnOverlayClosingAsync() => Task.CompletedTask;

        /// <summary>Marks the lightweight overlay disposed; it owns no unmanaged resources.</summary>
        public void Dispose() => IsDisposed = true;
    }
}
