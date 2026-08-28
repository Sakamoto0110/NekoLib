using System;
using System.Threading;

namespace NekoLib.Core.Inspection
{
    /// <summary>Provides the optional process-wide Inspection recorder slot.</summary>
    /// <remarks>
    /// The slot is scoped to the loaded Core assembly context and is always non-null.
    /// It is a composition bridge, not service discovery, authorization, or recorder
    /// lifetime ownership.
    /// </remarks>
    public static class InspectionProvider
    {
        private static IInspectionRecorder _current = NullInspection.Instance;

        /// <summary>Gets the installed enabled recorder or <see cref="NullInspection.Instance"/>.</summary>
        public static IInspectionRecorder Current => Volatile.Read(ref _current);

        /// <summary>Installs one enabled recorder into the process-wide slot.</summary>
        /// <param name="inspection">Enabled recorder to expose.</param>
        /// <returns>
        /// An idempotent handle that conditionally restores the null recorder when
        /// disposed. The handle does not dispose <paramref name="inspection"/>.
        /// </returns>
        /// <exception cref="ArgumentNullException"><paramref name="inspection"/> is null.</exception>
        /// <exception cref="ArgumentException">
        /// The recorder is disabled or becomes disabled while installation is being completed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Another enabled recorder already owns the slot.
        /// </exception>
        public static IDisposable Install(IInspectionRecorder inspection)
        {
            if (inspection == null)
                throw new ArgumentNullException(nameof(inspection));
            if (!inspection.IsEnabled)
                throw new ArgumentException("The process-wide Inspection runtime must be enabled.", nameof(inspection));

            var previous = Interlocked.CompareExchange(
                ref _current,
                inspection,
                NullInspection.Instance);

            if (!ReferenceEquals(previous, NullInspection.Instance))
                throw new InvalidOperationException("A process-wide Inspection runtime is already enabled.");

            try
            {
                if (!inspection.IsEnabled)
                    throw new ArgumentException("The Inspection runtime became disabled while it was being installed.", nameof(inspection));
            }
            catch
            {
                Interlocked.CompareExchange(ref _current, NullInspection.Instance, inspection);
                throw;
            }

            return new Installation(inspection);
        }

        private sealed class Installation : IDisposable
        {
            private IInspectionRecorder? _installed;

            public Installation(IInspectionRecorder installed)
            {
                _installed = installed;
            }

            public void Dispose()
            {
                var installed = Interlocked.Exchange(ref _installed, null);
                if (installed != null)
                    Interlocked.CompareExchange(ref _current, NullInspection.Instance, installed);
            }
        }
    }
}
