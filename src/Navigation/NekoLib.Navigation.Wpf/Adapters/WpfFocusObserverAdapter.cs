using System;
using System.Windows;
using System.Windows.Input;
using NekoLib.Navigation.Contracts.Platform;

namespace NekoLib.Navigation.Wpf.Adapters
{
    /// <summary>
    /// WPF <see cref="IFocusObserverAdapter"/>. Observes <c>LostKeyboardFocus</c>
    /// on the tracked element and <c>Window.Deactivated</c> on its owning window,
    /// so a popover dismisses both when the user clicks a sibling element AND
    /// when the whole window loses focus.
    /// </summary>
    public sealed class WpfFocusObserverAdapter : IFocusObserverAdapter
    {
        public IDisposable Track(object nativeView, Action onUnfocus)
        {
            if (onUnfocus == null) throw new ArgumentNullException(nameof(onUnfocus));
            if (nativeView is not UIElement element) return EmptySubscription.Instance;

            KeyboardFocusChangedEventHandler lost = (_, _) => onUnfocus();
            element.LostKeyboardFocus += lost;

            // Walk up to the owning Window so window-level focus loss also dismisses.
            // GetWindow returns null when the element isn't yet parented to a window —
            // skip the window-level subscription in that case.
            var window = Window.GetWindow(element);
            EventHandler deactivated = null;
            if (window != null)
            {
                deactivated = (_, _) => onUnfocus();
                window.Deactivated += deactivated;
            }

            return new Subscription(element, lost, window, deactivated);
        }

        private sealed class Subscription : IDisposable
        {
            private UIElement _element;
            private KeyboardFocusChangedEventHandler _lost;
            private Window _window;
            private EventHandler _deactivated;

            public Subscription(UIElement element, KeyboardFocusChangedEventHandler lost, Window window, EventHandler deactivated)
            {
                _element = element;
                _lost = lost;
                _window = window;
                _deactivated = deactivated;
            }

            public void Dispose()
            {
                if (_element != null && _lost != null)
                {
                    try { _element.LostKeyboardFocus -= _lost; } catch { }
                }

                if (_window != null && _deactivated != null)
                {
                    try { _window.Deactivated -= _deactivated; } catch { }
                }

                _element = null;
                _lost = null;
                _window = null;
                _deactivated = null;
            }
        }

        private sealed class EmptySubscription : IDisposable
        {
            public static readonly EmptySubscription Instance = new EmptySubscription();
            public void Dispose() { }
        }
    }
}
