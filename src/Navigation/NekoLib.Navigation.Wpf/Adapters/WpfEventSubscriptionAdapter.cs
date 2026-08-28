using System;
using System.Reflection;
using NekoLib.Navigation.Contracts.Platform;

namespace NekoLib.Navigation.Wpf.Adapters
{
    /// <summary>
    /// Reflection-based event subscriber. Identical to WinFormsEventSubscriptionAdapter
    /// — the contract is platform-neutral; only the implementing assembly differs.
    /// </summary>
    public sealed class WpfEventSubscriptionAdapter : IEventSubscriptionAdapter
    {
        /// <inheritdoc />
        public void Attach<THandler>(object receiver, string eventName, THandler handler)
            where THandler : Delegate
        {
            if (receiver == null) throw new ArgumentNullException(nameof(receiver));
            if (string.IsNullOrWhiteSpace(eventName)) throw new ArgumentNullException(nameof(eventName));
            if (handler == null) throw new ArgumentNullException(nameof(handler));

            var ev = receiver.GetType().GetEvent(eventName, BindingFlags.Instance | BindingFlags.Public)
                ?? throw new MissingMemberException(receiver.GetType().Name, eventName);

            ev.AddEventHandler(receiver, handler);
        }

        /// <inheritdoc />
        public void Detach<THandler>(object receiver, string eventName, THandler handler)
            where THandler : Delegate
        {
            if (receiver == null || handler == null) return;
            if (string.IsNullOrWhiteSpace(eventName)) return;

            var ev = receiver.GetType().GetEvent(eventName, BindingFlags.Instance | BindingFlags.Public);
            ev?.RemoveEventHandler(receiver, handler);
        }
    }
}
