// FILE: NekoLib.Navigation/Diagnostics/INavigationDiagnosticsSink.cs
#nullable enable
namespace NekoLib.Navigation.Diagnostics
{
    /// <summary>
    /// Optional sink that forwards navigation events to an external diagnostics system.
    /// </summary>
    public interface INavigationDiagnosticsSink
    {
        void OnNavigation(PageLogEntry entry);
        void OnGuardDenied(GuardDeniedEvent e);
    }
}
