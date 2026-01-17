namespace NekoLib.Core
{
    /// <summary>
    /// Context passed to modules during configuration.
    /// </summary>
    public interface INekoModuleContext
    {
        INekoServiceRegistry Services { get; }
        INekoConfiguration Configuration { get; }
        INekoEnvironment Environment { get; }
    }
}
