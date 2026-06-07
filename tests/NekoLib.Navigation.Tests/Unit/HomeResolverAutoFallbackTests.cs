using System.Threading.Tasks;
using NekoLib.Navigation.Contracts.Platform;
using NekoLib.Navigation.Runtime.Core;
using NekoLib.Navigation.Runtime.Factories;
using NekoLib.Navigation.Runtime.Registry;
using NekoLib.Navigation.Runtime.Services;
using NekoLib.Navigation.Tests.Unit.Fakes;
using Xunit;

namespace NekoLib.Navigation.Tests.Unit
{
    /// <summary>
    /// Pins the name-convention fallback in
    /// <c>NavigationRuntime.ResolveHomeDescriptor()</c>: when no page is tagged
    /// <c>Role=Home</c> and no page carries the <c>"home"</c> tag, a registered
    /// page whose descriptor <c>Name</c> is <c>HomePage</c> or <c>MainPage</c>
    /// must still be reachable via <c>GoHomeAsync()</c>. Without this fallback,
    /// the bootstrap's idle timeout could silently no-op when the consumer
    /// forgot <c>SetHome&lt;T&gt;()</c> / <c>.AsHome()</c>.
    /// </summary>
    public class HomeResolverAutoFallbackTests
    {
        [Fact]
        public async Task GoHomeAsync_finds_page_named_HomePage_with_no_explicit_home_metadata()
        {
            var fixture = BuildRuntimeWithRenamedPage<StubHome>("HomePage");

            await fixture.Runtime.GoHomeAsync();

            Assert.NotNull(fixture.Runtime.Current);
            Assert.IsType<StubHome>(fixture.Runtime.Current);
        }

        [Fact]
        public async Task GoHomeAsync_finds_page_named_MainPage_with_no_explicit_home_metadata()
        {
            var fixture = BuildRuntimeWithRenamedPage<StubA>("MainPage");

            await fixture.Runtime.GoHomeAsync();

            Assert.NotNull(fixture.Runtime.Current);
            Assert.IsType<StubA>(fixture.Runtime.Current);
        }

        /// <summary>
        /// Mirrors the wiring in <see cref="RuntimeTestFixture.Build{THome}"/>
        /// but registers the page WITHOUT <c>Role=Home</c> — only the descriptor
        /// <c>Name</c> is overridden, so the home resolver has nothing but the
        /// name-convention tier to match on.
        /// </summary>
        private static RuntimeWiring BuildRuntimeWithRenamedPage<TPage>(string descriptorName)
            where TPage : StubPageView, new()
        {
            var host = new FakePageHost();

            var registry = PageRegistry.Create(builder =>
            {
                builder.RegisterType(typeof(TPage), d => d.Name = descriptorName);
            });

            var factory = PageFactory.AutoWireFromRegistry(new[] { typeof(TPage) });

            var services = new ServiceLocator();
            services.Register<IEventDispatcherAdapter>(new SyncEventDispatcherAdapter());
            services.Register<PageFactory>(factory);
            services.Lock();

            var platform = new FakePlatformAdapter();
            var ctx = new NavigationContext(host, services, registry, platform);
            var runtime = new NavigationRuntime(ctx);

            return new RuntimeWiring(runtime);
        }

        private sealed class RuntimeWiring
        {
            public NavigationRuntime Runtime { get; }
            public RuntimeWiring(NavigationRuntime runtime) => Runtime = runtime;
        }
    }
}
