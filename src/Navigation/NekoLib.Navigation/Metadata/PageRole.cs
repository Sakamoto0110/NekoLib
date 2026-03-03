 
namespace NekoLib.Navigation.Metadata
{
    /// <summary>
    /// Logical role of a page within the navigation system.
    /// 
    /// This does NOT affect presentation or caching.
    /// It defines semantic identity used by registry,
    /// timeout resolution, and flow logic.
    /// </summary>
    public enum PageRole
    {
        /// <summary>
        /// Standard navigable page.
        /// </summary>
        Normal = 0,

        /// <summary>
        /// Default entry page for the application.
        /// Often used as fallback or reset target.
        /// </summary>
        Home = 1,

        /// <summary>
        /// Explicit timeout destination.
        /// Overrides default home timeout target.
        /// </summary>
        TimeoutTarget = 2
    }
}