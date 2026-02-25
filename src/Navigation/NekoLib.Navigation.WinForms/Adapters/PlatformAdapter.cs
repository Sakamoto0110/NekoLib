using NekoLib.Navigation.Contracts.Platform;
using NekoLib.Navigation.Contracts.Pages;
using NekoLib.Navigation.WinForms.Hosting;
using System;
using System.Windows.Forms;
using NekoLib.Navigation.Contracts.Runtime;

namespace NekoLib.Navigation.WinForms.Adapters
{
    public sealed class WinFormsPlatformAdapter : IPlatformAdapter
    {
        public bool CanHandle(object nativeHost)
            => nativeHost is Control;

        public IPageHost CreateHost(object nativeHost)
        {
            if (nativeHost is not Control control)
                throw new ArgumentException(
                    "WinFormsPlatformAdapter requires a System.Windows.Forms.Control as native host.",
                    nameof(nativeHost));

            if (control is not Panel panel)
                throw new InvalidOperationException(
                    "WinFormsPlatformAdapter requires the host to be a Panel.");

            return new PanelPageHost(panel);
        }

        public IEventDispatcherAdapter CreateEventDispatcher(object nativeHost)
        {
            if (nativeHost is not Control control)
                throw new ArgumentException(nameof(nativeHost));

            return new WinFormsEventDispatcherAdapter(control);
        }

        public IEventSubscriptionAdapter CreateEventSubscriber(object nativeHost)
        {
            return new WinFormsEventSubscriptionAdapter();
        }

        public IInteractionBlocker CreateInteractionBlocker(object nativeHost)
        {
            if (nativeHost is not Control control)
                throw new ArgumentException(nameof(nativeHost));

            return new WinFormsInteractionBlocker(control);
        }

        public ITimerAdapter CreateTimerAdapter()
            => new WinFormsTimerAdapter();

        public IPageOverlay CreateOverlayService(object nativeHost)
        {
            // Optional — return null if not used
            return null;
        }

        public IInteractionObserverService CreateInteractionObserverAdapter(object nativeHost)
        {
            if (nativeHost is not Control control)
                throw new ArgumentException(nameof(nativeHost));

            return new WinFormsInteractionObserver(control);
        }
    }
}
