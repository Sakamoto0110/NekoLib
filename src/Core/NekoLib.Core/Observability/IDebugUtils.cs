using System;

namespace NekoLib.Core.Observability
{
    /// <summary>
    /// Optional global observability sink. Modules push operations and expose
    /// read-only state through this contract without referencing any concrete
    /// implementation, keeping the dependency inverted.
    ///
    /// When no implementation is registered, <see cref="NullDebugUtils"/> is used:
    /// every member is a no-op and <see cref="IsEnabled"/> is false, so production
    /// builds pay only a branch check.
    /// </summary>
    public interface IDebugUtils
    {
        /// <summary>
        /// True when a real implementation is attached. Guard expensive payload
        /// construction with this flag.
        /// </summary>
        bool IsEnabled { get; }

        /// <summary>
        /// Records a single operation. <paramref name="payload"/> is only invoked
        /// when <see cref="IsEnabled"/> is true, so it costs nothing when disabled.
        /// </summary>
        void Record(string module, string operation, Func<object>? payload = null);

        /// <summary>
        /// Registers a pull-based state provider. The snapshot delegate MUST return
        /// an immutable snapshot taken under the module's own synchronization.
        /// Dispose the returned handle to unregister.
        /// </summary>
        IDisposable RegisterStateProvider(string module, string key, Func<object> snapshot);

        /// <summary>
        /// Registers a command the observability tool can invoke back on the module
        /// without a cyclic reference. Dispose the returned handle to unregister.
        /// </summary>
        IDisposable RegisterCommand(string module, string name, Func<object?, object?> command);
    }
}
