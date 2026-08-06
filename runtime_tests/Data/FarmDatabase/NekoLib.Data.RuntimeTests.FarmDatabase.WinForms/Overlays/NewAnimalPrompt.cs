using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using NekoLib.Data.RuntimeTests.FarmDatabase.Core.Model;
using NekoLib.Data.RuntimeTests.FarmDatabase.Core.Schema;
using NekoLib.Data.RuntimeTests.FarmDatabase.WinForms.Theme;

namespace NekoLib.Data.RuntimeTests.FarmDatabase.WinForms.Overlays
{
    /// <summary>
    /// Collects the details of a new animal. Returns the request, or <c>null</c> when
    /// the user backs out.
    /// <para/>
    /// Notably it does not collect a tag: the database assigns one from a persisted
    /// counter, inside the same transaction as the insert, so the number can never be
    /// one a removed animal used.
    /// </summary>
    public partial class NewAnimalPrompt : NewAnimalPromptBase
    {
        public NewAnimalPrompt()
        {
            InitializeComponent();
            ApplyTheme();

            foreach (string species in FarmSeed.Species)
                _speciesCombo.Items.Add(species);

            foreach (string gender in FarmSeed.Genders)
                _genderCombo.Items.Add(gender);

            if (_speciesCombo.Items.Count > 0) _speciesCombo.SelectedIndex = 0;
            if (_genderCombo.Items.Count > 0) _genderCombo.SelectedIndex = 0;

            _confirmButton.Click += (s, e) => Confirm();
            _cancelButton.Click += (s, e) => CompletePrompt(null);

            KeyDown += OnKeyDown;
            _notesBox.KeyDown += OnKeyDown;
            _speciesCombo.KeyDown += OnKeyDown;
            _genderCombo.KeyDown += OnKeyDown;

            _speciesCombo.SelectedIndexChanged += (s, e) => UpdateSubtitle();
            UpdateSubtitle();
        }

        private void ApplyTheme()
        {
            FarmTheme.StyleCombo(_speciesCombo);
            FarmTheme.StyleCombo(_genderCombo);
            FarmTheme.StyleInput(_notesBox);

            _ageValue.BackColor = FarmTheme.SurfaceAlt;
            _ageValue.ForeColor = FarmTheme.TextPrimary;
            BackColor = FarmTheme.Surface;
        }

        /// <summary>
        /// Shows which prefix the selected species will draw from, so the assigned tag
        /// is not a surprise even though the operator cannot choose it.
        /// </summary>
        private void UpdateSubtitle()
        {
            string species = _speciesCombo.SelectedItem as string;
            if (string.IsNullOrEmpty(species))
            {
                _subtitle.Text = "O brinco é atribuído pelo banco.";
                return;
            }

            _subtitle.Text = "O brinco é atribuído pelo banco, com prefixo " +
                FarmSeed.PrefixFor(species) + " e sem reaproveitar números.";
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                e.SuppressKeyPress = true;
                CompletePrompt(null);
                return;
            }

            if (e.Control && e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                Confirm();
            }
        }

        private void Confirm()
        {
            string species = _speciesCombo.SelectedItem as string;
            string gender = _genderCombo.SelectedItem as string;

            if (string.IsNullOrEmpty(species) || string.IsNullOrEmpty(gender))
                return;

            string notes = _notesBox.Text?.Trim();

            CompletePrompt(new NewAnimalRequest
            {
                Species = species,
                Gender = gender,
                AgeYears = (int)_ageValue.Value,
                Notes = string.IsNullOrEmpty(notes) ? null : notes
            });
        }

        /// <summary>Card border, with an accent bar matching the additive intent.</summary>
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

            using (var accent = new SolidBrush(FarmTheme.Accent))
            {
                g.FillRectangle(accent, 0, 0, 4, Height);
            }
        }
    }
}
