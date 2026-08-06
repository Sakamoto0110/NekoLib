using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace NekoLib.Data.RuntimeTests.FarmDatabase.WinForms.Theme
{
    /// <summary>Rounded-rectangle helper shared by the custom-painted controls.</summary>
    public static class Shapes
    {
        public static GraphicsPath Rounded(Rectangle bounds, int radius)
        {
            var path = new GraphicsPath();

            if (radius <= 0)
            {
                path.AddRectangle(bounds);
                return path;
            }

            int d = radius * 2;
            path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    /// <summary>
    /// A titled surface panel. Everything on a page sits inside one of these, which
    /// is what keeps the layout from looking like a raw dialog.
    /// </summary>
    [DesignerCategory("Code")]
    public sealed class Card : Panel
    {
        private string _title = string.Empty;
        private string _subtitle = string.Empty;

        public Card()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.UserPaint, true);

            BackColor = FarmTheme.Surface;
            ForeColor = FarmTheme.TextPrimary;
            Font = FarmTheme.FontBody;
            Padding = new Padding(16, 44, 16, 16);
        }

        [Category("Farm"), DefaultValue("")]
        public string Title
        {
            get => _title;
            set { _title = value ?? string.Empty; Invalidate(); }
        }

        [Category("Farm"), DefaultValue("")]
        public string Subtitle
        {
            get => _subtitle;
            set { _subtitle = value ?? string.Empty; Invalidate(); }
        }

        /// <summary>When false the card renders without the header band.</summary>
        [Category("Farm"), DefaultValue(true)]
        public bool ShowHeader { get; set; } = true;

        /// <summary>
        /// A card owns a double-buffered surface, so when its children are moved by a
        /// layout pass the area they left behind keeps the previous frame's pixels.
        /// Repainting on layout - not just on resize - is what keeps what is drawn and
        /// what is clickable in the same place.
        /// </summary>
        protected override void OnLayout(LayoutEventArgs levent)
        {
            base.OnLayout(levent);
            Invalidate(true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent?.BackColor ?? FarmTheme.Canvas);

            var bounds = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath path = Shapes.Rounded(bounds, 6))
            using (var fill = new SolidBrush(FarmTheme.Surface))
            using (var pen = new Pen(FarmTheme.Border))
            {
                g.FillPath(fill, path);
                g.DrawPath(pen, path);
            }

            if (!ShowHeader || string.IsNullOrEmpty(_title))
                return;

            using (var titleBrush = new SolidBrush(FarmTheme.TextPrimary))
            {
                g.DrawString(_title, FarmTheme.FontSection, titleBrush, 15, 12);
            }

            if (!string.IsNullOrEmpty(_subtitle))
            {
                SizeF titleSize = g.MeasureString(_title, FarmTheme.FontSection);
                using (var subBrush = new SolidBrush(FarmTheme.TextFaint))
                {
                    g.DrawString(_subtitle, FarmTheme.FontSmall, subBrush,
                        18 + titleSize.Width, 16);
                }
            }

            using (var line = new Pen(FarmTheme.Border))
            {
                g.DrawLine(line, 12, 36, Width - 13, 36);
            }
        }
    }

    /// <summary>Visual weight of an <see cref="FarmButton"/>.</summary>
    public enum FarmButtonKind
    {
        Primary,
        Ghost,
        Danger
    }

    /// <summary>
    /// Flat themed button. WinForms' FlatStyle still paints a system border on
    /// hover, so this paints itself entirely.
    /// </summary>
    [DesignerCategory("Code")]
    public sealed class FarmButton : Control, IButtonControl
    {
        private bool _hover;
        private bool _pressed;
        private FarmButtonKind _kind = FarmButtonKind.Primary;

        public FarmButton()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.UserPaint |
                ControlStyles.SupportsTransparentBackColor, true);

            Font = FarmTheme.FontBody;
            Size = new Size(120, 32);
            Cursor = Cursors.Hand;
        }

        [Category("Farm"), DefaultValue(FarmButtonKind.Primary)]
        public FarmButtonKind Kind
        {
            get => _kind;
            set { _kind = value; Invalidate(); }
        }

        /// <summary>Optional leading glyph, drawn before the text.</summary>
        [Category("Farm"), DefaultValue("")]
        public string Glyph { get; set; } = string.Empty;

        // Runtime-only members carry an explicit serialization contract: the .NET 9
        // WinForms analyzer (WFO1000) turns an unannotated public property on a
        // designable control into a build error, not a warning.
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public DialogResult DialogResult { get; set; }

        public void NotifyDefault(bool value) { }

        public void PerformClick() => OnClick(EventArgs.Empty);

        protected override void OnMouseEnter(EventArgs e)
        {
            _hover = true; Invalidate(); base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hover = false; _pressed = false; Invalidate(); base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            _pressed = true; Invalidate(); base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            _pressed = false; Invalidate(); base.OnMouseUp(e);
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            Invalidate(); base.OnEnabledChanged(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent?.BackColor ?? FarmTheme.Surface);

            Color face, text, border;
            ResolveColors(out face, out text, out border);

            var bounds = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath path = Shapes.Rounded(bounds, 5))
            using (var fill = new SolidBrush(face))
            using (var pen = new Pen(border))
            {
                g.FillPath(fill, path);
                g.DrawPath(pen, path);
            }

            string caption = string.IsNullOrEmpty(Glyph) ? Text : Glyph + "  " + Text;
            TextRenderer.DrawText(g, caption, Font, ClientRectangle, text,
                TextFormatFlags.HorizontalCenter |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.EndEllipsis);
        }

        private void ResolveColors(out Color face, out Color text, out Color border)
        {
            if (!Enabled)
            {
                face = FarmTheme.SurfaceAlt;
                text = FarmTheme.TextFaint;
                border = FarmTheme.Border;
                return;
            }

            switch (_kind)
            {
                case FarmButtonKind.Danger:
                    face = _pressed ? FarmTheme.DangerSoft
                         : _hover   ? Blend(FarmTheme.DangerSoft, FarmTheme.Danger, 0.35f)
                                    : FarmTheme.DangerSoft;
                    text = FarmTheme.Danger;
                    border = FarmTheme.Danger;
                    break;

                case FarmButtonKind.Ghost:
                    face = _pressed ? FarmTheme.SurfaceAlt
                         : _hover   ? FarmTheme.SurfaceAlt
                                    : FarmTheme.Surface;
                    text = FarmTheme.TextPrimary;
                    border = FarmTheme.Border;
                    break;

                default:
                    face = _pressed ? FarmTheme.AccentDeep
                         : _hover   ? Blend(FarmTheme.AccentDeep, FarmTheme.Accent, 0.45f)
                                    : FarmTheme.AccentDeep;
                    text = Color.White;
                    border = FarmTheme.Accent;
                    break;
            }
        }

        private static Color Blend(Color a, Color b, float amount) =>
            Color.FromArgb(
                (int)(a.R + (b.R - a.R) * amount),
                (int)(a.G + (b.G - a.G) * amount),
                (int)(a.B + (b.B - a.B) * amount));
    }

    /// <summary>
    /// One entry in the shell's left rail. Owner-drawn so the active item can carry
    /// an accent bar without a third-party control.
    /// </summary>
    [DesignerCategory("Code")]
    public sealed class SidebarButton : Control
    {
        private bool _hover;
        private bool _active;

        public SidebarButton()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.UserPaint, true);

            Font = FarmTheme.FontBody;
            Height = 42;
            Dock = DockStyle.Top;
            Cursor = Cursors.Hand;
            BackColor = FarmTheme.Sidebar;
        }

        [Category("Farm"), DefaultValue("")]
        public string Glyph { get; set; } = string.Empty;

        /// <summary>The page this entry navigates to. Set by the shell, not the designer.</summary>
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Type PageType { get; set; }

        [Category("Farm"), DefaultValue(false)]
        public bool Active
        {
            get => _active;
            set { _active = value; Invalidate(); }
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            _hover = true; Invalidate(); base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hover = false; Invalidate(); base.OnMouseLeave(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Color back = _active ? FarmTheme.AccentSoft
                       : _hover  ? FarmTheme.Surface
                                 : FarmTheme.Sidebar;

            using (var fill = new SolidBrush(back))
                g.FillRectangle(fill, ClientRectangle);

            if (_active)
            {
                using (var bar = new SolidBrush(FarmTheme.Accent))
                    g.FillRectangle(bar, 0, 6, 3, Height - 12);
            }

            Color fore = _active ? FarmTheme.Accent
                       : _hover  ? FarmTheme.TextPrimary
                                 : FarmTheme.TextMuted;

            if (!string.IsNullOrEmpty(Glyph))
            {
                TextRenderer.DrawText(g, Glyph, FarmTheme.FontGlyph,
                    new Rectangle(14, 0, 26, Height), fore,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter);
            }

            TextRenderer.DrawText(g, Text, Font,
                new Rectangle(46, 0, Width - 52, Height), fore,
                TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }

    /// <summary>Small rounded status badge (connected / disconnected, provider name).</summary>
    [DesignerCategory("Code")]
    public sealed class Pill : Control
    {
        private Color _tone = FarmTheme.TextMuted;

        public Pill()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.UserPaint, true);

            Font = FarmTheme.FontSmall;
            Height = 22;
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Color Tone
        {
            get => _tone;
            set { _tone = value; Invalidate(); }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent?.BackColor ?? FarmTheme.Sidebar);

            var bounds = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath path = Shapes.Rounded(bounds, Height / 2))
            using (var fill = new SolidBrush(Color.FromArgb(40, _tone)))
            using (var pen = new Pen(Color.FromArgb(120, _tone)))
            {
                g.FillPath(fill, path);
                g.DrawPath(pen, path);
            }

            TextRenderer.DrawText(g, Text, Font, ClientRectangle, _tone,
                TextFormatFlags.HorizontalCenter |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.EndEllipsis);
        }
    }

    /// <summary>
    /// Page header: large title, muted subtitle, and a hairline underneath. Every
    /// page docks one of these at the top.
    /// </summary>
    [DesignerCategory("Code")]
    public sealed class PageHeader : Control
    {
        public PageHeader()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.UserPaint, true);

            Height = 62;
            Dock = DockStyle.Top;
            BackColor = FarmTheme.Canvas;
        }

        [Category("Farm"), DefaultValue("")]
        public string Subtitle { get; set; } = string.Empty;

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(BackColor);

            TextRenderer.DrawText(g, Text, FarmTheme.FontTitle,
                new Point(0, 6), FarmTheme.TextPrimary);

            if (!string.IsNullOrEmpty(Subtitle))
            {
                TextRenderer.DrawText(g, Subtitle, FarmTheme.FontBody,
                    new Point(2, 34), FarmTheme.TextMuted);
            }
        }
    }

    /// <summary>
    /// Footer strip carrying the busy indicator, the last status line and any error.
    /// Pages bind it straight to their view-model.
    /// </summary>
    [DesignerCategory("Code")]
    public sealed class StatusLine : Control
    {
        private string _status = string.Empty;
        private string _error = string.Empty;
        private bool _busy;

        public StatusLine()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.UserPaint, true);

            Height = 30;
            Dock = DockStyle.Bottom;
            Font = FarmTheme.FontSmall;
            BackColor = FarmTheme.Canvas;
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string Status
        {
            get => _status;
            set { _status = value ?? string.Empty; Invalidate(); }
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string Error
        {
            get => _error;
            set { _error = value ?? string.Empty; Invalidate(); }
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool Busy
        {
            get => _busy;
            set { _busy = value; Invalidate(); }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(BackColor);

            bool hasError = !string.IsNullOrEmpty(_error);
            Color dot = hasError ? FarmTheme.Danger : _busy ? FarmTheme.Warn : FarmTheme.Accent;

            using (var brush = new SolidBrush(dot))
                g.FillEllipse(brush, 2, Height / 2 - 4, 8, 8);

            string text = hasError ? _error : _busy ? "Executando…" : _status;
            Color fore = hasError ? FarmTheme.Danger : FarmTheme.TextMuted;

            TextRenderer.DrawText(g, text, Font,
                new Rectangle(18, 0, Width - 22, Height), fore,
                TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }
}
