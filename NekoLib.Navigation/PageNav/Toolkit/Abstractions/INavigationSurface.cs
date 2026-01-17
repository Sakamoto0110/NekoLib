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
        Rectangle ClientBounds { get; }
        float Scale { get; }
        bool IsActive { get; }
        Point ResolveAnchor(SurfaceAnchor anchor);
    }
}
