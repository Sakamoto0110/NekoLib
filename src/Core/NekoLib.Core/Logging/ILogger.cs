using System;

namespace NekoLib.Core.Logging
{
    /// <summary>Small feature-facing contract for severity-based log emission.</summary>
    public interface ILogger
    {
        void Log(
            LogLevel level,
            string message,
            Exception? exception = null,
            string? category = null);
    }
}
