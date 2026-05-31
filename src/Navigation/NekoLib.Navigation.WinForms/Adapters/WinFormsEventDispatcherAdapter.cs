using NekoLib.Navigation.Contracts.Platform;
using System;
using System.Windows.Forms;

namespace NekoLib.Navigation.WinForms.Adapters
{
    public sealed class WinFormsEventDispatcherAdapter
     : IEventDispatcherAdapter
    {
        private readonly Control _root;

        public WinFormsEventDispatcherAdapter(Control root)
        {
            _root = root ?? throw new ArgumentNullException(nameof(root));
        }

        public void Invoke(Action action)
        {
            if (action == null)
                return;

            // BeginInvoke/Invoke require a created window handle. Before the host is
            // shown (or after it is torn down) we cannot marshal; run inline when we are
            // already on the UI thread (A-4).
            if (_root.IsHandleCreated && _root.InvokeRequired)
                _root.Invoke(action);
            else
                action();
        }

        public void BeginInvoke(Action action)
        {
            if (action == null)
                return;

            // The control's handle must exist before we can post to its message queue.
            if (_root.IsHandleCreated)
            {
                _root.BeginInvoke(action);
                return;
            }

            // Handle not created yet (or already destroyed). If we are on the UI thread,
            // run inline — the best we can do without a live message pump (A-4). If we
            // are on another thread, marshaling is impossible: throw so callers (and the
            // runtime's dispose fallback) can react instead of silently dropping work.
            if (!_root.InvokeRequired)
            {
                action();
                return;
            }

            throw new InvalidOperationException(
                "Cannot marshal to the UI thread before the host control's handle is created. " +
                "Defer navigation until the host window is shown (e.g. Form.Load).");
        }
    }

}
