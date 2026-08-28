using System;

namespace NekoLib.Core.Logging
{
    /// <summary>Defines the feature-facing contract for severity-based log emission.</summary>
    /// <remarks>
    /// The composition root owns the implementation. Supplying an
    /// <see cref="ILogger"/> to a feature does not transfer disposal ownership.
    /// Implementations decide filtering, retention, dispatch, and persistence;
    /// callers must assume that messages and exceptions can contain sensitive data.
    /// </remarks>
    public interface ILogger
    {
        /// <summary>Emits one log message to the configured logging implementation.</summary>
        /// <param name="level">Severity assigned to the message.</param>
        /// <param name="message">Message text. Callers should supply a non-null value.</param>
        /// <param name="exception">
        /// Optional exception evidence. The reference can contain sensitive messages,
        /// stack traces, and application state.
        /// </param>
        /// <param name="category">Optional application-defined category.</param>
        /// <remarks>
        /// This is a synchronous call boundary. A custom implementation owns its
        /// concurrency, failure-isolation, redaction, and truncation policy.
        /// </remarks>
        void Log(
            LogLevel level,
            string message,
            Exception? exception = null,
            string? category = null);
    }
}
