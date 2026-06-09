using System;

namespace NekoLib.DebugUtils
{
    /// <summary>
    /// Configuration for <see cref="DebugUtilsRuntime"/>. Policy only — no runtime state.
    /// </summary>
    public sealed class DebugUtilsOptions
    {
        /// <summary>
        /// Maximum number of operations retained in the ring buffer. Older entries
        /// are evicted FIFO once the cap is reached. Must be at least 1.
        /// </summary>
        public int Capacity { get; set; } = 1024;
    }
}
