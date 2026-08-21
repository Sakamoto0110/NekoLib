using System;

namespace NekoLib.Navigation.Contracts.Pages
{
    /// <summary>
    /// Represents an ephemeral, fire-and-forget visual surface (a "toast").
    /// The view must wire an external dismiss callback supplied by the ToastService
    /// and may be auto-dismissed by the service when the timer elapses.
    /// </summary>
    public interface IToastView : IPageView
    {
        /// <summary>
        /// Called by the ToastService right after the view is attached and made visible.
        /// </summary>
        void OnShown(object? payload);

        /// <summary>
        /// Supplies the dismiss callback that the view should invoke when the user
        /// requests an early dismissal (e.g. tapping the toast).
        /// </summary>
        void BindDismiss(Action dismissCallback);
    }
}
