 
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
        /// Idle / default-state page. The system returns here when the user
        /// is inactive (idle timeout) and uses it as the fallback or reset
        /// target. Replaces the older "Home" name to make the semantics
        /// explicit. This is not necessarily the app's launch page; it is
        /// the page the runtime falls back to when there is nothing else
        /// to show.
        /// </summary>
        Idle = 1
    }
}
