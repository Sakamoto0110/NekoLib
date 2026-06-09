using System;

namespace NekoLib.Core
{
    /// <summary>
    /// Small helpers for <see cref="IDisposable"/> handles.
    /// </summary>
    public static class Disposable
    {
        /// <summary>A shared no-op disposable.</summary>
        public static IDisposable Empty { get; } = new NoOp();

        private sealed class NoOp : IDisposable
        {
            public void Dispose() { }
        }
    }
}
