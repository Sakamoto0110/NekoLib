// FILE: PageNav.Toolkit/Models/SurfaceAnchor.cs
namespace NekoLib.Navigation.Toolkit.Models
{
    /// <summary>
    /// Named anchor points on the navigation surface.
    /// Used to position overlays, dialogs, keyboards, debug panels.
    /// </summary>
    public enum SurfaceAnchor
    {
        /// <summary>Top-left corner.</summary>
        TopLeft,
        /// <summary>Horizontal center of the top edge.</summary>
        TopCenter,
        /// <summary>Top-right corner.</summary>
        TopRight,
        /// <summary>Vertical center of the left edge.</summary>
        CenterLeft,
        /// <summary>Center of the surface.</summary>
        Center,
        /// <summary>Vertical center of the right edge.</summary>
        CenterRight,
        /// <summary>Bottom-left corner.</summary>
        BottomLeft,
        /// <summary>Horizontal center of the bottom edge.</summary>
        BottomCenter,
        /// <summary>Bottom-right corner.</summary>
        BottomRight
    }
}
