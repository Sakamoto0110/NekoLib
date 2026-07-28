using System;
using System.Threading.Tasks;
using NekoLib.DebugUtils;
using NekoLib.Navigation.Bootstrap;
using NekoLib.Navigation.Contracts.Pages;
using NekoLib.Navigation.Contracts.Platform;
using NekoLib.Navigation.Contracts.Runtime;
using NekoLib.Navigation.Runtime.Registry;
using NekoLib.Navigation.Tests.Unit.Fakes;
using Xunit;

namespace NekoLib.Navigation.Tests.Unit
{
    [Collection("NavigationServiceFacade")]
    public sealed class DebugUtilsGlobalBootstrapTests
    {
        [Fact]
        public async Task UseDebugUtils_GlobalEnabledAtStart_ShutdownRemovesContextProviders()
        {
            var registry = PageRegistry.Create(
                builder => builder.RegisterType(typeof(StubIdle)));
            var bootstrap = PageNavBootstrap
                .Use<BootstrapPlatformAdapter>(new object())
                .UseRegistry(registry)
                .UseDebugUtils();

            using var debug = DebugUtilsRuntime.EnableGlobal();
            try
            {
                bootstrap.Start();

                var mountedKeys = debug.StateKeys();
                Assert.NotEmpty(mountedKeys);
                Assert.Contains("Navigation::runtime", mountedKeys);
                Assert.Contains("Navigation::currentPage", mountedKeys);
                Assert.Contains("Navigation::history", mountedKeys);
                Assert.Contains("Navigation::session", mountedKeys);

                await NavigationService.Shutdown();

                Assert.Empty(debug.StateKeys());
            }
            finally
            {
                await NavigationService.Shutdown();
            }
        }

        [Fact]
        public async Task UseDebugUtils_GlobalNoOp_StartsWithoutObserver()
        {
            var registry = PageRegistry.Create(
                builder => builder.RegisterType(typeof(StubIdle)));
            try
            {
                PageNavBootstrap
                    .Use<BootstrapPlatformAdapter>(new object())
                    .UseRegistry(registry)
                    .UseDebugUtils()
                    .Start();

                await NavigationService.Shutdown();
            }
            finally
            {
                await NavigationService.Shutdown();
            }
        }

        [Fact]
        public async Task Start_WithIdleObserver_ExposesArmedIdleImmediately()
        {
            await NavigationService.Shutdown();
            var registry = PageRegistry.Create(
                builder => builder.RegisterType(typeof(StubIdle)));
            using var debug = DebugUtilsRuntime.EnableGlobal();

            try
            {
                PageNavBootstrap
                    .Use<ObservedBootstrapPlatformAdapter>(new object())
                    .UseRegistry(registry)
                    .UseIdleTimeout(1_000)
                    .UseDebugUtils()
                    .Start();

                var idle =
                    debug.CaptureState()["Navigation::idle"];
                Assert.Equal("Armed", Prop(idle, "status"));
                Assert.Equal("Configured", Prop(idle, "decision"));
                Assert.Equal(1_000, Prop(idle, "intervalMs"));
            }
            finally
            {
                await NavigationService.Shutdown();
            }
        }

        [Fact]
        public async Task Start_WithoutIdleObserver_ExposesUnavailableIdleImmediately()
        {
            await NavigationService.Shutdown();
            var registry = PageRegistry.Create(
                builder => builder.RegisterType(typeof(StubIdle)));
            using var debug = DebugUtilsRuntime.EnableGlobal();

            try
            {
                PageNavBootstrap
                    .Use<BootstrapPlatformAdapter>(new object())
                    .UseRegistry(registry)
                    .UseIdleTimeout(1_000)
                    .UseDebugUtils()
                    .Start();

                var idle =
                    debug.CaptureState()["Navigation::idle"];
                Assert.Equal("Unavailable", Prop(idle, "status"));
                Assert.Equal(
                    "InteractionObserverUnavailable",
                    Prop(idle, "decision"));
                Assert.Equal(1_000, Prop(idle, "intervalMs"));
            }
            finally
            {
                await NavigationService.Shutdown();
            }
        }

        public sealed class BootstrapPlatformAdapter : IPlatformAdapter
        {
            public bool CanHandle(object host) => true;

            public IPageHost CreateHost(object host) => new FakePageHost();

            public IEventDispatcherAdapter CreateEventDispatcher(object host)
                => new SyncEventDispatcherAdapter();

            public IEventSubscriptionAdapter CreateEventSubscriber(object host)
                => null;

            public IInteractionBlocker CreateInteractionBlocker(object host)
                => new NoOpInteractionBlocker();

            public ITimerAdapter CreateTimerAdapter() => new NoOpTimer();

            public Type GetDefaultLoadingMaskType() => null;

            public IInteractionObserverService CreateInteractionObserverAdapter(object host)
                => null;

            public IFocusObserverAdapter CreateFocusObserver(object host) => null;
        }

        public sealed class ObservedBootstrapPlatformAdapter : IPlatformAdapter
        {
            public bool CanHandle(object host) => true;

            public IPageHost CreateHost(object host) => new FakePageHost();

            public IEventDispatcherAdapter CreateEventDispatcher(object host)
                => new SyncEventDispatcherAdapter();

            public IEventSubscriptionAdapter CreateEventSubscriber(object host)
                => null;

            public IInteractionBlocker CreateInteractionBlocker(object host)
                => new NoOpInteractionBlocker();

            public ITimerAdapter CreateTimerAdapter() => new NoOpTimer();

            public Type GetDefaultLoadingMaskType() => null;

            public IInteractionObserverService CreateInteractionObserverAdapter(
                object host)
                => new NoOpInteractionObserver();

            public IFocusObserverAdapter CreateFocusObserver(object host) => null;
        }

        private sealed class NoOpInteractionBlocker : IInteractionBlocker
        {
            public void Block()
            {
            }

            public void Unblock()
            {
            }
        }

        private sealed class NoOpTimer : ITimerAdapter
        {
            public int IntervalMilliseconds { get; set; }
            public event Action Tick
            {
                add { }
                remove { }
            }

            public void Start()
            {
            }

            public void Stop()
            {
            }

            public void Dispose()
            {
            }
        }

        private sealed class NoOpInteractionObserver :
            IInteractionObserverService
        {
            public event Action InteractionDetected
            {
                add { }
                remove { }
            }
        }

        private static object Prop(object value, string property)
            => value.GetType()
                .GetProperty(property)
                .GetValue(value);
    }
}
