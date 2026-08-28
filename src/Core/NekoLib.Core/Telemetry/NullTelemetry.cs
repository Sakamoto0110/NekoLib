using System.Collections.Generic;

namespace NekoLib.Core.Telemetry
{
    /// <summary>
    /// Provides a shared telemetry implementation that records and retains nothing.
    /// </summary>
    public sealed class NullTelemetry : ITelemetry, ITelemetrySnapshotSource
    {
        /// <summary>Shared stateless null telemetry service.</summary>
        public static readonly NullTelemetry Instance = new NullTelemetry();

        private NullTelemetry() { }

        /// <summary>Returns one shared, already-completed no-op operation.</summary>
        /// <param name="module">Ignored module identity.</param>
        /// <param name="name">Ignored operation name.</param>
        /// <param name="operationId">Ignored operation identity.</param>
        /// <param name="parentOperationId">Ignored parent identity.</param>
        /// <param name="dimensions">
        /// Ignored dimensions. The dictionary is not enumerated.
        /// </param>
        /// <returns>A shared operation whose identity is empty and whose elapsed time is zero.</returns>
        public ITelemetryOperation StartOperation(
            string module,
            string name,
            string? operationId = null,
            string? parentOperationId = null,
            IReadOnlyDictionary<string, object>? dimensions = null)
            => NullOperation.Instance;

        /// <summary>Returns an empty completed-operation snapshot.</summary>
        /// <param name="maxOperations">Ignored maximum operation count.</param>
        /// <returns>A non-null empty collection.</returns>
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
