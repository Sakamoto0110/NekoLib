using System.Drawing;
using System.Windows.Forms;

namespace NekoLib.Data.RuntimeTests.FarmDatabase.WinForms.Theme
{
    /// <summary>
    /// Central palette and typography. Everything visual reads from here so the app
    /// stays coherent across pages, and so a page added later cannot drift.
    /// </summary>
    public static class FarmTheme
    {
        // --- surfaces -----------------------------------------------------
        public static readonly Color Canvas      = Color.FromArgb(0x12, 0x17, 0x1A);
        public static readonly Color Sidebar     = Color.FromArgb(0x0D, 0x11, 0x14);
        public static readonly Color Surface     = Color.FromArgb(0x1A, 0x21, 0x26);
        public static readonly Color SurfaceAlt  = Color.FromArgb(0x20, 0x29, 0x2F);
        public static readonly Color Border      = Color.FromArgb(0x2B, 0x36, 0x3D);

        // --- text ---------------------------------------------------------
        public static readonly Color TextPrimary = Color.FromArgb(0xE4, 0xEC, 0xF0);
        public static readonly Color TextMuted   = Color.FromArgb(0x87, 0x99, 0xA4);
        public static readonly Color TextFaint   = Color.FromArgb(0x5C, 0x6B, 0x74);

        // --- accents ------------------------------------------------------
        public static readonly Color Accent      = Color.FromArgb(0x5F, 0xB3, 0x7A);
        public static readonly Color AccentDeep  = Color.FromArgb(0x35, 0x6E, 0x4A);
        public static readonly Color AccentSoft  = Color.FromArgb(0x24, 0x3A, 0x2E);
        public static readonly Color Warn        = Color.FromArgb(0xD9, 0xA4, 0x41);
        public static readonly Color Danger      = Color.FromArgb(0xD8, 0x65, 0x4F);
        public static readonly Color DangerSoft  = Color.FromArgb(0x3A, 0x23, 0x20);

        // --- typography ---------------------------------------------------
        public static readonly Font FontBody    = new Font("Segoe UI", 9F);
        public static readonly Font FontBodyBold = new Font("Segoe UI", 9F, FontStyle.Bold);
        public static readonly Font FontTitle   = new Font("Segoe UI Semibold", 15F);
        public static readonly Font FontSection = new Font("Segoe UI Semibold", 10F);
        public static readonly Font FontSmall   = new Font("Segoe UI", 8.25F);
        public static readonly Font FontMono    = new Font("Consolas", 9F);
        public static readonly Font FontGlyph   = new Font("Segoe UI Symbol", 11F);

        /// <summary>
        /// Applies the dark grid look. DataGridView defaults to a light Windows-98
        /// palette that fights everything else, so every grid in the app goes
        /// through here.
        /// </summary>
        public static void StyleGrid(DataGridView grid)
        {
            grid.EnableHeadersVisualStyles = false;
            grid.BackgroundColor = FarmTheme.Surface;
            grid.BorderStyle = BorderStyle.None;
            grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            grid.GridColor = FarmTheme.Border;
            grid.RowHeadersVisible = false;
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.AllowUserToResizeRows = false;
            grid.ReadOnly = true;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.MultiSelect = false;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            grid.ColumnHeadersHeight = 34;
            grid.RowTemplate.Height = 27;

            grid.ColumnHeadersDefaultCellStyle.BackColor = FarmTheme.SurfaceAlt;
            grid.ColumnHeadersDefaultCellStyle.ForeColor = FarmTheme.TextMuted;
            grid.ColumnHeadersDefaultCellStyle.Font = FarmTheme.FontBodyBold;
            grid.ColumnHeadersDefaultCellStyle.Padding = new Padding(8, 0, 8, 0);
            grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = FarmTheme.SurfaceAlt;
            grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = FarmTheme.TextMuted;

            grid.DefaultCellStyle.BackColor = FarmTheme.Surface;
            grid.DefaultCellStyle.ForeColor = FarmTheme.TextPrimary;
            grid.DefaultCellStyle.Font = FarmTheme.FontBody;
            grid.DefaultCellStyle.Padding = new Padding(8, 0, 8, 0);
            grid.DefaultCellStyle.SelectionBackColor = FarmTheme.AccentSoft;
            grid.DefaultCellStyle.SelectionForeColor = FarmTheme.TextPrimary;

            grid.AlternatingRowsDefaultCellStyle.BackColor = FarmTheme.SurfaceAlt;
            grid.AlternatingRowsDefaultCellStyle.SelectionBackColor = FarmTheme.AccentSoft;
        }

        /// <summary>Applies the dark look to a plain text input.</summary>
        public static void StyleInput(TextBoxBase box)
        {
            box.BackColor = FarmTheme.SurfaceAlt;
            box.ForeColor = FarmTheme.TextPrimary;
            box.BorderStyle = BorderStyle.FixedSingle;
            box.Font = FarmTheme.FontBody;
        }

        /// <summary>Applies the dark look to a drop-down.</summary>
        public static void StyleCombo(ComboBox combo)
        {
            combo.BackColor = FarmTheme.SurfaceAlt;
            combo.ForeColor = FarmTheme.TextPrimary;
            combo.FlatStyle = FlatStyle.Flat;
            combo.Font = FarmTheme.FontBody;
            combo.DropDownStyle = ComboBoxStyle.DropDownList;
        }
    }
}
