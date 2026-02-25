using System;
using System.Collections.Generic;

namespace NekoLib.Navigation.Runtime.Services
{
    /// <summary>
    /// Deterministic, thread-safe service locator used by PageNav runtime.
    ///
    /// Rules:
    /// - Services must be registered before Lock()
    /// - After Lock(), registration is forbidden
    /// - CanResolve(type) is safe but not required before Get(type)
    /// - Get(type) is fully thread-safe
    /// </summary>
    public sealed class ServiceLocator
    {
        private readonly Dictionary<Type, object> _services = new();
        private readonly object _sync = new object();

        private bool _locked;

        // ------------------------------------------------------------
        // Registration
        // ------------------------------------------------------------

        public void Register(Type serviceType, object instance)
        {
            if (serviceType == null)
                throw new ArgumentNullException(nameof(serviceType));

            if (instance == null)
                throw new ArgumentNullException(nameof(instance));

            lock (_sync)
            {
                if (_locked)
                    throw new InvalidOperationException("Service registration is locked.");

                if (_services.ContainsKey(serviceType))
                    throw new InvalidOperationException(
                        $"Service '{serviceType.FullName}' already registered.");

                _services[serviceType] = instance;
            }
        }

        public void Register<T>(T instance) where T : class
            => Register(typeof(T), instance);

        // ------------------------------------------------------------
        // Resolution
        // ------------------------------------------------------------

        public bool CanResolve(Type type)
        {
            if (type == null)
                return false;

            lock (_sync)
            {
                return _services.ContainsKey(type);
            }
        }

        public object Get(Type type)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));

            lock (_sync)
            {
                if (!_services.TryGetValue(type, out var instance))
                    throw new InvalidOperationException(
                        $"Service '{type.FullName}' is not registered.");

                return instance;
            }
        }

        public T Get<T>() where T : class
        {
            return (T)Get(typeof(T));
        }

        // ------------------------------------------------------------
        // Lifecycle
        // ------------------------------------------------------------

        public void Lock()
        {
            lock (_sync)
            {
                _locked = true;
            }
        }

#if DEBUG
        internal void Clear()
        {
            lock (_sync)
            {
                _services.Clear();
                _locked = false;
            }
        }
#endif
    }
}
