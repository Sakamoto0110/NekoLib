using System;
using System.Threading.Tasks;

namespace NekoLib.Navigation.Contracts.Pages
{
    /// <summary>
    /// Non-generic marker for prompt views. Prefer <see cref="IPromptView{TResult}"/>
    /// when implementing concrete prompts.
    /// </summary>
    public interface IPromptView : IPageView
    {
        /// <summary>
        /// Called by the PromptService right after the view is attached and made visible.
        /// </summary>
        Task OnShownAsync(object payload);
    }

    /// <summary>
    /// Represents a modal prompt view that completes asynchronously with a typed result.
    /// </summary>
    public interface IPromptView<TResult> : IPromptView
    {
        /// <summary>
        /// Supplies the completion callback that the view should invoke when the user
        /// confirms or cancels the prompt and a result is available.
        /// </summary>
        void BindCompletion(Action<TResult> completionCallback);
    }
}
