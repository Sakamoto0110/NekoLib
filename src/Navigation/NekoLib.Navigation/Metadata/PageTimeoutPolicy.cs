namespace NekoLib.Navigation.Metadata
{
    /// <summary>
    /// Defines how a page interacts with the global timeout system.
    /// 
    /// This controls idle handling behavior.
    /// </summary>
    public enum PageTimeoutPolicy
    {
        /// <summary>
        /// Use framework default timeout behavior.
        /// </summary>
        Inherit = 0,

        /// <summary>
        /// Page is never affected by timeout.
        /// </summary>
        Disabled = 1,

        /// <summary>
        /// Reset global timeout when entering this page.
        /// </summary>
        ResetOnEnter = 2,

        /// <summary>
        /// This page becomes the timeout navigation target.
        /// </summary>
        IsTimeoutTarget = 3
    }
}