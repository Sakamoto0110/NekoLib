namespace NekoLib.Navigation.Contracts.Pages
{
    /// <summary>
    /// Legacy compatibility contract. It is not invoked by the navigation runtime;
    /// application-wide modal interaction is controlled by the platform
    /// <c>IInteractionBlocker</c>.
    /// </summary>
    public interface IPageInteraction
    {
        /// <summary>Enables user interaction for this page.</summary>
        void EnableInteraction();

        /// <summary>Disables user interaction for this page.</summary>
        void DisableInteraction();
    }


 

}
