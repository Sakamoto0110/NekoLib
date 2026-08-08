using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using NekoLib.Data.RuntimeTests.FarmDatabase.Core.Simulation;

namespace NekoLib.Data.RuntimeTests.FarmDatabase.WinForms.Theme
{
    /// <summary>
    /// Draws the farm: three terrains of twenty-five tiles on a divider grid, each with
    /// its own shed below it, and a dot per worker walking out from the shed to the
    /// tile it tends.
    /// <para/>
    /// This observes the simulation and never participates in it. The drawing gives up
    /// in stages as the speed rises — workers stop being drawn, then tiles stop being
    /// drawn individually — while travel and growth keep being computed either way.
    /// <b>Above real time this is not evidence.</b> What a run did is in the digest and
    /// in the operation log.
    /// </summary>
    [DesignerCategory("Code")]
    public sealed class FarmField : Control
    {
        private const int Side = SimRules.TerrainSide;

        /// <summary>Three terrains side by side, with a tile of empty ground between them.</summary>
        private const int Columns = (3 * Side) + 2;

        private const int Rows = Side;

        /// <summary>Stride from one terrain's first column to the next one's.</summary>
        private const int TerrainStride = Side + 1;

        /// <summary>Dots parked under each terrain before the count takes over.</summary>
        private const int ShedDots = 5;

        private readonly System.Diagnostics.Stopwatch _sinceTick =
            System.Diagnostics.Stopwatch.StartNew();

        private SimSnapshot _snapshot;
        private bool _showWorkers = true;
        private bool _showTiles = true;
        private int _ticksPerSecond = 1;
        private long _lastSeenTick = -1;

        public FarmField()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.UserPaint, true);

