// FILE: PageNav.Core/Services/PageFactory.cs
using NekoLib.Navigation.Contracts.Pages;
using NekoLib.Navigation.Runtime.Registry;
using System;
using System.Collections.Generic;

namespace NekoLib.Navigation.Runtime.Factories
{
    /// <summary>
    /// Responsible ONLY for creating page instances.
    /// Pages must be explicitly registered.
    /// </summary>
    public sealed class PageFactory
    {
        private readonly Dictionary<Type, Func<IPageView>> _factories
            = new();

        // ----------------------------
        // Manual registration (still allowed)
        // ----------------------------
        public void Register<T>() where T : IPageView, new()
            => _factories[typeof(T)] = () => new T();

        public void Register(Type pageType, Func<IPageView> factory)
        {
            _factories[pageType] = factory
                ?? throw new ArgumentNullException(nameof(factory));
        }

        // ----------------------------
        // AUTO-WIRING (THE IMPORTANT PART)
        // ----------------------------
        public static PageFactory AutoWireFromRegistry(
            Func<Type, IPageView> defaultFactory = null)
        {
            var factory = new PageFactory();

            defaultFactory ??= CreateUsingDefaultCtor;

            foreach (var pageType in PageRegistry.RegisteredPageTypes())
            {
                factory.Register(
                    pageType,
                    () => defaultFactory(pageType));
            }

            return factory;
        }

        // ----------------------------
        // Creation
        // ----------------------------
         
            public IPageView Create(Type pageType)
        {
            if(pageType == null)
                throw new ArgumentNullException(nameof(pageType));

            if(!typeof(IPageView).IsAssignableFrom(pageType))
                throw new InvalidOperationException(
                    $"{pageType.FullName} does not implement IPageView.");

            try
            {
                return (IPageView)Activator.CreateInstance(pageType);
            }
            catch(Exception ex)
            {
                throw new InvalidOperationException(
                    $"Failed to create page instance of type {pageType.FullName}.", ex);
            }
        }
        

        public T Create<T>() where T : IPageView
            => (T)Create(typeof(T));

        // ----------------------------
        // Helpers
        // ----------------------------
        private static IPageView CreateUsingDefaultCtor(Type t)
        {
            try
            {
                return (IPageView)Activator.CreateInstance(t);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Failed to create page '{t.FullName}'. " +
                    $"Ensure it has a public parameterless constructor " +
                    $"or provide a custom factory.", ex);
            }
        }
    }
}
