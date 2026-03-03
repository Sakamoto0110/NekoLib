namespace NekoLib.Navigation.Metadata
{
    /// <summary>
    /// Defines how page instances are reused and cached.
    /// 
    /// This policy is handled exclusively by PageFactory.
    /// </summary>
    public enum PageReusePolicy
    {
        /// <summary>
        /// Always create a new instance.
        /// Dispose when detached.
        /// </summary>
        Transient = 0,

        /// <summary>
        /// Keep a single instance while the navigation
        /// context is alive.
        /// </summary>
        Cached = 1,

        /// <summary>
        /// Single instance for the entire application lifetime.
        /// </summary>
        Singleton = 2
    }
}