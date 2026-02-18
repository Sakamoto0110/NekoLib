using NekoLib.Navigation.Contracts.Platform;
using System;
using System.Reflection;

namespace NekoLib.Navigation.WinForms.Adapters
{
    public sealed class WinFormsEventSubscriptionAdapter
     : IEventSubscriptionAdapter
    {
        public void Attach<THandler>(
            object receiver,
            string eventName,
            THandler handler)
            where THandler : Delegate
        {
            if(receiver == null)
                throw new ArgumentNullException(nameof(receiver));
            if(string.IsNullOrWhiteSpace(eventName))
                throw new ArgumentNullException(nameof(eventName));
            if(handler == null)
                throw new ArgumentNullException(nameof(handler));

            var ev = receiver.GetType().GetEvent(
                eventName,
                BindingFlags.Instance | BindingFlags.Public);

            if(ev == null)
                throw new MissingMemberException(
                    receiver.GetType().Name,
                    eventName);

            ev.AddEventHandler(receiver, handler);
        }

        public void Detach<THandler>(
            object receiver,
            string eventName,
            THandler handler)
            where THandler : Delegate
        {
            if(receiver == null || handler == null)
                return;
            if(string.IsNullOrWhiteSpace(eventName))
                return;

            var ev = receiver.GetType().GetEvent(
                eventName,
                BindingFlags.Instance | BindingFlags.Public);

            ev?.RemoveEventHandler(receiver, handler);
        }
    }

}
