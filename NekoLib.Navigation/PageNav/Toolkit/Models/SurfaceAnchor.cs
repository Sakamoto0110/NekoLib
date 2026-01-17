// FILE: PageNav.Toolkit/Models/SurfaceAnchor.cs
namespace NekoLib.Navigation.Toolkit.Models
{
    /// <summary>
    /// Named anchor points on the navigation surface.
    /// Used to position overlays, dialogs, keyboards, debug panels.
    /// </summary>
    public enum SurfaceAnchor
    {
        TopLeft, TopCenter, TopRight,
        CenterLeft, Center, CenterRight,
        BottomLeft, BottomCenter, BottomRight
    }
}
