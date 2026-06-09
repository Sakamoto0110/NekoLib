using System;

namespace NekoLib.Core.Observability
{
    /// <summary>
    /// No-op <see cref="IDebugUtils"/>. Default when no observability tool is attached;
    /// follows the same Null Object pattern as <c>NullLogger</c> / <c>NullTelemetrySink</c>.
    /// </summary>
    public sealed class NullDebugUtils : IDebugUtils
    {
        public static readonly NullDebugUtils Instance = new NullDebugUtils();

        private NullDebugUtils() { }

        public bool IsEnabled => false;

        public void Record(string module, string operation, Func<object>? payload = null) { }

        public IDisposable RegisterStateProvider(string module, string key, Func<object> snapshot)
            => Disposable.Empty;

        public IDisposable RegisterCommand(string module, string name, Func<object?, object?> command)
            => Disposable.Empty;
    }
}
