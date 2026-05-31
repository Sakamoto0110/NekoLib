using System.Threading.Tasks;

namespace NekoLib.Navigation.Contracts.Pages
{
    /// <summary>
    /// Represents a visual overlay rendered above the current page.
    /// </summary>
    public interface IPageOverlay : IPageView
    {
        /// <summary>
        /// Called when the overlay has been attached and is about to become visible.
        /// </summary>
        Task OnOverlayOpenedAsync(object payload);

        /// <summary>
        /// Called when the overlay is about to be closed.
        /// </summary>
        Task OnOverlayClosingAsync();
    }
}
