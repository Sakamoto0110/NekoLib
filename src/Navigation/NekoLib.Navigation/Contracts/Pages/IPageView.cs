using System;


namespace NekoLib.Navigation.Contracts.Pages
{

    /// <summary>
    /// Minimal, framework-agnostic representation of a page.
    /// Navigation owns attach/detach via IPageHost.
    /// </summary>
    public interface IPageView : IDisposable
    {
        /// <summary>Logical name used for registration/navigation/debug.</summary>
        string Name { get;       }

        /// <summary>Native UI object (Control, UserControl, FrameworkElement...).</summary>
        object NativeView { get; }

        bool IsDisposed { get; }
         
    }


 

}
