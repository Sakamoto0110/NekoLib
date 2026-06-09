using NekoLib.Core.Diagnostics;
using System.Diagnostics;

namespace NekoLib.Logger.Sinks
{
    /// <summary>
    /// Minimal console sink for debug. Safe for net481 and net9.
    /// </summary>
    public sealed class DebugLogSink : ILogSink
    {
        public void Write(LogEntry entry)
        {
            if (entry == null) return;
            try
            {
                var ex = entry.Exception == null ? "" : (" | " + entry.Exception);
                Debug.WriteLine($"[{entry.TimestampUtc:O}] {entry.Level} {entry.Category}: {entry.Message}{ex}");
            }
            catch { }
        }
    }
}
