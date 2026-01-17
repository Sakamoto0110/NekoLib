using PageNav.Core.Abstractions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace PageNav.Wpf.Adapters
{
    public class PageMaskAdapter : IPageMask
    {
        private readonly Grid _overlay;
        private readonly TextBlock _text;

        public PageMaskAdapter(Panel host)
        {
            _overlay = new Grid
            {
                Background = new SolidColorBrush(Color.FromArgb(160, 0, 0, 0)),
                Visibility = Visibility.Collapsed
            };

            _text = new TextBlock
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                Foreground = Brushes.White,
                FontSize = 22,
                FontWeight = FontWeights.Bold
            };

            _overlay.Children.Add(_text);

            // Host should be: a Grid, DockPanel, Canvas, StackPanel, etc.
            host.Children.Add(_overlay);

            // Make overlay fill host
            if(host is Grid)
            {
                Grid.SetRowSpan(_overlay, int.MaxValue);
                Grid.SetColumnSpan(_overlay, int.MaxValue);
            }
        }

        public void Show(string message = "")
        {
            _text.Text = message;
            _overlay.Visibility = Visibility.Visible;
        }

        public void Hide()
        {
            _overlay.Visibility = Visibility.Collapsed;
        }
    }

}
