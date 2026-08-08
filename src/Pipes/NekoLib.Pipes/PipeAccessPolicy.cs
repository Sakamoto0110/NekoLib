namespace NekoLib.Pipes
{
    /// <summary>
    /// Selects the operating-system access boundary applied when a named-pipe
    /// server is created.
    /// </summary>
    public enum PipeAccessPolicy
    {
        /// <summary>
        /// Use the platform default pipe security. This preserves the original
        /// NekoLib.Pipes behavior and is not an authorization boundary.
        /// </summary>
        PlatformDefault = 0,

        /// <summary>
        /// Allow connections only from the operating-system user that created
        /// the server. This does not protect against another hostile process
        /// already running as that user.
        /// </summary>
        CurrentUserOnly = 1
    }
}
