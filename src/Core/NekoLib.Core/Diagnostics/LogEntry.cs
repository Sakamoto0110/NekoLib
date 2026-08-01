using System;

namespace NekoLib.Core.Diagnostics
{
    /// <summary>
    /// Represents a structured log entry.
    /// This is a pure data model used by logging implementations.
    /// </summary>
    public class LogEntry
    {
        public DateTime TimestampUtc { get; }
        public LogLevel Level { get; }
        public string? Category { get; }
        public string Message { get; } = string.Empty;
        public Exception? Exception { get; }
        public LogEntry() { }

        public override string ToString()
            => $"[{TimestampUtc:O}] {Level}: {Message} {(Exception != null ? $"| Exception: {Exception}" : "")}";
        public LogEntry(
            DateTime timestampUtc,
            LogLevel level,
            string message,
            Exception? exception = null)
            : this(timestampUtc, level, null, message, exception)
        {
        }

        public LogEntry(
            DateTime timestampUtc,
            LogLevel level,
            string? category,
            string message,
            Exception? exception = null)
        {
            TimestampUtc = timestampUtc;
            Level = level;
            Category = category;
            Message = message ?? string.Empty;
            Exception = exception;
        }
    }
}
