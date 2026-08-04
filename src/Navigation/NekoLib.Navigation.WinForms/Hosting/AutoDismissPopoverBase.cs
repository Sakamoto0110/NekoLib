using System.Threading.Tasks;
using NekoLib.Navigation.Contracts.Pages;

namespace NekoLib.Navigation.WinForms.Hosting
{
    /// <summary>
    /// Popover that auto-dismisses when it loses focus. The default
    /// <see cref="OnUnfocusAsync"/> resolves the completion callback with
    /// <c>false</c>; override to add policy (e.g. "save changes?" confirm
    /// before closing). The platform's
    /// <see cref="NekoLib.Navigation.Contracts.Platform.IFocusObserverAdapter"/>
    /// raises the unfocus notification when focus leaves this popover's subtree
    /// or the owning form deactivates.
    /// </summary>
    /// <remarks>
    /// <b>Dismissal follows focus, not hit testing.</b> Clicking a sibling
    /// control dismisses only if that control can take focus; clicking inert
    /// area — labels, panels, a page of static content — moves no focus and
    /// leaves the popover open. Tabbing between this popover's own fields does
    /// not dismiss it either. See <see cref="IUnfocusAware"/> and the overlay
    /// section of the Navigation README.
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
