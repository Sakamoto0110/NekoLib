using System;

namespace NekoLib.Navigation.Contracts.Platform
{
    /// <summary>
    /// Attaches and detaches event handlers dynamically.
    /// </summary>
    public interface IEventSubscriptionAdapter
    {
        /// <summary>
        /// Attaches the supplied delegate to a named public instance event on the
        /// receiver.
        /// </summary>
        void Attach<THandler>(
            object receiver,
            string eventName,
            THandler handler)
            where THandler : Delegate;

        /// <summary>
        /// Detaches the same delegate instance from the named event. Implementations
        /// should tolerate cleanup after a partially completed attachment.
        /// </summary>
        void Detach<THandler>(
            object receiver,
            string eventName,
            THandler handler)
            where THandler : Delegate;
    }
}
