using System;
using System.Windows;
using System.Windows.Controls;
using NekoLib.Navigation.Contracts.Pages;
using NekoLib.Navigation.Contracts.Platform;
using NekoLib.Navigation.Contracts.Runtime;
using NekoLib.Navigation.Wpf.Defaults;
using NekoLib.Navigation.Wpf.Hosting;

namespace NekoLib.Navigation.Wpf.Adapters
{
    /// <summary>
    /// WPF platform adapter. Mirrors WinFormsPlatformAdapter — the host is expected
    /// to be a <see cref="Panel"/> (typically a <see cref="Grid"/>) so layered
    /// pages and overlays can coexist via Panel.ZIndex.
    /// </summary>
    public sealed class WpfPlatformAdapter : IPlatformAdapter
    {
        /// <inheritdoc />
        public bool CanHandle(object host) => host is Panel;

        /// <inheritdoc />
        public IPageHost CreateHost(object host)
        {
            if (host is not Panel panel)
                throw new ArgumentException(
                    "WpfPlatformAdapter requires a System.Windows.Controls.Panel as native host.",
                    nameof(host));

            return new WpfLayeredPageHostBase(panel);
        }

        /// <inheritdoc />
        public IEventDispatcherAdapter CreateEventDispatcher(object host)
        {
            if (host is not UIElement element)
                throw new ArgumentException("Host must be a UIElement.", nameof(host));

            return new WpfEventDispatcherAdapter(element.Dispatcher);
        }

        /// <inheritdoc />
        public IEventSubscriptionAdapter CreateEventSubscriber(object host)
            => new WpfEventSubscriptionAdapter();

        /// <inheritdoc />
        public IInteractionBlocker CreateInteractionBlocker(object host)
        {
            if (host is not UIElement element)
                throw new ArgumentException("Host must be a UIElement.", nameof(host));

            return new WpfInteractionBlocker(element);
        }

        /// <inheritdoc />
        public ITimerAdapter CreateTimerAdapter() => new WpfTimerAdapter();

        /// <inheritdoc />
        public Type GetDefaultLoadingMaskType() => typeof(DefaultLoadingMask);

        /// <inheritdoc />
        public IInteractionObserverService CreateInteractionObserverAdapter(object host)
        {
            if (host is not UIElement element)
                throw new ArgumentException("Host must be a UIElement.", nameof(host));

            return new WpfInteractionObserver(element);
        }

        /// <inheritdoc />
        public IFocusObserverAdapter CreateFocusObserver(object host)
            => new WpfFocusObserverAdapter();
    }
}
