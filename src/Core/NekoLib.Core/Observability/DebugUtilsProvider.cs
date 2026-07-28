using System;
using System.Threading;

namespace NekoLib.Core.Observability
{
    /// <summary>
    /// Process-wide slot for the optional <see cref="IDebugUtils"/> hub.
    /// The default is always <see cref="NullDebugUtils.Instance"/>.
    /// </summary>
    public static class DebugUtilsProvider
    {
        private static IDebugUtils _current = NullDebugUtils.Instance;

        /// <summary>
        /// Gets the active process-wide hub. Never returns <c>null</c>.
        /// </summary>
        public static IDebugUtils Current => Volatile.Read(ref _current);

        /// <summary>
        /// Installs one process-wide hub and returns a handle that restores the
        /// no-op implementation. A concurrent second installation is rejected.
        /// </summary>
        public static IDisposable Install(IDebugUtils debugUtils)
        {
            if (debugUtils == null)
                throw new ArgumentNullException(nameof(debugUtils));
            if (!debugUtils.IsEnabled)
                throw new ArgumentException(
                    "The process-wide DebugUtils hub must be enabled.",
                    nameof(debugUtils));

            var previous = Interlocked.CompareExchange(
                ref _current,
                debugUtils,
                NullDebugUtils.Instance);

            if (!ReferenceEquals(previous, NullDebugUtils.Instance))
                throw new InvalidOperationException(
                    "A process-wide DebugUtils hub is already enabled.");

            try
            {
                // IsEnabled may change while the CAS publishes a mutable hub.
                // Never leave a hub that became disabled during installation in
                // the process-wide slot.
                if (!debugUtils.IsEnabled)
                {
                    throw new ArgumentException(
                        "The process-wide DebugUtils hub became disabled while it was being installed.",
                        nameof(debugUtils));
                }
            }
            catch
            {
                Interlocked.CompareExchange(
                    ref _current,
                    NullDebugUtils.Instance,
                    debugUtils);
                throw;
            }

            return new Installation(debugUtils);
        }

        private sealed class Installation : IDisposable
        {
            private IDebugUtils? _installed;

            public Installation(IDebugUtils installed)
            {
                _installed = installed;
            }

            public void Dispose()
            {
                var installed = Interlocked.Exchange(ref _installed, null);
                if (installed == null)
                    return;

                Interlocked.CompareExchange(
                    ref _current,
                    NullDebugUtils.Instance,
                    installed);
            }
        }
    }
}
