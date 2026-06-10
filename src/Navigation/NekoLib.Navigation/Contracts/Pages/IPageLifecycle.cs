using NekoLib.Navigation.Metadata;
using System.Threading.Tasks;


namespace NekoLib.Navigation.Contracts.Pages
{
    /// <summary>
    /// Optional lifecycle callbacks invoked by the runtime during navigation.
    /// </summary>
    public interface IPageLifecycle
    {
        /// <summary>
        /// Called after the page is attached and after load work required before
        /// entry has completed.
        /// </summary>
        Task OnNavigatedToAsync(NavigationArgs args);

        /// <summary>
        /// Called before the page is detached, hidden, or replaced.
        /// </summary>
        Task OnNavigatedFromAsync();
    }


 

}
