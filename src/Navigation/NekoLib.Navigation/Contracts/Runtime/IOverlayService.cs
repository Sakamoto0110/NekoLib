using NekoLib.Navigation.Contracts.Pages;
using System.Threading.Tasks;

namespace NekoLib.Navigation.Contracts.Runtime
{
    public interface IOverlayService
    {
        // ------------------------------------------------------------
        // State
        // ------------------------------------------------------------

        /// <summary>
        /// True if at least one overlay is currently visible.
        /// </summary>
        bool HasOverlay { get; }

        /// <summary>
        /// Number of active overlays (stack depth).
        /// </summary>
        int OverlayCount { get; }

        // ------------------------------------------------------------
        // Show (fire-and-forget)
        // ------------------------------------------------------------

        /// <summary>
        /// Shows an overlay without expecting a result.
        /// </summary>
        void Show<TOverlay>()
            where TOverlay : class, IPageOverlay;

        /// <summary>
        /// Shows an overlay with a payload.
        /// </summary>
        void Show<TOverlay>(object payload)
            where TOverlay : class, IPageOverlay;

        // ------------------------------------------------------------
        // Show (awaitable result)
        // ------------------------------------------------------------

        /// <summary>
        /// Shows an overlay and awaits a result.
        /// </summary>
        Task<TResult> ShowAsync<TOverlay, TResult>()
            where TOverlay : class, IPageOverlay<TResult>;

        /// <summary>
        /// Shows an overlay with a payload and awaits a result.
        /// </summary>
        Task<TResult> ShowAsync<TOverlay, TResult>(object payload)
            where TOverlay : class, IPageOverlay<TResult>;

        // ------------------------------------------------------------
        // Close
        // ------------------------------------------------------------

        /// <summary>
        /// Closes the topmost overlay.
        /// </summary>
        void CloseTop();

        /// <summary>
        /// Closes all overlays.
        /// </summary>
        void CloseAll();
    }
}
