using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows.Forms;
using NekoLib.Navigation.Metadata;
using NekoLib.Navigation.Metadata.Attributes;
using NekoLib.Data.RuntimeTests.FarmDatabase.Core.Simulation;
using NekoLib.Data.RuntimeTests.FarmDatabase.Core.ViewModels;
using NekoLib.Data.RuntimeTests.FarmDatabase.WinForms.Theme;

namespace NekoLib.Data.RuntimeTests.FarmDatabase.WinForms.Pages
{
    /// <summary>
    /// The farm running itself.
    /// <para/>
    /// Every tick is a transaction, so leaving this page running is a sustained
    /// database load that nobody is clicking - which is the whole point. The page owns
    /// the cadence; the view-model owns the step.
    /// </summary>
    [PageMetadata(Name = "Simulação", Role = PageRole.Normal, Tags = new[] { "dados" })]
    [PageReuse(PageReusePolicy.StrongSingleton)]
    public partial class SimulationPage : FarmPageBase
    {
        /// <summary>One pulse per second, matching one tick to one second in real time.</summary>
        private const int PulseMs = 1000;

        /// <summary>
        /// The field redraws faster than the simulation advances so motion stays
        /// smooth. This timer never touches state.
        /// </summary>
        private const int FrameMs = 60;

        private SimulationViewModel _vm;
        private Timer _pulse;
        private Timer _frames;

        public SimulationPage()
        {
            InitializeComponent();
            ApplyTheme();

            if (IsInert) return;

            _vm = App.Simulation;

            BuildSpeeds();
            WireControls();

            Bind(_startButton, _vm.StartCommand);
            Bind(_resumeButton, _vm.ResumeCommand);
            Bind(_playButton, _vm.PlayPauseCommand);
            Bind(_vm, _status, ApplyViewModel);

            _pulse = new Timer { Interval = PulseMs };
            _pulse.Tick += OnPulse;
            _pulse.Start();

            _frames = new Timer { Interval = FrameMs };
            _frames.Tick += (s, e) =>
            {
                // Only the walking dots move between simulation ticks. Once they stop
                // being drawn there is nothing to interpolate, so the frame timer stops
                // repainting rather than redrawing an identical field sixteen times a
                // second.
                if (_vm.ShowFarm && _vm.IsRunning && _vm.RendersWorkers)
                    _field.Pulse();
            };
            _frames.Start();
        }

        private void ApplyTheme()
        {
            _toolbar.BackColor = FarmTheme.Surface;
            _stats.BackColor = FarmTheme.Surface;
            _prices.BackColor = FarmTheme.Surface;
            _field.BackColor = FarmTheme.Canvas;

            FarmTheme.StyleCombo(_speed);
            _seed.BackColor = FarmTheme.SurfaceAlt;
            _seed.ForeColor = FarmTheme.TextPrimary;
            _seed.BorderStyle = BorderStyle.FixedSingle;
            _showFarm.FlatStyle = FlatStyle.Flat;
            _showWorkers.FlatStyle = FlatStyle.Flat;

            _stats.Paint += PaintStats;
            _prices.Paint += PaintPrices;
        }

        /// <summary>
        /// Ticks advanced per one-second pulse. Anything above 1 is accelerated mode:
        /// the drawing stops being faithful and only the numbers and the log count as
        /// evidence.
        /// <para/>
        /// The top settings ask for more than the database can deliver - SQLite
        /// measured around 250 ticks a second and Access around 6 - so past a point
        /// these stop being a speed and become a request. The view-model drops a pulse
        /// that arrives while the previous one is still running rather than queueing
        /// it, so the run simply goes as fast as the engine allows.
        /// </summary>
        private static readonly int[] Speeds = { 1, 2, 5, 10, 20, 60, 100, 200, 500 };

        private void BuildSpeeds()
        {
            foreach (int speed in Speeds)
            {
                _speed.Items.Add(speed == 1
                    ? "1× (tempo real)"
                    : speed.ToString(CultureInfo.CurrentCulture) + "×");
            }

            _speed.SelectedIndex = 0;
        }

        private void WireControls()
        {
            _seed.Value = _vm.Seed;
            _seed.ValueChanged += (s, e) => _vm.Seed = (int)_seed.Value;

            _showFarm.Checked = _vm.ShowFarm;
            _showFarm.CheckedChanged += (s, e) =>
            {
                _vm.ShowFarm = _showFarm.Checked;
                _field.Visible = _showFarm.Checked;
            };

            _showWorkers.Checked = _vm.ShowWorkers;
            _showWorkers.CheckedChanged += (s, e) =>
            {
                _vm.ShowWorkers = _showWorkers.Checked;
                _field.ShowWorkers = _vm.RendersWorkers;
            };

            _speed.SelectedIndexChanged += (s, e) =>
            {
                int index = _speed.SelectedIndex;
                _vm.TicksPerPulse = index >= 0 && index < Speeds.Length ? Speeds[index] : 1;
            };
        }

        /// <summary>
        /// Advancing is asynchronous and the timer is not, so a pulse that overruns is
        /// dropped by the view-model's own guard rather than queued here.
        /// </summary>
        private async void OnPulse(object sender, EventArgs e)
        {
            if (IsDisposed || _vm == null) return;

            try
            {
                await _vm.AdvanceAsync();
            }
            catch (Exception)
            {
                // AdvanceAsync already routes failures into the view-model's error
                // line; this catch only stops an async void from reaching the UI
                // thread's exception path.
            }
        }

