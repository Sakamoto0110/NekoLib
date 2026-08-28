using NekoLib.Navigation.Telemetry;

namespace NekoLib.Navigation.Metadata;

/// <summary>
/// Immutable navigation request passed from a call site to the runtime.
/// Carries the optional user <see cref="Payload"/>, the requested
/// <see cref="LoadMode"/>, and the <see cref="IsBackNavigation"/> flag the
/// runtime sets when this request is a history back-step. Once the target is
/// resolved, its immutable <c>PageDescriptor.LoadMode</c> is authoritative; the
/// runtime passes an effective copy of these arguments to events and page hooks.
/// Instances are created via the static factories and are never mutated after
/// construction, so they are safe to share across threads.
/// </summary>
public sealed class NavigationArgs
{
    /// <summary>Gets the shared request with no payload and immediate display requested.</summary>
    public static readonly NavigationArgs Empty =
        new NavigationArgs(null, NavigationLoadMode.ShowImmediately);

    /// <summary>
    /// Caller-supplied payload. On a back-navigation (<see cref="IsBackNavigation"/>
    /// is <c>true</c>) this carries the state previously captured by
    /// <c>IPageStateful.CaptureState()</c>; the runtime also pushes it through
    /// <c>IPageStateful.RestoreState(object)</c> before the page's
    /// <c>OnNavigatedToAsync</c> runs, so stateful pages should prefer that channel.
    /// </summary>
    public object? Payload { get; }

    /// <summary>
    /// Requested load mode before registry lookup, and the descriptor-effective
    /// mode on arguments delivered by the runtime to events and lifecycle hooks.
    /// </summary>
    public NavigationLoadMode LoadMode { get; }

    /// <summary>
    /// <c>true</c> when the runtime is replaying a history entry via <c>GoBack</c>.
    /// In that case <see cref="Payload"/> is the restored state blob (see its docs).
    /// Forward navigations are always <c>false</c>.
    /// </summary>
    public bool IsBackNavigation { get; }

    /// <summary>
    /// Optional application-owned timing correlation. Custom guards can report
    /// authentication completion without exposing authentication or API details
    /// to the Navigation runtime.
    /// </summary>
    public NavigationTimingContext? Timing { get; }

    private NavigationArgs(
        object? payload,
        NavigationLoadMode loadMode,
        bool isBackNavigation = false,
        NavigationTimingContext? timing = null)
    {
        Payload = payload;
        LoadMode = loadMode;
        IsBackNavigation = isBackNavigation;
        Timing = timing;
    }

    // Factories

    /// <summary>
    /// Requests immediate display. Registered descriptor metadata remains
    /// authoritative after the target is resolved.
    /// </summary>
    public static NavigationArgs Default(object? payload = null)
        => new(payload, NavigationLoadMode.ShowImmediately);

    /// <summary>
    /// Back-navigation request created by the runtime when replaying a history
    /// entry. <paramref name="state"/> is the blob captured by
    /// <c>IPageStateful.CaptureState()</c>; it is delivered both via
    /// <see cref="Payload"/> and via <c>IPageStateful.RestoreState(object)</c>.
    /// </summary>
    internal static NavigationArgs Back(object? state = null)
        => new(state, NavigationLoadMode.ShowImmediately, isBackNavigation: true);

    /// <summary>
    /// Returns a request copy correlated with application-supplied page-switch
    /// timing checkpoints.
    /// </summary>
    /// <param name="timing">Application-owned timing correlation to attach.</param>
    /// <returns>A new immutable request carrying <paramref name="timing"/>.</returns>
    /// <exception cref="System.ArgumentNullException"><paramref name="timing"/> is <see langword="null"/>.</exception>
    public NavigationArgs WithTiming(NavigationTimingContext timing)
        => new NavigationArgs(
            Payload,
            LoadMode,
            IsBackNavigation,
            timing ?? throw new System.ArgumentNullException(nameof(timing)));

    /// <summary>
    /// Creates the runtime-effective arguments after descriptor resolution while
    /// preserving the caller payload and the back-navigation marker.
    /// </summary>
    internal NavigationArgs WithLoadMode(NavigationLoadMode loadMode)
        => LoadMode == loadMode
            ? this
            : new NavigationArgs(Payload, loadMode, IsBackNavigation, Timing);
}
