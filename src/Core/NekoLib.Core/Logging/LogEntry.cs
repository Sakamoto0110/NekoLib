using System;

namespace NekoLib.Core.Logging
{
    /// <summary>Represents a structurally read-only entry accepted by logging sinks.</summary>
    /// <remarks>
    /// The exception reference is retained rather than cloned. Message, category,
    /// and exception text can contain sensitive data; a persistence or transport
    /// sink owns redaction and truncation.
    /// </remarks>
    public sealed class LogEntry
    {
        /// <summary>Initializes a structured log entry.</summary>
        /// <param name="timestampUtc">
        /// Caller-supplied timestamp. The constructor does not validate or rewrite
        /// <see cref="DateTime.Kind"/>.
        /// </param>
        /// <param name="level">Severity assigned to the entry.</param>
        /// <param name="message">
        /// Message text. A runtime-null value is normalized to an empty string for
        /// compatibility despite the non-null annotation.
        /// </param>
        /// <param name="exception">Optional exception reference retained by the entry.</param>
        /// <param name="category">
        /// Optional category. Null, empty, and whitespace-only values are normalized
        /// to null; non-blank text is preserved verbatim.
        /// </param>
        public LogEntry(
            DateTime timestampUtc,
            LogLevel level,
            string message,
            Exception? exception = null,
            string? category = null)
        {
            TimestampUtc = timestampUtc;
            Level = level;
            Message = message ?? string.Empty;
            Exception = exception;
            Category = string.IsNullOrWhiteSpace(category) ? null : category;
        }

        /// <summary>Gets the caller-supplied timestamp.</summary>
        public DateTime TimestampUtc { get; }

        /// <summary>Gets the entry severity.</summary>
        public LogLevel Level { get; }

        /// <summary>Gets the normalized optional category.</summary>
        public string? Category { get; }

        /// <summary>Gets the non-null message text.</summary>
        public string Message { get; }

        /// <summary>Gets the retained optional exception reference.</summary>
        public Exception? Exception { get; }

        /// <summary>Formats the timestamp, severity, optional category, message, and exception.</summary>
        /// <returns>A human-readable representation that can contain sensitive data.</returns>
        public override string ToString()
        {
            var category = Category == null ? string.Empty : " [" + Category + "]";
            var exception = Exception == null
                ? string.Empty
                : " | Exception: " + Exception;

            return $"[{TimestampUtc:O}] {Level}{category}: {Message}{exception}";
        }
    }
}
