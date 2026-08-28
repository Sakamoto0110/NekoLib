// FILE: NekoLib.Navigation.Contracts/Pages/IGlobalLoadingMask.cs
namespace NekoLib.Navigation.Contracts.Pages
{
    /// <summary>
    /// Identifies the global loading overlay selected by bootstrap. At most one
    /// instance is opened around navigation load work.
    /// </summary>
    public interface IGlobalLoadingMask : IPageOverlay { }
}
