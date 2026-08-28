namespace NekoLib.Core.Logging
{
    /// <summary>Receives structured entries accepted by a logging pipeline.</summary>
    /// <remarks>
    /// This is a consumer implementation seam. The composing pipeline owns call
    /// ordering, concurrency, failure isolation, and sink lifetime. A sink that
    /// persists or transmits entries owns redaction, truncation, access control,
    /// and delivery diagnostics.
    /// </remarks>
    public interface ILogSink
    {
        /// <summary>Writes one accepted structured log entry.</summary>
        /// <param name="entry">The non-null entry to consume.</param>
        /// <remarks>
        /// Implementations should return promptly unless their documented pipeline
        /// deliberately uses synchronous backpressure.
        /// </remarks>
        void Write(LogEntry entry);
    }
}
