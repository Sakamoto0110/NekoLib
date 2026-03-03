namespace NekoLib.Navigation.Contracts.Pages
{
    /// <summary>
    /// Low-level view operations for platform-specific services (overlays, focus, z-order).
    /// </summary>
    public interface IViewHost
    {
        void AddView(object view);
        void RemoveView(object view);
        void BringToFront(object view);
        void Focus(object view);
    }

}
