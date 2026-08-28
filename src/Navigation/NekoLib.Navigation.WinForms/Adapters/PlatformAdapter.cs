using NekoLib.Navigation.Contracts.Pages;
using NekoLib.Navigation.Contracts.Platform;
using NekoLib.Navigation.Contracts.Runtime;
using NekoLib.Navigation.WinForms.Defaults;
using NekoLib.Navigation.WinForms.Hosting;
using System;
using System.Data;
using System.Windows.Forms;

namespace NekoLib.Navigation.WinForms.Adapters
{
    /// <summary>Composes Navigation services for a WinForms <see cref="Control"/> root.</summary>
    public sealed class WinFormsPlatformAdapter : IPlatformAdapter
    {
        /// <inheritdoc />
        public bool CanHandle(object nativeHost)
            => nativeHost is Control;

        /// <inheritdoc />
        public IPageHost CreateHost(object nativeHost)
        {
            if (nativeHost is not Control control)
                throw new ArgumentException(
                    "WinFormsPlatformAdapter requires a System.Windows.Forms.Control as native host.",
                    nameof(nativeHost));

        

            return new WinFormsLayeredPageHostBase(control);
        }
     
        /// <inheritdoc />
        public IEventDispatcherAdapter CreateEventDispatcher(object nativeHost)
        {
            if (nativeHost is not Control control)
                throw new ArgumentException(
                    "WinFormsPlatformAdapter requires a System.Windows.Forms.Control as native host.",
                    nameof(nativeHost));

            return new WinFormsEventDispatcherAdapter(control);
        }

        /// <inheritdoc />
        public IEventSubscriptionAdapter CreateEventSubscriber(object nativeHost)
        {
            return new WinFormsEventSubscriptionAdapter();
        }

        /// <inheritdoc />
        public IInteractionBlocker CreateInteractionBlocker(object nativeHost)
        {
            if (nativeHost is not Control control)
                throw new ArgumentException(
                    "WinFormsPlatformAdapter requires a System.Windows.Forms.Control as native host.",
                    nameof(nativeHost));

            return new WinFormsInteractionBlocker(control);
        }

        /// <inheritdoc />
        public ITimerAdapter CreateTimerAdapter()
            => new WinFormsTimerAdapter();
        /// <inheritdoc />
        public Type GetDefaultLoadingMaskType() =>  typeof(DefaultLoadingMask);

        /// <inheritdoc />
        public IInteractionObserverService CreateInteractionObserverAdapter(object nativeHost)
        {
            if (nativeHost is not Control control)
                throw new ArgumentException(
                    "WinFormsPlatformAdapter requires a System.Windows.Forms.Control as native host.",
                    nameof(nativeHost));

            return new WinFormsInteractionObserver(control);
        }

        /// <inheritdoc />
        public IFocusObserverAdapter CreateFocusObserver(object nativeHost)
            => new WinFormsFocusObserverAdapter();
    }
}
