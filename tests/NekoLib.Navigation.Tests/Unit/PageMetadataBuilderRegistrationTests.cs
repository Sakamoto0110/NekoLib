using NekoLib.Navigation.Bootstrap;
using NekoLib.Navigation.Metadata;
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

        [Fact]
        public void Register_RepeatedConfiguration_ComposesInCallOrder()
        {
            var registry = PageRegistry.Create(builder =>
            {
                builder.Register<StubA>(descriptor => descriptor.Name = "first");
                builder.Register<StubA>(descriptor => descriptor.Role = PageRole.Idle);
                builder.Register<StubA>(descriptor => descriptor.AddTag("configured"));
                builder.Register<StubA>(descriptor => descriptor.Name += "-last");
            });

            var descriptor = registry.GetDescriptor(typeof(StubA));

            Assert.Equal("first-last", descriptor.Name);
            Assert.Equal(PageRole.Idle, descriptor.Role);
            Assert.Equal("configured", Assert.Single(descriptor.Tags));
        }

        [Fact]
        public void Build_CopiesTagCollection()
        {
            var builder = new PageDescriptorBuilder(typeof(StubA));
            builder.AddTag("first");

            var descriptor = builder.Build();
            builder.AddTag("second");

            Assert.Equal("first", Assert.Single(descriptor.Tags));
        }

        [Fact]
        public void Build_BlankName_Throws()
        {
            var builder = new PageDescriptorBuilder(typeof(StubA)) { Name = " " };

            Assert.Throws<System.InvalidOperationException>(() => builder.Build());
        }
    }
}
