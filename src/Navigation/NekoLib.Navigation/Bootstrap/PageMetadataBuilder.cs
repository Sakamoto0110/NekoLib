using NekoLib.Navigation.Contracts.Pages;
using NekoLib.Navigation.Metadata;
using NekoLib.Navigation.Metadata.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace NekoLib.Navigation.Bootstrap
{
    public sealed class PageMetadataBuilder
    {
        
        private readonly List<Assembly> _assemblies = new();
        private readonly Dictionary<Type, Action<PageDescriptorBuilder>> _manual
            = new();
        private readonly HashSet<Type> _explicitTypes = new(); // <-- NEW: Track explicit types
        public void RegisterFromAssembly(Assembly assembly)
        {
            if (assembly == null)
                throw new ArgumentNullException(nameof(assembly));

            _assemblies.Add(assembly);
        }
        public void RegisterFromAssemblies(IEnumerable<Assembly> assemblies)
        {
            if (assemblies == null) throw new ArgumentNullException(nameof(assemblies));
            foreach (var asm in assemblies) RegisterFromAssembly(asm);
        }

        public void RegisterFromAssemblies(params Assembly[] assemblies)
            => RegisterFromAssemblies((IEnumerable<Assembly>)assemblies);

        // Optional: scan references too (careful in large apps)
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
        public void RegisterType(Type type, Action<PageDescriptorBuilder>  configure = null)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));
            if (!IsPageType(type)) throw new InvalidOperationException($"Not a valid page: {type.FullName}");

            _explicitTypes.Add(type);

            if (configure != null)
                _manual[type] = configure;
        }

       

        public void Register<TPage>(Action<PageDescriptorBuilder>? configure = null)
            where TPage : IPageView
        {
            _manual[typeof(TPage)] = configure ?? (_ => { });
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

        // Assembly.GetTypes() throws if any type fails to load (e.g. a missing
        // dependency). Recover the types that DID load instead of aborting the
        // whole scan (NEW-5).
        private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[Navigation] Failed to load some types from '{assembly.FullName}': {ex.Message}");
                return ex.Types.OfType<Type>();
            }
        }

        private static void ApplyAttributes(Type type, PageDescriptorBuilder builder)
        {
            var meta = type.GetCustomAttribute<PageMetadataAttribute>();
            if (meta != null)
            {
                if (!string.IsNullOrWhiteSpace(meta.Name))
                    builder.Name = meta.Name;

                builder.Role = meta.Role;
                builder.Presentation = meta.Presentation;

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