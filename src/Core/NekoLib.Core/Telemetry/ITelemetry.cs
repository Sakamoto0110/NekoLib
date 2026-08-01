using System.Collections.Generic;

namespace NekoLib.Core.Telemetry
{
    /// <summary>Feature-facing factory for correlated operation timing.</summary>
    public interface ITelemetry
    {
        ITelemetryOperation StartOperation(
            string module,
            string name,
            string? operationId = null,
            string? parentOperationId = null,
            IReadOnlyDictionary<string, object>? dimensions = null);
    }
}
