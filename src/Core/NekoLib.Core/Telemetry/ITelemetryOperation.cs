using System.Collections.Generic;

namespace NekoLib.Core.Telemetry
{
    public interface ITelemetryOperation
    {
        string OperationId { get; }
        bool IsCompleted { get; }

        System.TimeSpan Checkpoint(
            string name,
            IReadOnlyDictionary<string, object>? dimensions = null);

        void Complete(
            TelemetryOutcome outcome,
            IReadOnlyDictionary<string, object>? dimensions = null,
            IReadOnlyDictionary<string, double>? measurements = null);
    }
}
