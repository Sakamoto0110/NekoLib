using System.Collections.Generic;

namespace NekoLib.Core.Telemetry
{
    public sealed class NullTelemetry : ITelemetry, ITelemetrySnapshotSource
    {
        public static readonly NullTelemetry Instance = new NullTelemetry();

        private NullTelemetry() { }

        public ITelemetryOperation StartOperation(
            string module,
            string name,
            string? operationId = null,
            string? parentOperationId = null,
            IReadOnlyDictionary<string, object>? dimensions = null)
            => NullOperation.Instance;

        public IReadOnlyList<TelemetryOperation> GetRecentOperations(int maxOperations)
            => System.Array.Empty<TelemetryOperation>();

        private sealed class NullOperation : ITelemetryOperation
        {
            public static readonly NullOperation Instance = new NullOperation();
            private NullOperation() { }

            public string OperationId => string.Empty;
            public bool IsCompleted => true;

            public System.TimeSpan Checkpoint(
                string name,
                IReadOnlyDictionary<string, object>? dimensions = null)
            {
                return System.TimeSpan.Zero;
            }

            public void Complete(
                TelemetryOutcome outcome,
                IReadOnlyDictionary<string, object>? dimensions = null,
                IReadOnlyDictionary<string, double>? measurements = null)
            {
            }
        }
    }
}
