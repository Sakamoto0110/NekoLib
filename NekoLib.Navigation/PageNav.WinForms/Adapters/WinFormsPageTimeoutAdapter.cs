namespace NekoLib.Navigation.Adapters
{
    using NekoLib.Navigation.Contracts.Plataform;
    using System;
    using System.Windows.Forms;

    public sealed class WinFormsPageTimeoutAdapter : IPageTimeoutAdapter
    {
        private readonly Timer _timer;

        public event Action TimeoutElapsed;

        public WinFormsPageTimeoutAdapter()
        {
            _timer = new Timer();
            _timer.Tick += OnTick;
        }

        public void Start(int timeoutMilliseconds)
        {
            if(timeoutMilliseconds <= 0)
                return;

            _timer.Stop();
            _timer.Interval = timeoutMilliseconds;
            _timer.Start();
        }

        public void Stop()
        {
            _timer.Stop();
        }

        private void OnTick(object sender, EventArgs e)
        {
            _timer.Stop(); // single-shot
            TimeoutElapsed?.Invoke();
        }

        public void Dispose()
        {
            _timer.Tick -= OnTick;
            _timer.Dispose();
        }
    }

}
