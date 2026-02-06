using System;
using System.Diagnostics;

namespace NekoLib.Diagnostics.Sinks
{
    /// <summary>
    /// Minimal console sink. Safe for net481 and net9.
    /// </summary>
    public sealed class ConsoleLogSink : ILogSink
    {
        public void Write(LogEntry entry)
        {
            if(entry == null) return;
            try
            {
                var ex = entry.Exception == null ? "" : (" | " + entry.Exception);
                Debug.WriteLine($"[{entry.TimestampUtc:O}] {entry.Level} {entry.Category}: {entry.Message}{ex}");
            }
            catch { }
        }
    }
}
