using System;


namespace NekoLib.Navigation.Contracts.Pages
{

    /// <summary>
    /// Minimal, framework-agnostic representation of a navigable view. The
    /// runtime owns attach/detach through <see cref="IPageHost"/>; the page owns
    /// its native view and disposal state.
    /// </summary>
    public interface IPageView : IDisposable
    {
        /// <summary>Logical name used for registration/navigation/debug.</summary>
        string Name { get;       }

        /// <summary>Native UI object, for example a WinForms <c>Control</c> or WPF <c>FrameworkElement</c>.</summary>
        object NativeView { get; }

        /// <summary>True once the page has released its native resources.</summary>
        bool IsDisposed { get; }
         
    }


 

}
