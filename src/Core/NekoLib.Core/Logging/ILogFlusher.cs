using System;

namespace NekoLib.Core.Logging
{
    /// <summary>Requests completion of pending logging work within a caller budget.</summary>
    public interface ILogFlusher
    {
        bool Flush(TimeSpan timeout);
    }
}
