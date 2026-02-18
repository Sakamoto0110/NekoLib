using System;

namespace NekoLib.Navigation.Contracts.Platform
{
     
    /// <summary>
    /// Dispatches actions onto the UI thread.
    /// If already on UI thread, Invoke MUST execute inline.
    /// Must be safe to call multiple times.
    /// </summary>
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
