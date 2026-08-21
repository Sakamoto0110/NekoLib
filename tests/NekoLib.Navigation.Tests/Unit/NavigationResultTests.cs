using System;
using System.Threading.Tasks;
using NekoLib.Navigation.Metadata;
using NekoLib.Navigation.Telemetry;
using NekoLib.Navigation.Tests.Unit.Fakes;
using Xunit;

namespace NekoLib.Navigation.Tests.Unit
{
    public sealed class NavigationResultTests
    {
        [Fact]
        public async Task NavigateAsync_WhenTargetSucceeds_ReturnsFinalPage()
        {
            var fixture = RuntimeTestFixture.Build<StubIdle>(typeof(StubA));

            var result = await fixture.Runtime.NavigateAsync(
                typeof(StubA),
                NavigationArgs.Default());

            Assert.True(result.Succeeded);
            Assert.False(result.WasRedirected);
            Assert.Equal(typeof(StubA), result.RequestedPage);
            Assert.Equal(typeof(StubA), result.FinalPage);
            Assert.Null(result.DenialReason);
        }

        [Fact]
        public async Task NavigateAsync_WhenGuardDenies_ReturnsReason()
        {
            var fixture = RuntimeTestFixture.Build<StubIdle>(
                typeof(StubAuthenticated));

            var result = await fixture.Runtime.NavigateAsync(
                typeof(StubAuthenticated),
                NavigationArgs.Default());

            Assert.False(result.Succeeded);
            Assert.False(result.WasRedirected);
            Assert.Equal(typeof(StubAuthenticated), result.RequestedPage);
            Assert.Null(result.FinalPage);
            Assert.Equal("Authentication required.", result.DenialReason);
        }

        [Fact]
        public async Task NavigateAsync_WhenGuardRedirects_ReturnsRedirectTarget()
        {
            var fixture = RuntimeTestFixture.Build<StubIdle>(
                typeof(StubRoleRedirect));

            var result = await fixture.Runtime.NavigateAsync(
                typeof(StubRoleRedirect),
                NavigationArgs.Default());

            Assert.True(result.Succeeded);
            Assert.True(result.WasRedirected);
            Assert.Equal(typeof(StubRoleRedirect), result.RequestedPage);
            Assert.Equal(typeof(StubIdle), result.FinalPage);
            Assert.Null(result.DenialReason);
        }
    }

    [Collection("NavigationServiceFacade")]
    public sealed class NavigationResultFacadeTests
    {
        [Fact]
        public async Task SwitchPage_WithRequest_PreservesPayloadAndTiming()
        {
            await NavigationService.Shutdown();
            var fixture = RuntimeTestFixture.Build<StubIdle>(typeof(StubA));
            NavigationService.UseContext(fixture.Context);

            try
            {
                var timing = new NavigationTimingContext();
                var request = NavigationArgs.Default("payload").WithTiming(timing);

                var result = await NavigationService.SwitchPage<StubA>(request);

                var page = Assert.IsType<StubA>(NavigationService.Current);
                Assert.True(result.Succeeded);
                Assert.Equal("payload", page.LastNavArgs.Payload);
                Assert.Same(timing, page.LastNavArgs.Timing);
            }
            finally
            {
                await NavigationService.Shutdown();
            }
        }
    }
}
