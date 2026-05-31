using System;
using System.Threading.Tasks;

namespace NekoLib.Navigation.Contracts.Pages
{
    /// <summary>
    /// Represents a modal "choose" dialog: the user picks between confirm / cancel
    /// and the service resolves to a <see cref="bool"/>.
    /// </summary>
    public interface IDialogView : IPageView
    {
        /// <summary>
        /// Called by the DialogService right after the view is attached and made visible.
        /// </summary>
        Task OnShownAsync(object payload);

        /// <summary>
        /// Supplies the completion callback that the view invokes with the user choice.
        /// </summary>
        void BindCompletion(Action<bool> completionCallback);
    }
}
