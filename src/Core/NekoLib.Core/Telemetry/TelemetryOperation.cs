using System;
using System.Collections.Generic;

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
            Checkpoints = checkpoints ?? EmptyCheckpoints;
            Dimensions = dimensions ?? EmptyDimensions;
            Measurements = measurements ?? EmptyMeasurements;
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
            new TelemetryCheckpoint[0];
        private static readonly IReadOnlyDictionary<string, object> EmptyDimensions =
            new Dictionary<string, object>();
        private static readonly IReadOnlyDictionary<string, double> EmptyMeasurements =
            new Dictionary<string, double>();
    }
}
