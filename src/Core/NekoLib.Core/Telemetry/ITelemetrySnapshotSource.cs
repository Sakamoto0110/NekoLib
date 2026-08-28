using System.Collections.Generic;

namespace NekoLib.Core.Telemetry
{
    /// <summary>Provides read-only bounded access to recently completed operations.</summary>
    /// <remarks>
    /// This capability is separate from <see cref="ITelemetry"/> production;
    /// consumers must not assume every telemetry implementation also retains data.
    /// </remarks>
    public interface ITelemetrySnapshotSource
    {
        /// <summary>Gets the newest requested completed-operation window in order.</summary>
        /// <param name="maxOperations">
        /// Maximum number of operations to return. A non-positive value requests an
        /// empty result.
        /// </param>
        /// <returns>A non-null read-only view or detached collection.</returns>
        IReadOnlyList<TelemetryOperation> GetRecentOperations(int maxOperations);
    }
}
