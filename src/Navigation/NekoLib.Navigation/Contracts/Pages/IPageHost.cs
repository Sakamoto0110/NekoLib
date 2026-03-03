using System.Threading.Tasks;

namespace NekoLib.Navigation.Contracts.Pages
{

    /// <summary>
    /// Container that knows how to attach/detach pages (page-level operations).
    /// </summary>
    public interface IPageHost
    {
        void Attach(IPageView page);
        void Detach(IPageView page);
        void BringToFront(IPageView page);
         
    }

}
