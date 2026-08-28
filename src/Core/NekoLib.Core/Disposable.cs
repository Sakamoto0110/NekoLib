using System;

namespace NekoLib.Core
{
    /// <summary>Provides shared helpers for ownership handles.</summary>
    /// <remarks>
    /// The helpers do not acquire, release, or imply ownership of any external
    /// resource beyond the behavior stated by the returned handle.
    /// </remarks>
    public static class Disposable
    {
        /// <summary>Gets a shared, stateless handle whose disposal has no effect.</summary>
        /// <remarks>The handle is safe to dispose repeatedly.</remarks>
        public static IDisposable Empty { get; } = new NoOp();

        private sealed class NoOp : IDisposable
        {
            public void Dispose() { }
        }
    }
}
