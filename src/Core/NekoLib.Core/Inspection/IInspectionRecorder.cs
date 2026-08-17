using System;

namespace NekoLib.Core.Inspection
{
    /// <summary>
    /// Opt-in module-facing runtime inspection contract. Implementations run
    /// inline, so payload and provider work must remain bounded.
    /// </summary>
    public interface IInspectionRecorder
    {
        bool IsEnabled { get; }
        void Record(string module, string operation, Func<object>? payload = null);
        IDisposable RegisterStateProvider(string module, string key, Func<object> snapshot);

        /// <summary>
        /// Registers an in-process action with the concrete Inspection owner.
        /// Authorization, asynchronous execution, cancellation, timeout, UI
        /// marshalling, and module adoption are not stable contracts.
        /// </summary>
        [Obsolete("Experimental API NEKOEXP0001: compatibility is not guaranteed.", error: false)]
        IDisposable RegisterAction(string module, string name, Func<object?, object?> action);
    }
}
