/// <summary>
/// TODO: Document this type.
/// Describe responsibility, lifecycle expectations,
/// threading guarantees, and ownership rules.
/// </summary>
namespace NekoLib.Navigation.Metadata;
 
public sealed class NavigationArgs
{
    public static readonly NavigationArgs Empty =
        new NavigationArgs(null, NavigationBehavior.Default, NavigationLoadMode.ShowImmediately);

    public object Payload { get; }
    public NavigationBehavior Behavior { get; }
    public NavigationLoadMode LoadMode { get; }

    private NavigationArgs(
        object payload,
        NavigationBehavior behavior,
        NavigationLoadMode loadMode)
    {
        Payload = payload;
        Behavior = behavior;
        LoadMode = loadMode;
    }

    // Factories
    public static NavigationArgs Default(object payload = null)
        => new(payload, NavigationBehavior.Default, NavigationLoadMode.ShowImmediately);

    public static NavigationArgs Transient(object payload = null)
        => new(payload, NavigationBehavior.Transient, NavigationLoadMode.ShowImmediately);

    public static NavigationArgs Silent(object payload = null)
        => new(payload, NavigationBehavior.NoMask, NavigationLoadMode.ShowImmediately);

    public static NavigationArgs Preload(object payload = null)
        => new(payload, NavigationBehavior.Default, NavigationLoadMode.LoadBeforeShow);

    public static NavigationArgs Background(object payload = null)
        => new(payload, NavigationBehavior.Default, NavigationLoadMode.LoadInBackground);
}