using System;
using System.Collections.Generic;

namespace NekoLib.Core
{
    /// <summary>
    /// Simple service registry with lazy factories and singleton semantics.
    /// </summary>
    public sealed class NekoServiceRegistry : INekoServiceRegistry
    {
        private readonly object _lock = new object();
        private readonly Dictionary<Type, Func<INekoServiceRegistry, object>> _factories = new Dictionary<Type, Func<INekoServiceRegistry, object>>();
        private readonly Dictionary<Type, object> _instances = new Dictionary<Type, object>();

        public void Register<TService>(Func<INekoServiceRegistry, TService> factory) where TService : class
        {
            if(factory == null) throw new ArgumentNullException(nameof(factory));
            lock(_lock)
                _factories[typeof(TService)] = r => factory(r);
        }

        public void RegisterInstance<TService>(TService instance) where TService : class
        {
            if(instance == null) throw new ArgumentNullException(nameof(instance));
            lock(_lock)
                _instances[typeof(TService)] = instance;
        }

        public bool TryResolve<TService>(out TService service) where TService : class
        {
            var t = typeof(TService);
            lock(_lock)
            {
                object existing;
                if(_instances.TryGetValue(t, out existing))
                {
                    service = (TService)existing;
                    return true;
                }

                Func<INekoServiceRegistry, object> factory;
                if(!_factories.TryGetValue(t, out factory))
                {
                    service = null;
                    return false;
                }

                var created = factory(this);
                _instances[t] = created;
                service = (TService)created;
                return true;
            }
        }

        public TService Resolve<TService>() where TService : class
        {
            TService svc;
            if(!TryResolve(out svc))
                throw new InvalidOperationException("Service not registered: " + typeof(TService).FullName);
            return svc;
        }
    }
}
