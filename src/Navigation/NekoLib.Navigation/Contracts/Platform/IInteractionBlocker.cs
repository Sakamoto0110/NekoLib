namespace NekoLib.Navigation.Contracts.Platform
{
    /// <summary>
    /// Platform service that temporarily prevents user interaction with the
    /// navigation host while a modal surface is active.
    /// </summary>
    public interface IInteractionBlocker  
    {
        /// <summary>
        /// Disable background interaction. The bootstrap coordinates overlapping
        /// modal surfaces before invoking the platform implementation.
        /// </summary>
        void Block();

        /// <summary>
        /// Restore the interaction state changed by <see cref="Block"/>.
        /// </summary>
        void Unblock();
    }

    /// <summary>
    /// Optional extension for hosts whose view collection can change while a
    /// modal surface is active. Implementations keep late pages and overlays
    /// blocked while allowing only the top-most modal surface to interact.
    /// </summary>
    public interface IPageAwareInteractionBlocker : IInteractionBlocker
    {
        /// <summary>
        /// Notifies the blocker that a native view was added to the host.
        /// Modal surfaces participate in the interactive modal stack; every
        /// other view remains background while blocking is active.
        /// </summary>
        void OnViewAdded(object view, bool isModalSurface);

        /// <summary>
        /// Notifies the blocker that a native view left the host.
        /// </summary>
        void OnViewRemoved(object view);
    }
}
