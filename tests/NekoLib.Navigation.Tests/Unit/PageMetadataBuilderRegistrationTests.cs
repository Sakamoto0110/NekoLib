using NekoLib.Navigation.Bootstrap;
using NekoLib.Navigation.Runtime.Registry;
using NekoLib.Navigation.Tests.Unit.Fakes;
using Xunit;

namespace NekoLib.Navigation.Tests.Unit
{
    public sealed class PageMetadataBuilderRegistrationTests
    {
        [Fact]
        public void Register_GenericPageWithoutAssemblyScan_RegistersDescriptor()
        {
            var registry = PageRegistry.Create(
                builder => builder.Register<StubA>(
                    descriptor => descriptor.Name = "configured-a"));

            var descriptor = registry.GetDescriptor(typeof(StubA));

            Assert.Equal("configured-a", descriptor.Name);
        }
    }
}
