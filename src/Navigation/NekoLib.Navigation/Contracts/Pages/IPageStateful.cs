namespace NekoLib.Navigation.Contracts.Pages
{
    /// <summary>
    /// Implemented by pages that want to participate in navigation history with state
    /// restoration. When the user navigates away, the runtime calls
    /// <see cref="CaptureState"/> and stores the result in the history entry. When the
    /// user navigates <em>back</em> to this page, the runtime calls
    /// <see cref="RestoreState"/> with that blob <b>before</b> the page's
    /// <c>OnNavigatedToAsync</c> runs. The same blob is also exposed as
    /// <c>NavigationArgs.Payload</c> (with <c>NavigationArgs.IsBackNavigation == true</c>),
    /// but <see cref="RestoreState"/> is the preferred, unambiguous channel.
    /// </summary>
    public interface IPageStateful
    {
        /// <summary>
        /// Capture the minimal state needed to restore this page later. Must be safe to
        /// hold in memory for the lifetime of the history entry. Return <c>null</c> if
        /// there is nothing to restore.
        /// </summary>
        object CaptureState();

        /// <summary>
        /// Restore state previously produced by <see cref="CaptureState"/>. Called by the
        /// runtime only on back-navigation, before <c>OnNavigatedToAsync</c>. The argument
        /// is whatever <see cref="CaptureState"/> returned (may be <c>null</c>).
        /// </summary>
        void RestoreState(object state);
    }
}
