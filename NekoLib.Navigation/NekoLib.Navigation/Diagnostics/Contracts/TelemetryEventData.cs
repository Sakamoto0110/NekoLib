using System;
using System.Collections.Generic;

namespace NekoLib.Diagnostics.Contracts
{
    /// <summary>
    /// Represents structured telemetry data.
    /// This is a pure data model used by telemetry implementations.
    /// </summary>
    public sealed class TelemetryEventData
    {
        public DateTime TimestampUtc { get; }
        public string Name { get; }
        public TimeSpan? Duration { get; }
        public IReadOnlyDictionary<string, object> Data { get; }

        public TelemetryEventData(
            DateTime timestampUtc,
            string name,
            TimeSpan? duration = null,
            IReadOnlyDictionary<string, object> data = null)
        {
            TimestampUtc = timestampUtc;
            Name = name ?? string.Empty;
            Duration = duration;
            Data = data ?? Empty;
        }

        private static readonly IReadOnlyDictionary<string, object> Empty =
            new Dictionary<string, object>();
    }
}
