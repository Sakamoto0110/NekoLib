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
        IDisposable RegisterAction(string module, string name, Func<object?, object?> action);
    }
}
