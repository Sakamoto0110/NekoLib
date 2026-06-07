using NekoLib.Navigation.Contracts.Pages;
using NekoLib.Navigation.Contracts.Runtime;
using System;

/// <summary>
/// TODO: Document this type.
/// Describe responsibility, lifecycle expectations,
/// threading guarantees, and ownership rules.
/// </summary>
namespace NekoLib.Navigation.Contracts.Platform
{
    public interface IPlatformAdapter
    {
        
        bool CanHandle(object host);
        IPageHost CreateHost(object host);
        IEventDispatcherAdapter CreateEventDispatcher(object host);
        IEventSubscriptionAdapter CreateEventSubscriber(object host);

        IInteractionBlocker CreateInteractionBlocker(object host);
        ITimerAdapter CreateTimerAdapter();

        Type GetDefaultLoadingMaskType( );


          IInteractionObserverService CreateInteractionObserverAdapter(object host);

        /// <summary>
        /// Builds a focus observer for the given host. Required by <c>PopoverService</c>
        /// to wire <c>IUnfocusAware</c> views; adapters that cannot observe focus may
        /// return <c>null</c>, in which case popovers will simply not auto-dismiss.
        /// </summary>
        IFocusObserverAdapter CreateFocusObserver(object host);
    }
}
