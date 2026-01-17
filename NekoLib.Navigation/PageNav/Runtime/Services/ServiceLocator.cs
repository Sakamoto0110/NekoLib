using System;
using System.Collections.Generic;

namespace NekoLib.Navigation.Runtime.Services
{

    /// <summary>
    /// Simple, deterministic service locator used by PageNav runtime.
    /// 
    /// Rules:
    /// - Services must be registered before Lock()
    /// - After Lock(), registration is forbidden
    /// - CanResolve(type) MUST be checked before Get(type)
    /// - If CanResolve(type) == true, Get(type) will not throw
    /// </summary>
    public sealed class ServiceLocator
    {
        private readonly Dictionary<Type, object> _services = new();
        private bool _locked;

        // ------------------------------------------------------------
        // Registration
        // ------------------------------------------------------------

        public void Register(Type serviceType, object instance)
        {/// <summary>
         /// Multiple instances stacked (push/pop behavior).
         /// </summary>/// <summary>
         /// Multiple instances stacked (push/pop behavior).
         /// </summary>
            if (serviceType == null)
                throw new ArgumentNullException(nameof(serviceType));

            if (instance == null)
                throw new ArgumentNullException(nameof(instance));

            if (_locked)
                throw new InvalidOperationException("Service registration is locked.");

            _services[serviceType] = instance;
        }

        public void Register<T>(T instance) where T : class
            => Register(typeof(T), instance);

        // ------------------------------------------------------------
        // Resolution
        // ------------------------------------------------------------

        /// <summary>
        /// Returns true if the service is registered.
        /// This method MUST be side-effect free.
        /// </summary>
        public bool CanResolve(Type type)
        {
            if (type == null)
                return false;

            return _services.ContainsKey(type);
        }

        /// <summary>
        /// Gets a registered service.
        /// Must only be called if CanResolve(type) == true.
        /// </summary>
        public object Get(Type type)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));

            if (!_services.TryGetValue(type, out var instance))
                throw new InvalidOperationException(
                    $"Service '{type.FullName}' is not registered.");

            return instance;
        }

        // ------------------------------------------------------------
        // Lifecycle
        // ------------------------------------------------------------

        /// <summary>
        /// Locks the locator, preventing further registrations.
        /// Call this after bootstrap is complete.
        /// </summary>
        public void Lock() => _locked = true;

        /// <summary>
        /// Clears all registrations and unlocks the locator.
        /// Intended for shutdown / test scenarios.
        /// </summary>
        public void Clear()
        {
            _services.Clear();
            _locked = false;
        }
    }
}