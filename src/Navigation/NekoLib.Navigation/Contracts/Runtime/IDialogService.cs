using System.Threading.Tasks;
using NekoLib.Navigation.Contracts.Pages;

namespace NekoLib.Navigation.Contracts.Runtime
{
    /// <summary>
    /// Blocking, awaitable service for binary-choice dialogs (Confirm / Cancel).
    /// Background interaction is blocked while the dialog is on screen.
    /// </summary>
    public interface IDialogService
    {
        /// <summary>
        /// Shows a dialog of type <typeparamref name="TDialog"/> and awaits the user's
        /// boolean answer. Returns <c>true</c> when the user confirms.
        /// </summary>
        Task<bool> ShowDialogAsync<TDialog>(object payload = null)
            where TDialog : class, IDialogView;

        /// <summary>
        /// Forcibly closes every dialog currently on screen, resolving each pending
        /// awaiter with <c>false</c> (treated as a cancel/negative answer). Used during
        /// runtime reset/teardown so awaiting callers never hang.
        /// </summary>
        void CloseAll();
    }
}
