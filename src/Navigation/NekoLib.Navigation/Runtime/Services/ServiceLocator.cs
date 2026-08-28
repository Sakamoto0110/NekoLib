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

        /// <summary>Registers one exact service type before the locator is locked.</summary>
        /// <param name="serviceType">Exact key used for later resolution; assignable types are not searched.</param>
        /// <param name="instance">Context-scoped instance retained without automatic disposal.</param>
        /// <exception cref="ArgumentNullException">Either argument is <see langword="null"/>.</exception>
        /// <exception cref="InvalidOperationException">Registration is locked or the key is already present.</exception>
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

        /// <summary>Registers an instance under the exact compile-time type <typeparamref name="T"/>.</summary>
        /// <typeparam name="T">Service key type.</typeparam>
        /// <param name="instance">Context-scoped instance retained without automatic disposal.</param>
        public void Register<T>(T instance) where T : class
            => Register(typeof(T), instance);

        // ------------------------------------------------------------
        // Resolution
        // ------------------------------------------------------------

        /// <summary>Checks whether an exact service type is registered.</summary>
        /// <param name="type">Exact service key; <see langword="null"/> returns <see langword="false"/>.</param>
        /// <returns><see langword="true"/> when the key is present.</returns>
        public bool CanResolve(Type type)
        {
            if (type == null)
                return false;

            lock (_sync)
            {
                return _services.ContainsKey(type);
            }
        }

        /// <summary>Resolves an instance by its exact registered type.</summary>
        /// <param name="type">Exact service key.</param>
        /// <returns>The registered instance.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="type"/> is <see langword="null"/>.</exception>
        /// <exception cref="InvalidOperationException">The key is not registered.</exception>
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

        /// <summary>Resolves an instance by the exact compile-time type <typeparamref name="T"/>.</summary>
        /// <typeparam name="T">Registered service key type.</typeparam>
        /// <returns>The registered instance cast to <typeparamref name="T"/>.</returns>
        public T Get<T>() where T : class
        {
            return (T)Get(typeof(T));
        }

        // ------------------------------------------------------------
        // Lifecycle
        // ------------------------------------------------------------

        /// <summary>Permanently closes registration while leaving resolution available.</summary>
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
