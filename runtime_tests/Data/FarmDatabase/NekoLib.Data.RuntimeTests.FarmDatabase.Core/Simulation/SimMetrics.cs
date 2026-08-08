#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using NekoLib.Core.Logging;
using NekoLib.Core.Telemetry;

namespace NekoLib.Data.RuntimeTests.FarmDatabase.Core.Simulation
{
    /// <summary>What one reporting window measured. Immutable once handed out.</summary>
    public sealed class SimMetricsWindow
    {
        public SimMetricsWindow(
            long ticks,
            long pulses,
            long dropped,
            long statements,
            double wallMs,
            double databaseMs,
            double slowestPulseMs)
        {
            Ticks = ticks;
            Pulses = pulses;
            Dropped = dropped;
            Statements = statements;
            WallMs = wallMs;
            DatabaseMs = databaseMs;
            SlowestPulseMs = slowestPulseMs;
        }

        public long Ticks { get; }
        public long Pulses { get; }

        /// <summary>Pulses skipped because the previous one had not finished.</summary>
        public long Dropped { get; }

        public long Statements { get; }
        public double WallMs { get; }
        public double DatabaseMs { get; }
        public double SlowestPulseMs { get; }

        public double TicksPerSecond => WallMs <= 0 ? 0 : Ticks / (WallMs / 1000.0);
        public double MsPerTick => Ticks <= 0 ? 0 : DatabaseMs / Ticks;
        public double StatementsPerTick => Ticks <= 0 ? 0 : (double)Statements / Ticks;

        /// <summary>Share of the window spent inside the database.</summary>
        public double DatabaseShare => WallMs <= 0 ? 0 : DatabaseMs / WallMs;

        public bool IsEmpty => Ticks == 0 && Pulses == 0 && Dropped == 0;

        public string ToLine() =>
            "ticks=" + Ticks.ToString(CultureInfo.InvariantCulture) +
            " tps=" + TicksPerSecond.ToString("F1", CultureInfo.InvariantCulture) +
            " db/tick=" + MsPerTick.ToString("F2", CultureInfo.InvariantCulture) + "ms" +
            " sql/tick=" + StatementsPerTick.ToString("F1", CultureInfo.InvariantCulture) +
            " db=" + (DatabaseShare * 100).ToString("F0", CultureInfo.InvariantCulture) + "%" +
            " pulsos=" + Pulses.ToString(CultureInfo.InvariantCulture) +
            " descartados=" + Dropped.ToString(CultureInfo.InvariantCulture) +
            " pior=" + SlowestPulseMs.ToString("F0", CultureInfo.InvariantCulture) + "ms";
    }

    /// <summary>
    /// Collects what the simulation costs and reports it on a window, never per tick.
    /// <para/>
    /// The reporting cadence is the whole design. <c>RollingFileLogSink</c> opens,
    /// writes and closes the file on every entry, so a line per tick would be one file
    /// open per tick — the same mistake as repainting the SQL console per statement,
    /// which is what made the UI stutter in the first place. Counters are incremented
    /// in memory and one rolled-up line reaches disk per window.
    /// <para/>
    /// Telemetry gets the same window as a completed operation with measurements
    /// attached. It is bounded and in-memory by design, so it holds the live picture
    /// while the file holds the history — neither can do the other's job.
    /// </summary>
    public sealed class SimMetrics
    {
        private readonly object _gate = new object();
        private readonly ILogger? _logger;
        private readonly ITelemetry? _telemetry;
        private readonly TimeSpan _window;

        private long _ticks;
        private long _pulses;
        private long _dropped;
        private long _statements;
        private double _databaseMs;
        private double _slowestPulseMs;
        private DateTime _windowStartedUtc = DateTime.UtcNow;

        private SimMetricsWindow _last = new SimMetricsWindow(0, 0, 0, 0, 0, 0, 0);
        private SimMetricsWindow _total = new SimMetricsWindow(0, 0, 0, 0, 0, 0, 0);

        public SimMetrics(ILogger? logger, ITelemetry? telemetry, TimeSpan? window = null)
        {
            _logger = logger;
            _telemetry = telemetry;
            _window = window ?? TimeSpan.FromSeconds(10);
        }

        /// <summary>The last completed window. What the panel shows.</summary>
        public SimMetricsWindow Last
        {
            get { lock (_gate) return _last; }
        }

