using System.Drawing;
using System.Windows.Forms;
using NekoLib.Navigation.Metadata.Attributes;
using NekoLib.Navigation.WinForms.Hosting;

namespace NekoLib.Navigation.RuntimeTests.WinFormsSmoke
{
    /// <summary>
    /// Idle page. <c>[PageTimeout(20)]</c> declares the inactivity timeout in
    /// seconds; after 20s without input inside the navigation host the runtime
    /// signs out and returns here. Program also marks the role explicitly with
    /// <c>SetIdle&lt;IdlePage&gt;()</c>. Mirrors the WPF smoke scenario's IdlePage.
    /// </summary>
    [PageTimeout(20)]
    public sealed class IdlePage : PageView
    {
        public IdlePage()
        {
            BackColor = Color.FromArgb(25, 35, 70);

            var title = new Label
            {
                Text = "IDLE PAGE",
                AutoSize = true,
                Anchor = AnchorStyles.None,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 22F, FontStyle.Bold)
            };

            var subtitle = new Label
            {
                Text = "20s sem interação -> o app volta pra cá (idle timeout via [PageTimeout(20)]).",
                AutoSize = true,
                Anchor = AnchorStyles.None,
                MaximumSize = new Size(440, 0),
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Color.Gainsboro,
                Margin = new Padding(0, 14, 0, 0),
                Font = new Font("Segoe UI", 9F)
            };

            Controls.Add(PageLayout.Centered(title, subtitle));
        }
    }

    /// <summary>
    /// Interactive page used to demonstrate the interaction blocker: while a modal
    /// dialog or prompt is open this page is a child of the navigation host, so it
    /// is disabled and the counter button greys out, while the dialog's own buttons
    /// stay live. The buttons in the left panel sit outside the host and stay
    /// active. Mirrors the WPF smoke scenario's DashboardPage.
    /// </summary>
    public sealed class DashboardPage : PageView
    {
        private int _count;
        private readonly Button _counter;

        public DashboardPage()
        {
            BackColor = Color.FromArgb(244, 246, 249);

            var title = new Label
            {
                Text = "DASHBOARD",
                AutoSize = true,
                Anchor = AnchorStyles.None,
                Font = new Font("Segoe UI", 18F, FontStyle.Bold)
            };

            _counter = new Button
            {
                Text = "Cliques: 0",
                AutoSize = true,
                Anchor = AnchorStyles.None,
                Padding = new Padding(18, 10, 18, 10),
                Margin = new Padding(0, 14, 0, 0)
            };
            _counter.Click += (_, __) =>
            {
                _count++;
                _counter.Text = "Cliques: " + _count;
            };

            var hint = new Label
            {
                Text = "Abra um Dialog/Prompt: este botão fica DESABILITADO (blocker). " +
                       "Os botões do painel à esquerda ficam fora do host, então continuam ativos.",
                AutoSize = true,
                Anchor = AnchorStyles.None,
                MaximumSize = new Size(440, 0),
                TextAlign = ContentAlignment.MiddleCenter,
                Margin = new Padding(0, 14, 0, 0),
                Font = new Font("Segoe UI", 9F)
            };

            Controls.Add(PageLayout.Centered(title, _counter, hint));
        }
    }

    /// <summary>
    /// Small layout helper. WinForms has no direct equivalent of the WPF
    /// "centered StackPanel", so a single-column TableLayoutPanel with
    /// <c>Anchor = None</c> children is used to centre content in the host.
    /// </summary>
    internal static class PageLayout
    {
        public static Control Centered(params Control[] children)
        {
            var inner = new TableLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Anchor = AnchorStyles.None,
                ColumnCount = 1,
                RowCount = children.Length
            };
            inner.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            foreach (var child in children)
            {
                inner.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                inner.Controls.Add(child);
            }

            var outer = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3
            };
            outer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            outer.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            outer.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            outer.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            outer.Controls.Add(inner, 0, 1);

            return outer;
        }
    }
}
