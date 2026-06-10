using NekoLib.Navigation.Contracts.Pages;
using NekoLib.Navigation.Contracts.Runtime;
using System;

namespace NekoLib.Navigation.Contracts.Platform
{
    /// <summary>
    /// Factory boundary between the platform-agnostic navigation runtime and a
    /// concrete UI stack such as WinForms or WPF. Implementations create native
    /// host, dispatcher, timer, interaction, focus, and loading-mask services for
    /// one navigation root.
    /// </summary>
    public interface IPlatformAdapter
    {
        /// <summary>Returns <c>true</c> when this adapter can own <paramref name="host"/>.</summary>
        bool CanHandle(object host);

        /// <summary>Create the page host that attaches and detaches page views.</summary>
        IPageHost CreateHost(object host);

        /// <summary>Create the UI dispatcher used to marshal runtime work.</summary>
        IEventDispatcherAdapter CreateEventDispatcher(object host);

        /// <summary>Create optional platform event subscription support.</summary>
        IEventSubscriptionAdapter CreateEventSubscriber(object host);

        /// <summary>Create the blocker used by modal dialogs and prompts.</summary>
        IInteractionBlocker CreateInteractionBlocker(object host);

        /// <summary>Create a timer suitable for idle timeout wiring.</summary>
        ITimerAdapter CreateTimerAdapter();

        /// <summary>
        /// Return the platform default loading mask type, or <c>null</c> when no
        /// default mask should be auto-registered.
        /// </summary>
        Type GetDefaultLoadingMaskType( );

        /// <summary>
        /// Create an observer for user input under the host. Used by idle timeout.
        /// Return <c>null</c> when the platform cannot observe interaction.
        /// </summary>
        IInteractionObserverService CreateInteractionObserverAdapter(object host);

        /// <summary>
        /// Builds a focus observer for the given host. Required by <c>PopoverService</c>
        /// to wire <c>IUnfocusAware</c> views; adapters that cannot observe focus may
        /// return <c>null</c>, in which case popovers will simply not auto-dismiss.
        /// </summary>
        IFocusObserverAdapter CreateFocusObserver(object host);
    }
}
