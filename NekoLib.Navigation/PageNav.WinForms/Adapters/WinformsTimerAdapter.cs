using NekoLib.Navigation.Contracts.Plataform;
using System;

namespace NekoLib.Navigation.Adapters
{

    public sealed class WinFormsTimerAdapter : ITimerAdapter
    {
        private readonly System.Windows.Forms.Timer _timer;

        public event Action Tick;

        public int IntervalMilliseconds
        {
            get => _timer.Interval;
            set => _timer.Interval = value;
        }

        public WinFormsTimerAdapter(int intervalMilis = 15000)
        {
            _timer = new System.Windows.Forms.Timer();
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
