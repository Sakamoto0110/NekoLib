using NekoLib.Navigation.Contracts.Pages;

namespace NekoLib.Navigation.Contracts.Runtime
{
    /// <summary>
    /// Fire-and-forget service for ephemeral visual surfaces (toasts).
    /// Calling <see cref="ShowToast{TToast}"/> replaces any toast currently on screen.
    /// </summary>
    public interface IToastService
    {
        /// <summary>
        /// Shows a toast of type <typeparamref name="TToast"/>. If another toast is
        /// already visible, it is cancelled and removed before the new one is shown.
        /// </summary>
        /// <param name="payload">Optional data passed to the toast view.</param>
        /// <param name="durationMs">Auto-dismiss timeout in milliseconds.</param>
        void ShowToast<TToast>(object payload = null, int durationMs = 3000)
            where TToast : class, IToastView;

        /// <summary>
        /// Dismisses the toast currently on screen, if any.
        /// </summary>
        void DismissCurrentToast();
    }
}
