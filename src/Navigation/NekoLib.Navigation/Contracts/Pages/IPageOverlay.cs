using System.Threading.Tasks;

namespace NekoLib.Navigation.Contracts.Pages
{
    /// <summary>
    /// Represents a visual overlay rendered above the current page. The built-in
    /// runtime consumes this contract for <see cref="IGlobalLoadingMask"/>
    /// instances. Toast, dialog, prompt, and popover surfaces use their dedicated
    /// contracts; merely implementing <see cref="IPageOverlay"/> does not turn a
    /// registered page into a surface.
    /// </summary>
    public interface IPageOverlay : IPageView
    {
        /// <summary>
        /// Called after the overlay native view has been added and brought to front.
        /// </summary>
        Task OnOverlayOpenedAsync(object payload);

        /// <summary>
        /// Called before the overlay native view is removed and disposed.
        /// </summary>
        Task OnOverlayClosingAsync();
    }
}
