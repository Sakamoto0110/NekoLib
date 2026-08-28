namespace NekoLib.Navigation.Contracts.Pages
{
    /// <summary>
    /// Low-level view operations for platform-specific services (overlays, focus, z-order).
    /// </summary>
    public interface IViewHost
    {
        /// <summary>Adds a native view to the host.</summary>
        /// <param name="view">Platform-native view owned by a Navigation view contract.</param>
        void AddView(object view);

        /// <summary>Removes a native view from the host.</summary>
        /// <param name="view">Previously added native view.</param>
        void RemoveView(object view);

        /// <summary>Moves a native view above its siblings in the host z-order.</summary>
        /// <param name="view">Previously added native view.</param>
        void BringToFront(object view);

        /// <summary>Requests keyboard focus for a native view.</summary>
        /// <param name="view">Previously added native view.</param>
        void Focus(object view);
    }

}
