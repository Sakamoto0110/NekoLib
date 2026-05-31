namespace NekoLib.Navigation.Metadata;

/// <summary>
/// Immutable navigation request passed from a call site to the runtime.
/// Carries the optional user <see cref="Payload"/>, the <see cref="LoadMode"/>
/// that controls when the target page is shown relative to its load work, and the
/// <see cref="IsBackNavigation"/> flag the runtime sets when this request is a
/// history back-step.
/// Instances are created via the static factories and are never mutated after
/// construction, so they are safe to share across threads.
/// </summary>
public sealed class NavigationArgs
{
    public static readonly NavigationArgs Empty =
        new NavigationArgs(null, NavigationLoadMode.ShowImmediately);

    /// <summary>
    /// Caller-supplied payload. On a back-navigation (<see cref="IsBackNavigation"/>
    /// is <c>true</c>) this carries the state previously captured by
    /// <c>IPageStateful.CaptureState()</c>; the runtime also pushes it through
    /// <c>IPageStateful.RestoreState(object)</c> before the page's
    /// <c>OnNavigatedToAsync</c> runs, so stateful pages should prefer that channel.
    /// </summary>
    public object Payload { get; }

    public NavigationLoadMode LoadMode { get; }

    /// <summary>
    /// <c>true</c> when the runtime is replaying a history entry via <c>GoBack</c>.
    /// In that case <see cref="Payload"/> is the restored state blob (see its docs).
    /// Forward navigations are always <c>false</c>.
    /// </summary>
    public bool IsBackNavigation { get; }

    private NavigationArgs(object payload, NavigationLoadMode loadMode, bool isBackNavigation = false)
    {
        Payload = payload;
        LoadMode = loadMode;
        IsBackNavigation = isBackNavigation;
    }

    // Factories

    /// <summary>Standard navigation: show the page immediately.</summary>
    public static NavigationArgs Default(object payload = null)
        => new(payload, NavigationLoadMode.ShowImmediately);

    /// <summary>
    /// Currently an alias of <see cref="Default"/>; retained as a distinct entry
    /// point for transient navigations so reuse-policy semantics can be attached
    /// here later without churning call sites.
    /// </summary>
    public static NavigationArgs Transient(object payload = null)
        => new(payload, NavigationLoadMode.ShowImmediately);

    /// <summary>Load the page fully before it becomes visible.</summary>
    public static NavigationArgs Preload(object payload = null)
        => new(payload, NavigationLoadMode.LoadBeforeShow);

    /// <summary>Show the page first, then continue loading in the background.</summary>
    public static NavigationArgs Background(object payload = null)
        => new(payload, NavigationLoadMode.LoadInBackground);

    /// <summary>
    /// Back-navigation request created by the runtime when replaying a history
    /// entry. <paramref name="state"/> is the blob captured by
    /// <c>IPageStateful.CaptureState()</c>; it is delivered both via
    /// <see cref="Payload"/> and via <c>IPageStateful.RestoreState(object)</c>.
    /// </summary>
    public static NavigationArgs Back(object state = null)
        => new(state, NavigationLoadMode.ShowImmediately, isBackNavigation: true);
}
