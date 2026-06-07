using System;
using System.Windows.Threading;
using NekoLib.Navigation.Contracts.Platform;

namespace NekoLib.Navigation.Wpf.Adapters
{
    /// <summary>
    /// Marshals callbacks onto the WPF UI thread via the host's <see cref="Dispatcher"/>.
    /// Mirrors WinFormsEventDispatcherAdapter: runs inline when already on the UI
    /// thread and the dispatcher is alive; otherwise posts via BeginInvoke.
    /// </summary>
    public sealed class WpfEventDispatcherAdapter : IEventDispatcherAdapter
    {
        private readonly Dispatcher _dispatcher;

        public WpfEventDispatcherAdapter(Dispatcher dispatcher)
        {
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        }

        public void Invoke(Action action)
        {
            if (action == null) return;

            if (_dispatcher.HasShutdownStarted || _dispatcher.HasShutdownFinished)
            {
                action();
                return;
            }

            if (_dispatcher.CheckAccess())
                action();
            else
                _dispatcher.Invoke(action);
        }

        public void BeginInvoke(Action action)
        {
            if (action == null) return;

            // Dispatcher torn down (e.g. app shutdown). Run inline as a best-effort
            // fallback so DisposeAsync paths still complete (A-4 analogue).
            if (_dispatcher.HasShutdownStarted || _dispatcher.HasShutdownFinished)
            {
                action();
                return;
            }

            _dispatcher.BeginInvoke(action);
        }
    }
}
