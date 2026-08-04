using System;

namespace NekoLib.Navigation.Contracts.Platform
{
    /// <summary>
    /// Platform abstraction for "focus left this native view". Used by
    /// <see cref="NekoLib.Navigation.Contracts.Runtime.IPopoverService"/>
    /// to drive auto-dismissal of views that implement
    /// <see cref="NekoLib.Navigation.Contracts.Pages.IUnfocusAware"/>.
    /// </summary>
    /// <remarks>
    /// <b>The contract is focus, not hit testing.</b> An implementation reports
    /// focus leaving the tracked view's <i>subtree</i> — a move between the view's
    /// own children is not a loss — and additionally reports the owning
    /// form/window being deactivated, so app-level focus loss dismisses too. It
    /// must not attempt to detect clicks outside the view: a click on an area that
    /// cannot take focus moves no focus and correctly raises nothing. The shipped
    /// adapters observe <c>Control.Leave</c> + <c>Form.Deactivate</c> on WinForms
    /// and <c>LostKeyboardFocus</c> + <c>Window.Deactivated</c> on WPF.
    /// </remarks>
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
