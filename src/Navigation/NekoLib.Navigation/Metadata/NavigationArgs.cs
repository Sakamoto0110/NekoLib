/// <summary>
/// TODO: Document this type.
/// Describe responsibility, lifecycle expectations,
/// threading guarantees, and ownership rules.
/// </summary>
namespace NekoLib.Navigation.Metadata;
 
public sealed class NavigationArgs
{
    public static readonly NavigationArgs Empty =
        new NavigationArgs(null,   NavigationLoadMode.ShowImmediately);

    public object Payload { get; }
     
    public NavigationLoadMode LoadMode { get; }

    private NavigationArgs(
        object payload,
         
        NavigationLoadMode loadMode)
    {
        Payload = payload;
         
        LoadMode = loadMode;
    }

    // Factories
    public static NavigationArgs Default(object payload = null)
        => new(payload,   NavigationLoadMode.ShowImmediately);

    public static NavigationArgs Transient(object payload = null)
        => new(payload,   NavigationLoadMode.ShowImmediately);

    public static NavigationArgs Silent(object payload = null)
        => new(payload,   NavigationLoadMode.ShowImmediately);

    public static NavigationArgs Preload(object payload = null)
        => new(payload,   NavigationLoadMode.LoadBeforeShow);

    public static NavigationArgs Background(object payload = null)
        => new(payload,   NavigationLoadMode.LoadInBackground);
}