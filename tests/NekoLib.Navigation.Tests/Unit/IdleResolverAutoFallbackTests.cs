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
    /// <c>NavigationRuntime.ResolveIdleDescriptor()</c>: when no page is tagged
    /// <c>Role=Idle</c> and no page carries the <c>"idle"</c> tag, a registered
    /// page whose descriptor <c>Name</c> is <c>IdlePage</c> must still be
    /// reachable via <c>GoIdleAsync()</c>. Without this fallback, the bootstrap's
    /// idle timeout could silently no-op when the consumer forgot
    /// <c>SetIdle&lt;T&gt;()</c> / <c>.AsIdle()</c>.
    /// <para>
    /// "MainPage" is deliberately NOT a fallback alias — a main page is often a
    /// hub of options that is distinct from the idle page, so the runtime must
    /// not silently treat one as the other.
    /// </para>
    /// </summary>
    public class IdleResolverAutoFallbackTests
    {
        [Fact]
        public async Task GoIdleAsync_finds_page_named_IdlePage_with_no_explicit_idle_metadata()
        {
            var fixture = BuildRuntimeWithRenamedPage<StubIdle>("IdlePage");

            await fixture.Runtime.GoIdleAsync();

            Assert.NotNull(fixture.Runtime.Current);
            Assert.IsType<StubIdle>(fixture.Runtime.Current);
        }

        [Fact]
        public async Task GoIdleAsync_does_not_fall_back_to_page_named_MainPage()
        {
            // MainPage is a separate concept (entry / hub of options) and must
            // not satisfy the idle-resolver. With no Role=Idle, no "idle" tag,
            // and only a MainPage-named descriptor in the registry, the resolver
            // must return null and GoIdleAsync must be a no-op.
            var fixture = BuildRuntimeWithRenamedPage<StubA>("MainPage");

            await fixture.Runtime.GoIdleAsync();

            Assert.Null(fixture.Runtime.Current);
        }

        /// <summary>
        /// Mirrors the wiring in <see cref="RuntimeTestFixture.Build{TIdle}"/>
        /// but registers the page WITHOUT <c>Role=Idle</c> — only the descriptor
        /// <c>Name</c> is overridden, so the idle resolver has nothing but the
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
