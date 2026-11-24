using System;
using System.Windows.Threading;
using NekoLib.Navigation.Contracts.Platform;

namespace NekoLib.Navigation.Wpf.Adapters
{
    /// <summary>
    /// Marshals callbacks onto the WPF UI thread via the host's <see cref="Dispatcher"/>.
    /// Mirrors WinFormsEventDispatcherAdapter: runs inline when already on the UI
    /// thread; otherwise marshals while the dispatcher is alive. During shutdown
    /// only its owner may run callbacks inline; workers fail without execution.
    /// </summary>
    public sealed class WpfEventDispatcherAdapter : IEventDispatcherAdapter
    {
        private readonly Dispatcher _dispatcher;

        /// <summary>Initializes the adapter for one WPF dispatcher.</summary>
        /// <param name="dispatcher">Dispatcher that owns the navigation UI.</param>
        public WpfEventDispatcherAdapter(Dispatcher dispatcher)
        {
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        }

        /// <inheritdoc />
        public void Invoke(Action action)
        {
            if (action == null) return;

            if (_dispatcher.HasShutdownStarted || _dispatcher.HasShutdownFinished)
            {
                _dispatcher.VerifyAccess();
                action();
                return;
            }

            if (_dispatcher.CheckAccess())
                action();
            else
                _dispatcher.Invoke(action);
        }

        /// <inheritdoc />
        public void BeginInvoke(Action action)
        {
            if (action == null) return;

            // Teardown can run inline only on the owning thread. A dead dispatcher
            // never licenses callbacks to mutate WPF objects on a worker thread.
            if (_dispatcher.HasShutdownStarted || _dispatcher.HasShutdownFinished)
            {
                _dispatcher.VerifyAccess();
                action();
                return;
            }

            _dispatcher.BeginInvoke(action);
        }
    }
}
