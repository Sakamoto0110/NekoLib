using System;
using NekoLib.Navigation.Bootstrap;
using NekoLib.Navigation.Infrastructure;
using NekoLib.Navigation.Tests.Unit.Fakes;
using Xunit;

namespace NekoLib.Navigation.Tests.Unit
{
    /// <summary>
    /// Pins the conflict-detection contract on <see cref="PageNavBootstrap.SetHome{TPage}"/>.
    /// SetHome is a top-level convenience equivalent to <c>ConfigurePages(cfg =&gt;
    /// cfg.Page&lt;T&gt;().AsHome())</c>; the framework must fail loudly when home is
    /// declared more than once or split across two different pages, so an ambiguous
    /// home never reaches the runtime's home-resolver.
    /// </summary>
    public class PageNavBootstrapSetHomeTests : IDisposable
    {
        // PlatformRegistry is a process-wide singleton — reset around every test
        // so PageNavBootstrap.Use<T>() inside the test body doesn't trip the
        // "already registered" guard left by a previous test in the same fixture.
        public PageNavBootstrapSetHomeTests() => PlatformRegistry.Reset();
        public void Dispose() => PlatformRegistry.Reset();

        [Fact]
        public void SetHome_called_twice_throws()
        {
            var bootstrap = PageNavBootstrap.Use<FakePlatformAdapter>(new object());
            bootstrap.SetHome<StubHome>();

            var ex = Assert.Throws<InvalidOperationException>(() => bootstrap.SetHome<StubA>());
            Assert.Contains("SetHome was already called", ex.Message);
        }

        [Fact]
        public void SetHome_and_ConfigurePages_AsHome_on_different_pages_throws_on_start()
        {
            var bootstrap = PageNavBootstrap.Use<FakePlatformAdapter>(new object())
                .RegisterPagesFromAssembly(typeof(StubHome).Assembly)
                .SetHome<StubHome>()
                .ConfigurePages(cfg => cfg.Page<StubA>().AsHome());

            var ex = Assert.Throws<InvalidOperationException>(() => bootstrap.Start());
            Assert.Contains("Multiple home pages", ex.Message);
        }

        [Fact]
        public void SetHome_and_ConfigurePages_AsHome_on_same_page_does_not_conflict()
        {
            // Redundant but unambiguous — both target StubHome, so only one
            // descriptor ends up with Role=Home and the collision check is silent.
            // Start() will still fail later because FakePlatformAdapter throws on
            // CreateHost — we just need to confirm the throw is NOT the "multiple
            // home pages" one our new check raises.
            var bootstrap = PageNavBootstrap.Use<FakePlatformAdapter>(new object())
                .RegisterPagesFromAssembly(typeof(StubHome).Assembly)
                .SetHome<StubHome>()
                .ConfigurePages(cfg => cfg.Page<StubHome>().AsHome());

            var ex = Record.Exception(() => bootstrap.Start());

            Assert.NotNull(ex);
            Assert.DoesNotContain("Multiple home pages", ex.Message);
        }
    }
}
