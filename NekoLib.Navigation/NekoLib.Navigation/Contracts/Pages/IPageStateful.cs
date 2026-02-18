namespace NekoLib.Navigation.Contracts.Pages
{
    /// <summary>
    /// Implemented by pages that want to participate in navigation history
    /// with state restoration.
    /// </summary>
    public interface IPageStateful
    {
        /// <summary>
        /// Capture minimal state needed to restore this page later.
        /// Must be safe to store in memory.
        /// </summary>
        object CaptureState();

        /// <summary>
        /// Restore previously captured state.
        /// </summary>
        void RestoreState(object state);
    }
}
