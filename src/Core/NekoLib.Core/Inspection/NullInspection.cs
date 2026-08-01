using System;

namespace NekoLib.Core.Inspection
{
    public sealed class NullInspection : IInspectionRecorder, IInspectionSnapshotSource
    {
        public static readonly NullInspection Instance = new NullInspection();

        private NullInspection() { }

        public bool IsEnabled => false;

        public void Record(string module, string operation, Func<object>? payload = null) { }

        public IDisposable RegisterStateProvider(string module, string key, Func<object> snapshot)
            => Disposable.Empty;

        public IDisposable RegisterAction(string module, string name, Func<object?, object?> action)
            => Disposable.Empty;

        public InspectionSnapshot CaptureSnapshot(int maxOperations, TimeSpan timeout)
            => new InspectionSnapshot(
                DateTime.UtcNow,
                Array.Empty<InspectionOperation>(),
                new System.Collections.Generic.Dictionary<string, object>(),
                0,
                0,
                0);
    }
}
