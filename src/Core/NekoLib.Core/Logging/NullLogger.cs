namespace NekoLib.Core.Logging
{
    /// <summary>
    /// Provides a shared logging implementation that drops writes and retains no data.
    /// </summary>
    public sealed class NullLogger : ILogger, ILogSnapshotSource, ILogFlusher
    {
        /// <summary>Shared stateless null logger.</summary>
        public static readonly NullLogger Instance = new NullLogger();

        private NullLogger() { }

        /// <summary>Drops the supplied message without inspecting its values.</summary>
        /// <param name="level">Ignored severity.</param>
        /// <param name="message">Ignored message.</param>
        /// <param name="exception">Ignored exception reference.</param>
        /// <param name="category">Ignored category.</param>
        public void Log(
            LogLevel level,
            string message,
            System.Exception? exception = null,
            string? category = null)
        {
        }

        /// <summary>Returns an empty snapshot.</summary>
        /// <param name="maxEntries">Ignored maximum entry count.</param>
        /// <returns>A non-null empty collection.</returns>
        public System.Collections.Generic.IReadOnlyList<LogEntry> GetRecentEntries(int maxEntries)
            => System.Array.Empty<LogEntry>();

        /// <summary>Reports immediate completion because no work is buffered.</summary>
        /// <param name="timeout">Ignored caller budget.</param>
        /// <returns>Always <see langword="true"/>.</returns>
        public bool Flush(System.TimeSpan timeout) => true;
    }
}
