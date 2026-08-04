using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using NekoLib.Navigation.Wpf.Hosting;

namespace NekoLib.Navigation.RuntimeTests.WpfSmoke
{
    /// <summary>
    /// Modal dialog (bool). Sized box, NOT full-screen — validates that the host no
    /// longer force-stretches overlays (fix #2) and that the modal is interactive
    /// while the background is blocked (fix #1).
    /// </summary>
    public sealed class SampleDialog : DialogViewBase
    {
        public SampleDialog()
        {
            Width = 380;
            Height = 190;

            var ok = new Button { Content = "Confirmar", Width = 90, Margin = new Thickness(0, 0, 8, 0), IsDefault = true };
            var cancel = new Button { Content = "Cancelar", Width = 90, IsCancel = true };
            ok.Click += (_, __) => Confirm();
            cancel.Click += (_, __) => Cancel();

            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 16, 0, 0)
            };
            buttons.Children.Add(ok);
            buttons.Children.Add(cancel);

            var stack = new StackPanel();
            stack.Children.Add(new TextBlock { Text = "Dialog modal", FontSize = 16, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 10) });
            stack.Children.Add(new TextBlock { Text = "Caixa centralizada (não tela cheia). Os botões funcionam mesmo com o fundo bloqueado.", TextWrapping = TextWrapping.Wrap });
            stack.Children.Add(buttons);

            Content = Card(stack, Brushes.White, Brushes.SteelBlue);
        }

        internal static Border Card(UIElement child, Brush bg, Brush border) => new Border
        {
            Background = bg,
            BorderBrush = border,
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(16),
            Child = child
        };
    }

    /// <summary>Modal prompt returning the typed text (null on cancel). Sized box.</summary>
    public sealed class SamplePrompt : PromptViewBase<string>
    {
        private readonly TextBox _input;

        public SamplePrompt()
        {
            Width = 400;
            Height = 210;

            _input = new TextBox { Margin = new Thickness(0, 10, 0, 0), Padding = new Thickness(4) };

            var ok = new Button { Content = "OK", Width = 90, Margin = new Thickness(0, 0, 8, 0), IsDefault = true };
            var cancel = new Button { Content = "Cancelar", Width = 90, IsCancel = true };
            ok.Click += (_, __) => CompletePrompt(_input.Text);
            cancel.Click += (_, __) => CompletePrompt(null);

            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 16, 0, 0)
            };
            buttons.Children.Add(ok);
            buttons.Children.Add(cancel);

            var stack = new StackPanel();
            stack.Children.Add(new TextBlock { Text = "Digite algo:", FontSize = 16, FontWeight = FontWeights.Bold });
            stack.Children.Add(_input);
            stack.Children.Add(buttons);

            Content = SampleDialog.Card(stack, Brushes.White, Brushes.MediumPurple);
        }

        protected override Task OnShownAsync(object payload)
        {
            Dispatcher.BeginInvoke(new Action(() => _input.Focus()), DispatcherPriority.Input);
            return Task.CompletedTask;
        }
    }

    /// <summary>Non-blocking toast, bottom-right (ToastViewBase default). Small box — validates fix #2.</summary>
    public sealed class SampleToast : ToastViewBase
    {
        public SampleToast()
        {
            Width = 300;
            Height = 70;

            Content = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(40, 50, 60)),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(14),
                Child = new TextBlock
                {
                    Text = "Toast no canto inferior-direito. Some em 3s ou clique para fechar.",
                    Foreground = Brushes.White,
                    TextWrapping = TextWrapping.Wrap,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
        }
    }

    /// <summary>
    /// Auto-dismiss popover, anchored top-left (NOT centered / full-screen). Has two
    /// focusable controls so tabbing between them does NOT dismiss it (fix #3);
    /// clicking outside the popover dismisses it.
    /// </summary>
    public sealed class SamplePopover : AutoDismissPopoverBase
    {
        private readonly TextBox _field;

        public SamplePopover()
        {
            Width = 300;
            Height = 170;
            HorizontalAlignment = HorizontalAlignment.Left;
            VerticalAlignment = VerticalAlignment.Top;
            Margin = new Thickness(24, 24, 0, 0);

            _field = new TextBox { Margin = new Thickness(0, 0, 0, 8), Padding = new Thickness(4) };
            var ok = new Button { Content = "Fechar", Width = 90, HorizontalAlignment = HorizontalAlignment.Left };
            ok.Click += (_, __) => Complete(true);

            var stack = new StackPanel();
            stack.Children.Add(new TextBlock { Text = "Popover (top-left)", FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 6) });
            // Light dismissal follows focus, not hit testing: clicking a control that
            // can take focus dismisses, clicking inert area does not.
            stack.Children.Add(new TextBlock { Text = "Tab entre o campo e o botão NÃO fecha. Clicar em outro controle fecha; clicar em área inerte, não.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8) });
            stack.Children.Add(_field);
            stack.Children.Add(ok);

            Content = new Border
            {
                Background = Brushes.LightYellow,
                BorderBrush = Brushes.Goldenrod,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(12),
                Child = stack
            };
        }

        protected override Task OnShownAsync(object payload)
        {
            // Put keyboard focus inside the popover so LostKeyboardFocus only fires
            // when focus actually leaves it (exercises the subtree check in fix #3).
            Dispatcher.BeginInvoke(new Action(() => _field.Focus()), DispatcherPriority.Input);
            return Task.CompletedTask;
        }
    }
}
