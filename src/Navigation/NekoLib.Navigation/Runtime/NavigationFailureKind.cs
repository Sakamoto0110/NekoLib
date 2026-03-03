namespace NekoLib.Navigation.Runtime
{
    /// <summary>
    /// Categorizes navigation failures for diagnostics
    /// and telemetry reporting.
    /// </summary>
    public enum NavigationFailureKind
    {
        None = 0,
        ReentrancyBlocked,
        PageNotRegistered,
        PageCreationFailed,
        LifecycleFailed,
        LoadFailed,
        TimeoutNavigationFailed,
        UnhandledException
    }
}