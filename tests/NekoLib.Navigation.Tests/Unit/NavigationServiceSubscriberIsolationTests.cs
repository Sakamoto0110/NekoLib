using NekoLib.Navigation.Contracts.Pages;
using NekoLib.Navigation.Metadata;
using NekoLib.Navigation.Tests.Unit.Fakes;
using System;
using System.Threading.Tasks;
using Xunit;

namespace NekoLib.Navigation.Tests.Unit
{
    [Collection("NavigationServiceFacade")]
    public class NavigationServiceSubscriberIsolationTests
    {
        [Fact]
        public async Task FacadeEvents_ThrowingSubscriber_DoesNotSuppressLaterSubscribers()
        {
            await NavigationService.Shutdown();
            var fixture = RuntimeTestFixture.Build<StubIdle>(
                typeof(StubA),
                typeof(StubB));
            NavigationService.UseContext(fixture.Context);

            int navigating = 0;
            int navigated = 0;
            int failed = 0;
            int currentChanged = 0;
            int historyChanged = 0;
            int firstAttached = 0;
            int noAttached = 0;
            int noVisible = 0;

            NavigationService.Navigating += ThrowNavigating;
            NavigationService.Navigating += (_, __, ___) => navigating++;
            NavigationService.Navigated += ThrowNavigated;
            NavigationService.Navigated += (_, __, ___) => navigated++;
            NavigationService.NavigationFailed += ThrowFailed;
            NavigationService.NavigationFailed += (_, __, ___) => failed++;
            NavigationService.CurrentChanged += ThrowPage;
            NavigationService.CurrentChanged += _ => currentChanged++;
            NavigationService.HistoryChanged += ThrowAction;
            NavigationService.HistoryChanged += () => historyChanged++;
            NavigationService.OnFirstPageAttached += ThrowPage;
            NavigationService.OnFirstPageAttached += _ => firstAttached++;
            NavigationService.OnNoPageAttached += ThrowAction;
            NavigationService.OnNoPageAttached += () => noAttached++;
            NavigationService.OnNoPageVisible += ThrowAction;
            NavigationService.OnNoPageVisible += () => noVisible++;

            try
            {
                await NavigationService.SwitchPage<StubA>();

                Assert.Equal(1, navigating);
                Assert.Equal(1, navigated);
                Assert.Equal(1, currentChanged);
                Assert.Equal(1, firstAttached);

                await NavigationService.SwitchPage<StubB>();
                Assert.Equal(2, currentChanged);

                // Reset the blank-shell counters so this assertion covers the
                // intentional teardown transition rather than a transient gap
                // between two pages.
                noAttached = 0;
                noVisible = 0;
                await NavigationService.ResetAsync();

                await Assert.ThrowsAsync<InvalidOperationException>(
                    () => NavigationService.SwitchPage(typeof(string)));

                Assert.Equal(2, navigating);
                Assert.Equal(2, navigated);
                Assert.Equal(1, failed);
                Assert.True(historyChanged >= 1);
                Assert.Equal(1, noAttached);
                Assert.Equal(1, noVisible);
            }
            finally
            {
                await NavigationService.Shutdown();
            }
        }

        private static void ThrowNavigating(
            IPageView from,
            Type to,
            NavigationArgs args)
            => throw new InvalidOperationException("subscriber failed");

        private static void ThrowNavigated(
            IPageView from,
            IPageView to,
            NavigationArgs args)
            => throw new InvalidOperationException("subscriber failed");

        private static void ThrowFailed(
            IPageView from,
            Type to,
            Exception error)
            => throw new InvalidOperationException("subscriber failed");

        private static void ThrowPage(IPageView page)
            => throw new InvalidOperationException("subscriber failed");

        private static void ThrowAction()
            => throw new InvalidOperationException("subscriber failed");
    }
}
