#nullable enable
using System;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace NekoLib.Data.RuntimeTests.SqlServer.SoakStatus
{
    /// <summary>
    /// A window that says how long the soak has been running, and nothing else.
    /// <para/>
    /// It exists so a sixteen-hour run can be checked with a glance instead of
    /// a terminal. It references neither the scenario nor the library, watches
    /// a clock and optionally a process id, and has no way to influence the run
    /// it reports on - a status window that could disturb the measurement would
    /// be worse than no window.
    /// </summary>
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            Options options = Options.Parse(args);

            ApplicationConfiguration.Initialize();
            Application.Run(new StatusForm(options));
        }
    }

    internal sealed class Options
    {
        public DateTime StartedUtc = DateTime.UtcNow;
        public TimeSpan? Duration;
        public int? ProcessId;
        public string Title = "--soak running";

        public static Options Parse(string[] args)
        {
            Options options = new Options();

            for (int i = 0; i < args.Length; i++)
            {
                bool hasValue = i + 1 < args.Length;

                switch (args[i].ToLowerInvariant())
                {
                    case "--started":
                        // So a window opened after the run began still shows the
                        // real elapsed time rather than restarting the clock.
                        if (hasValue && DateTime.TryParse(
                                args[i + 1],
                                CultureInfo.InvariantCulture,
                                DateTimeStyles.RoundtripKind | DateTimeStyles.AdjustToUniversal,
                                out DateTime started))
                        {
                            options.StartedUtc = started;
                            i++;
                        }
                        break;

                    case "--duration":
                        if (hasValue && TryParseDuration(args[i + 1], out TimeSpan duration))
                        {
                            options.Duration = duration;
                            i++;
                        }
                        break;

                    case "--pid":
                        if (hasValue && int.TryParse(args[i + 1], NumberStyles.Integer,
                                CultureInfo.InvariantCulture, out int pid))
                        {
                            options.ProcessId = pid;
                            i++;
                        }
                        break;

                    case "--title":
                        if (hasValue) { options.Title = args[i + 1]; i++; }
                        break;
                }
            }

            return options;
        }

        private static bool TryParseDuration(string text, out TimeSpan duration)
        {
            duration = TimeSpan.Zero;
            if (string.IsNullOrWhiteSpace(text)) return false;

            string trimmed = text.Trim();
            char suffix = trimmed[trimmed.Length - 1];
            string number = char.IsDigit(suffix) ? trimmed : trimmed.Substring(0, trimmed.Length - 1);

            if (!double.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out double amount) ||
                amount <= 0)
            {
                return false;
            }

            switch (char.ToLowerInvariant(suffix))
            {
                case 'h': duration = TimeSpan.FromHours(amount); return true;
                case 'm': duration = TimeSpan.FromMinutes(amount); return true;
                case 's': duration = TimeSpan.FromSeconds(amount); return true;
                default: duration = TimeSpan.FromSeconds(amount); return true;
            }
        }
    }

    internal sealed class StatusForm : Form
    {
        private readonly Options _options;
        private readonly Label _elapsed;
        private readonly Label _detail;
        private readonly System.Windows.Forms.Timer _tick;
        private Process? _watched;
        private bool _finished;

        public StatusForm(Options options)
        {
            _options = options;

            Text = options.Title;
            TopMost = true;
            FormBorderStyle = FormBorderStyle.FixedToolWindow;
            StartPosition = FormStartPosition.Manual;
            ClientSize = new Size(300, 96);
            BackColor = Color.FromArgb(24, 24, 28);
            ShowInTaskbar = true;

            _elapsed = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Color.FromArgb(235, 235, 240),
                Font = new Font("Consolas", 30F, FontStyle.Regular, GraphicsUnit.Point),
                Text = "00:00:00"
            };

            _detail = new Label
            {
                Dock = DockStyle.Bottom,
                Height = 24,
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Color.FromArgb(150, 150, 158),
                Font = new Font("Segoe UI", 8.25F, FontStyle.Regular, GraphicsUnit.Point),
                Text = string.Empty
            };

            Controls.Add(_elapsed);
            Controls.Add(_detail);

            if (options.ProcessId.HasValue)
            {
                try { _watched = Process.GetProcessById(options.ProcessId.Value); }
                catch (ArgumentException) { _watched = null; }
            }

            PlaceBottomRight();

            _tick = new System.Windows.Forms.Timer { Interval = 250 };
            _tick.Tick += (_, __) => Refresh(DateTime.UtcNow);
            _tick.Start();

            Refresh(DateTime.UtcNow);
        }

        /// <summary>
        /// Bottom right of the working area, so a window that stays on top all
        /// night sits where nothing else wants to be.
        /// </summary>
        private void PlaceBottomRight()
        {
            Rectangle work = Screen.PrimaryScreen == null
                ? new Rectangle(0, 0, 1280, 800)
                : Screen.PrimaryScreen.WorkingArea;

            Location = new Point(work.Right - Width - 16, work.Bottom - Height - 16);
        }

        private void Refresh(DateTime nowUtc)
        {
            TimeSpan elapsed = nowUtc - _options.StartedUtc;
            if (elapsed < TimeSpan.Zero) elapsed = TimeSpan.Zero;

            _elapsed.Text = string.Format(
                CultureInfo.InvariantCulture,
                "{0:00}:{1:00}:{2:00}",
                (int)elapsed.TotalHours,
                elapsed.Minutes,
                elapsed.Seconds);

            if (_watched != null && !_finished)
            {
                _watched.Refresh();
                if (_watched.HasExited)
                {
                    // The clock stops meaning anything once the run is over, so
                    // say so rather than counting into the void.
                    _finished = true;
                    _tick.Stop();
                    _elapsed.ForeColor = Color.FromArgb(120, 200, 140);
                    _detail.Text = "finished - exit " + _watched.ExitCode.ToString(CultureInfo.InvariantCulture);
                    Text = "soak finished";
                    return;
                }
            }

            _detail.Text = DescribeRemaining(elapsed);
        }

        private string DescribeRemaining(TimeSpan elapsed)
        {
            if (!_options.Duration.HasValue) return "running";

            TimeSpan remaining = _options.Duration.Value - elapsed;
            if (remaining <= TimeSpan.Zero) return "past its requested window; still running";

            DateTime endsLocal = (_options.StartedUtc + _options.Duration.Value).ToLocalTime();

            return string.Format(
                CultureInfo.InvariantCulture,
                "{0:00}:{1:00} left  -  ends {2:HH:mm}",
                (int)remaining.TotalHours,
                remaining.Minutes,
                endsLocal);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _tick.Dispose();
                if (_watched != null) _watched.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
