using System;

namespace NekoLib.Core
{
    /// <summary>
    /// Minimal service registry for framework/modules.
    /// Intentionally lightweight to remain net481-friendly.
    /// </summary>
    public interface INekoServiceRegistry
    {
        void Register<TService>(Func<INekoServiceRegistry, TService> factory) where TService : class;
        void RegisterInstance<TService>(TService instance) where TService : class;

        bool TryResolve<TService>(out TService service) where TService : class;
        TService Resolve<TService>() where TService : class;
    }
}
