using NekoLib.Navigation.Contracts.Platform;
using System;
using System.Threading;
using System.Windows.Forms;

namespace NekoLib.Navigation.WinForms.Adapters
{
    /// <summary>
    /// Marshals callbacks onto the WinForms UI thread that owns the host control.
    /// <para>
    /// Construct it on that thread — <c>PageNavBootstrap.Start()</c> does, because it
    /// requires the native host and runs on the UI thread. The constructing thread is
    /// what the adapter treats as the UI thread while the host has no window handle.
    /// </para>
    /// </summary>
    public sealed class WinFormsEventDispatcherAdapter
     : IEventDispatcherAdapter
    {
        private readonly Control _root;
        private readonly int _uiThreadId;

        public WinFormsEventDispatcherAdapter(Control root)
        {
            _root = root ?? throw new ArgumentNullException(nameof(root));

            // NAV-006: Control.InvokeRequired answers false on *every* thread while no
            // handle exists anywhere in the parent chain, so it cannot tell the UI
            // thread apart before the host is shown or after it is torn down. Capture
            // the owning thread instead of inferring it.
            _uiThreadId = Thread.CurrentThread.ManagedThreadId;
        }

        public void Invoke(Action action)
        {
            if (action == null)
                return;

            // A live handle makes InvokeRequired authoritative: it compares against the
            // thread that actually owns that handle.
            if (_root.IsHandleCreated)
            {
                if (_root.InvokeRequired)
                    _root.Invoke(action);
                else
                    action();

                return;
            }

            // No handle, so nothing can be marshaled: run inline on the real UI thread,
            // fail loudly anywhere else (A-4).
            EnsureOwningThread();
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

            EnsureOwningThread();
            action();
        }

        /// <summary>
        /// Throws unless the caller is the thread this adapter was built on. Used only
        /// when the host has no handle, where marshaling is impossible: running the
        /// action here anyway would execute page lifecycle and view mutation on the
        /// calling thread. Throwing lets callers react — the runtime's dispose path
        /// catches it and tears down inline instead of silently corrupting UI state.
        /// </summary>
        private void EnsureOwningThread()
        {
            if (Thread.CurrentThread.ManagedThreadId == _uiThreadId)
                return;

            throw new InvalidOperationException(
                "Cannot marshal to the UI thread: the host control has no window handle " +
                "(it has not been shown yet, or has already been torn down) and the caller " +
                "is not the UI thread. Defer navigation until the host window is shown " +
                "(e.g. Form.Load), or issue it from the UI thread.");
        }
    }

}
