using System;
using System.Collections.Generic;

namespace NekoLib.Core.Telemetry
{
    public sealed class TelemetryCheckpoint
    {
        public TelemetryCheckpoint(
            string name,
            TimeSpan elapsed,
            IReadOnlyDictionary<string, object>? dimensions = null)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Elapsed = elapsed;
            Dimensions = dimensions ?? EmptyDimensions;
        }

        public string Name { get; }
        public TimeSpan Elapsed { get; }
        public IReadOnlyDictionary<string, object> Dimensions { get; }

        private static readonly IReadOnlyDictionary<string, object> EmptyDimensions =
            new Dictionary<string, object>();
    }
}
