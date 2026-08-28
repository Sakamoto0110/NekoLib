using System;

namespace NekoLib.Core.Inspection
{
    /// <summary>
    /// Provides a shared disabled Inspection implementation that retains no data.
    /// </summary>
    public sealed class NullInspection : IInspectionRecorder, IInspectionSnapshotSource
    {
        /// <summary>Shared stateless null Inspection service.</summary>
        public static readonly NullInspection Instance = new NullInspection();

        private NullInspection() { }

        /// <summary>Gets <see langword="false"/> because recording is disabled.</summary>
        public bool IsEnabled => false;

        /// <summary>Drops the operation without invoking the payload factory.</summary>
        /// <param name="module">Ignored module identity.</param>
        /// <param name="operation">Ignored operation name.</param>
        /// <param name="payload">Ignored payload factory, which is never invoked.</param>
        public void Record(string module, string operation, Func<object>? payload = null) { }

        /// <summary>Returns a no-op registration without invoking the provider.</summary>
        /// <param name="module">Ignored module identity.</param>
        /// <param name="key">Ignored provider key.</param>
        /// <param name="snapshot">Ignored provider delegate, which is never invoked.</param>
        /// <returns><see cref="Disposable.Empty"/>.</returns>
        public IDisposable RegisterStateProvider(string module, string key, Func<object> snapshot)
            => Disposable.Empty;

        /// <summary>Returns a no-op experimental registration without invoking the action.</summary>
        /// <param name="module">Ignored module identity.</param>
        /// <param name="name">Ignored action name.</param>
        /// <param name="action">Ignored action delegate, which is never invoked.</param>
        /// <returns><see cref="Disposable.Empty"/>.</returns>
        /// <remarks>This implementation does not make the experimental action contract stable.</remarks>
        public IDisposable RegisterAction(string module, string name, Func<object?, object?> action)
            => Disposable.Empty;

        /// <summary>Creates an empty snapshot with the current UTC timestamp.</summary>
        /// <param name="maxOperations">Ignored maximum operation count.</param>
        /// <param name="timeout">Ignored caller budget.</param>
        /// <returns>An empty snapshot whose capacity and counters are zero.</returns>
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
