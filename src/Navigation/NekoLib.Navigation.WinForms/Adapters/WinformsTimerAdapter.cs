using NekoLib.Navigation.Contracts.Platform;
using System;

namespace NekoLib.Navigation.WinForms.Adapters
{

    /// <summary>Wraps a WinForms UI timer for Navigation idle-timeout composition.</summary>
    public sealed class WinFormsTimerAdapter : ITimerAdapter
    {
        private readonly System.Windows.Forms.Timer _timer;

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
            get => _timer.Interval;
            set => _timer.Interval = value;
        }

        /// <summary>Initializes the timer with a positive interval.</summary>
        /// <param name="intervalMillis">Initial interval in milliseconds.</param>
        public WinFormsTimerAdapter(int intervalMillis = 15000)
        {
            // NAV-008(a): the parameter used to be ignored, leaving the WinForms
            // default of 100 ms. WpfTimerAdapter always honoured it, and
            // NavigationBootstrapLifetime assigns IntervalMilliseconds anyway, so only
            // a direct construction was affected — and it silently ticked 150x too fast.
            _timer = new System.Windows.Forms.Timer { Interval = intervalMillis };
            _timer.Tick += (_, __) => Tick?.Invoke();
        }

        /// <inheritdoc />
        public void Start() => _timer.Start();
        /// <inheritdoc />
        public void Stop() => _timer.Stop();

        /// <summary>Stops and disposes the native timer.</summary>
        public void Dispose()
        {
            _timer.Stop();
            _timer.Dispose();
        }
    }

}
