using NekoLib.Navigation.Bootstrap;
using NekoLib.Navigation.Metadata;
using System;
using System.Collections.Generic;
#if NET9_0_OR_GREATER
using System.Collections.Frozen;
#endif
using System.Linq;
using System.Reflection;

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

        /// <summary>Builds a registry from an explicitly configured metadata builder.</summary>
        /// <param name="configure">Callback that adds scans and explicit page registrations.</param>
        /// <returns>An immutable registry with case-insensitive name lookup.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="configure"/> is <see langword="null"/>.</exception>
        /// <exception cref="InvalidOperationException">A page descriptor is invalid or a type or name is duplicated.</exception>
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
        internal static void InjectPage(PageDescriptor descriptor)
        {
            CreateInternal(new[] { descriptor });
        }

        /// <summary>Builds a registry by scanning one assembly for concrete page types.</summary>
        /// <param name="assembly">Assembly to scan.</param>
        /// <returns>An immutable page registry.</returns>
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

        /// <summary>Attempts to resolve metadata by concrete page type.</summary>
        /// <param name="pageType">Registered page type.</param>
        /// <param name="descriptor">Resolved descriptor when the method returns <see langword="true"/>.</param>
        /// <returns><see langword="true"/> when the type is registered.</returns>
        public bool TryGetDescriptor(Type pageType, out PageDescriptor descriptor)
            => _byType.TryGetValue(pageType, out descriptor);

        /// <summary>Resolves metadata by concrete page type.</summary>
        /// <param name="pageType">Registered page type.</param>
        /// <returns>The matching immutable descriptor.</returns>
        /// <exception cref="KeyNotFoundException"><paramref name="pageType"/> is not registered.</exception>
        public PageDescriptor GetDescriptor(Type pageType)
        {
            if (!_byType.TryGetValue(pageType, out var d))
                throw new KeyNotFoundException(
                    $"Page '{pageType.FullName}' is not registered.");

            return d;
        }

        /// <summary>Resolves metadata by case-insensitive registry name.</summary>
        /// <param name="name">Registered page name.</param>
        /// <returns>The matching immutable descriptor.</returns>
        /// <exception cref="KeyNotFoundException"><paramref name="name"/> is not registered.</exception>
        public PageDescriptor GetDescriptor(string name)
        {
            if (!_byName.TryGetValue(name, out var type))
                throw new KeyNotFoundException($"Page '{name}' not registered.");

            return _byType[type];
        }

        /// <summary>Enumerates every immutable descriptor in registry order.</summary>
        /// <returns>A read-only enumeration backed by the immutable registry.</returns>
        public IEnumerable<PageDescriptor> AllDescriptors()
            => _byType.Values;
    }
}
