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
    /// raises the unfocus notification when the user clicks elsewhere or the
    /// owning window deactivates.
    /// </summary>
    public abstract class AutoDismissPopoverBase : PopoverViewBase, IUnfocusAware
    {
        public virtual Task OnUnfocusAsync()
        {
            Complete(false);
            return Task.CompletedTask;
        }
    }
}
