using NekoLib.Navigation.Attributes;
using NekoLib.Navigation.Contracts.Guards;
using NekoLib.Navigation.Contracts.Pages;
using NekoLib.Navigation.Diagnostics;
using NekoLib.Navigation.Metadata;
using NekoLib.Navigation.Runtime.Guards;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace NekoLib.Navigation.Runtime.Registry
{
    /// <summary>
    /// Static page metadata registry.
    /// Responsible ONLY for describing pages.
    /// No runtime state. No instance caching.
    /// </summary>
    public static class PageRegistry
    {
        private static readonly Dictionary<Type, PageDescriptor> _byType =
            new Dictionary<Type, PageDescriptor>();

        private static readonly Dictionary<string, Type> _byName =
            new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);

        private static readonly object _lock = new object();

        // --------------------------------------------------------------------
        // Registration
        // --------------------------------------------------------------------

        public static void RegisterFromAssembly(Assembly asm)
        {
            if (asm == null) throw new ArgumentNullException(nameof(asm));

            foreach (var t in asm.GetTypes())
            {
                if (IsPageType(t))
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
            if (pageType == null)
                throw new ArgumentNullException(nameof(pageType));

            if (!IsPageType(pageType))
                throw new ArgumentException(
                    "Type must implement IPageView and not be abstract.",
                    nameof(pageType));

            lock (_lock)
            {
                if (_byType.ContainsKey(pageType))
                {
                    NavigationDiagnostics.EmitWarn(
                        $"Page '{pageType.FullName}' already registered. Ignored.");
                    return;
                }

                var desc = BuildDescriptor(pageType);
                configure?.Invoke(desc);

                if (_byName.ContainsKey(desc.Name))
                {
                    throw new InvalidOperationException(
                        $"Duplicate page name '{desc.Name}'. " +
                        $"Types: {_byName[desc.Name].FullName} and {pageType.FullName}");
                }

                _byType[pageType] = desc;
                _byName[desc.Name] = pageType;

                NavigationDiagnostics.EmitInfo(
                    $"Registered Page '{desc.Name}' " +
                    $"(Type={pageType.Name}, Kind={desc.Kind}, Cache={desc.ReusePolicy})");
            }
        }

        private static bool IsPageType(Type t)
            => typeof(IPageView).IsAssignableFrom(t) && !t.IsAbstract;

        private static PageDescriptor BuildDescriptor(
    Type pageType )
        {
            if (pageType == null)
                throw new ArgumentNullException(nameof(pageType));

            // ------------------------------------------------------------
            // PageBehavior (single)
            // ------------------------------------------------------------
            var behaviorAttr = pageType
                .GetCustomAttributes(typeof(PageBehaviorAttribute), true)
                .OfType<PageBehaviorAttribute>()
                .FirstOrDefault();

            // ------------------------------------------------------------
            // Guard attributes (stacked, AllowMultiple = true)
            // ------------------------------------------------------------
            var guardAttributes = pageType
                .GetCustomAttributes(typeof(GuardAttribute), true)
                .OfType<GuardAttribute>()
                .ToArray();

            IGuard composedGuard = null;

            if (guardAttributes.Length > 0)
            {
                var guards = new List<IGuard>(guardAttributes.Length);

                foreach (var attr in guardAttributes)
                {
                    var guard = attr.CreateGuard( );

                    if (guard != null)
                        guards.Add(guard);
                }

                if (guards.Count == 1)
                {
                    composedGuard = guards[0];
                }
                else if (guards.Count > 1)
                {
                    composedGuard = new AndGuard(guards.ToArray());
                }
            }

            var name = behaviorAttr?.NameOverride ?? pageType.Name;

            return new PageDescriptor
            {
                PageType = pageType,
                Guard = composedGuard,
                Name = name,
                Kind = behaviorAttr?.Kind ?? PageKind.Default,
                ReusePolicy = behaviorAttr?.ReusePolicy ?? PageReusePolicy.Transient,
                Timeout = behaviorAttr?.Timeout ?? PageTimeoutBehavior.Default,
                WaitCompletionBeforeShow =
                    behaviorAttr?.LoadMode ?? NavigationLoadMode.ShowImmediately,
                Tags = behaviorAttr?.Tags?.ToHashSet(StringComparer.OrdinalIgnoreCase)
                       ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            };
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

            lock (_lock)
                return _byType.TryGetValue(pageType, out descriptor);
        }

        public static PageDescriptor GetDescriptor(Type pageType)
        {
            if (!TryGetDescriptor(pageType, out var d))
                throw new KeyNotFoundException(
                    $"Page '{pageType.FullName}' is not registered.");

            return d;
        }

        public static PageDescriptor GetDescriptor(string name)
        {
            if (name == null) throw new ArgumentNullException(nameof(name));

            lock (_lock)
            {
                if (!_byName.TryGetValue(name, out var t))
                    throw new KeyNotFoundException($"Page '{name}' not registered.");

                return _byType[t];
            }
        }

        public static IEnumerable<PageDescriptor> AllDescriptors()
        {
            lock (_lock)
                return _byType.Values.ToList();
        }

        public static IEnumerable<Type> RegisteredPageTypes()
        {
            lock (_lock)
                return _byType.Keys.ToList();
        }

        // --------------------------------------------------------------------
        // Timeout Resolution (Metadata Only)
        // --------------------------------------------------------------------

        public static PageDescriptor ResolveTimeoutTarget()
        {
            lock (_lock)
            {
                return _byType.Values.FirstOrDefault(x => x.Kind == PageKind.Home)
                    ?? _byType.Values.FirstOrDefault(
                        x => x.Tags.Contains("home", StringComparer.OrdinalIgnoreCase));
            }
        }

#if DEBUG
        internal static void ResetForTests()
        {
            lock (_lock)
            {
                _byType.Clear();
                _byName.Clear();
            }
        }
#endif
    }
}
