using System;

namespace NekoLib.Navigation.Contracts.Platform
{

    /// <summary>
    /// Dispatches actions onto the UI thread.
    /// If already on UI thread, Invoke MUST execute inline.
    /// Must be safe to call multiple times.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>UI-thread identity must be decided, not inferred.</b> An implementation has
    /// to answer "am I on the UI thread?" truthfully at every moment of the host's
    /// life, including before the host is realized and after it is torn down. A
    /// platform helper that only works once the host exists is not enough:
    /// <c>Control.InvokeRequired</c> answers <c>false</c> on every thread while the
    /// WinForms host has no window handle (NAV-006). Capture the owning thread
    /// instead.
    /// </para>
    /// <para>
    /// <b>Unreachable UI thread.</b> When there is no way to reach the UI thread —
    /// no message queue yet, or one that is already gone — the rule is: execute the
    /// action inline when the caller *is* the UI thread, and throw
    /// <see cref="InvalidOperationException"/> otherwise. Never run the action on the
    /// calling thread as a substitute, because it would execute page lifecycle and
    /// view mutation off the UI thread. Callers may react to that exception: the
    /// runtime's teardown path reports dispatch failure without running UI cleanup
    /// on a worker. Await shutdown on the owning thread before destroying the host
    /// or stopping its message loop. Already accepted asynchronous posts require a
    /// live message loop until completion; this interface cannot acknowledge a post
    /// abandoned by the native framework during shutdown.
    /// </para>
    /// </remarks>
    public interface IEventDispatcherAdapter
    {
        /// <summary>
        /// Executes the action synchronously on the UI thread.
        /// </summary>
        void Invoke(Action action);
        /// <summary>
        /// Executes the action asynchronously on the UI thread.
        /// </summary>
        void BeginInvoke(Action action);
    }
}
