using NekoLib.Navigation.Contracts.Platform;
using System;

namespace NekoLib.Navigation.WinForms.Adapters
{

    public sealed class WinFormsTimerAdapter : ITimerAdapter
    {
        private readonly System.Windows.Forms.Timer _timer;

        public event Action? Tick;

        event Action? ITimerAdapter.Tick
        {
            add { if (value != null) Tick += value; }
            remove { if (value != null) Tick -= value; }
        }

        public int IntervalMilliseconds
        {
            get => _timer.Interval;
            set => _timer.Interval = value;
        }

        public WinFormsTimerAdapter(int intervalMillis = 15000)
        {
            // NAV-008(a): the parameter used to be ignored, leaving the WinForms
            // default of 100 ms. WpfTimerAdapter always honoured it, and
            // NavigationBootstrapLifetime assigns IntervalMilliseconds anyway, so only
            // a direct construction was affected — and it silently ticked 150x too fast.
            _timer = new System.Windows.Forms.Timer { Interval = intervalMillis };
            _timer.Tick += (_, __) => Tick?.Invoke();
        }

        public void Start() => _timer.Start();
        public void Stop() => _timer.Stop();

        public void Dispose()
        {
            _timer.Stop();
            _timer.Dispose();
        }
    }

}
