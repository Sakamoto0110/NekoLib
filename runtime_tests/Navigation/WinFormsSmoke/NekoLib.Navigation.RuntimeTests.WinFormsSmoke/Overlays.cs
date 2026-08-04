using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using NekoLib.Navigation.WinForms.Hosting;

namespace NekoLib.Navigation.RuntimeTests.WinFormsSmoke
{
    /// <summary>
    /// Modal dialog (bool). Sized box, not full screen: the layered host docks every
    /// overlay to Fill on AddView and <see cref="DialogViewBase"/> undoes that and
    /// recentres itself at the constructor-defined size.
    /// </summary>
    public sealed class SampleDialog : DialogViewBase
    {
        public SampleDialog()
        {
            Size = new Size(380, 190);
            BackColor = Color.White;
            SurfaceChrome.Apply(this, Color.SteelBlue);

            var confirm = new Button { Text = "Confirmar", Width = 90, Margin = new Padding(0, 0, 8, 0) };
            var cancel = new Button { Text = "Cancelar", Width = 90 };
            confirm.Click += (_, __) => Confirm();
            cancel.Click += (_, __) => Cancel();

            Controls.Add(SurfaceChrome.Card(
                title: "Dialog modal",
                body: "Caixa centralizada (não tela cheia). Os botões funcionam mesmo " +
                      "com o fundo bloqueado.",
                field: null,
                buttons: new[] { confirm, cancel }));
        }
    }

    /// <summary>
    /// Modal prompt returning the typed text, or <c>null</c> on cancel. Focuses its
    /// own input from <c>OnShownAsync</c>, exactly like the WPF scenario's prompt, so
    /// both platforms are compared under the same conditions.
    /// </summary>
    public sealed class SamplePrompt : PromptViewBase<string>
    {
        private readonly TextBox _input;

        public SamplePrompt()
        {
            Size = new Size(400, 210);
            BackColor = Color.White;
            SurfaceChrome.Apply(this, Color.MediumPurple);

            _input = new TextBox { Width = 340, Margin = new Padding(0, 10, 0, 0) };

            var ok = new Button { Text = "OK", Width = 90, Margin = new Padding(0, 0, 8, 0) };
            var cancel = new Button { Text = "Cancelar", Width = 90 };
            ok.Click += (_, __) => CompletePrompt(_input.Text);
            cancel.Click += (_, __) => CompletePrompt(null);

            Controls.Add(SurfaceChrome.Card(
                title: "Digite algo:",
                body: null,
                field: _input,
                buttons: new[] { ok, cancel }));
        }