        /// <summary>Everything since the run began, for the closing line.</summary>
        public SimMetricsWindow Total
        {
            get { lock (_gate) return _total; }
        }

        public void Reset()
        {
            lock (_gate)
            {
                _ticks = _pulses = _dropped = _statements = 0;
                _databaseMs = _slowestPulseMs = 0;
                _windowStartedUtc = DateTime.UtcNow;
                _last = new SimMetricsWindow(0, 0, 0, 0, 0, 0, 0);
                _total = new SimMetricsWindow(0, 0, 0, 0, 0, 0, 0);
            }
        }

        /// <summary>A pulse that could not start because the previous one was still running.</summary>
        public void RecordDropped()
        {
            lock (_gate) { _dropped++; }
            TryReport();
        }

        /// <summary>
        /// One completed pulse. <paramref name="databaseMs"/> is the part spent waiting
        /// on the gateway, which is what separates "the database is slow" from "the
        /// simulation is slow".
        /// </summary>
        public void RecordPulse(int ticks, double pulseMs, double databaseMs, long statements)
        {
            lock (_gate)
            {
                _ticks += ticks;
                _pulses++;
                _statements += statements;
                _databaseMs += databaseMs;

                if (pulseMs > _slowestPulseMs)
                    _slowestPulseMs = pulseMs;
            }

            TryReport();
        }

        /// <summary>Closes the window if it is due, and pushes it to the logger and telemetry.</summary>
        private void TryReport()
        {
            SimMetricsWindow? closed = null;

            lock (_gate)
            {
                DateTime now = DateTime.UtcNow;
                double wallMs = (now - _windowStartedUtc).TotalMilliseconds;
                if (wallMs < _window.TotalMilliseconds)
                    return;

                closed = new SimMetricsWindow(
                    _ticks, _pulses, _dropped, _statements, wallMs, _databaseMs, _slowestPulseMs);

                _total = new SimMetricsWindow(
                    _total.Ticks + _ticks,
                    _total.Pulses + _pulses,
                    _total.Dropped + _dropped,
                    _total.Statements + _statements,
                    _total.WallMs + wallMs,
                    _total.DatabaseMs + _databaseMs,
                    Math.Max(_total.SlowestPulseMs, _slowestPulseMs));

                _last = closed;
                _ticks = _pulses = _dropped = _statements = 0;
                _databaseMs = _slowestPulseMs = 0;
                _windowStartedUtc = now;
            }

            if (closed.IsEmpty)
                return;

            Publish(closed);
        }

        private void Publish(SimMetricsWindow window)
        {
            // Both of these are outside the lock: a slow sink must never hold up the
            // simulation, and the logger already isolates a failing sink.
            if (_logger != null)
            {
                LogLevel level = window.Dropped > 0 ? LogLevel.Warn : LogLevel.Info;
                _logger.Log(level, window.ToLine(), null, "Simulacao");
            }

            if (_telemetry == null)
                return;

            ITelemetryOperation operation = _telemetry.StartOperation(
                "FarmSimulation",
                "window",
                dimensions: new Dictionary<string, object>
                {
                    ["engine"] = Engine ?? "?"
                });

            operation.Complete(
                window.Dropped > 0 ? TelemetryOutcome.Failed : TelemetryOutcome.Succeeded,
                null,
                new Dictionary<string, double>
                {
                    ["ticks"] = window.Ticks,
                    ["ticksPerSecond"] = window.TicksPerSecond,
                    ["dbMsPerTick"] = window.MsPerTick,
                    ["statementsPerTick"] = window.StatementsPerTick,
                    ["databaseShare"] = window.DatabaseShare,
                    ["droppedPulses"] = window.Dropped,
                    ["slowestPulseMs"] = window.SlowestPulseMs
                });
        }

        /// <summary>Provider label carried into telemetry, so windows can be compared per engine.</summary>
        public string? Engine { get; set; }

        /// <summary>Writes the closing summary for a run. Called when the simulation stops.</summary>
        public void ReportTotal(string reason)
        {
            SimMetricsWindow total = Total;
            if (total.IsEmpty || _logger == null)
                return;

            _logger.Log(
                LogLevel.Info,
                "fim (" + reason + ") · " + total.ToLine(),
                null,
                "Simulacao");
        }
    }
}
