namespace NekoLib.Navigation.Metadata
{
    /// <summary>
    /// Timeout metadata carried on page descriptors. The current idle timeout
    /// path resolves the idle page through <see cref="PageRole.Idle"/> and naming
    /// conventions; these values are retained for timeout-specific policies.
    /// </summary>
    public enum PageTimeoutPolicy
    {
        /// <summary>
        /// Use the default timeout behavior for the current runtime.
        /// </summary>
        Inherit = 0,

        /// <summary>
        /// Page opts out of timeout-specific navigation.
        /// </summary>
        Disabled = 1,

        /// <summary>
        /// Page entry should reset timeout tracking when a timeout policy consumes this metadata.
        /// </summary>
        ResetOnEnter = 2,

        /// <summary>
        /// Page is an explicit timeout navigation target when a timeout policy consumes this metadata.
        /// </summary>
        IsTimeoutTarget = 3
    }
}
