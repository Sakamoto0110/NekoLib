using System.Collections.Generic;

namespace NekoLib.Core.Logging
{
    /// <summary>Read-only bounded access to recently accepted log entries.</summary>
    public interface ILogSnapshotSource
    {
        IReadOnlyList<LogEntry> GetRecentEntries(int maxEntries);
    }
}
