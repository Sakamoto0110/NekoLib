// FILE: PageNav.Toolkit/Abstractions/INavigationSurface.cs
using NekoLib.Navigation.Toolkit.Models;
using System.Drawing;

namespace NekoLib.Navigation.Toolkit.Abstractions
{
    /// <summary>
    /// Read-only description of the visual surface where pages/overlays render.
    /// This lives OUTSIDE PageNav.Core on purpose.
    /// </summary>
    public interface INavigationSurface
    {
        /// <summary>Gets client bounds in platform-independent integer coordinates.</summary>
        Rectangle ClientBounds { get; }
        /// <summary>Gets the platform scale factor applied to the surface.</summary>
        float Scale { get; }
        /// <summary>Gets whether the owning window is currently active.</summary>
        bool IsActive { get; }
        /// <summary>Resolves a named anchor to a point within <see cref="ClientBounds"/>.</summary>
        /// <param name="anchor">Named surface anchor.</param>
        /// <returns>The anchor point in surface coordinates.</returns>
        Point ResolveAnchor(SurfaceAnchor anchor);
    }
}
