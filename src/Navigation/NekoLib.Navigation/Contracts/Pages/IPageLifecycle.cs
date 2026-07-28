using NekoLib.Navigation.Metadata;
using System.Threading.Tasks;


namespace NekoLib.Navigation.Contracts.Pages
{
    /// <summary>
    /// Optional lifecycle callbacks invoked by the runtime on the UI thread.
    /// Implementations should avoid throwing after mutating external state: the
    /// runtime reports hook failures, but cannot transactionally undo arbitrary
    /// page-side effects.
    /// </summary>
    public interface IPageLifecycle
    {
        /// <summary>
        /// Called after the page is attached, shown, restored (for back navigation),
        /// and after any load work that must complete before entry.
        /// </summary>
        Task OnNavigatedToAsync(NavigationArgs args);

        /// <summary>
        /// Called after <c>IPageVisibility.HidePage()</c> and before detach/dispose
        /// when the page is replaced, reset, or shut down. A hidden keep-attached
        /// page is not called a second time during teardown.
        /// </summary>
        Task OnNavigatedFromAsync();
    }


 

}
