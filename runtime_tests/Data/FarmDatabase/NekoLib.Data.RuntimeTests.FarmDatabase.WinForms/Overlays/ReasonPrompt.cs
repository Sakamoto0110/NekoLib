using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Threading.Tasks;
using System.Windows.Forms;
using NekoLib.Data.RuntimeTests.FarmDatabase.Core.Model;
using NekoLib.Data.RuntimeTests.FarmDatabase.WinForms.Theme;

namespace NekoLib.Data.RuntimeTests.FarmDatabase.WinForms.Overlays
{
    /// <summary>
    /// Blocking prompt that collects the reason for removing an animal. Returns the
    /// reason text, or <c>null</c> when the user backs out.
    /// <para/>
    /// Derives from <see cref="ReasonPromptBase"/> rather than
    /// <c>PromptViewBase&lt;string&gt;</c> so the designer can open it - see the shim
    /// for why.
    /// </summary>
    public partial class ReasonPrompt : ReasonPromptBase
    {
        private static readonly string[] Presets =
        {
            "Abate programado",
            "Venda para outro produtor",
            "Morte natural",
            "Doença / recomendação veterinária",
            "Descarte por queda de produtividade",
            "Outro (descrever abaixo)"
        };

        public ReasonPrompt()
        {
            InitializeComponent();
            ApplyTheme();

            _presetCombo.Items.AddRange(Presets);
            _presetCombo.SelectedIndexChanged += OnPresetChanged;

            _confirmButton.Click += (s, e) => Confirm();
            _cancelButton.Click += (s, e) => CompletePrompt(null);

            _reasonBox.TextChanged += (s, e) => UpdateConfirmState();

            // Escape backs out, Ctrl+Enter confirms - the same gestures the rest of
            // the app uses.
            KeyPreview();
            UpdateConfirmState();
        }

        private void ApplyTheme()
        {
            FarmTheme.StyleCombo(_presetCombo);
            FarmTheme.StyleInput(_reasonBox);
            BackColor = FarmTheme.Surface;
        }

        private void KeyPreview()
        {
            KeyDown += OnKeyDown;
            _reasonBox.KeyDown += OnKeyDown;
            _presetCombo.KeyDown += OnKeyDown;
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                e.SuppressKeyPress = true;
                CompletePrompt(null);
                return;
            }

            if (e.Control && e.KeyCode == Keys.Enter && _confirmButton.Enabled)
            {
                e.SuppressKeyPress = true;
                Confirm();
            }
        }

        /// <summary>
        /// Receives the animal being removed. The prompt service passes whatever the
        /// caller handed to <c>ShowPromptAsync</c>.
        /// </summary>
        protected override Task OnShownAsync(object payload)
        {
            if (payload is Animal animal)
            {
                _subject.Text = animal.Tag + "  ·  " + animal.Species + "  ·  " +
                    animal.Gender + "  ·  " + animal.AgeYears + " ano(s)";
            }

            _reasonBox.Focus();
            return Task.CompletedTask;
        }

        private void OnPresetChanged(object sender, EventArgs e)
        {
            string preset = _presetCombo.SelectedItem as string;
            if (preset == null) return;

            // The catch-all leaves the box empty so the operator has to type something
            // specific; the others prefill and stay editable.
            _reasonBox.Text = preset == Presets[Presets.Length - 1] ? string.Empty : preset;
            _reasonBox.Focus();
            _reasonBox.SelectionStart = _reasonBox.TextLength;
        }

        private void UpdateConfirmState()
        {
            _confirmButton.Enabled = !string.IsNullOrWhiteSpace(_reasonBox.Text);
        }

        private void Confirm()
        {
            string reason = _reasonBox.Text?.Trim();
            if (string.IsNullOrEmpty(reason)) return;

            CompletePrompt(reason);
        }

        /// <summary>Draws the card border so the prompt reads as a raised surface.</summary>
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var bounds = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath path = Shapes.Rounded(bounds, 8))
            using (var pen = new Pen(FarmTheme.Border, 1))
            {
                g.DrawPath(pen, path);
            }

            using (var accent = new SolidBrush(FarmTheme.Danger))
            {
                g.FillRectangle(accent, 0, 0, 4, Height);
            }
        }
    }
}
