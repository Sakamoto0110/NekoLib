using System.Threading.Tasks;

namespace NekoLib.Navigation.Contracts.Pages
{
    public interface IModalHost
    {
        Task ShowModalAsync(IPageView page);
        Task HideModalAsync(IPageView page);
    }

}
