using NekoLib.Core.Logging;
using System.Diagnostics;

namespace NekoLib.Logging.Sinks
{
    public sealed class DebugLogSink : ILogSink
    {
        public void Write(LogEntry entry)
        {
            if (entry == null)
                return;

            try { Debug.WriteLine(entry.ToString()); }
            catch { }
        }
    }
}
