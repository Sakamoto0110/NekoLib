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
        public object NativeView => this;
        public bool IsDisposed { get; private set; }

        private readonly TextBlock _message;

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

        public Task OnOverlayOpenedAsync(object payload)
        {
            _message.Text = payload?.ToString() ?? "Loading...";
            return Task.CompletedTask;
        }

        public Task OnOverlayClosingAsync() => Task.CompletedTask;

        public void Dispose() => IsDisposed = true;
    }
}
