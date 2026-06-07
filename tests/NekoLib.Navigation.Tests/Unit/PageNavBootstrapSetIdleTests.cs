using System;
using NekoLib.Navigation.Bootstrap;
using NekoLib.Navigation.Infrastructure;
using NekoLib.Navigation.Tests.Unit.Fakes;
using Xunit;

namespace NekoLib.Navigation.Tests.Unit
{
    /// <summary>
    /// Pins the conflict-detection contract on <see cref="PageNavBootstrap.SetIdle{TPage}"/>.
    /// SetIdle is a top-level convenience equivalent to <c>ConfigurePages(cfg =&gt;
    /// cfg.Page&lt;T&gt;().AsIdle())</c>; the framework must fail loudly when the idle
    /// page is declared more than once or split across two different pages, so an
    /// ambiguous idle page never reaches the runtime's idle-resolver.
    /// </summary>
    public class PageNavBootstrapSetIdleTests : IDisposable
    {
        // PlatformRegistry is a process-wide singleton — reset around every test
        // so PageNavBootstrap.Use<T>() inside the test body doesn't trip the
        // "already registered" guard left by a previous test in the same fixture.
        public PageNavBootstrapSetIdleTests() => PlatformRegistry.Reset();
        public void Dispose() => PlatformRegistry.Reset();

        [Fact]
        public void SetIdle_called_twice_throws()
        {
            var bootstrap = PageNavBootstrap.Use<FakePlatformAdapter>(new object());
            bootstrap.SetIdle<StubIdle>();

            var ex = Assert.Throws<InvalidOperationException>(() => bootstrap.SetIdle<StubA>());
            Assert.Contains("SetIdle was already called", ex.Message);
        }

        [Fact]
        public void SetIdle_and_ConfigurePages_AsIdle_on_different_pages_throws_on_start()
        {
            var bootstrap = PageNavBootstrap.Use<FakePlatformAdapter>(new object())
                .RegisterPagesFromAssembly(typeof(StubIdle).Assembly)
                .SetIdle<StubIdle>()
                .ConfigurePages(cfg => cfg.Page<StubA>().AsIdle());

            var ex = Assert.Throws<InvalidOperationException>(() => bootstrap.Start());
            Assert.Contains("Multiple idle pages", ex.Message);
        }

        [Fact]
        public void SetIdle_and_ConfigurePages_AsIdle_on_same_page_does_not_conflict()
        {
            // Redundant but unambiguous — both target StubIdle, so only one
            // descriptor ends up with Role=Idle and the collision check is silent.
            // Start() will still fail later because FakePlatformAdapter throws on
            // CreateHost — we just need to confirm the throw is NOT the "multiple
            // idle pages" one our new check raises.
            var bootstrap = PageNavBootstrap.Use<FakePlatformAdapter>(new object())
                .RegisterPagesFromAssembly(typeof(StubIdle).Assembly)
                .SetIdle<StubIdle>()
                .ConfigurePages(cfg => cfg.Page<StubIdle>().AsIdle());

            var ex = Record.Exception(() => bootstrap.Start());

            Assert.NotNull(ex);
            Assert.DoesNotContain("Multiple idle pages", ex.Message);
        }
    }
}
