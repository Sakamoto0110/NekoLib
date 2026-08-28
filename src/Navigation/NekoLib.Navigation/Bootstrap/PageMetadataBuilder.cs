using NekoLib.Navigation.Contracts.Pages;
using NekoLib.Navigation.Metadata;
using NekoLib.Navigation.Metadata.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace NekoLib.Navigation.Bootstrap
{
    /// <summary>
    /// Collects assembly scans and explicit page registrations used to build an
    /// immutable <see cref="Runtime.Registry.PageRegistry"/>.
    /// </summary>
    public sealed class PageMetadataBuilder
    {
        
        private readonly List<Assembly> _assemblies = new();
        private readonly Dictionary<Type, Action<PageDescriptorBuilder>> _manual
            = new();
        private readonly HashSet<Type> _explicitTypes = new();

        internal PageMetadataBuilder()
        {
        }
        /// <summary>Queues one assembly for concrete <see cref="IPageView"/> discovery.</summary>
        /// <param name="assembly">Assembly to scan during registry construction.</param>
        /// <exception cref="ArgumentNullException"><paramref name="assembly"/> is <see langword="null"/>.</exception>
        public void RegisterFromAssembly(Assembly assembly)
        {
            if (assembly == null)
                throw new ArgumentNullException(nameof(assembly));

            _assemblies.Add(assembly);
        }
        /// <summary>Queues each supplied assembly for page discovery.</summary>
        /// <param name="assemblies">Assemblies enumerated immediately.</param>
        /// <exception cref="ArgumentNullException"><paramref name="assemblies"/> or an element is <see langword="null"/>.</exception>
        public void RegisterFromAssemblies(IEnumerable<Assembly> assemblies)
        {
            if (assemblies == null) throw new ArgumentNullException(nameof(assemblies));
            foreach (var asm in assemblies) RegisterFromAssembly(asm);
        }

        /// <summary>Queues the supplied assemblies for page discovery.</summary>
        /// <param name="assemblies">Assemblies to scan during registry construction.</param>
        /// <exception cref="ArgumentNullException"><paramref name="assemblies"/> or an element is <see langword="null"/>.</exception>
        public void RegisterFromAssemblies(params Assembly[] assemblies)
            => RegisterFromAssemblies((IEnumerable<Assembly>)assemblies);

        /// <summary>
        /// Queues an assembly and every reference that can be loaded transitively.
        /// References that cannot be loaded are skipped and reported to the debug output.
        /// </summary>
        /// <param name="root">Root assembly for the breadth-first reference scan.</param>
        /// <exception cref="ArgumentNullException"><paramref name="root"/> is <see langword="null"/>.</exception>
        public void RegisterFromAssemblyAndReferences(Assembly root)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var queue = new Queue<Assembly>();
            queue.Enqueue(root);

            while (queue.Count > 0)
            {
                var asm = queue.Dequeue();
                if (asm == null) continue;

                var name = asm.FullName ?? asm.GetName().Name ?? "";
                if (!visited.Add(name)) continue;

                RegisterFromAssembly(asm);

                foreach (var an in asm.GetReferencedAssemblies())
                {
                    try
                    {
                        queue.Enqueue(Assembly.Load(an));
                    }
                    catch (Exception ex)
                    {
                        // Missing dependencies are common when scanning references
                        // (plugin scenarios); surface them in Debug so a silently
                        // skipped assembly is at least diagnosable (D-9).
                        System.Diagnostics.Debug.WriteLine(
                            $"[Navigation] Failed to load referenced assembly '{an.FullName}': {ex.Message}");
                    }
                }
            }
        }
        /// <summary>
        /// Explicitly registers one concrete page type and optionally appends a
        /// manual descriptor override.
        /// </summary>
        /// <param name="type">Concrete type implementing <see cref="IPageView"/>.</param>
        /// <param name="configure">Optional override applied after attributes.</param>
        /// <exception cref="ArgumentNullException"><paramref name="type"/> is <see langword="null"/>.</exception>
        /// <exception cref="InvalidOperationException"><paramref name="type"/> is abstract or does not implement <see cref="IPageView"/>.</exception>
        public void RegisterType(Type type, Action<PageDescriptorBuilder>? configure = null)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));
            if (!IsPageType(type)) throw new InvalidOperationException($"Not a valid page: {type.FullName}");

            _explicitTypes.Add(type);

            if (configure != null)
            {
                if (_manual.TryGetValue(type, out var existing))
                {
                    _manual[type] = descriptor =>
                    {
                        existing(descriptor);
                        configure(descriptor);
                    };
                }
                else
                {
                    _manual[type] = configure;
                }
            }
        }

       

        /// <summary>Explicitly registers a concrete page type.</summary>
        /// <typeparam name="TPage">Page type to register.</typeparam>
        /// <param name="configure">Optional override applied after attributes.</param>
        public void Register<TPage>(Action<PageDescriptorBuilder>? configure = null)
            where TPage : IPageView
        {
            RegisterType(typeof(TPage), configure);
        }

        internal IEnumerable<PageDescriptor> Build()
        {
            var descriptors = new Dictionary<Type, PageDescriptor>();

            foreach (var asm in _assemblies)
            {
                foreach (var type in GetLoadableTypes(asm))
                {
                    if (!IsPageType(type))
                        continue;

                    if (descriptors.ContainsKey(type))
                        continue;

                    var desc = BuildDescriptor(type);
                    descriptors[type] = desc;
                }
            }
            // 2. Build Explicitly Registered Types (This saves our Default Mask!)
            foreach (var type in _explicitTypes)
            {
                if (descriptors.ContainsKey(type)) continue;

                descriptors[type] = BuildDescriptor(type);
            }
            return descriptors.Values;
        }

        private PageDescriptor BuildDescriptor(Type type)
        {
            var builder = new PageDescriptorBuilder(type);

            ApplyAttributes(type, builder);

            if (_manual.TryGetValue(type, out var configure))
                configure(builder);

            return builder.Build();
        }

        private static bool IsPageType(Type t)
            => typeof(IPageView).IsAssignableFrom(t) && !t.IsAbstract;

        // Shared with the bootstrap's custom-loading-mask probe so both tolerate a
        // partially loadable assembly the same way (NAV-008(b)).
        private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
            => AssemblyTypeScanner.GetLoadableTypes(assembly);

        private static void ApplyAttributes(Type type, PageDescriptorBuilder builder)
        {
            var meta = type.GetCustomAttribute<PageMetadataAttribute>();
            if (meta != null)
            {
                if (!string.IsNullOrWhiteSpace(meta.Name))
                    builder.Name = meta.Name!;

                builder.Role = meta.Role;
                if (meta.Tags != null)
                    foreach (var tag in meta.Tags)
                        builder.AddTag(tag);
            }
            var loadMeta = type.GetCustomAttribute<PageLoadAttribute>();
            if (loadMeta != null)
            {
                // Make sure the property name matches whatever is inside your PageLoadAttribute
                builder.LoadMode = loadMeta.Mode;
            }
            // Instance reuse / cache policy. The DSL (.Transient()/.StrongSingleton()/
            // .WeakSingleton()) runs in the manual phase and overrides this.
            var reuseMeta = type.GetCustomAttribute<PageReuseAttribute>();
            if (reuseMeta != null)
            {
                builder.ReusePolicy = reuseMeta.Policy;
            }
            // Idle timeout (seconds). Only meaningful on the idle page; the bootstrap
            // validates placement.
            var timeoutMeta = type.GetCustomAttribute<PageTimeoutAttribute>();
            if (timeoutMeta != null)
            {
                builder.IdleTimeoutSeconds = timeoutMeta.Seconds;
            }
            foreach (var guardAttr in type.GetCustomAttributes<GuardAttribute>())
            {
                var guard = guardAttr.CreateGuard();
                builder.AddGuard(guard);
            }

            if (type.IsDefined(typeof(AllowAnonymousAttribute), false))
                builder.AllowAnonymous = true;
            if (type.IsDefined(typeof(KeepAttachedAttribute), false))
                builder.KeepAttachedWhenHidden = true;
        }
    }
}
