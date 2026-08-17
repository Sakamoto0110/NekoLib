using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace NekoLib.Core.Telemetry
{
    /// <summary>Immutable completed operation captured by the telemetry pipeline.</summary>
    public sealed class TelemetryOperation
    {
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

        public DateTime StartedUtc { get; }
        public string Module { get; }
        public string Name { get; }
        public string OperationId { get; }
        public string? ParentOperationId { get; }
        public TelemetryOutcome Outcome { get; }
        public TimeSpan Duration { get; }
        public IReadOnlyList<TelemetryCheckpoint> Checkpoints { get; }
        public IReadOnlyDictionary<string, object> Dimensions { get; }
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
