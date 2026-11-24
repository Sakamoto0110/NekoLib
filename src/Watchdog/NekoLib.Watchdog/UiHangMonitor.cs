using System;

namespace NekoLib.Watchdog
{
    internal sealed class UiHangMonitor
    {
        private readonly object _gate = new object();
        private readonly bool _enabled;
        private readonly int _timeoutMs;
        private readonly int _graceMs;
        private readonly Func<int, UiWindowSample> _probe;
        private int _pid;
        private long _startedAt;
        private long _nextProbe;
        private long? _failedSince;
        private long _window;
        private int _error;
        private string _state;
        private long _revision;
        internal long Revision => System.Threading.Interlocked.Read(ref _revision);

        internal UiHangMonitor(bool enabled, int timeoutMs, int graceMs, Func<int, UiWindowSample>? probe = null)
        {
            _enabled = enabled;
            _timeoutMs = timeoutMs;
            _graceMs = graceMs;
            _probe = probe ?? new UiWindowProbe().Check;
            _state = enabled ? "waiting" : "disabled";
        }

        internal void Reset(int pid, long now, string state = "warming_up")
        {
            lock (_gate)
            {
                System.Threading.Interlocked.Increment(ref _revision);
                _pid = pid;
                _startedAt = now;
                _nextProbe = now;
                _failedSince = null;
                _window = 0;
                _error = 0;
                _state = _enabled ? state : "disabled";
            }
        }

        internal bool Check(int pid, long now, bool running)
            => Check(pid, now, running, out _);

        internal bool Check(int pid, long now, bool running, out long revision)
        {
            lock (_gate)
            {
                revision = _revision;
                if (!_enabled) return false;
                if (_pid != pid) Reset(pid, now);
                revision = _revision;
                if (!running) { Reset(pid, now, "paused"); return false; }
                if (now < _nextProbe) return false;
                _nextProbe = now + Math.Min(1000, _timeoutMs / 4);
                UiWindowSample sample;
                try { sample = _probe(pid); }
                catch { sample = new UiWindowSample(UiWindowState.Unavailable); }
                if (_window != sample.Window) _failedSince = null;
                _window = sample.Window;
                _error = sample.Error;
                if (now - _startedAt < _graceMs)
                { _state = "warming_up"; _failedSince = null; return false; }
                switch (sample.State)
                {
                    case UiWindowState.Responding: _state = "responsive"; _failedSince = null; return false;
                    case UiWindowState.NoWindow: _state = "no_window"; _failedSince = null; return false;
                    case UiWindowState.Unavailable: _state = "unavailable"; _failedSince = null; return false;
                    default:
                        _state = "unresponsive";
                        if (!_failedSince.HasValue) _failedSince = now;
                        return now - _failedSince.Value >= _timeoutMs;
                }
            }
        }

        internal object Snapshot(long now)
        {
            lock (_gate) return new
            {
                enabled = _enabled, state = _state, pid = _pid == 0 ? (int?)null : _pid,
                windowHandle = _window == 0 ? null : _window.ToString("X", System.Globalization.CultureInfo.InvariantCulture),
                consecutiveFailureMs = _failedSince.HasValue ? Math.Max(0, now - _failedSince.Value) : 0,
                timeoutMs = _timeoutMs, startupGraceMs = _graceMs, lastErrorCode = _error
            };
        }
    }
}