            BackColor = FarmTheme.Surface;
            ForeColor = FarmTheme.TextPrimary;
            Font = FarmTheme.FontSmall;
        }

        /// <summary>
        /// The world to draw. Null before a run exists.
        /// <para/>
        /// Serialization is explicitly hidden: this is live state pushed in at runtime,
        /// and the WinForms analyzer rejects a settable reference property that does
        /// not say so (WFO1000).
        /// </summary>
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public SimSnapshot Snapshot
        {
            get => _snapshot;
            set
            {
                _snapshot = value;

                // Only a genuinely new tick restarts the interpolation clock. The page
                // re-applies the view-model on any property change, and restarting on
                // a status line would make the dots stutter backwards.
                long tick = value == null ? 0 : value.State.Tick;
                if (tick != _lastSeenTick)
                {
                    _lastSeenTick = tick;
                    _sinceTick.Restart();
                }

                Invalidate();
            }
        }

        /// <summary>
        /// How many simulated ticks pass per second of wall time.
        /// <para/>
        /// Worker motion is derived from the tick rather than from the clock, so the
        /// dots simply move twice as fast at 2× and five times as fast at 5× without
        /// anything here knowing about speeds.
        /// </summary>
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int TicksPerSecond
        {
            get => _ticksPerSecond;
            set { _ticksPerSecond = value < 1 ? 1 : value; }
        }

        /// <summary>
        /// The tick as it stands right now, carried forward from the last committed one
        /// by however long ago it arrived. Capped at one pulse ahead so a stalled
        /// simulation cannot let the drawing run away from it.
        /// </summary>
        private double EffectiveTick()
        {
            if (_snapshot == null) return 0;

            double ahead = _sinceTick.Elapsed.TotalSeconds * _ticksPerSecond;
            if (ahead > _ticksPerSecond) ahead = _ticksPerSecond;

            return _snapshot.State.Tick + ahead;
        }

        /// <summary>Draw the walking dots. Off above the faithful speeds.</summary>
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool ShowWorkers
        {
            get => _showWorkers;
            set { _showWorkers = value; Invalidate(); }
        }

        /// <summary>
        /// Draw each tile at its own growth. Off at the top speeds, where the terrain
        /// is painted as one flat block instead - at a hundred ticks a second the
        /// individual bars are a blur that costs more than it says.
        /// </summary>
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool ShowTiles
        {
            get => _showTiles;
            set { _showTiles = value; Invalidate(); }
        }

        /// <summary>Advances the animation without changing state.</summary>
        public void Pulse() => Invalidate();

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(BackColor);

            if (_snapshot == null)
            {
                DrawPlaceholder(g);
                return;
            }

            int tile = Math.Min((Width - 24) / Columns, (Height - 56) / Rows);
            if (tile < 6) return;

            int boardWidth = tile * Columns;
            int originX = (Width - boardWidth) / 2;
            int originY = 16;
            int shedY = originY + (tile * Rows) + 16;

            DrawGrid(g, tile, originX, originY);

            if (_showTiles)
                DrawTiles(g, tile, originX, originY);
            else
                DrawStaticTiles(g, tile, originX, originY);

            if (_showWorkers)
                DrawWorkers(g, tile, originX, originY, shedY);

            DrawSheds(g, tile, originX, shedY);
        }

        private void DrawPlaceholder(Graphics g)
        {
            using (var brush = new SolidBrush(FarmTheme.TextFaint))
            using (var format = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            })
            {
                g.DrawString(
                    "sem run · escolha uma semente e inicie",
                    FarmTheme.FontBody,
                    brush,
                    new RectangleF(0, 0, Width, Height),
                    format);
            }
        }

        // -----------------------------------------------------------------
        // Geometry
        // -----------------------------------------------------------------

        private static Rectangle TileBounds(SimTile cell, int tile, int originX, int originY)
        {
            // Inset by one pixel only: the separation comes from the divider grid rather
            // than from padding, which leaves more of each cell for the crop.
            const int pad = 1;

            int column = (cell.Terrain * TerrainStride) + (cell.Slot % Side);
            int row = cell.Slot / Side;

            return new Rectangle(
                originX + (column * tile) + pad,
                originY + (row * tile) + pad,
                tile - (pad * 2),
                tile - (pad * 2));
        }

        /// <summary>
        /// Where a terrain's workers are parked when they are not out in the field:
        /// centred below the middle column, which is the point the travel distances are
        /// measured from.
        /// </summary>
        private static Point Shed(int terrain, int tile, int originX, int shedY) =>
            new Point(
                originX + (terrain * TerrainStride * tile) + ((Side * tile) / 2),
                shedY);

        // -----------------------------------------------------------------
        // Painting
        // -----------------------------------------------------------------

        /// <summary>
        /// The lattice each terrain sits on, drawn underneath the tiles.
        /// <para/>
        /// With twenty-five cells to a terrain the crops no longer separate themselves
        /// by spacing alone: two neighbours a tick apart in growth are nearly the same
        /// colour, and without a line between them the block reads as one wash.
        /// </summary>
        private void DrawGrid(Graphics g, int tile, int originX, int originY)
        {
            int height = tile * Rows;

            using (var pen = new Pen(Color.FromArgb(70, FarmTheme.Border)))
            {
                for (int terrain = 0; terrain < 3; terrain++)
                {
                    int left = originX + (terrain * TerrainStride * tile);
                    int right = left + (tile * Side);

                    for (int column = 0; column <= Side; column++)
                    {
                        int x = left + (column * tile);
                        g.DrawLine(pen, x, originY, x, originY + height);
                    }

                    for (int row = 0; row <= Side; row++)
                    {
                        int y = originY + (row * tile);
                        g.DrawLine(pen, left, y, right, y);
                    }
                }
            }
        }

        private void DrawTiles(Graphics g, int tile, int originX, int originY)
        {
            long tick = _snapshot.State.Tick;

            foreach (SimTile cell in _snapshot.Tiles)
            {
                if (cell.Terrain >= 3) continue;

                Rectangle bounds = TileBounds(cell, tile, originX, originY);
                bool owned = FarmSimulation.IsOwned(_snapshot.State, cell);
                DrawTile(g, bounds, cell, owned, tick);
            }
        }

        /// <summary>
        /// The top-speed fallback. Still one square per tile - an empty slot stays
        /// empty and only what actually holds a seed is coloured - but the colour is
        /// flat: no growth fill, no maturity ring, no rounded path. At a hundred ticks
        /// a second the growth bars are a blur that costs more than it says, while
        /// which tiles are planted still reads at a glance.
        /// </summary>
        private void DrawStaticTiles(Graphics g, int tile, int originX, int originY)
        {
            foreach (SimTile cell in _snapshot.Tiles)
            {
                if (cell.Terrain >= 3 || cell.IsEmpty) continue;
                if (!FarmSimulation.IsOwned(_snapshot.State, cell)) continue;

                Rectangle bounds = TileBounds(cell, tile, originX, originY);

                using (var brush = new SolidBrush(Color.FromArgb(180, Swatch(cell.Crop))))
                    g.FillRectangle(brush, bounds);
            }
        }

        private void DrawTile(Graphics g, Rectangle bounds, SimTile cell, bool owned, long tick)
        {
            int radius = Math.Max(2, bounds.Width / 8);

            using (GraphicsPath path = Shapes.Rounded(bounds, radius))
            {
                if (!owned)
                {
                    using (var pen = new Pen(Color.FromArgb(90, FarmTheme.Border)))
                        g.DrawPath(pen, path);
                    return;
                }

                using (var soil = new SolidBrush(FarmTheme.SurfaceAlt))
                    g.FillPath(soil, path);

                if (!cell.IsEmpty)
                {
                    Color crop = Swatch(cell.Crop);
                    double progress = cell.Progress(tick);

                    // Growth fills from the bottom, so a terrain reads as a field of
                    // bars at different heights rather than as coloured squares.
                    int grown = (int)Math.Round(bounds.Height * progress);
                    if (grown > 0)
                    {
                        var filled = new Rectangle(
                            bounds.X,
                            bounds.Bottom - grown,
                            bounds.Width,
                            grown);

                        Region clip = g.Clip;
                        g.SetClip(path);
                        using (var brush = new SolidBrush(Color.FromArgb(cell.IsMature(tick) ? 235 : 150, crop)))
                            g.FillRectangle(brush, filled);
                        g.Clip = clip;
                    }
                }

                using (var pen = new Pen(cell.IsMature(tick) ? FarmTheme.Accent : FarmTheme.Border))
                    g.DrawPath(pen, path);
            }
        }

        /// <summary>
        /// A dot per staffed tile, walking out from its terrain's shed and back.
        /// <para/>
        /// The dots fan out from one corner rather than moving in lanes, which is what
        /// the geometry actually is: the shed is at the bottom left and the far tile is
        /// nine steps away, so the spread of the fan is the spread of the travel times
        /// that keeps the field out of step.
        /// </summary>
        private void DrawWorkers(Graphics g, int tile, int originX, int originY, int shedY)
        {
            int size = Math.Max(4, tile / 6);
            double now = EffectiveTick();
            var chosen = _snapshot.ChosenCrops();

            foreach (SimTile cell in _snapshot.Tiles)
            {
                if (cell.Terrain >= 3 || !cell.HasWorker) continue;
                if (!FarmSimulation.IsOwned(_snapshot.State, cell)) continue;

                int travel = cell.TravelTicks;
                if (travel < 1) travel = 1;

                double outbound = cell.NextActionTick - travel;
                double along;
                bool carrying = false;

                if (now >= outbound)
                {
                    // On the way out, hands empty.
                    along = (now - outbound) / travel;
                    if (along > 1) along = 1;
                }
                else
                {
                    // On the way back, if the last visit was recent enough. When the
                    // tile is empty the visit was a harvest, so the walk home is
                    // carrying; when it holds a crop the visit was a planting and the
                    // walk home carries nothing.
                    //
                    // Both instants are derived rather than stored: a harvest sets the
                    // next arrival exactly one round trip out, and a planting stamps
                    // PlantedAtTick. Neither needs a column of its own.
                    double acted = cell.IsEmpty
                        ? cell.NextActionTick - (2.0 * travel)
                        : cell.PlantedAtTick;

                    double since = now - acted;
                    if (since < 0 || since >= travel)
                        continue;   // idle in the shed; the group already stands for it

                    along = 1.0 - (since / travel);
                    carrying = cell.IsEmpty;
                }

                if (along < 0) along = 0;

                Rectangle bounds = TileBounds(cell, tile, originX, originY);
                Point shed = Shed(cell.Terrain, tile, originX, shedY);

                int targetX = bounds.X + (bounds.Width / 2);
                int targetY = bounds.Y + (bounds.Height / 2);

                int x = (int)Math.Round(shed.X + ((targetX - shed.X) * along)) - (size / 2);
                int y = (int)Math.Round(shed.Y + ((targetY - shed.Y) * along)) - (size / 2);

                // Colour means a load. The harvested crop is gone from the tile by the
                // time it is being carried, so the terrain's current crop stands in for
                // it - the same thing in every case except the tick after the month
                // changes what is being planted.
                Color tone = FarmTheme.TextMuted;
                if (carrying)
                {
                    tone = cell.Terrain < chosen.Count
                        ? Swatch(chosen[cell.Terrain])
                        : FarmTheme.Accent;
                }

                using (var brush = new SolidBrush(tone))
                    g.FillEllipse(brush, x, y, size, size);
            }
        }

        /// <summary>
        /// Each terrain's shed. Five dots, then a count - past a handful the dots stop
        /// saying anything a number does not say better.
        /// </summary>
        private void DrawSheds(Graphics g, int tile, int originX, int shedY)
        {
            if (shedY > Height - 10) return;

            int dot = Math.Max(4, tile / 6);
            int spacing = dot + 3;

            using (var brush = new SolidBrush(FarmTheme.TextFaint))
            using (var text = new SolidBrush(FarmTheme.TextMuted))
            {
                for (int terrain = 0; terrain < 3; terrain++)
                {
                    int staffed = 0;
                    foreach (SimTile cell in _snapshot.Tiles)
                        if (cell.Terrain == terrain && cell.HasWorker &&
                            FarmSimulation.IsOwned(_snapshot.State, cell))
                            staffed++;

                    if (staffed == 0) continue;

                    Point centre = Shed(terrain, tile, originX, shedY);
                    int shown = Math.Min(staffed, ShedDots);

                    // The group straddles the shed point, so the fan of walking dots
                    // reads as leaving from the middle of it.
                    int x = centre.X - (((shown * spacing) - (spacing - dot)) / 2);

                    for (int i = 0; i < shown; i++)
                    {
                        g.FillEllipse(brush, x, shedY, dot, dot);
                        x += spacing;
                    }

                    if (staffed > ShedDots)
                        g.DrawString("+" + (staffed - ShedDots), FarmTheme.FontSmall, text, x + 2, shedY - 3);
                }
            }
        }

        private static Color Swatch(string cropName)
        {
            string hex = SimRules.CropByName(cropName).Swatch;

            try
            {
                return ColorTranslator.FromHtml(hex);
            }
            catch (Exception)
            {
                return FarmTheme.Accent;
            }
        }
    }
}
