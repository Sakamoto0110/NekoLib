using System;

namespace NekoLib.Navigation.Contracts.Platform
{
    /// <summary>
    /// Platform timer used by navigation infrastructure, primarily idle timeout.
    /// Implementations should raise <see cref="Tick"/> on the UI thread when the
    /// native platform requires UI work to stay on that thread.
    /// </summary>
    public interface ITimerAdapter : IDisposable
    {
        /// <summary>
        /// Gets or sets the timer interval in milliseconds.
        /// Must be set before <see cref="Start"/>.
        /// </summary>
        int IntervalMilliseconds { get; set; }

        /// <summary>
        /// Raised when the timer interval elapses.
        /// </summary>
        event Action Tick;

        /// <summary>
        /// Starts or resumes the timer.
        /// </summary>
        void Start();

        /// <summary>
        /// Stops the timer.
        /// </summary>
        void Stop();
    }
}
