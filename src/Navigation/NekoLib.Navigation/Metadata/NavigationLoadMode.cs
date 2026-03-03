namespace NekoLib.Navigation.Metadata
{
    /// <summary>
    /// Defines when page loading occurs relative to UI attachment.
    /// 
    /// This controls perceived responsiveness vs. preload behavior.
    /// </summary>
    public enum NavigationLoadMode
    {
        /// <summary>
        /// Attach immediately, then load synchronously.
        /// </summary>
        ShowImmediately = 0,

        /// <summary>
        /// Fully load before attaching to UI.
        /// </summary>
        LoadBeforeShow = 1,

        /// <summary>
        /// Attach immediately and load asynchronously.
        /// </summary>
        LoadInBackground = 2
    }
}