        public override Task OnNavigatedToAsync(NavigationArgs args)
        {
            if (!IsInert)
                ApplyViewModel();

            return Task.CompletedTask;
        }

        private void ApplyViewModel()
        {
            if (_vm == null) return;

            _playButton.Text = _vm.PlayPauseCaption;
            _field.ShowWorkers = _vm.RendersWorkers;
            _field.ShowTiles = _vm.RendersTiles;

            // One pulse a second, so ticks per pulse is ticks per second - and the
            // walking dots take their pace from that rather than from a fixed animation.
            _field.TicksPerSecond = _vm.TicksPerPulse;
            _field.Snapshot = _vm.Snapshot;
            _field.Visible = _vm.ShowFarm;

            _stats.Invalidate();
            _prices.Invalidate();
        }

        // -----------------------------------------------------------------
        // Panels
        // -----------------------------------------------------------------

        /// <summary>
        /// The headline numbers, as flat blocks. No boxes, no borders - the label sits
        /// above the value and the spacing does the separating.
        /// </summary>
        private void PaintStats(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(FarmTheme.Surface);

            if (_vm == null) return;

            var blocks = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("tick", _vm.TickText),
                new KeyValuePair<string, string>("calendário", _vm.CalendarText),
                new KeyValuePair<string, string>("ouro", _vm.GoldText),
                new KeyValuePair<string, string>("fazenda", _vm.FarmText),
                new KeyValuePair<string, string>("mercado", _vm.CycleText)
            };

            int x = 4;
            int width = Math.Max(120, (_stats.Width - 8) / blocks.Count);

            using (var labelBrush = new SolidBrush(FarmTheme.TextFaint))
            using (var valueBrush = new SolidBrush(FarmTheme.TextPrimary))
            {
                foreach (KeyValuePair<string, string> block in blocks)
                {
                    g.DrawString(block.Key, FarmTheme.FontSmall, labelBrush, x, 8);
                    g.DrawString(block.Value, FarmTheme.FontSection, valueBrush, x, 26);
                    x += width;
                }
            }

            if (_vm.IsAccelerated)
            {
                string note = "acelerado · o desenho é ilustrativo";
                using (var brush = new SolidBrush(FarmTheme.Warn))
                {
                    SizeF size = g.MeasureString(note, FarmTheme.FontSmall);
                    g.DrawString(note, FarmTheme.FontSmall, brush, _stats.Width - size.Width - 6, 8);
                }
            }

            // The measured window, along the bottom. Empty until the first one closes,
            // which is deliberate: a number from half a window is worse than no number.
            SimMetricsWindow measured = _vm.Measured;
            if (measured.IsEmpty) return;

            using (var brush = new SolidBrush(measured.Dropped > 0 ? FarmTheme.Warn : FarmTheme.TextFaint))
                g.DrawString(measured.ToLine(), FarmTheme.FontMono, brush, 4, _stats.Height - 16);
        }

        /// <summary>
        /// Prices, as bars against each crop's own base. The world's stock is never
        /// shown - the bar is the only thing the player gets, which is what makes the
        /// market hidden rather than merely uninteresting.
        /// </summary>
        private void PaintPrices(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(FarmTheme.Surface);

            if (_vm == null) return;

            using (var caption = new SolidBrush(FarmTheme.TextFaint))
                g.DrawString("preço", FarmTheme.FontSmall, caption, 8, 4);

            IReadOnlyList<CropPrice> prices = _vm.Prices;
            int y = 24;

            foreach (CropPrice price in prices)
            {
                Color tone = price.Planted ? FarmTheme.Accent : FarmTheme.TextMuted;

                using (var nameBrush = new SolidBrush(price.Planted ? FarmTheme.TextPrimary : FarmTheme.TextMuted))
                    g.DrawString(price.Crop, FarmTheme.FontBody, nameBrush, 8, y);

                string value = price.Price.ToString("F1", CultureInfo.CurrentCulture);
                using (var valueBrush = new SolidBrush(tone))
                {
                    SizeF size = g.MeasureString(value, FarmTheme.FontBody);
                    g.DrawString(value, FarmTheme.FontBody, valueBrush, _prices.Width - size.Width - 8, y);
                }

                var track = new Rectangle(8, y + 20, _prices.Width - 16, 4);
                using (var back = new SolidBrush(FarmTheme.SurfaceAlt))
                    g.FillRectangle(back, track);

                int filled = (int)Math.Round(track.Width * Math.Max(0, Math.Min(1, price.Health)));
                if (filled > 0)
                {
                    using (var bar = new SolidBrush(tone))
                        g.FillRectangle(bar, new Rectangle(track.X, track.Y, filled, track.Height));
                }

                if (price.InStock > 0)
                {
                    using (var stock = new SolidBrush(FarmTheme.TextFaint))
                        g.DrawString(
                            price.InStock + " colhido(s)",
                            FarmTheme.FontSmall,
                            stock,
                            8,
                            y + 26);
                }

                y += 52;
                if (y > _prices.Height - 20) break;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_pulse != null) { _pulse.Stop(); _pulse.Dispose(); _pulse = null; }
                if (_frames != null) { _frames.Stop(); _frames.Dispose(); _frames = null; }
                if (components != null) components.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
