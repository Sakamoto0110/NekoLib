using System;
using System.Windows.Threading;
using NekoLib.Navigation.Contracts.Platform;

namespace NekoLib.Navigation.Wpf.Adapters
{
    /// <summary>Wraps a WPF dispatcher timer for Navigation idle-timeout composition.</summary>
    public sealed class WpfTimerAdapter : ITimerAdapter
    {
        private readonly DispatcherTimer _timer;

        /// <inheritdoc />
        public event Action? Tick;

        event Action? ITimerAdapter.Tick
        {
            add { if (value != null) Tick += value; }
            remove { if (value != null) Tick -= value; }
        }

        /// <inheritdoc />
        public int IntervalMilliseconds
        {
            get => (int)_timer.Interval.TotalMilliseconds;
            set => _timer.Interval = TimeSpan.FromMilliseconds(value);
        }

        /// <summary>Initializes the timer with a positive interval.</summary>
        /// <param name="intervalMillis">Initial interval in milliseconds.</param>
        public WpfTimerAdapter(int intervalMillis = 15000)
        {
            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(intervalMillis) };
            _timer.Tick += (_, __) => Tick?.Invoke();
        }

        /// <inheritdoc />
        public void Start() => _timer.Start();
        /// <inheritdoc />
        public void Stop() => _timer.Stop();

        /// <summary>Stops the native dispatcher timer.</summary>
        public void Dispose()
        {
            _timer.Stop();
        }
    }
}
