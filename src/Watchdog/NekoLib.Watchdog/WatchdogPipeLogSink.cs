using NekoLib.Diagnostics.Contracts;
using System;

namespace NekoLib.Watchdog
{
    public sealed class WatchdogPipeLogSink : ILogSink, IDisposable
    {
        [Obsolete("The pipe name is resolved from the current process path; this parameter is ignored.")]
        public WatchdogPipeLogSink(string pipeName = "NekoLib.Watchdog.logs")
        {
        }

        public void Write(LogEntry entry)
        {
            WatchdogController.NotifyLog(entry);
        }

        public void Dispose() { }
    }
}
