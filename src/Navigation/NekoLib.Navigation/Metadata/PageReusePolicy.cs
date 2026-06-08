namespace NekoLib.Navigation.Metadata
{
    /// <summary>
    /// Defines how page instances are reused by the navigation runtime.
    /// </summary>
    public enum PageReusePolicy
    {
        /// <summary>
        /// Always create a new instance and dispose it when detached.
        /// </summary>
        Transient = 0,

        /// <summary>
        /// Keep a weakly referenced instance that may be reused while it remains
        /// alive and undisposed.
        /// </summary>
        Cached = 1,

        /// <summary>
        /// Keep one strong instance for the lifetime of the navigation context,
        /// or until the runtime is reset/disposed.
        /// </summary>
        Singleton = 2
    }
}
