using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using NekoLib.Navigation.Metadata.Attributes;
using NekoLib.Navigation.Wpf.Hosting;

namespace NekoLib.Navigation.RuntimeTests.WpfSmoke
{
    /// <summary>
    /// Idle page. <c>[PageTimeout(20)]</c> declares the inactivity timeout in
    /// seconds (the feature added this session): after 20s without input the
    /// runtime returns here. Program also marks the role with SetIdle&lt;IdlePage&gt;().
    /// </summary>
    [PageTimeout(20)]
    public sealed class IdlePage : PageView
    {
        public IdlePage()
        {
            var stack = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            stack.Children.Add(new TextBlock
            {
                Text = "IDLE PAGE",
                Foreground = Brushes.White,
                FontSize = 30,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center
            });
            stack.Children.Add(new TextBlock
            {
                Text = "20s sem interação -> o app volta pra cá (idle timeout via [PageTimeout(20)]).",
                Foreground = Brushes.Gainsboro,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 440,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 14, 0, 0)
            });

            Content = new Grid
            {
                Background = new SolidColorBrush(Color.FromRgb(25, 35, 70)),
                Children = { stack }
            };
        }
    }

    /// <summary>
    /// Interactive page used to demonstrate the interaction blocker: while a modal
    /// dialog/prompt is open, this page (a child of the navigation host) is disabled,
    /// so the counter button greys out — while the dialog's own buttons stay live.
    /// </summary>
    public sealed class DashboardPage : PageView
    {
        private int _count;
        private readonly Button _counter;

        public DashboardPage()
        {
            _counter = new Button
            {
                Content = "Cliques: 0",
                Padding = new Thickness(18, 10, 18, 10),
                Margin = new Thickness(0, 14, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            _counter.Click += (_, __) =>
            {
                _count++;
                _counter.Content = "Cliques: " + _count;
            };

            var stack = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            stack.Children.Add(new TextBlock
            {
                Text = "DASHBOARD",
                FontSize = 26,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center
            });
            stack.Children.Add(_counter);
            stack.Children.Add(new TextBlock
            {
                Text = "Abra um Dialog/Prompt: este botão fica DESABILITADO (blocker). " +
                       "Os botões do painel à esquerda ficam fora do host, então continuam ativos.",
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 440,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 14, 0, 0)
            });

            Content = new Grid
            {
                Background = new SolidColorBrush(Color.FromRgb(244, 246, 249)),
                Children = { stack }
            };
        }
    }
}
