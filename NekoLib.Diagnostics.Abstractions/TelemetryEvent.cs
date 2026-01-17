using System;
using System.Collections.Generic;

namespace NekoLib.Diagnostics
{
    public sealed class TelemetryEvent
    {
        public DateTime TimestampUtc { get; }
        public string Name { get; }
        public TimeSpan? Duration { get; }
        public IReadOnlyDictionary<string, object> Data { get; }

        public TelemetryEvent(DateTime timestampUtc, string name, TimeSpan? duration = null, IReadOnlyDictionary<string, object> data = null)
        {
            TimestampUtc = timestampUtc;
            Name = name;
            Duration = duration;
            Data = data ?? Empty;
        }

        private static readonly IReadOnlyDictionary<string, object> Empty = new Dictionary<string, object>();
    }
}
