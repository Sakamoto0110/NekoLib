namespace NekoLib.Core.Logging
{
    /// <summary>Adds synchronous flush capability to a custom <see cref="ILogSink"/>.</summary>
    /// <remarks>
    /// The method carries no timeout or cancellation token. A composing pipeline
    /// may impose its own completion budget and may allow a timed-out flush to
    /// continue concurrently with later writes, so implementations must protect
    /// buffered state accordingly.
    /// </remarks>
    public interface IFlushableLogSink : ILogSink
    {
        /// <summary>Synchronously requests delivery of work buffered by the sink.</summary>
        /// <remarks>
        /// Return only after the sink-specific flush attempt completes. Failure is
        /// reported by throwing; the composing pipeline decides whether to isolate it.
        /// </remarks>
        void Flush();
    }
}
