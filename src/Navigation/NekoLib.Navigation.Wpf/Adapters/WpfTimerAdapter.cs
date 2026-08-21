using System;
using System.Windows.Threading;
using NekoLib.Navigation.Contracts.Platform;

namespace NekoLib.Navigation.Wpf.Adapters
{
    public sealed class WpfTimerAdapter : ITimerAdapter
    {
        private readonly DispatcherTimer _timer;

        public event Action Tick;

        event Action? ITimerAdapter.Tick
        {
            add { if (value != null) Tick += value; }
            remove { if (value != null) Tick -= value; }
        }

        public int IntervalMilliseconds
        {
            get => (int)_timer.Interval.TotalMilliseconds;
            set => _timer.Interval = TimeSpan.FromMilliseconds(value);
        }

        public WpfTimerAdapter(int intervalMillis = 15000)
        {
            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(intervalMillis) };
            _timer.Tick += (_, __) => Tick?.Invoke();
        }

        public void Start() => _timer.Start();
        public void Stop() => _timer.Stop();

        public void Dispose()
        {
            _timer.Stop();
        }
    }
}
