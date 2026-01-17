using System;

namespace NekoLib.Core
{
    /// <summary>
    /// Running host instance that owns module lifecycle.
    /// </summary>
    public interface INekoHost : IDisposable
    {
        void Start();
        void Stop();
    }
}
