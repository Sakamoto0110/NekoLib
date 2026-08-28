using System.Collections.Generic;

namespace NekoLib.Core.Logging
{
    /// <summary>Provides read-only bounded access to recently accepted log entries.</summary>
    /// <remarks>
    /// This is an optional read capability, separate from emission. Consumers must
    /// not infer that every <see cref="ILogger"/> also implements it.
    /// </remarks>
    public interface ILogSnapshotSource
    {
        /// <summary>Gets the newest requested window in chronological order.</summary>
        /// <param name="maxEntries">
        /// Maximum number of entries to return. A non-positive value requests an
        /// empty result.
        /// </param>
        /// <returns>A non-null read-only view or detached collection of entries.</returns>
        /// <remarks>
        /// The source owns its retention capacity. Returned entries can still
        /// contain sensitive message and exception data.
        /// </remarks>
        IReadOnlyList<LogEntry> GetRecentEntries(int maxEntries);
    }
}
