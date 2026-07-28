using System;
using NekoLib.Navigation.Diagnostics;
using NekoLib.Navigation.Metadata;
using NekoLib.Navigation.Tests.Unit.Fakes;
using Xunit;

namespace NekoLib.Navigation.Tests.Unit
{
    public class NavigationEventHubTests
    {
        [Fact]
        public void Publish_WhenFirstSubscriberThrows_NotifiesLaterSubscriber()
        {
            var hub = new NavigationEventHub();
            int notified = 0;

            hub.NavigationLogged += _ => throw new InvalidOperationException();
            hub.NavigationLogged += _ => notified++;

            hub.Publish(new PageLogEntry(
                null,
                null,
                typeof(StubA),
                nameof(StubA),
                NavigationArgs.Empty,
                success: true,
                navigationBehavior: PagePresentationMode.Replace,
                navigationLoadMode: NavigationLoadMode.ShowImmediately,
                reusePolicy: PageReusePolicy.Transient));

            Assert.Equal(1, notified);
        }

        [Fact]
        public void PublishGuardDenied_WhenFirstSubscriberThrows_NotifiesLaterSubscriber()
        {
            var hub = new NavigationEventHub();
            int notified = 0;

            hub.GuardDenied += _ => throw new InvalidOperationException();
            hub.GuardDenied += _ => notified++;

            hub.Publish(new GuardDeniedEvent(
                null,
                typeof(StubA),
                null,
                "denied"));

            Assert.Equal(1, notified);
        }

        [Fact]
        public void PublishStarted_WhenFirstSubscriberThrows_NotifiesLaterSubscriber()
        {
            var hub = new NavigationEventHub();
            int notified = 0;

            hub.NavigationStarted += _ => throw new InvalidOperationException();
            hub.NavigationStarted += _ => notified++;

            hub.PublishStarted(null, typeof(StubA), NavigationArgs.Empty);

            Assert.Equal(1, notified);
        }
    }
}
