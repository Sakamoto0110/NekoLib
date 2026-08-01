using System;
using System.Threading;

namespace NekoLib.Core.Inspection
{
    public static class InspectionProvider
    {
        private static IInspectionRecorder _current = NullInspection.Instance;

        public static IInspectionRecorder Current => Volatile.Read(ref _current);

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
