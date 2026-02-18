using NekoLib.Navigation.Attributes;
using NekoLib.Navigation.Contracts.Guards;
using NekoLib.Navigation.Contracts.Pages;
using NekoLib.Navigation.Diagnostics;
using NekoLib.Navigation.Metadata;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace NekoLib.Navigation.Runtime.Registry
{
    /// <summary>
    /// Instance-scoped page metadata registry.
    /// Responsible ONLY for describing pages.
    /// No runtime state. No instance caching.
    /// </summary>
    public sealed class PageRegistry
    {
        private readonly Dictionary<Type, PageDescriptor> _byType = new();
        private readonly Dictionary<string, Type> _byName =
            new(StringComparer.OrdinalIgnoreCase);

        private readonly object _lock = new();

        // --------------------------------------------------------------------
        // Registration
        // --------------------------------------------------------------------

        public void RegisterFromAssembly(Assembly asm)
        {
            if (asm == null) throw new ArgumentNullException(nameof(asm));

            foreach (var t in asm.GetTypes())
            {
                if (IsPageType(t))
                    Register(t);
            }
        }

        public void Register<T>() where T : IPageView
            => Register(typeof(T));

        public void Register<T>(Action<PageDescriptor> configure)
            where T : IPageView
            => Register(typeof(T), configure);

        public void Register(Type pageType)
            => Register(pageType, null);

        public void Register(Type pageType, Action<PageDescriptor> configure)
        {
            if (pageType == null)
                throw new ArgumentNullException(nameof(pageType));

            if (!IsPageType(pageType))
                throw new ArgumentException(
                    "Type must implement IPageView and not be abstract.",
                    nameof(pageType));

            PageDescriptor desc;
            bool alreadyRegistered;

            lock (_lock)
            {
                if (_byType.ContainsKey(pageType))
                {
                    alreadyRegistered = true;
                    desc = null;
                }
                else
                {
                    alreadyRegistered = false;

                    desc = BuildDescriptor(pageType);
                    configure?.Invoke(desc);

                    if (_byName.ContainsKey(desc.Name))
                    {
                        throw new InvalidOperationException(
                            $"Duplicate page name '{desc.Name}'. " +
                            $"Types: {_byName[desc.Name].FullName} and {pageType.FullName}");
                    }

                    _byType[pageType] = desc;
                    _byName[desc.Name] = pageType;
                }
            }

            // Emit outside lock
            if (alreadyRegistered)
            {
                NavigationDiagnostics.EmitWarn(
                    $"Page '{pageType.FullName}' already registered. Ignored.");
                return;
            }

            NavigationDiagnostics.EmitInfo(
                $"Registered Page '{desc.Name}' (Type={pageType.Name}, Kind={desc.Kind}, Cache={desc.ReusePolicy})");
        }

        private static bool IsPageType(Type t)
            => typeof(IPageView).IsAssignableFrom(t) && !t.IsAbstract;

        private static PageDescriptor BuildDescriptor(Type pageType)
        {
            if (pageType == null)
                throw new ArgumentNullException(nameof(pageType));

            var behaviorAttr = pageType
                .GetCustomAttributes(typeof(PageBehaviorAttribute), true)
                .OfType<PageBehaviorAttribute>()
                .FirstOrDefault();

            var guardAttributes = pageType
                .GetCustomAttributes(typeof(GuardAttribute), true)
                .OfType<GuardAttribute>()
                .ToArray();

            // NOTE: Registry must not depend on Runtime.Guards.
            // We only store a single guard instance if attributes produce one.
            // If multiple guards exist, you should compose them during descriptor creation
            // via an injected composition strategy OR a builder/configurator.
            // For now, we keep the previous behavior: AND-composition requires runtime.
            // So we only keep first guard unless you compose elsewhere.
            IGuard composedGuard = null;

            if (guardAttributes.Length > 0)
            {
                var guards = new List<IGuard>(guardAttributes.Length);
                foreach (var attr in guardAttributes)
                {
                    var g = attr.CreateGuard();
                    if (g != null) guards.Add(g);
                }

                // Minimal: store single guard only.
                // If you want AND composition, do it in the configurator or a runtime composer.
                if (guards.Count == 1)
                    composedGuard = guards[0];
                else if (guards.Count > 1)
                    composedGuard = new CompositeAndGuard(guards.ToArray());
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
                LoadMode = behaviorAttr?.LoadMode ?? NavigationLoadMode.ShowImmediately,
                Tags = behaviorAttr?.Tags?.ToHashSet(StringComparer.OrdinalIgnoreCase)
                       ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            };
        }

        // --------------------------------------------------------------------
        // Queries
        // --------------------------------------------------------------------
        internal PageDescriptor ResolveTimeoutTarget()
        {
            return AllDescriptors()
                .FirstOrDefault(x => x.Kind == PageKind.Home)
                ?? null;
}
        public bool TryGetDescriptor(Type pageType, out PageDescriptor descriptor)
        {
            if (pageType == null)
            {
                descriptor = null;
                return false;
            }

            lock (_lock)
                return _byType.TryGetValue(pageType, out descriptor);
        }

        public PageDescriptor GetDescriptor(Type pageType)
        {
            if (!TryGetDescriptor(pageType, out var d))
                throw new KeyNotFoundException(
                    $"Page '{pageType.FullName}' is not registered.");

            return d;
        }

        public PageDescriptor GetDescriptor(string name)
        {
            if (name == null) throw new ArgumentNullException(nameof(name));

            lock (_lock)
            {
                if (!_byName.TryGetValue(name, out var t))
                    throw new KeyNotFoundException($"Page '{name}' not registered.");

                return _byType[t];
            }
        }

        public IEnumerable<PageDescriptor> AllDescriptors()
        {
            lock (_lock)
                return _byType.Values.ToList();
        }

        public IEnumerable<Type> RegisteredPageTypes()
        {
            lock (_lock)
                return _byType.Keys.ToList();
        }

        // --------------------------------------------------------------------
        // Minimal AND composition that does NOT require Runtime.Guards
        // --------------------------------------------------------------------
        private sealed class CompositeAndGuard : IGuard
        {
            private readonly IGuard[] _guards;
            public CompositeAndGuard(IGuard[] guards) => _guards = guards ?? Array.Empty<IGuard>();

            public async Task<GuardResult> EvaluateAsync(GuardContext context)
            {
                foreach (var g in _guards)
                {
                    var r = await g.EvaluateAsync(context);
                    if (!r.Allowed) return r;
                }
                return GuardResult.Allow();
            }
        }
    }
}
