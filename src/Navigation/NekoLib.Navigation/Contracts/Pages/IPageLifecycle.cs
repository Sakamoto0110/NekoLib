using NekoLib.Navigation.Metadata;
using System.Threading.Tasks;


namespace NekoLib.Navigation.Contracts.Pages
{
    /// <summary>
    /// Optional lifecycle callbacks for pages.
    /// </summary>
    public interface IPageLifecycle
    {
        /// <summary>
        /// Called after the page is attached and about to become active.
        /// </summary>
        Task OnNavigatedToAsync(NavigationArgs args);

        /// <summary>
        /// Called before the page is detached or replaced.
        /// </summary>
        Task OnNavigatedFromAsync();
    }


 

}
