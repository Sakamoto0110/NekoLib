namespace NekoLib.Core.Logging
{
    public sealed class NullLogger : ILogger, ILogSnapshotSource, ILogFlusher
    {
        public static readonly NullLogger Instance = new NullLogger();

        private NullLogger() { }

        public void Log(
            LogLevel level,
            string message,
            System.Exception? exception = null,
            string? category = null)
        {
        }

        public System.Collections.Generic.IReadOnlyList<LogEntry> GetRecentEntries(int maxEntries)
            => System.Array.Empty<LogEntry>();

        public bool Flush(System.TimeSpan timeout) => true;
    }
}
