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


    }
}
