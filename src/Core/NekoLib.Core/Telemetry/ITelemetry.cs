using System.Collections.Generic;

namespace NekoLib.Core.Telemetry
{
    /// <summary>Defines the feature-facing factory for correlated operation timing.</summary>
    /// <remarks>
    /// The composition root owns the implementation. The caller owns every
    /// returned operation and must complete it explicitly; this interface does
    /// not imply a terminal through disposal.
    /// </remarks>
    public interface ITelemetry
    {
        /// <summary>Starts one application-defined telemetry operation.</summary>
        /// <param name="module">Non-null logical module identity.</param>
        /// <param name="name">Non-null operation name.</param>
        /// <param name="operationId">
        /// Optional correlation identity. A concrete implementation documents how
        /// a null or blank value is replaced.
        /// </param>
        /// <param name="parentOperationId">Optional parent correlation identity.</param>
        /// <param name="dimensions">
        /// Optional initial dimensions. Values are application evidence and can be
        /// sensitive or mutable.
        /// </param>
        /// <returns>A caller-owned operation that requires explicit completion.</returns>
        ITelemetryOperation StartOperation(
            string module,
            string name,
            string? operationId = null,
            string? parentOperationId = null,
            IReadOnlyDictionary<string, object>? dimensions = null);
    }
}
