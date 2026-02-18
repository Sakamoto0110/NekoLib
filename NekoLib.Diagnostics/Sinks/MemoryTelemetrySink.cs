using NekoLib.Diagnostics.Contracts;
using System;
using System.Collections.Generic;

namespace NekoLib.Diagnostics.Runtime.Sinks
{
    /// <summary>
    /// In-memory telemetry sink for testing / debugging.
    /// Not intended for production persistence.
    /// </summary>
    public sealed class MemoryTelemetrySink : ITelemetrySink
    {
        private readonly object _lock = new object();
        private readonly List<TelemetryEvent> _events = new List<TelemetryEvent>();

        public IReadOnlyList<TelemetryEvent> Snapshot()
        {
            lock(_lock) return _events.ToArray();
        }

        public void Track(TelemetryEvent evt)
        {
            if(evt == null) return;
            lock(_lock) _events.Add(evt);
        }
    }
}
