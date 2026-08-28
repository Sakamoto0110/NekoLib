using System;

namespace NekoLib.Core.Inspection
{
    /// <summary>Defines the read-only Inspection snapshot surface.</summary>
    /// <remarks>
    /// It deliberately exposes no registered actions. Snapshot values can contain
    /// sensitive or mutable application data; a consumer that persists or transmits
    /// them owns access control, redaction, truncation, and retention.
    /// </remarks>
    public interface IInspectionSnapshotSource
    {
        /// <summary>Captures a bounded best-effort view of operations and state.</summary>
        /// <param name="maxOperations">Maximum number of newest operations requested.</param>
        /// <param name="timeout">Shared caller completion budget for state providers.</param>
        /// <returns>A non-null snapshot, which may contain partial state.</returns>
        /// <remarks>
        /// The timeout bounds caller completion; it is not cancellation of
        /// third-party provider code. Concrete implementations define validation of
        /// negative inputs and their provider ordering and failure markers.
        /// </remarks>
        InspectionSnapshot CaptureSnapshot(int maxOperations, TimeSpan timeout);
    }
}
