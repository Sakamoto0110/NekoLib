using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace NekoLib.Core.Telemetry
{
    /// <summary>Represents a structurally read-only checkpoint within an operation.</summary>
    /// <remarks>
    /// The dimensions dictionary is copied and read-only-wrapped with ordinal keys.
    /// Contained values remain shallow application references and are not redacted.
    /// </remarks>
    public sealed class TelemetryCheckpoint
    {
        /// <summary>Initializes a telemetry checkpoint.</summary>
        /// <param name="name">Non-null checkpoint name. Empty text is preserved.</param>
        /// <param name="elapsed">Caller-supplied elapsed duration.</param>
        /// <param name="dimensions">Optional dimensions to copy into the checkpoint.</param>
        /// <exception cref="ArgumentNullException"><paramref name="name"/> is null.</exception>
        public TelemetryCheckpoint(
            string name,
            TimeSpan elapsed,
            IReadOnlyDictionary<string, object>? dimensions = null)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Elapsed = elapsed;
            Dimensions = CopyDimensions(dimensions);
        }

        /// <summary>Gets the checkpoint name.</summary>
        public string Name { get; }

        /// <summary>Gets the caller-supplied elapsed duration.</summary>
        public TimeSpan Elapsed { get; }

        /// <summary>Gets the read-only outer snapshot of checkpoint dimensions.</summary>
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
