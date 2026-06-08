using System.Threading.Tasks;

namespace NekoLib.Navigation.Contracts.Pages
{
    /// <summary>
    /// Indicates that a page has load work that can be separated from UI attach.
    /// The active <c>NavigationLoadMode</c> decides whether this work runs before
    /// show, immediately after show, or in a guarded background path.
    /// </summary>
    public interface IBackgroundLoadable
    {
        /// <summary>
        /// May be executed off the UI thread. Must not touch UI elements directly.
        /// </summary>
        Task LoadInBackgroundAsync(object args);

        /// <summary>
        /// Executed on the UI thread after background load completes. Safe to update UI.
        /// </summary>
        Task ApplyBackgroundResultAsync();
    }
}
