using System;

namespace NekoLib.Diagnostics.Contracts
{
    /// <summary>
    /// Defines logging capabilities for diagnostics consumers.
    /// This is a pure contract and contains no implementation.
    /// </summary>
    public interface ILogger
    {
        /// <summary>
        /// Logs a message with the specified level.
        /// </summary>
        void Log(LogLevel level, string message, Exception exception = null);

        void Trace(string message);
        void Debug(string message);
        void Info(string message);
        void Warn(string message);
        void Error(string message, Exception exception = null);
        void Fatal(string message, Exception exception = null);
    }
}