using NekoLib.Navigation.Metadata;
using System;
using System.Threading.Tasks;


namespace NekoLib.Navigation.Contracts.Pages
{

    /// <summary>
    /// Minimal, framework-agnostic representation of a page.
    /// Navigation owns attach/detach via IPageHost.
    /// </summary>
    public interface IPageView : IDisposable
    {
        /// <summary>Logical name used for registration/navigation/debug.</summary>
        string Name { get; set; }

        /// <summary>Native UI object (Control, UserControl, FrameworkElement...).</summary>
        object NativeView { get; }

        bool IsDisposed { get; }
        bool DesignMode { get; }
    }


    /// <summary>
    /// Optional lifecycle callbacks for pages.
    /// </summary>
    public interface IPageLifecycle
    {
        /// <summary>
        /// Called after the page is attached and about to become active.
        /// </summary>
        Task OnNavigatedToAsync(NavigationArgs args);

        /// <summary>
        /// Called before the page is detached or replaced.
        /// </summary>
        Task OnNavigatedFromAsync();
    }
    public interface IHostAttachable
    {
        void OnAttach(IPageHost host);
        void OnDetach();
    }
    public interface IPageResources
    {
        /// <summary>Load heavy or deferred resources.</summary>
        Task LoadResourcesAsync();

        /// <summary>Release resources when page is no longer active.</summary>
        Task ReleaseResourcesAsync();
    }

    public interface IPageInteraction
    {
        /// <summary>Enables user interaction for this page.</summary>
        void EnableInteraction();

        /// <summary>Disables user interaction for this page.</summary>
        void DisableInteraction();
    }

    public interface IPageVisibility
    {
        void ShowPage();
        void HidePage();
    }


 

}
