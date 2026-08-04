using System.Threading.Tasks;
using NekoLib.Navigation.Contracts.Pages;

namespace NekoLib.Navigation.Wpf.Hosting
{
    /// <summary>
    /// Popover that auto-dismisses when it loses focus. The default
    /// <see cref="OnUnfocusAsync"/> resolves the completion callback with
    /// <c>false</c>; override to add policy (e.g. confirm before closing).
    /// The platform's
    /// <see cref="NekoLib.Navigation.Contracts.Platform.IFocusObserverAdapter"/>
    /// raises the unfocus notification when keyboard focus leaves this popover's
    /// subtree or the owning window deactivates.
    /// </summary>
    /// <remarks>
    /// <b>Dismissal follows focus, not hit testing.</b> Clicking elsewhere
    /// dismisses only if what was clicked can take keyboard focus; clicking a
    /// non-focusable element moves no focus and leaves the popover open. Tabbing
    /// between this popover's own fields does not dismiss it either. See
    /// <see cref="IUnfocusAware"/> and the overlay section of the Navigation
    /// README.
    /// </remarks>
    public abstract class AutoDismissPopoverBase : PopoverViewBase, IUnfocusAware
    {
        public virtual Task OnUnfocusAsync()
        {
            Complete(false);
            return Task.CompletedTask;
        }
    }
}
