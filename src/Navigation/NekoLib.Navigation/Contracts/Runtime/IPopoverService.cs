using System.Threading.Tasks;
using NekoLib.Navigation.Contracts.Pages;

namespace NekoLib.Navigation.Contracts.Runtime
{
    /// <summary>
    /// Non-blocking overlay service. Mirrors <see cref="IDialogService"/> in shape
    /// but never engages an <see cref="NekoLib.Navigation.Contracts.Platform.IInteractionBlocker"/>.
    /// Views that implement <see cref="IUnfocusAware"/> receive an unfocus
    /// notification from the platform focus observer and may resolve their own
    /// completion callback to auto-dismiss.
    /// </summary>
    public interface IPopoverService
    {
        /// <summary>
        /// Shows a popover of type <typeparamref name="TPopover"/> and asynchronously
        /// awaits its boolean completion. Background interaction is NOT blocked
        /// while the popover is on screen.
        /// </summary>
        Task<bool> ShowPopoverAsync<TPopover>(object payload = null)
            where TPopover : class, IPopoverView;

        /// <summary>
        /// Forcibly closes every popover currently on screen, resolving each pending
        /// awaiter with <c>false</c>. Used during runtime reset/teardown.
        /// </summary>
        void CloseAll();
    }
}
