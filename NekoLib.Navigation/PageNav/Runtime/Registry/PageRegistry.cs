using NekoLib.Navigation.Contracts.Pages;
using NekoLib.Navigation.Diagnostics;
using NekoLib.Navigation.Metadata;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace NekoLib.Navigation.Runtime.Registry
{
    /// <summary>
    /// Static page metadata registry.
    /// Responsible for describing pages, NOT for navigation.
    /// </summary>
    public static class PageRegistry
    {
        // --------------------------------------------------------------------
        // Internal storage
        // --------------------------------------------------------------------

        // Type is the true identity
        private static readonly Dictionary<Type, PageDescriptor> _byType =
            new Dictionary<Type, PageDescriptor>();

        // Name is just an index
        private static readonly Dictionary<string, Type> _byName =
            new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);

        private static readonly object _lock = new object();

        // --------------------------------------------------------------------
        // Registration
        // --------------------------------------------------------------------

        public static void RegisterFromAssembly(Assembly asm)
        {
            if(asm == null) throw new ArgumentNullException(nameof(asm));

            foreach(var t in asm.GetTypes())
            {
                if(IsPageType(t))
                    Register(t);
            }
        }

        public static void Register<T>() where T : IPageView
            => Register(typeof(T));

        public static void Register<T>(Action<PageDescriptor> configure)
            where T : IPageView
            => Register(typeof(T), configure);

        public static void Register(Type pageType)
            => Register(pageType, null);

        public static void Register(Type pageType, Action<PageDescriptor> configure)
        {
            if(pageType == null)
                throw new ArgumentNullException(nameof(pageType));

            if(!IsPageType(pageType))
                throw new ArgumentException(
                    "Type must implement IPageView and not be abstract.",
                    nameof(pageType));

            lock(_lock)
            {
                if(_byType.ContainsKey(pageType))
                {
                    NavigationDiagnostics.EmitWarn(
                        $"Page '{pageType.FullName}' already registered. Ignored.");
                    return;
                }
               
                var desc = BuildDescriptor(pageType);

                // Manual override hook (hybrid mode)
                configure?.Invoke(desc);

                // Name collision detection
                if(_byName.ContainsKey(desc.Name))
                {
                    throw new InvalidOperationException(
                        $"Duplicate page name '{desc.Name}'. " +
                        $"Types: {_byName[desc.Name].FullName} and {pageType.FullName}");
                }

                _byType[pageType] = desc;
                _byName[desc.Name] = pageType;

                NavigationDiagnostics.EmitInfo(
                    $"Registered Page '{desc.Name}' " +
                    $"(Type={pageType.Name}, Kind={desc.Kind}, Cache={desc.CachePolicy})");
            }
        }

        private static bool IsPageType(Type t)
            => typeof(IPageView).IsAssignableFrom(t) && !t.IsAbstract;

        // --------------------------------------------------------------------
        // Descriptor building
        // --------------------------------------------------------------------

        private static PageDescriptor BuildDescriptor(Type pageType)
        {
            var attr = pageType
                .GetCustomAttributes(typeof(PageBehaviorAttribute), true)
                .FirstOrDefault() as PageBehaviorAttribute;

            var name = attr?.NameOverride ?? pageType.Name;

            var d = new PageDescriptor
            {
                PageType = pageType,
                Name = name,
                Kind = attr?.Kind ?? PageKind.Default,
                CachePolicy = attr?.CachePolicy ?? PageCachePolicy.Transient,
                Timeout = attr?.Timeout ?? PageTimeoutBehavior.Default,
                WaitCompletionBeforeShow =
        attr?.LoadMode ?? NavigationLoadMode.ShowImmediately
            };

            if(attr?.Tags != null)
            {
                foreach(var tag in attr.Tags)
                    d.Tags.Add(tag);
            }

            return d;
        }

        // --------------------------------------------------------------------
        // Queries
        // --------------------------------------------------------------------

        public static bool TryGetDescriptor(Type pageType, out PageDescriptor descriptor)
        {
            if (pageType == null)
            {
                descriptor = null;

                return false;
            }
            lock(_lock)
                if(_byType.TryGetValue(pageType, out descriptor))
                    return true;
            descriptor = null;
            return false;
        }

        public static PageDescriptor GetDescriptor(Type pageType)
        {
            if(!TryGetDescriptor(pageType, out var d))
                throw new KeyNotFoundException(
                    $"Page '{pageType.FullName}' is not registered.");

            return d;
        }

        public static PageDescriptor GetDescriptor(string name)
        {
            if(name == null) throw new ArgumentNullException(nameof(name));

            lock(_lock)
            {
                if(!_byName.TryGetValue(name, out var t))
                    throw new KeyNotFoundException($"Page '{name}' not registered.");

                return _byType[t];
            }
        }

        public static IEnumerable<PageDescriptor> AllDescriptors()
        {
            lock(_lock)
                return _byType.Values.ToList();
        }

        public static IEnumerable<Type> RegisteredPageTypes()
        {
            lock(_lock)
                return _byType.Keys.ToList();
        }

        // --------------------------------------------------------------------
        // Timeout resolution
        // --------------------------------------------------------------------

        public static PageDescriptor ResolveTimeoutTarget()
        {
            lock(_lock)
            {
                return _byType.Values.FirstOrDefault(x => x.Kind == PageKind.Home)
                    ?? _byType.Values.FirstOrDefault(
                        x => x.Tags.Contains("home", StringComparer.OrdinalIgnoreCase));
            }
        }

        // --------------------------------------------------------------------
        // Instance resolution (cache policy)
        // --------------------------------------------------------------------

        public static IPageView ResolveInstance(
            PageDescriptor d,
            Func<Type, IPageView> factory)
        {
            if(d == null) throw new ArgumentNullException(nameof(d));
            if(factory == null) throw new ArgumentNullException(nameof(factory));

            switch(d.CachePolicy)
            {
                case PageCachePolicy.Transient:
                    return factory(d.PageType);

                case PageCachePolicy.WeakSingleton:
                case PageCachePolicy.StrongSingleton:
                    return d.CachedInstance ?? (d.CachedInstance = factory(d.PageType));

                case PageCachePolicy.Stackable:
                    var page = factory(d.PageType);
                    d.StackInstances.Push(page);
                    return page;

                default:
                    return factory(d.PageType);
            }
        }

        public static bool TryPopStack(PageDescriptor d, out IPageView page)
        {
            page = null;
            if(d == null) return false;

            if(d.CachePolicy != PageCachePolicy.Stackable)
                return false;

            if(d.StackInstances.Count == 0)
                return false;

            page = d.StackInstances.Pop();
            return true;
        }

#if DEBUG
        // --------------------------------------------------------------------
        // Test / debug helpers
        // --------------------------------------------------------------------
        internal static void ResetForTests()
        {
            lock(_lock)
            {
                _byType.Clear();
                _byName.Clear();
            }
        }
#endif
    }
}
