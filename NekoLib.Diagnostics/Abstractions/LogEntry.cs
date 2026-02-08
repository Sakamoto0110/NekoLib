    using System;

namespace NekoLib.Diagnostics.Abstractions
{
    public sealed class LogEntry
    {
        public DateTime TimestampUtc { get; }
        public LogLevel Level { get; }
        public string Category { get; }
        public string Message { get; }
        public Exception Exception { get; }

        public LogEntry(DateTime timestampUtc, LogLevel level, string message, Exception exception = null)
        {
            TimestampUtc = timestampUtc;
            Level = level;
            Category = "";
            Message = message;
            Exception = exception;
        }
        public LogEntry(DateTime timestampUtc, LogLevel level, string category, string message, Exception exception = null)
        {
            TimestampUtc = timestampUtc;
            Level = level;
            Category = category;
            Message = message;
            Exception = exception;
        }
    }
}
