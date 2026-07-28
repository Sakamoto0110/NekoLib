using NekoLib.Navigation.Contracts.Platform;

namespace NekoLib.Navigation.Runtime.Services
{
    /// <summary>
    /// Internal attachment seam that keeps the public surface-service
    /// constructors unchanged while allowing the runtime/bootstrap to provide
    /// the optional late-view blocker extension.
    /// </summary>
    internal interface INavigationInteractionBlockerAware
    {
        void AttachInteractionBlocker(
            IPageAwareInteractionBlocker interactionBlocker);
    }
}
