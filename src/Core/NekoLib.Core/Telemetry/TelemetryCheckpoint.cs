using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

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
            Dimensions = CopyDimensions(dimensions);
        }

        public string Name { get; }
        public TimeSpan Elapsed { get; }
        public IReadOnlyDictionary<string, object> Dimensions { get; }

        private static readonly IReadOnlyDictionary<string, object> EmptyDimensions =
            new ReadOnlyDictionary<string, object>(
                new Dictionary<string, object>(StringComparer.Ordinal));

        private static IReadOnlyDictionary<string, object> CopyDimensions(
            IReadOnlyDictionary<string, object>? dimensions)
        {
            if (dimensions == null || dimensions.Count == 0)
                return EmptyDimensions;

            var copy = new Dictionary<string, object>(
                dimensions.Count,
                StringComparer.Ordinal);
            foreach (var pair in dimensions)
                copy[pair.Key] = pair.Value;

            return new ReadOnlyDictionary<string, object>(copy);
        }
    }
}
