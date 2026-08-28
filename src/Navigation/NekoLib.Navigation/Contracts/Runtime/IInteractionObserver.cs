using System;

namespace NekoLib.Navigation.Contracts.Runtime
{
    /// <summary>
    /// Reports host-level user interaction used to restart the idle timeout.
    /// </summary>
    public interface IInteractionObserverService
    {
        /// <summary>
        /// Called when any user interaction is detected.
        /// </summary>
        event Action? InteractionDetected;
    }

}
