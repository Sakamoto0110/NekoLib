using NekoLib.Navigation.Contracts.Pages;
using NekoLib.Navigation.Metadata.Attributes;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace NekoLib.Navigation.Wpf.Defaults
{
    /// <summary>
    /// Default loading mask. Renders as a dimmed overlay that fills the host Panel and
    /// shows a centered message while a load is in progress.
    /// </summary>
    [PageMetadata(Name = "DefaultLoadingMask")]
    public class DefaultLoadingMask : UserControl, IGlobalLoadingMask
    {
        // Fulfill the IPageView contract manually (UserControl is not IDisposable).
        public object NativeView => this;
        public bool IsDisposed { get; private set; }

        private readonly TextBlock _message;

        public DefaultLoadingMask()
        {
            HorizontalAlignment = HorizontalAlignment.Stretch;
            VerticalAlignment = VerticalAlignment.Stretch;

            _message = new TextBlock
            {
                Foreground = Brushes.White,
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center
            };

            // Dimmed scrim filling the host.
            var root = new Grid
            {
                Background = new SolidColorBrush(Color.FromArgb(160, 0, 0, 0))
            };
            root.Children.Add(_message);

            Content = root;
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
