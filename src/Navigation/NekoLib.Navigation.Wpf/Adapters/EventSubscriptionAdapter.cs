using NekoLib.Navigation.Contracts.Platform;
using System;
using System.Windows;
using System.Windows.Media;

namespace NekoLib.Navigation.Wpf.Adapters
{
    /// <summary>
    /// Attaches/detaches event handlers across a visual subtree by reflecting over the
    /// CLR event named <c>eventName</c> on each element. Walks the WPF visual tree from
    /// the receiver downward.
    /// </summary>
    public sealed class EventSubscriptionAdapter : IEventSubscriptionAdapter
    {
        public void Attach<THandler>(object receiver, string eventName, THandler handler)
            where THandler : Delegate
        {
            if (receiver is DependencyObject d)
                AttachInternal(d, eventName, handler);
        }

        public void Detach<THandler>(object receiver, string eventName, THandler handler)
            where THandler : Delegate
        {
            if (receiver is DependencyObject d)
                DetachInternal(d, eventName, handler);
        }

        private void AttachInternal<THandler>(DependencyObject receiver, string eventName, THandler handler)
            where THandler : Delegate
        {
            AttachSingle(receiver, eventName, handler);

            int count = VisualTreeHelper.GetChildrenCount(receiver);
            for (int i = 0; i < count; i++)
                AttachInternal(VisualTreeHelper.GetChild(receiver, i), eventName, handler);
        }

        private void DetachInternal<THandler>(DependencyObject receiver, string eventName, THandler handler)
            where THandler : Delegate
        {
            DetachSingle(receiver, eventName, handler);

            int count = VisualTreeHelper.GetChildrenCount(receiver);
            for (int i = 0; i < count; i++)
                DetachInternal(VisualTreeHelper.GetChild(receiver, i), eventName, handler);
        }

        private static void AttachSingle<THandler>(DependencyObject obj, string eventName, THandler handler)
            where THandler : Delegate
        {
            var ev = obj.GetType().GetEvent(eventName);
            if (ev == null)
                return;

            try
            {
                ev.AddEventHandler(obj, handler);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed attaching event '{eventName}' on {obj.GetType().Name}: {ex.Message}", ex);
            }
        }

        private static void DetachSingle<THandler>(DependencyObject obj, string eventName, THandler handler)
            where THandler : Delegate
        {
            var ev = obj.GetType().GetEvent(eventName);
            if (ev == null)
                return;

            try
            {
                ev.RemoveEventHandler(obj, handler);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed detaching event '{eventName}' on {obj.GetType().Name}: {ex.Message}", ex);
            }
        }
    }
}
