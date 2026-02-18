using System;

namespace NekoLib.Navigation.Contracts.Platform
{
    /// <summary>
    /// Attaches and detaches event handlers dynamically.
    /// </summary>
    public interface IEventSubscriptionAdapter
    {
        void Attach<THandler>(
            object receiver,
            string eventName,
            THandler handler)
            where THandler : Delegate;

        void Detach<THandler>(
            object receiver,
            string eventName,
            THandler handler)
            where THandler : Delegate;
    }
}
