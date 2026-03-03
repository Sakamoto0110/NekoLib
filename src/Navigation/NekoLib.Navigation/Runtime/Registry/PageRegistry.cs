using NekoLib.Navigation.Bootstrap;
using NekoLib.Navigation.Contracts.Guards;
using NekoLib.Navigation.Contracts.Pages;
using NekoLib.Navigation.Diagnostics;
using NekoLib.Navigation.Metadata;
using NekoLib.Navigation.Metadata.Attributes;
using System;
using System.Collections.Generic;
#if NET9_0_OR_GREATER
using System.Collections.Frozen;
#endif
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
        private readonly IReadOnlyDictionary<Type, PageDescriptor> _byType;
        private readonly IReadOnlyDictionary<string, Type> _byName;

        private PageRegistry(
            IReadOnlyDictionary<Type, PageDescriptor> byType,
            IReadOnlyDictionary<string, Type> byName)
        {
            _byType = byType;
            _byName = byName;
        }

        // -----------------------------
        // Factory Methods
        // -----------------------------

        public static PageRegistry Create(
            Action<PageMetadataBuilder> configure)
        {
            if (configure == null)
                throw new ArgumentNullException(nameof(configure));

            var builder = new PageMetadataBuilder();
            configure(builder);

            var descriptors = builder.Build();

            return CreateInternal(descriptors);
        }

        public static PageRegistry CreateFromAssembly(Assembly assembly)
        {
            return Create(b => b.RegisterFromAssembly(assembly));
        }

        private static PageRegistry CreateInternal(
            IEnumerable<PageDescriptor> descriptors)
        {
            var byType = new Dictionary<Type, PageDescriptor>();
            var byName = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);

            foreach (var desc in descriptors)
            {
                if (byType.ContainsKey(desc.PageType))
                    throw new InvalidOperationException(
                        $"Page '{desc.PageType.FullName}' already registered.");

                if (byName.ContainsKey(desc.Name))
                    throw new InvalidOperationException(
                        $"Duplicate page name '{desc.Name}'.");

                byType[desc.PageType] = desc;
                byName[desc.Name] = desc.PageType;
            }

#if NET9_0_OR_GREATER
        // Optional performance improvement
        return new PageRegistry(
            byType.ToFrozenDictionary(),
            byName.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase));
#else
            return new PageRegistry(byType, byName);
#endif
        }

        // -----------------------------
        // Queries
        // -----------------------------

        public bool TryGetDescriptor(Type pageType, out PageDescriptor descriptor)
            => _byType.TryGetValue(pageType, out descriptor);

        public PageDescriptor GetDescriptor(Type pageType)
        {
            if (!_byType.TryGetValue(pageType, out var d))
                throw new KeyNotFoundException(
                    $"Page '{pageType.FullName}' is not registered.");

            return d;
        }

        public PageDescriptor GetDescriptor(string name)
        {
            if (!_byName.TryGetValue(name, out var type))
                throw new KeyNotFoundException($"Page '{name}' not registered.");

            return _byType[type];
        }

        public IEnumerable<PageDescriptor> AllDescriptors()
            => _byType.Values;

        public PageDescriptor? ResolveTimeoutTarget()
            => _byType.Values
                      .FirstOrDefault(x => x.Role == PageRole.Home);
    }
}
