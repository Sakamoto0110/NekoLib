using System.Collections.Generic;

namespace NekoLib.Core.Telemetry
{
    /// <summary>Represents one caller-owned telemetry operation in progress.</summary>
    /// <remarks>
    /// The contract is intentionally not disposable. Callers explicitly record
    /// checkpoints and one terminal outcome; abandoning an operation does not
    /// invent an outcome.
    /// </remarks>
    public interface ITelemetryOperation
    {
        /// <summary>Gets the implementation-selected correlation identity.</summary>
        string OperationId { get; }

        /// <summary>Gets whether a terminal completion has been accepted.</summary>
        bool IsCompleted { get; }

        /// <summary>Records an intermediate checkpoint.</summary>
        /// <param name="name">Non-null checkpoint name.</param>
        /// <param name="dimensions">Optional checkpoint dimensions.</param>
        /// <returns>The implementation's monotonic elapsed time for the checkpoint.</returns>
        /// <remarks>
        /// A concrete implementation defines duplicate names and calls made after
        /// completion. Dimension values remain application-owned evidence.
        /// </remarks>
        System.TimeSpan Checkpoint(
            string name,
            IReadOnlyDictionary<string, object>? dimensions = null);

        /// <summary>Attempts to record the terminal outcome and terminal evidence.</summary>
        /// <param name="outcome">Terminal classification supplied by the caller.</param>
        /// <param name="dimensions">Optional terminal dimensions.</param>
        /// <param name="measurements">Optional named numeric measurements.</param>
        /// <remarks>
        /// Callers should invoke this once. The supplied NekoLib telemetry pipeline
        /// accepts only the first completion and ignores later attempts. Values are
        /// not serialized, cloned deeply, validated numerically, or redacted by Core.
        /// </remarks>
        void Complete(
            TelemetryOutcome outcome,
            IReadOnlyDictionary<string, object>? dimensions = null,
            IReadOnlyDictionary<string, double>? measurements = null);
    }
}
