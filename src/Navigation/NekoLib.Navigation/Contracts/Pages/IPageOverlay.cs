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


    /// <summary>
    /// Represents an overlay that completes with a result.
    /// </summary>
    public interface IPageOverlay<TResult> : IPageOverlay
    {
        /// <summary>
        /// Sets the result that will be returned when the overlay closes.
        /// </summary>
        void SetResult(TResult result);
    }



    public sealed class OverlayOptions
    {
        /// <summary>True = modal semantics (blocks navigation/UI per implementation).</summary>
        public bool Modal { get; }
        /// <summary>If true, OverlayService should block interaction while visible.</summary>

        public bool BlockInteraction { get; }
        /// <summary>If true, overlay should appear above current page (z-order hint).</summary>

        public bool BringToFront { get; }

        private OverlayOptions(bool modal, bool blockInteraction, bool bringToFront)
        {
            Modal = modal;
            BlockInteraction = blockInteraction;
            BringToFront = bringToFront;
        }

        public static OverlayOptions NonModal() => new OverlayOptions(false, false, true); 
        public static OverlayOptions ModalOverlay() => new OverlayOptions(true, true, true);
       
       
       
    }
}
