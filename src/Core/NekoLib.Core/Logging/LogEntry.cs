using System;

namespace NekoLib.Core.Logging
{
    /// <summary>Immutable structured entry accepted by logging sinks.</summary>
    public sealed class LogEntry
    {
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

        public DateTime TimestampUtc { get; }
        public LogLevel Level { get; }
        public string? Category { get; }
        public string Message { get; }
        public Exception? Exception { get; }

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
