using System;
using System.Windows.Forms;
using NekoLib.Navigation.Contracts.Platform;

namespace NekoLib.Navigation.WinForms.Adapters
{
    /// <summary>
    /// WinForms <see cref="IFocusObserverAdapter"/>. Observes both <c>LostFocus</c>
    /// on the tracked control and <c>Form.Deactivate</c> on its owner form, so a
    /// popover dismisses both when the user clicks a sibling control AND when the
    /// whole app loses focus.
    /// </summary>
    public sealed class WinFormsFocusObserverAdapter : IFocusObserverAdapter
    {
        public IDisposable Track(object nativeView, Action onUnfocus)
        {
            if (onUnfocus == null) throw new ArgumentNullException(nameof(onUnfocus));
            if (nativeView is not Control control) return EmptySubscription.Instance;

            // Wrap the callback once so add/remove use the same delegate instance.
            EventHandler lost = (_, _) => onUnfocus();
            control.LostFocus += lost;

            // Walk up to the owning Form so app-level focus loss also dismisses.
            // Form may be null when the control isn't parented yet — that's fine,
            // we just skip the form-level subscription.
            var form = control.FindForm();
            EventHandler deactivated = null;
            if (form != null)
            {
                deactivated = (_, _) => onUnfocus();
                form.Deactivate += deactivated;
            }

            return new Subscription(control, lost, form, deactivated);
        }

        private sealed class Subscription : IDisposable
        {
            private Control _control;
            private EventHandler _lost;
            private Form _form;
            private EventHandler _deactivated;

            public Subscription(Control control, EventHandler lost, Form form, EventHandler deactivated)
            {
                _control = control;
                _lost = lost;
                _form = form;
                _deactivated = deactivated;
            }

            public void Dispose()
            {
                if (_control != null && _lost != null)
                {
                    try { _control.LostFocus -= _lost; } catch { }
                }

                if (_form != null && _deactivated != null)
                {
                    try { _form.Deactivate -= _deactivated; } catch { }
                }

                _control = null;
                _lost = null;
                _form = null;
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
