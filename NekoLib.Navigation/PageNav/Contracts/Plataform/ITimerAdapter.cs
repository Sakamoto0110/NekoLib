/// <summary>
/// TODO: Document this type.
/// Describe responsibility, lifecycle expectations,
/// threading guarantees, and ownership rules.
/// </summary>
using System;

namespace NekoLib.Navigation.Contracts.Plataform
{
    /// <summary>
    /// Platform-agnostic timer abstraction.
    /// </summary>
    public interface ITimerAdapter : IDisposable
    {
        /// <summary>
        /// Gets or sets the timer interval in milliseconds.
        /// Must be set before Start().
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
