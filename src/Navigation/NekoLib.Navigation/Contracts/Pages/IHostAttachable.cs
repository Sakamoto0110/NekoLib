namespace NekoLib.Navigation.Contracts.Pages
{
    /// <summary>
    /// Optional notification implemented by a page whose native view needs to know
    /// about real host membership changes. Platform hosts invoke these callbacks on
    /// the UI thread only when an add/remove actually occurs; repeated attach or
    /// detach requests do not repeat the callback.
    /// </summary>
    public interface IHostAttachable
    {
        /// <summary>Called after the native view is added to <paramref name="host"/>.</summary>
        void OnAttach(IPageHost host);

        /// <summary>Called after the native view is removed from its host.</summary>
        void OnDetach();
    }


 

}
