using System;

namespace NekoLib.Core.Logging
{
    /// <summary>Exposes a pipeline-level bounded logging-completion request.</summary>
    /// <remarks>
    /// This capability is distinct from <see cref="IFlushableLogSink"/>, whose
    /// method carries no budget. A feature that receives only <see cref="ILogger"/>
    /// does not implicitly own or discover this capability.
    /// </remarks>
    public interface ILogFlusher
    {
        /// <summary>Requests completion of pending logging work within a caller budget.</summary>
        /// <param name="timeout">Maximum time the caller is willing to wait.</param>
        /// <returns>
        /// <see langword="true"/> when completion was confirmed within the budget;
        /// otherwise, <see langword="false"/>.
        /// </returns>
        /// <remarks>
        /// A <see langword="false"/> result does not promise cancellation of work
        /// that outlived the budget. Implementations document whether negative
        /// budgets are rejected; <see cref="NullLogger"/> ignores the value because
        /// it has no pending work.
        /// </remarks>
        bool Flush(TimeSpan timeout);
    }
}
