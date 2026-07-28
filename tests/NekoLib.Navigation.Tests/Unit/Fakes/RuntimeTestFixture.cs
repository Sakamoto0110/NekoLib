using System;
using System.Collections.Generic;
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
        public IReadOnlyList<IPageView> CreatedPages { get; }

        private RuntimeTestFixture(
            FakePageHost host,
            ServiceLocator services,
            PageRegistry registry,
            PageFactory factory,
            NavigationContext context,
            NavigationRuntime runtime,
            IReadOnlyList<IPageView> createdPages)
        {
            Host = host;
            Services = services;
            Registry = registry;
            Factory = factory;
            Context = context;
            Runtime = runtime;
            CreatedPages = createdPages;
        }

        /// <summary>
        /// Builds a runtime registered with <typeparamref name="TIdle"/> as Idle,
        /// plus any extra page types from <paramref name="otherPageTypes"/>. The
        /// PageFactory uses the parameterless constructor of each stub.
        /// </summary>
        public static RuntimeTestFixture Build<TIdle>(params Type[] otherPageTypes)
            where TIdle : StubPageView, new()
            => BuildCore<TIdle>(
                new SyncEventDispatcherAdapter(),
                null,
                null,
                null,
                otherPageTypes);

        public static RuntimeTestFixture BuildWithDispatcher<TIdle>(
            IEventDispatcherAdapter dispatcher,
            params Type[] otherPageTypes)
            where TIdle : StubPageView, new()
            => BuildCore<TIdle>(
                dispatcher ?? throw new ArgumentNullException(nameof(dispatcher)),
                null,
                null,
                null,
                otherPageTypes);

        public static RuntimeTestFixture BuildWithPageCreated<TIdle>(
            Action<IPageView> pageCreated,
            params Type[] otherPageTypes)
            where TIdle : StubPageView, new()
            => BuildCore<TIdle>(
                new SyncEventDispatcherAdapter(),
                pageCreated ?? throw new ArgumentNullException(nameof(pageCreated)),
                null,
                null,
                otherPageTypes);

        public static RuntimeTestFixture BuildWithInteractionBlocker<TIdle>(
            IInteractionBlocker interactionBlocker,
            params Type[] otherPageTypes)
            where TIdle : StubPageView, new()
            => BuildCore<TIdle>(
                new SyncEventDispatcherAdapter(),
                null,
                interactionBlocker ??
                    throw new ArgumentNullException(
                        nameof(interactionBlocker)),
                null,
                otherPageTypes);

        public static RuntimeTestFixture BuildWithServices<TIdle>(
            Action<ServiceLocator, FakePageHost, PageFactory> configure,
            params Type[] otherPageTypes)
            where TIdle : StubPageView, new()
            => BuildCore<TIdle>(
                new SyncEventDispatcherAdapter(),
                null,
                null,
                configure ??
                    throw new ArgumentNullException(nameof(configure)),
                otherPageTypes);

        private static RuntimeTestFixture BuildCore<TIdle>(
            IEventDispatcherAdapter dispatcher,
            Action<IPageView> pageCreated,
            IInteractionBlocker interactionBlocker,
            Action<ServiceLocator, FakePageHost, PageFactory> configure,
            Type[] otherPageTypes)
            where TIdle : StubPageView, new()
        {
            var host = new FakePageHost();

            var registry = PageRegistry.Create(builder =>
            {
                // Runtime fixtures use the Type overload because the additional
                // page set is discovered dynamically by each test.
                builder.RegisterType(typeof(TIdle), d => d.Role = PageRole.Idle);
                foreach (var t in otherPageTypes)
                    builder.RegisterType(t);
            });

            // Each registered stub page is wired with an explicit default-ctor
            // factory so the runtime's ResolvePage doesn't fall back to reflection.
            var createdPages = new List<IPageView>();
            var factory = PageFactory.AutoWireFromRegistry(
                EnumerateRegisteredTypes<TIdle>(otherPageTypes),
                type =>
                {
                    var page = (IPageView)Activator.CreateInstance(type);
                    createdPages.Add(page);
                    pageCreated?.Invoke(page);
                    return page;
                });

            // The runtime pulls IEventDispatcherAdapter AND PageFactory from the
            // ServiceLocator (see NavigationRuntime.EnsureRuntimeServices); register
            // both before Lock().
            var services = new ServiceLocator();
            services.Register<IEventDispatcherAdapter>(dispatcher);
            services.Register<PageFactory>(factory);
            if (interactionBlocker != null)
            {
                services.Register<IInteractionBlocker>(
                    interactionBlocker);
            }
            configure?.Invoke(services, host, factory);
            services.Lock();

            var platform = new FakePlatformAdapter();
            var ctx = new NavigationContext(host, services, registry, platform);
            var runtime = new NavigationRuntime(ctx);

            return new RuntimeTestFixture(
                host,
                services,
                registry,
                factory,
                ctx,
                runtime,
                createdPages);
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
