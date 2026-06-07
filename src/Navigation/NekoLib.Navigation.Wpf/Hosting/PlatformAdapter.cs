using NekoLib.Navigation.Contracts.Pages;
using NekoLib.Navigation.Contracts.Platform;
using NekoLib.Navigation.Contracts.Runtime;
using NekoLib.Navigation.Infrastructure;
using NekoLib.Navigation.Wpf.Adapters;
using NekoLib.Navigation.Wpf.Defaults;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace NekoLib.Navigation.Wpf
{
    public sealed class PlatformAdapter : IPlatformAdapter
    {
        public bool CanHandle(object host) => host is Panel;

        public IPageHost CreateHost(object host)
        {
            if (host is not Panel panel)
                throw new ArgumentException(
                    "WPF PlatformAdapter requires a System.Windows.Controls.Panel as native host.",
                    nameof(host));

            return new PageHost(panel);
        }

        public IEventDispatcherAdapter CreateEventDispatcher(object host)
            => new EventDispatcherAdapter(ResolveDispatcher(host));

        public IEventSubscriptionAdapter CreateEventSubscriber(object host)
            => new EventSubscriptionAdapter();

        public IInteractionBlocker CreateInteractionBlocker(object host)
        {
            if (host is not UIElement element)
                throw new ArgumentException(nameof(host));

            return new InteractionBlocker(element);
        }

        public ITimerAdapter CreateTimerAdapter()
            => new TimerAdapter();

        public Type GetDefaultLoadingMaskType()
            => typeof(DefaultLoadingMask);

        public IInteractionObserverService CreateInteractionObserverAdapter(object host)
        {
            if (host is not UIElement element)
                throw new ArgumentException(nameof(host));

            return new InteractionObserver(element);
        }

        private static Dispatcher ResolveDispatcher(object host)
        {
            if (host is DispatcherObject dispatcherObject && dispatcherObject.Dispatcher != null)
                return dispatcherObject.Dispatcher;

            return Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
        }
    }


    public static class WpfBootstrap
    {
        public static void Register() =>
            PlatformRegistry.Register(new PlatformAdapter());
    }
}
