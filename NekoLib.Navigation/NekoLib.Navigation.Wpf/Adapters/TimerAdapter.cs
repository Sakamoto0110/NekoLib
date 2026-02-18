using PageNav;
using PageNav.Contracts.Plataform;
using System;
using System.Windows.Threading;

namespace PageNav.Wpf.Adapters
{
    public class TimerAdapter : ITimerAdapter
    {
        private DispatcherTimer _timer;
        private Action _tick;

        public void Start(int intervalMilliseconds, Action tick)
        {
            _tick = tick;
            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromMilliseconds(intervalMilliseconds);
            _timer.Tick -= TimerTick;
            _timer.Tick += TimerTick;
            _timer.Start();
        }

        private void TimerTick(object s, EventArgs e) => _tick?.Invoke();

        public void Stop() => _timer.Stop();

        public void Dispose()
        {

            if(_timer != null)
            {
                _timer.Tick -= TimerTick;
                _timer.Stop();                
                _timer = null;
            }
            _tick = null;
        }

        public void Continue()
        {
            _timer.Start();
        }
    }
}