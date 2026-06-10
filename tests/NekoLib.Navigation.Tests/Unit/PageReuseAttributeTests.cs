using NekoLib.Navigation.Metadata;
using NekoLib.Navigation.Metadata.Attributes;
using NekoLib.Navigation.Runtime.Registry;
using NekoLib.Navigation.Tests.Unit.Fakes;
using Xunit;

namespace NekoLib.Navigation.Tests.Unit
{
    /// <summary>
    /// Pins the <c>[PageReuse]</c> attribute path: the scanner applies it to the
    /// descriptor, and manual/DSL configuration (the bootstrap fluent API) overrides
    /// it — keeping the "DSL beats attribute" precedence consistent across all page
    /// metadata.
    /// </summary>
    public class PageReuseAttributeTests
    {
        [Fact]
        public void Scanner_applies_PageReuse_attribute()
        {
            var registry = PageRegistry.Create(b => b.RegisterType(typeof(ReuseAttrStub)));

            Assert.Equal(
                PageReusePolicy.StrongSingleton,
                registry.GetDescriptor(typeof(ReuseAttrStub)).ReusePolicy);
        }

        [Fact]
        public void Manual_override_beats_PageReuse_attribute()
        {
            // The manual configurator is exactly what the DSL (.Transient()/...) emits;
            // it runs after attributes, so it wins.
            var registry = PageRegistry.Create(b =>
                b.RegisterType(typeof(ReuseAttrStub), d => d.ReusePolicy = PageReusePolicy.Transient));

            Assert.Equal(
                PageReusePolicy.Transient,
                registry.GetDescriptor(typeof(ReuseAttrStub)).ReusePolicy);
        }

        [PageReuse(PageReusePolicy.StrongSingleton)]
        private sealed class ReuseAttrStub : StubPageView { }
    }
}
