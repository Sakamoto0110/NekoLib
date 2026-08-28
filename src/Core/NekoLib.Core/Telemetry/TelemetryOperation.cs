using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace NekoLib.Core.Telemetry
{
    /// <summary>Represents a structurally read-only completed telemetry operation.</summary>
    /// <remarks>
    /// Checkpoints, dimensions, and measurements are copied and read-only-wrapped.
    /// Dimension values and checkpoint instances remain shallow references. Core
    /// does not serialize, deep-clone, validate, or redact application evidence.
    /// </remarks>
    public sealed class TelemetryOperation
    {
        /// <summary>Initializes a completed telemetry operation model.</summary>
        /// <param name="startedUtc">
        /// Caller-supplied start timestamp. The constructor does not validate or
        /// rewrite <see cref="DateTime.Kind"/>.
        /// </param>
        /// <param name="module">Non-null logical module identity.</param>
        /// <param name="name">Non-null operation name.</param>
        /// <param name="operationId">Non-null operation correlation identity.</param>
        /// <param name="parentOperationId">Optional parent correlation identity, retained verbatim.</param>
        /// <param name="outcome">Caller-supplied terminal classification.</param>
        /// <param name="duration">Caller-supplied duration.</param>
        /// <param name="checkpoints">Optional ordered checkpoint collection to copy.</param>
        /// <param name="dimensions">Optional dimension dictionary to copy with ordinal keys.</param>
        /// <param name="measurements">Optional measurement dictionary to copy with ordinal keys.</param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="module"/>, <paramref name="name"/>, or
        /// <paramref name="operationId"/> is null.
        /// </exception>
        public TelemetryOperation(
            DateTime startedUtc,
            string module,
            string name,
            string operationId,
            string? parentOperationId,
            TelemetryOutcome outcome,
            TimeSpan duration,
            IReadOnlyList<TelemetryCheckpoint>? checkpoints = null,
            IReadOnlyDictionary<string, object>? dimensions = null,
            IReadOnlyDictionary<string, double>? measurements = null)
        {
            StartedUtc = startedUtc;
            Module = module ?? throw new ArgumentNullException(nameof(module));
            Name = name ?? throw new ArgumentNullException(nameof(name));
            OperationId = operationId ?? throw new ArgumentNullException(nameof(operationId));
            ParentOperationId = parentOperationId;
            Outcome = outcome;
            Duration = duration;
            Checkpoints = CopyCheckpoints(checkpoints);
            Dimensions = CopyDimensions(dimensions);
            Measurements = CopyMeasurements(measurements);
        }

        /// <summary>Gets the caller-supplied operation start timestamp.</summary>
        public DateTime StartedUtc { get; }

        /// <summary>Gets the logical module identity.</summary>
        public string Module { get; }

        /// <summary>Gets the operation name.</summary>
        public string Name { get; }

        /// <summary>Gets the operation correlation identity.</summary>
        public string OperationId { get; }

        /// <summary>Gets the optional parent correlation identity.</summary>
        public string? ParentOperationId { get; }

        /// <summary>Gets the terminal outcome.</summary>
        public TelemetryOutcome Outcome { get; }

        /// <summary>Gets the caller-supplied operation duration.</summary>
        public TimeSpan Duration { get; }

        /// <summary>Gets the read-only outer snapshot of checkpoints in supplied order.</summary>
        public IReadOnlyList<TelemetryCheckpoint> Checkpoints { get; }

        /// <summary>Gets the read-only outer snapshot of dimensions.</summary>
        public IReadOnlyDictionary<string, object> Dimensions { get; }

        /// <summary>Gets the read-only outer snapshot of measurements.</summary>
        public IReadOnlyDictionary<string, double> Measurements { get; }

        private static readonly IReadOnlyList<TelemetryCheckpoint> EmptyCheckpoints =
            new ReadOnlyCollection<TelemetryCheckpoint>(
                new List<TelemetryCheckpoint>());
        private static readonly IReadOnlyDictionary<string, object> EmptyDimensions =
            new ReadOnlyDictionary<string, object>(
                new Dictionary<string, object>(StringComparer.Ordinal));
        private static readonly IReadOnlyDictionary<string, double> EmptyMeasurements =
            new ReadOnlyDictionary<string, double>(
                new Dictionary<string, double>(StringComparer.Ordinal));

        private static IReadOnlyList<TelemetryCheckpoint> CopyCheckpoints(
            IReadOnlyList<TelemetryCheckpoint>? checkpoints)
        {
            if (checkpoints == null || checkpoints.Count == 0)
                return EmptyCheckpoints;

            var copy = new List<TelemetryCheckpoint>(checkpoints.Count);
            for (int i = 0; i < checkpoints.Count; i++)
                copy.Add(checkpoints[i]);

            return new ReadOnlyCollection<TelemetryCheckpoint>(copy);
        }

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

        private static IReadOnlyDictionary<string, double> CopyMeasurements(
            IReadOnlyDictionary<string, double>? measurements)
        {
            if (measurements == null || measurements.Count == 0)
                return EmptyMeasurements;

            var copy = new Dictionary<string, double>(
                measurements.Count,
                StringComparer.Ordinal);
            foreach (var pair in measurements)
                copy[pair.Key] = pair.Value;

            return new ReadOnlyDictionary<string, double>(copy);
        }
    }
}
