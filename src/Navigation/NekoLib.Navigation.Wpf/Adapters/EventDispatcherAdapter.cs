using NekoLib.Navigation.Contracts.Platform;
using System;
using System.Windows.Threading;

namespace NekoLib.Navigation.Wpf.Adapters
{
    /// <summary>
    /// Marshals actions onto the WPF UI thread. When already on the UI thread, Invoke
    /// executes inline; otherwise it is dispatched synchronously.
    /// </summary>
    public sealed class EventDispatcherAdapter : IEventDispatcherAdapter
    {
        private readonly Dispatcher _dispatcher;

        public EventDispatcherAdapter(Dispatcher dispatcher)
        {
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        }

        public void Invoke(Action action)
        {
            if (action == null)
                return;

            if (_dispatcher.CheckAccess())
                action();
            else
                _dispatcher.Invoke(action);
        }

        public void BeginInvoke(Action action)
        {
            if (action == null)
                return;

            _dispatcher.BeginInvoke(action);
        }
    }
}
