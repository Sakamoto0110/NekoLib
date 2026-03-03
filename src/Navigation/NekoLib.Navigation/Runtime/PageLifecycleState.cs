namespace NekoLib.Navigation.Runtime
{
    /// <summary>
    /// Represents the lifecycle stage of a page instance.
    /// 
    /// Used for diagnostics and debugging only.
    /// </summary>
    public enum PageLifecycleState
    {
        Created,
        Attached,
        Entering,
        Entered,
        Leaving,
        Detached,
        Released,
        Disposed
    }
}