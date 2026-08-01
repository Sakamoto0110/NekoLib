using System;
using System.Threading;

namespace NekoLib.Navigation.Diagnostics
{
    internal sealed class NavigationObservationLifetime : IDisposable
    {
        private IDisposable[]? _handles;

        private NavigationObservationLifetime(IDisposable[] handles)
        {
            _handles = handles;
        }

        public static IDisposable Combine(params IDisposable[] handles)
            => new NavigationObservationLifetime(handles ?? Array.Empty<IDisposable>());

        public void Dispose()
        {
            var handles = Interlocked.Exchange(ref _handles, null);
            if (handles == null)
                return;

            for (int i = handles.Length - 1; i >= 0; i--)
            {
                try { handles[i]?.Dispose(); }
                catch { }
            }
        }
    }
}
