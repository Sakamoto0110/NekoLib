namespace NekoLib.Core
{
    /// <summary>
    /// Composition root used to register modules and build a host.
    /// Implementations live in the NekoLib entrypoint.
    /// </summary>
    public interface INekoBuilder
    {
        INekoBuilder UseConfiguration(INekoConfiguration configuration);
        INekoBuilder UseEnvironment(INekoEnvironment environment);

        INekoBuilder AddModule(INekoModule module);

        INekoHost Build();
    }
}
