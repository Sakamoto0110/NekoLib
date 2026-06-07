using System;
using System.Threading.Tasks;

namespace NekoLib.Navigation.Contracts.Pages
{
    /// <summary>
    /// Non-blocking, light-dismissable overlay. Same shape as <see cref="IDialogView"/>
    /// but the service that owns it never blocks background interaction, and the
    /// view may opt into auto-dismissal on focus loss by implementing
    /// <see cref="IUnfocusAware"/>.
    /// </summary>
    public interface IPopoverView : IPageView
    {
        /// <summary>
        /// Called by the PopoverService right after the view is attached and made visible.
        /// </summary>
        Task OnShownAsync(object payload);

        /// <summary>
        /// Supplies the completion callback the view invokes to dismiss itself.
        /// The boolean carries the view's chosen outcome — typically <c>true</c>
        /// for an explicit user action and <c>false</c> for cancel / auto-dismiss.
        /// </summary>
        void BindCompletion(Action<bool> completionCallback);
    }
}
