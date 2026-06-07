using System;
using NekoLib.Navigation.Bootstrap;
using NekoLib.Navigation.Contracts.Pages;
using NekoLib.Navigation.Contracts.Platform;
using NekoLib.Navigation.Metadata;
using NekoLib.Navigation.Runtime.Core;
using NekoLib.Navigation.Runtime.Factories;
using NekoLib.Navigation.Runtime.Registry;
using NekoLib.Navigation.Runtime.Services;

namespace NekoLib.Navigation.Tests.Unit.Fakes
{
    /// <summary>
    /// Wires a per-test instance of <see cref="NavigationRuntime"/> against
    /// fake host + sync dispatcher + the explicit stub page types. Each test
    /// instantiates its own fixture, so there is no shared state across tests
    /// (xunit's default parallel execution is fine).
    /// </summary>
    internal sealed class RuntimeTestFixture
    {
        public FakePageHost Host { get; }
        public ServiceLocator Services { get; }
        public PageRegistry Registry { get; }
        public NavigationContext Context { get; }
        public NavigationRuntime Runtime { get; }
        public PageFactory Factory { get; }

        private RuntimeTestFixture(
            FakePageHost host,
            ServiceLocator services,
            PageRegistry registry,
            PageFactory factory,
            NavigationContext context,
            NavigationRuntime runtime)
        {
            Host = host;
            Services = services;
            Registry = registry;
            Factory = factory;
            Context = context;
            Runtime = runtime;
        }

        /// <summary>
        /// Builds a runtime registered with <typeparamref name="TIdle"/> as Idle,
        /// plus any extra page types from <paramref name="otherPageTypes"/>. The
        /// PageFactory uses the parameterless constructor of each stub.
        /// </summary>
        public static RuntimeTestFixture Build<TIdle>(params Type[] otherPageTypes)
            where TIdle : StubPageView, new()
        {
            var host = new FakePageHost();

            var registry = PageRegistry.Create(builder =>
            {
                // Use RegisterType (not Register<T>) so the type lands in the
                // builder's _explicitTypes set — Register<T> only attaches a
                // configurator, which never runs unless the type is already
                // being built from an assembly scan.
                builder.RegisterType(typeof(TIdle), d => d.Role = PageRole.Idle);
                foreach (var t in otherPageTypes)
                    builder.RegisterType(t);
            });

            // Each registered stub page is wired with an explicit default-ctor
            // factory so the runtime's ResolvePage doesn't fall back to reflection.
            var factory = PageFactory.AutoWireFromRegistry(EnumerateRegisteredTypes<TIdle>(otherPageTypes));

            // The runtime pulls IEventDispatcherAdapter AND PageFactory from the
            // ServiceLocator (see NavigationRuntime.EnsureRuntimeServices); register
            // both before Lock().
            var services = new ServiceLocator();
            services.Register<IEventDispatcherAdapter>(new SyncEventDispatcherAdapter());
            services.Register<PageFactory>(factory);
            services.Lock();

            var platform = new FakePlatformAdapter();
            var ctx = new NavigationContext(host, services, registry, platform);
            var runtime = new NavigationRuntime(ctx);

            return new RuntimeTestFixture(host, services, registry, factory, ctx, runtime);
        }

        private static Type[] EnumerateRegisteredTypes<TIdle>(Type[] others)
        {
            var arr = new Type[others.Length + 1];
            arr[0] = typeof(TIdle);
            Array.Copy(others, 0, arr, 1, others.Length);
            return arr;
        }
    }
}
