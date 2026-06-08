namespace NekoLib.Navigation.Contracts.Pages
{

    /// <summary>
    /// Platform host that attaches, detaches, and orders page views inside a
    /// native container. Implemented by platform projects, not application pages.
    /// </summary>
    public interface IPageHost
    {
        /// <summary>Add the page native view to the host if needed.</summary>
        void Attach(IPageView page);

        /// <summary>Remove the page native view from the host if present.</summary>
        void Detach(IPageView page);

        /// <summary>Make the page the active content layer.</summary>
        void BringToFront(IPageView page);
         
    }

}
