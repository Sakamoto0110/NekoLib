namespace NekoLib.Navigation.Runtime
{
    /// <summary>
    /// Categorizes navigation failures for diagnostics
    /// and telemetry reporting.
    /// </summary>
    public enum NavigationFailureKind
    {
        /// <summary>No failure was recorded.</summary>
        None = 0,
        /// <summary>A nested navigation attempt was rejected by the navigation gate.</summary>
        ReentrancyBlocked,
        /// <summary>The requested target was absent from the page registry.</summary>
        PageNotRegistered,
        /// <summary>The page factory could not create the target instance.</summary>
        PageCreationFailed,
        /// <summary>A page navigation lifecycle callback failed.</summary>
        LifecycleFailed,
        /// <summary>Foreground or background page loading failed.</summary>
        LoadFailed,
        /// <summary>The idle timeout attempted navigation but did not complete it.</summary>
        TimeoutNavigationFailed,
        /// <summary>An exception escaped a navigation stage without a more specific category.</summary>
        UnhandledException
    }
}
