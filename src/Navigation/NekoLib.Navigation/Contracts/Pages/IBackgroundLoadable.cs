using System.Threading.Tasks;

namespace NekoLib.Navigation.Contracts.Pages
{
    /// <summary>
    /// Indicates that a page has load work that can be separated from UI attach.
    /// The target descriptor's <c>NavigationLoadMode</c> decides whether this work
    /// completes before show, immediately after show, or continues in a guarded
    /// background path.
    /// </summary>
    public interface IBackgroundLoadable
    {
        /// <summary>
        /// May be executed off the UI thread. Must not touch UI elements directly.
        /// </summary>
        Task LoadInBackgroundAsync(object args);

        /// <summary>
        /// Executed on the UI thread after background load completes. In
        /// <c>LoadInBackground</c> mode it is skipped if the page is no longer the
        /// live destination or has already been disposed.
        /// </summary>
        Task ApplyBackgroundResultAsync();
    }
}
