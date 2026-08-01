using System;

namespace NekoLib.Core.Inspection
{
    /// <summary>
    /// Read-only Inspection surface. It deliberately exposes no registered actions.
    /// </summary>
    public interface IInspectionSnapshotSource
    {
        InspectionSnapshot CaptureSnapshot(int maxOperations, TimeSpan timeout);
    }
}
