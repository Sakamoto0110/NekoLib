using System.Threading.Tasks;


namespace NekoLib.Navigation.Contracts.Pages
{
    public interface IPageResources
    {
        /// <summary>Load heavy or deferred resources.</summary>
        Task LoadResourcesAsync();

        /// <summary>Release resources when page is no longer active.</summary>
        Task ReleaseResourcesAsync();
    }


 

}