        protected override Task OnShownAsync(object payload)
        {
            // Deferred so it runs after PromptViewBase's own layout callback, which
            // undocks and recentres the surface.
            SurfaceChrome.Post(this, () => _input.Focus());
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Non-blocking toast, bottom-right.
    /// <para>
    /// NOTE: unlike the WPF <c>ToastViewBase</c>, which sets its own bottom-right
    /// alignment and margin in its constructor, the WinForms <c>ToastViewBase</c>
    /// carries no positioning logic. The layered host docks every added view to
    /// <c>Fill</c>, so a WinForms toast that does not undock itself covers the whole
    /// navigation host. This sample therefore does the undock/anchor work that the
    /// WPF base class does for you.
    /// </para>
    /// </summary>
    public sealed class SampleToast : ToastViewBase
    {
        public SampleToast()
        {
            Size = new Size(300, 70);
            BackColor = Color.FromArgb(40, 50, 60);

            Controls.Add(new Label
            {
                Text = "Toast no canto inferior-direito. Some em 3s ou clique para fechar.",
                Dock = DockStyle.Fill,
                ForeColor = Color.White,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(14, 0, 14, 0),
                Font = new Font("Segoe UI", 9F)
            });
        }

        // NAV-010: this class used to undock itself and place itself bottom-right
        // from ParentChanged, because the WinForms ToastViewBase never undid the
        // host's Dock=Fill and a stock toast covered the whole navigation host. The
        // base now anchors itself at BottomRight with a 20px DPI-scaled inset, so
        // that compensation is gone — and the toast step of this scenario now
        // actually exercises the base instead of the workaround.
        //
        // The label fills the toast, so clicking the text does NOT dismiss: WinForms
        // click events do not bubble to the container (NAV-007). Clicking the toast's
        // own background does, and the 3s timer always does.
    }

    /// <summary>
    /// Auto-dismiss popover anchored top-left. Has two focusable controls so tabbing
    /// between them must NOT dismiss it, and focuses its own field from
    /// <c>OnShownAsync</c> — the same compensation the WPF scenario applies — so the
    /// focus-loss contract is exercised identically on both platforms.
    /// </summary>
    public sealed class SamplePopover : AutoDismissPopoverBase
    {
        private readonly TextBox _field;

        public SamplePopover()
        {
            Size = new Size(300, 170);
            Location = new Point(24, 24);
            BackColor = Color.LightYellow;
            SurfaceChrome.Apply(this, Color.Goldenrod, thickness: 1);

            _field = new TextBox { Width = 260, Margin = new Padding(0, 0, 0, 8) };
            var close = new Button { Text = "Fechar", Width = 90 };
            close.Click += (_, __) => Complete(true);

            Controls.Add(SurfaceChrome.Card(
                title: "Popover (top-left)",
                // Light dismissal follows focus, not hit testing: clicking a control
                // that can take focus dismisses, clicking inert area does not.
                body: "Tab entre o campo e o botão NÃO fecha. Clicar em outro " +
                      "controle fecha; clicar em área inerte, não.",
                field: _field,
                buttons: new[] { close },
                padding: 12));
        }

        protected override Task OnShownAsync(object payload)
        {
            SurfaceChrome.Post(this, () => _field.Focus());
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Shared presentation helpers for the sample surfaces. WinForms has no
    /// equivalent of the WPF scenario's <c>Border</c> card, so the border is painted
    /// and the content laid out with a TableLayoutPanel.
    /// </summary>
    internal static class SurfaceChrome
    {
        /// <summary>
        /// Paints a flat border around a surface, mirroring the WPF card. The
        /// surface is padded by the border thickness so the Fill-docked content
        /// does not repaint over the border.
        /// </summary>
        public static void Apply(UserControl surface, Color border, int thickness = 2)
        {
            surface.Padding = new Padding(thickness);
            surface.Paint += (_, e) =>
            {
                using (var pen = new Pen(border, thickness))
                {
                    var inset = thickness / 2f;
                    e.Graphics.DrawRectangle(
                        pen,
                        inset,
                        inset,
                        surface.Width - thickness,
                        surface.Height - thickness);
                }
            };
            surface.Resize += (_, __) => surface.Invalidate();
        }

        /// <summary>
        /// Runs <paramref name="action"/> on the UI thread after the current message
        /// has been processed, tolerating a surface whose own handle does not exist
        /// yet (the parent's handle is used to marshal).
        /// </summary>
        public static void Post(Control surface, Action action)
        {
            try
            {
                surface.BeginInvoke(action);
            }
            catch (InvalidOperationException)
            {
                // No usable handle in the parent chain yet; nothing to defer to.
                action();
            }
        }

        public static Control Card(
            string title,
            string body,
            Control field,
            Button[] buttons,
            int padding = 16)
        {
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                Padding = new Padding(padding)
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            if (title != null)
            {
                layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                layout.Controls.Add(new Label
                {
                    Text = title,
                    AutoSize = true,
                    Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                    Margin = new Padding(0, 0, 0, 10)
                });
            }

            if (body != null)
            {
                layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                layout.Controls.Add(new Label
                {
                    Text = body,
                    AutoSize = true,
                    MaximumSize = new Size(320, 0),
                    Font = new Font("Segoe UI", 9F)
                });
            }

            if (field != null)
            {
                layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                layout.Controls.Add(field);
            }

            var buttonRow = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Anchor = AnchorStyles.Right,
                Margin = new Padding(0, 12, 0, 0)
            };
            foreach (var button in buttons)
                buttonRow.Controls.Add(button);

            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.Controls.Add(buttonRow);

            return layout;
        }
    }
}
