using NekoLib.Navigation.Contracts.Platform;
using System;
using System.Windows.Threading;

namespace NekoLib.Navigation.Wpf.Adapters
{
    public sealed class TimerAdapter : ITimerAdapter
    {
        private readonly DispatcherTimer _timer;

        public event Action Tick;

        public TimerAdapter()
        {
            _timer = new DispatcherTimer();
            _timer.Tick += OnTick;
        }

        public int IntervalMilliseconds
        {
            get => (int)_timer.Interval.TotalMilliseconds;
            set => _timer.Interval = TimeSpan.FromMilliseconds(value);
        }

        public void Start() => _timer.Start();

        public void Stop() => _timer.Stop();

        public void Dispose()
        {
            _timer.Stop();
            _timer.Tick -= OnTick;
        }

        private void OnTick(object sender, EventArgs e) => Tick?.Invoke();
    }
}
