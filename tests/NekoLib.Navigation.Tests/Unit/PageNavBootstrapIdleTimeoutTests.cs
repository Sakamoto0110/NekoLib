using System;
using NekoLib.Navigation.Bootstrap;
using NekoLib.Navigation.Tests.Unit.Fakes;
using Xunit;

namespace NekoLib.Navigation.Tests.Unit
{
    /// <summary>
    /// Pins the placement rule for the per-idle-page timeout: <c>[PageTimeout]</c> /
    /// the DSL <c>.IdleTimeout(seconds)</c> may only sit on the idle page. A timeout
    /// on any other page is a configuration mistake and must fail loud at bootstrap,
    /// because only the idle descriptor's value is read when wiring the timer.
    /// </summary>
    public class PageNavBootstrapIdleTimeoutTests
    {
        [Fact]
        public void IdleTimeout_on_non_idle_page_throws_on_start()
        {
            var bootstrap = PageNavBootstrap.Use<FakePlatformAdapter>(new object())
                .RegisterPagesFromAssembly(typeof(StubIdle).Assembly)
                .SetIdle<StubIdle>()
                .ConfigurePages(cfg => cfg.Page<StubA>().IdleTimeout(30));

            var ex = Assert.Throws<InvalidOperationException>(() => bootstrap.Start());
            Assert.Contains("is not the idle page", ex.Message);
        }

        [Fact]
        public void IdleTimeout_on_idle_page_passes_placement_validation()
        {
            var bootstrap = PageNavBootstrap.Use<FakePlatformAdapter>(new object())
                .RegisterPagesFromAssembly(typeof(StubIdle).Assembly)
                .SetIdle<StubIdle>()
                .ConfigurePages(cfg => cfg.Page<StubIdle>().IdleTimeout(30));

            // Placement is valid (the timeout sits on the idle page), so Start() gets
            // past the placement check and only fails later when FakePlatformAdapter
            // throws on CreateHost — i.e. NOT with our placement error.
            var ex = Record.Exception(() => bootstrap.Start());

            Assert.NotNull(ex);
            Assert.DoesNotContain("is not the idle page", ex.Message);
        }

        [Fact]
        public void IdleTimeout_DSL_rejects_non_positive_seconds()
        {
            // Exercise the DSL guard directly: the ConfigurePages(...) overload defers
            // execution to Start(), so build the configurator here to assert eagerly.
            var configurator = new PageBuilderConfigurator(new PageMetadataBuilder());

            Assert.Throws<ArgumentOutOfRangeException>(
                () => configurator.Page<StubIdle>().IdleTimeout(0));
        }
    }
}
