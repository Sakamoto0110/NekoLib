using System;

namespace NekoLib.Navigation.Contracts.Runtime
{
    public interface IInteractionObserverService
    {
        /// <summary>
        /// Called when any user interaction is detected.
        /// </summary>
        event Action? InteractionDetected;
    }

}
