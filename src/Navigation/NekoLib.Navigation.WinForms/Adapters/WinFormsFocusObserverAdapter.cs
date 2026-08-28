using System;
using System.Windows.Forms;
using NekoLib.Navigation.Contracts.Platform;

namespace NekoLib.Navigation.WinForms.Adapters
{
    /// <summary>
    /// WinForms <see cref="IFocusObserverAdapter"/>. Observes both <c>Leave</c> on
    /// the tracked control and <c>Form.Deactivate</c> on its owner form, so a
    /// popover dismisses both when the user clicks a sibling control AND when the
    /// whole app loses focus.
    /// <para>
    /// <c>Leave</c> is the subtree-scoped counterpart of <c>LostFocus</c>: moving
    /// focus between the tracked control's own children does not raise it, but
    /// focus leaving the control entirely does. <c>LostFocus</c> is per-control and
    /// does not bubble in WinForms, so it never fired for a container surface —
    /// focusing such a surface forwards focus to its first selectable child, which
    /// means the container itself never holds the focus it would have to lose.
    /// </para>
    /// </summary>
    public sealed class WinFormsFocusObserverAdapter : IFocusObserverAdapter
    {
        /// <inheritdoc />
        public IDisposable Track(object nativeView, Action onUnfocus)
        {
            if (onUnfocus == null) throw new ArgumentNullException(nameof(onUnfocus));
            if (nativeView is not Control control) return EmptySubscription.Instance;

            // Wrap the callback once so add/remove use the same delegate instance.
            EventHandler left = (_, _) => onUnfocus();
            control.Leave += left;

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

            return new Subscription(control, left, form, deactivated);
        }

        private sealed class Subscription : IDisposable
        {
            private Control _control;
            private EventHandler _left;
            private Form _form;
            private EventHandler _deactivated;

            public Subscription(Control control, EventHandler left, Form form, EventHandler deactivated)
            {
                _control = control;
                _left = left;
                _form = form;
                _deactivated = deactivated;
            }

            public void Dispose()
            {
                if (_control != null && _left != null)
                {
                    try { _control.Leave -= _left; } catch { }
                }

                if (_form != null && _deactivated != null)
                {
                    try { _form.Deactivate -= _deactivated; } catch { }
                }

                _control = null;
                _left = null;
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
