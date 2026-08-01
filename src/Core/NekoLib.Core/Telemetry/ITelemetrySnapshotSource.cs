using System.Collections.Generic;

namespace NekoLib.Core.Telemetry
{
    public interface ITelemetrySnapshotSource
    {
        IReadOnlyList<TelemetryOperation> GetRecentOperations(int maxOperations);
    }
}
