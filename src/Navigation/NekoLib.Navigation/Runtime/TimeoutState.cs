namespace NekoLib.Navigation.Runtime
{
    /// <summary>
    /// Internal state of the global timeout controller.
    /// </summary>
    public enum TimeoutState
    {
        Uninitialized,
        Running,
        Paused,
        Fired,
        Stopped
    }
}