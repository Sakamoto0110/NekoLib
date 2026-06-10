using System;

namespace NekoLib.Navigation.Contracts.Platform
{
    /// <summary>
    /// Platform abstraction for "the user clicked / tabbed away from this native
    /// view". Used by <see cref="NekoLib.Navigation.Contracts.Runtime.IPopoverService"/>
    /// to drive auto-dismissal of views that implement
    /// <see cref="NekoLib.Navigation.Contracts.Pages.IUnfocusAware"/>.
    /// </summary>
    public interface IFocusObserverAdapter
    {
        /// <summary>
        /// Subscribes to focus-loss notifications on <paramref name="nativeView"/>.
        /// The returned disposable, when disposed, unsubscribes; the service
        /// MUST dispose it when the view is closed or the runtime tears down.
        /// </summary>
        /// <param name="nativeView">The platform handle (e.g. WinForms <c>Control</c>).</param>
        /// <param name="onUnfocus">Callback invoked when the view loses focus.</param>
        IDisposable Track(object nativeView, Action onUnfocus);
    }
}
