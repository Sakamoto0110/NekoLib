using NekoLib.Navigation.Contracts.Pages;
using NekoLib.Navigation.Diagnostics;
using NekoLib.Navigation.Runtime.Registry;
using System;
using System.Collections.Generic;

namespace NekoLib.Navigation.Runtime.Factories
{
    /// <summary>
    /// Responsible ONLY for creating page instances.
    /// </summary>
    public sealed class PageFactory
    {
        private readonly Dictionary<Type, Func<IPageView>> _factories = new();

        /// <summary>
        /// If true, page types not explicitly registered will be created via default ctor.
        /// This is intended for migration / demos. Prefer explicit registration.
        /// </summary>
        public bool AllowUnregisteredPages { get; set; } = true;
        internal event Action<string> Warn;

        // ----------------------------
        // Manual registration
        // ----------------------------
        public void Register<T>() where T : IPageView, new()
            => Register(typeof(T), () => new T());

        public void Register(Type pageType, Func<IPageView> factory)
        {
            if (pageType == null)
                throw new ArgumentNullException(nameof(pageType));
            if (factory == null)
                throw new ArgumentNullException(nameof(factory));
            if (!typeof(IPageView).IsAssignableFrom(pageType))
                throw new InvalidOperationException($"{pageType.FullName} does not implement IPageView.");

            _factories[pageType] = factory;
        }

        // ----------------------------
        // Auto-wire from registry
        // ----------------------------
        

       public static PageFactory AutoWireFromRegistry(
    IEnumerable<Type> registeredPageTypes,
    Func<Type, IPageView> defaultFactory = null)
{
    if (registeredPageTypes == null)
        throw new ArgumentNullException(nameof(registeredPageTypes));

    var factory = new PageFactory();

    defaultFactory = defaultFactory?? CreateUsingDefaultCtor;

    foreach (var pageType in registeredPageTypes)
    {
        factory.Register(pageType, () => defaultFactory(pageType));
    }

    return factory;
}

    
        // ----------------------------
        // Creation (migration-friendly)
        // ----------------------------
        public IPageView Create(Type pageType)
        {
            if (pageType == null)
                throw new ArgumentNullException(nameof(pageType));

            if (!typeof(IPageView).IsAssignableFrom(pageType))
                throw new InvalidOperationException($"{pageType.FullName} does not implement IPageView.");

            if (_factories.TryGetValue(pageType, out var factory))
            {
                try {  
                    var f = factory();
                    //f.Name = pageType.Name;
                    return f;
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        $"Factory failed to create page '{pageType.FullName}'.", ex);
                }
            }

            if (!AllowUnregisteredPages)
            {
                throw new InvalidOperationException(
                    $"Page type '{pageType.FullName}' is not registered in PageFactory.");
            }

            // Fallback path (migration only)
             Warn?.Invoke(
                $"[PageFactory] Page '{pageType.FullName}' was not registered; using default ctor fallback.");

            return CreateUsingDefaultCtor(pageType);
        }

        public T Create<T>() where T : IPageView
            => (T)Create(typeof(T));

        private static IPageView CreateUsingDefaultCtor(Type t)
        {
            try
            {
                return (IPageView)Activator.CreateInstance(t);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Failed to create page '{t.FullName}'. Ensure it has a public parameterless constructor " +
                    $"or register a custom factory.", ex);
            }
        }
    }
}
