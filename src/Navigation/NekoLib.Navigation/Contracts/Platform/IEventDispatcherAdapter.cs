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
    /// runtime's teardown path catches it and disposes inline, so a dead message pump
    /// still completes shutdown.
    /// </para>
    /// <para>
    /// A platform whose UI framework reports its own shutdown may additionally treat
    /// that signal as a documented teardown fallback and complete the action inline;
    /// the WPF adapter does this when its <c>Dispatcher</c> has shut down.
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
