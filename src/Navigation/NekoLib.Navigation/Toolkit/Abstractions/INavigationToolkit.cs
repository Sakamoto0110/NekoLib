// FILE: PageNav.Toolkit/Abstractions/INavigationToolkit.cs

// FILE: PageNav.Toolkit/Abstractions/INavigationToolkit.cs
namespace NekoLib.Navigation.Toolkit.Abstractions
{
    /// <summary>
    /// Platform helper toolkit for navigation hosts.
    /// Optional service; Core does not depend on this.
    /// </summary>
    public interface INavigationToolkit
    {
        /// <summary>Gets the read-only navigation surface description.</summary>
        INavigationSurface Surface { get; }
        /// <summary>Requests focus for the native navigation root.</summary>
        void FocusSurface();
    }
}
