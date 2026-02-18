using PageNav.Contracts.Plataform;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace PageNav.Wpf.Adapters
{
    public class EventDispatcherAdapter : IEventDispatcherAdapter
    {
        public void Attach<THandler>(object receiver, string eventName, THandler handler)
           where THandler : Delegate
        {
            if(receiver is DependencyObject d)
                AttatchEventInternal(d, eventName, handler);
        }

        public void Detach<THandler>(object receiver, string eventName, THandler handler)
            where THandler : Delegate
        {
            if(receiver is DependencyObject d)
                DetatchEventInternal(d, eventName, handler);
        }


        // --------------------------------------------------------------------
        // INTERNAL RECURSIVE IMPLEMENTATION
        // --------------------------------------------------------------------

        private void AttatchEventInternal<THandler>(DependencyObject receiver, string eventName, THandler handler)
            where THandler : Delegate
        {
            AttachSingle(receiver, eventName, handler);

            int count = VisualTreeHelper.GetChildrenCount(receiver);
            for(int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(receiver, i);
                AttatchEventInternal(child, eventName, handler);
            }
        }

        private void DetatchEventInternal<THandler>(DependencyObject receiver, string eventName, THandler handler)
            where THandler : Delegate
        {
            DetachSingle(receiver, eventName, handler);

            int count = VisualTreeHelper.GetChildrenCount(receiver);
            for(int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(receiver, i);
                DetatchEventInternal(child, eventName, handler);
            }
        }


        // --------------------------------------------------------------------
        // HELPERS: Attach/Detach on a *single* object
        // --------------------------------------------------------------------

        private void AttachSingle<THandler>(DependencyObject obj, string eventName, THandler handler)
            where THandler : Delegate
        {
            // WPF-specific: UIElement, FrameworkElement, ContentElement
            var targetType = obj.GetType();
            var ev = targetType.GetEvent(eventName);

            if(ev != null)
            {
                try
                {
                    ev.AddEventHandler(obj, handler);
                }
                catch(Exception ex)
                {
                    throw new Exception($"Failed attaching event '{eventName}' on {targetType.Name}: {ex.Message}", ex);
                }
            }
        }
        private void DetachSingle<THandler>(DependencyObject obj, string eventName, THandler handler)
            where THandler : Delegate
        {
            var targetType = obj.GetType();
            var ev = targetType.GetEvent(eventName);

            if(ev != null)
            {
                try
                {
                    ev.RemoveEventHandler(obj, handler);
                }
                catch(Exception ex)
                {
                    throw new Exception($"Failed detaching event '{eventName}' on {targetType.Name}: {ex.Message}", ex);
                }
            }
        }
    }
}
