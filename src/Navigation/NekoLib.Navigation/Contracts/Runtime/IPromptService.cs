using System.Threading.Tasks;
using NekoLib.Navigation.Contracts.Pages;

namespace NekoLib.Navigation.Contracts.Runtime
{
    /// <summary>
    /// Blocking, awaitable service for modal prompts that produce a typed result.
    /// Background interaction is blocked (when an IInteractionBlocker is available)
    /// while a prompt is visible.
    /// </summary>
    public interface IPromptService
    {
        /// <summary>
        /// Shows a prompt of type <typeparamref name="TPrompt"/> and asynchronously
        /// awaits its <typeparamref name="TResult"/> completion. The view is removed
        /// and interaction is unblocked once the task is resolved.
        /// </summary>
        Task<TResult> ShowPromptAsync<TPrompt, TResult>(object payload = null)
            where TPrompt : class, IPromptView<TResult>;

        /// <summary>
        /// Forcibly closes every prompt currently on screen, resolving each pending
        /// awaiter with the default result for its type. Used during runtime
        /// reset/teardown so awaiting callers never hang.
        /// </summary>
        void CloseAll();
    }
}
