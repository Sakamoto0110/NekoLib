using NekoLib.Navigation.Contracts.Pages;
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
        /// <summary>Registers the public parameterless constructor for a page type.</summary>
        /// <typeparam name="T">Page type to create.</typeparam>
        public void Register<T>() where T : IPageView, new()
            => Register(typeof(T), () => new T());

        /// <summary>Registers or replaces the factory for a page type.</summary>
        /// <param name="pageType">Type implementing <see cref="IPageView"/>.</param>
        /// <param name="factory">Factory invoked for every requested instance; it must return a compatible non-null view.</param>
        /// <exception cref="ArgumentNullException"><paramref name="pageType"/> or <paramref name="factory"/> is <see langword="null"/>.</exception>
        /// <exception cref="InvalidOperationException"><paramref name="pageType"/> does not implement <see cref="IPageView"/>.</exception>
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
        

       /// <summary>Creates a factory with registrations for every supplied registry page type.</summary>
       /// <param name="registeredPageTypes">Page types enumerated and registered immediately.</param>
       /// <param name="defaultFactory">Optional application factory; the public parameterless constructor is used when omitted.</param>
       /// <returns>A new mutable page factory.</returns>
       /// <exception cref="ArgumentNullException"><paramref name="registeredPageTypes"/> is <see langword="null"/>.</exception>
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
        /// <summary>Creates one page instance through a registered factory or the enabled fallback.</summary>
        /// <param name="pageType">Concrete page type to create.</param>
        /// <returns>The newly created page instance.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="pageType"/> is <see langword="null"/>.</exception>
        /// <exception cref="InvalidOperationException">The type is invalid, unregistered while fallback is disabled, or its factory fails.</exception>
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

        /// <summary>Creates one page instance through the registration for <typeparamref name="T"/>.</summary>
        /// <typeparam name="T">Concrete page type to create.</typeparam>
        /// <returns>The newly created page instance.</returns>
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
