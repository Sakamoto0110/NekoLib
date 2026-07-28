using System.Threading.Tasks;


namespace NekoLib.Navigation.Contracts.Pages
{
    /// <summary>
    /// Legacy compatibility contract. The navigation runtime does not invoke these
    /// methods: load timing is represented by <see cref="IBackgroundLoadable"/>,
    /// while native resource ownership remains with <c>IPageView.Dispose()</c>.
    /// </summary>
    public interface IPageResources
    {
        /// <summary>Load heavy or deferred resources.</summary>
        Task LoadResourcesAsync();

        /// <summary>Release resources when page is no longer active.</summary>
        Task ReleaseResourcesAsync();
    }


 

}
