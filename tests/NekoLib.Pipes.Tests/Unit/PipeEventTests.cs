using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using NekoLib.Pipes.Tests.Unit.Fakes;
using Xunit;

namespace NekoLib.Pipes.Tests.Unit
{
    /// <summary>
    /// In-process tests for the pub/sub path: a <see cref="PipeServer"/> with
    /// events enabled (owning a <see cref="PipeEventHub"/>) and a
    /// <see cref="PipeEventClient"/> subscriber. Confirms a published event is
    /// delivered, carrying its name and payload.
    /// </summary>
    public class PipeEventTests
    {
        [Fact]
        public async Task PublishedEvent_IsDeliveredToSubscriber()
        {
            var name = PipeTestUtil.UniqueName();

            using (var server = new PipeServer(new PipeServerOptions
            {
                PipeName = name,
                EnableEvents = true,
                MaxEventSubscribers = 4
            }))
            {
                server.Start();

                PipeMessage received = null;
                var gotEvent = new ManualResetEventSlim(false);

                using (var sub = new PipeEventClient(name))
                {
                    sub.OnEvent += msg =>
                    {
                        received = msg;
                        gotEvent.Set();
                    };
                    sub.Start();

                    // Publish only once the hub actually has the subscriber connected,
                    // otherwise the event has no one to go to.
                    Assert.True(
                        PipeTestUtil.WaitUntil(() => server.Events.SubscriberCount >= 1, 5000),
                        "subscriber never connected to the event hub");

                    // Publish a few times: on net481 the subscriber's blocking read may
                    // not be parked on the very first frame yet; redundant publishes make
                    // delivery deterministic without weakening the assertion.
                    for (int i = 0; i < 3 && !gotEvent.IsSet; i++)
                    {
                        await server.Events.PublishAsync("telemetry", new { hello = "world" });
                        gotEvent.Wait(1500);
                    }

                    Assert.True(gotEvent.IsSet, "event was not delivered in time");
                }

                Assert.NotNull(received);
                Assert.Equal("evt", received.Type);
                Assert.Equal("telemetry", received.Name);
                Assert.Contains("world", PipeTestUtil.DataText(received));
            }
        }

        [Fact]
        public async Task PublishedEvent_FansOutToAllSubscribers()
        {
            // Locks parallel-delivery correctness (audit M3): both subscribers
            // receive the same published event.
            var name = PipeTestUtil.UniqueName();

            using (var server = new PipeServer(new PipeServerOptions
            {
                PipeName = name,
                EnableEvents = true,
                MaxEventSubscribers = 4
            }))
            {
                server.Start();

                var gotA = new ManualResetEventSlim(false);
                var gotB = new ManualResetEventSlim(false);

                using (var subA = new PipeEventClient(name))
                using (var subB = new PipeEventClient(name))
                {
                    subA.OnEvent += _ => gotA.Set();
                    subB.OnEvent += _ => gotB.Set();
                    subA.Start();
                    subB.Start();

                    Assert.True(
                        PipeTestUtil.WaitUntil(() => server.Events.SubscriberCount >= 2, 5000),
                        "both subscribers never connected");

                    for (int i = 0; i < 5 && !(gotA.IsSet && gotB.IsSet); i++)
                    {
                        await server.Events.PublishAsync("e", new { i });
                        gotA.Wait(800);
                        gotB.Wait(800);
                    }

                    Assert.True(gotA.IsSet, "subscriber A did not receive the event");
                    Assert.True(gotB.IsSet, "subscriber B did not receive the event");
                }
            }
        }

        [Fact]
        public async Task Publish_WithNoSubscribers_DoesNotThrow()
        {
            var name = PipeTestUtil.UniqueName();

            using (var server = new PipeServer(new PipeServerOptions
            {
                PipeName = name,
                EnableEvents = true
            }))
            {
                server.Start();

                var ex = await Record.ExceptionAsync(
                    () => server.Events.PublishAsync("noone_listening", new { x = 1 }));

                Assert.Null(ex);
            }
        }

        [Fact]
        public async Task OversizedPublish_IsRejectedWithoutDisconnectingSubscriberOrCountingPublication()
        {
            var name = PipeTestUtil.UniqueName();
            var metrics = new SimplePipeMetrics();

            using (var hub = new PipeEventHub(
                name,
                2,
                PipeAccessPolicy.PlatformDefault,
                subscriberQueueCapacity: 4,
                PipeEventQueueOverflowPolicy.DropNewest,
                metrics))
            using (var received = new ManualResetEventSlim(false))
            using (var client = new PipeEventClient(name))
            {
                client.OnEvent += message =>
                {
                    if (message.Name == "after")
                        received.Set();
                };
                hub.Start();
                client.Start();

                Assert.True(
                    PipeTestUtil.WaitUntil(() => hub.SubscriberCount == 1, 5000),
                    "subscriber never connected");

                await Assert.ThrowsAnyAsync<InvalidOperationException>(() =>
                    hub.PublishAsync("oversized", new { text = new string('x', 1_200_000) }));

                Assert.Equal(1, hub.SubscriberCount);
                Assert.Equal(0L, metrics.Snapshot().Events.Published);

                await hub.PublishAsync("after", new { value = 1 });

                Assert.True(received.Wait(5000), "normal event after rejection was not delivered");
                Assert.True(
                    PipeTestUtil.WaitUntil(
                        () => metrics.Snapshot().Events.Published == 1,
                        5000),
                    "normal publication metrics did not complete");
                Assert.Equal(1L, metrics.Snapshot().Events.Delivered);
                Assert.Equal(0L, metrics.Snapshot().Events.Failed);
                Assert.Equal(1, hub.SubscriberCount);
            }
        }

        [Theory]
        [InlineData(PipeAccessPolicy.PlatformDefault)]
        [InlineData(PipeAccessPolicy.CurrentUserOnly)]
        public void DisconnectedSubscribers_WithoutPublishedEvents_ReleaseCapacity(
            PipeAccessPolicy accessPolicy)
        {
            var name = PipeTestUtil.UniqueName();
            const int maxSubscribers = 4;

            using (var hub = new PipeEventHub(
                name,
                maxSubscribers,
                accessPolicy))
            {
                hub.Start();

                for (var i = 0; i < maxSubscribers + 1; i++)
                {
                    using (var connected = new ManualResetEventSlim(false))
                    using (var client = new PipeEventClient(name))
                    {
                        client.OnConnected += () => connected.Set();
                        client.Start();

                        Assert.True(
                            connected.Wait(5000),
                            "event subscriber " + (i + 1) + " did not connect");
                        Assert.True(
                            PipeTestUtil.WaitUntil(() => hub.SubscriberCount == 1, 5000),
                            "event subscriber " + (i + 1) + " was not registered");
                    }

                    Assert.True(
                        PipeTestUtil.WaitUntil(() => hub.SubscriberCount == 0, 5000),
                        "event subscriber " + (i + 1) + " was not removed");
                }
            }
        }

        [Fact]
        public async Task DuplexSubscriberInput_IsDiscardedWithoutCorruptingEventDelivery()
        {
            var name = PipeTestUtil.UniqueName();

            using (var hub = new PipeEventHub(name, maxSubscribers: 2))
            {
                hub.Start();

                using (var client = new NamedPipeClientStream(
                    ".",
                    name + ".events",
                    PipeDirection.InOut,
                    PipeOptions.Asynchronous))
                {
                    client.Connect(3000);
                    Assert.True(
                        PipeTestUtil.WaitUntil(() => hub.SubscriberCount == 1, 5000),
                        "duplex event subscriber did not connect");

                    var ignoredInput = new byte[] { 0x01, 0x02, 0x03, 0x04 };
                    await client.WriteAsync(ignoredInput, 0, ignoredInput.Length);
                    await client.FlushAsync();

                    await hub.PublishAsync("duplex.delivery", new { value = 42 });

                    using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
                    {
                        var received = await PipeFraming.TryReadAsync(client, timeout.Token);

                        Assert.NotNull(received);
                        Assert.Equal("evt", received.Type);
                        Assert.Equal("duplex.delivery", received.Name);
                        Assert.True(received.Ok);
                    }
                }

                Assert.True(
                    PipeTestUtil.WaitUntil(() => hub.SubscriberCount == 0, 5000),
                    "duplex event subscriber was not removed");
            }
        }

        [Fact]
        public async Task ConcurrentPublishers_DeliverIntactFramesThroughSingleWriter()
        {
            var name = PipeTestUtil.UniqueName();
            const int count = 100;

            using (var hub = new PipeEventHub(
                name,
                maxSubscribers: 2,
                PipeAccessPolicy.PlatformDefault,
                subscriberQueueCapacity: 256,
                PipeEventQueueOverflowPolicy.DropNewest))
            {
                hub.Start();

                var received = new ConcurrentDictionary<string, byte>();
                using (var client = new PipeEventClient(name))
                {
                    client.OnEvent += message => received.TryAdd(message.Name, 0);
                    client.Start();

                    Assert.True(
                        PipeTestUtil.WaitUntil(() => hub.SubscriberCount == 1, 5000),
                        "subscriber never connected");

                    var publishes = new Task[count];
                    for (var i = 0; i < count; i++)
                        publishes[i] = hub.PublishAsync("event." + i, new { index = i });

                    await Task.WhenAll(publishes);

                    Assert.True(
                        PipeTestUtil.WaitUntil(() => received.Count == count, 10000),
                        "not all concurrently published frames were delivered");
                    for (var i = 0; i < count; i++)
                        Assert.True(received.ContainsKey("event." + i));
                }
            }
        }

        [Fact]
        public async Task SlowSubscriber_DoesNotBlockPublisherOrHealthySubscriber_AndDropsAreObservable()
        {
            var name = PipeTestUtil.UniqueName();
            var metrics = new SimplePipeMetrics();

            using (var hub = new PipeEventHub(
                name,
                maxSubscribers: 4,
                PipeAccessPolicy.PlatformDefault,
                subscriberQueueCapacity: 1,
                PipeEventQueueOverflowPolicy.DropNewest,
                metrics))
            {
                hub.Start();

                using (var slow = new NamedPipeClientStream(
                    ".",
                    name + ".events",
                    PipeDirection.In,
                    PipeOptions.Asynchronous))
                {
                    slow.Connect(3000);

                    var healthyReceived = new ManualResetEventSlim(false);
                    using (var healthy = new PipeEventClient(name))
                    {
                        healthy.OnEvent += message => healthyReceived.Set();
                        healthy.Start();

                        Assert.True(
                            PipeTestUtil.WaitUntil(() => hub.SubscriberCount == 2, 5000),
                            "both subscribers never connected");

                        var largePayload = new { text = new string('x', 512 * 1024) };
                        var sw = Stopwatch.StartNew();
                        for (var i = 0; i < 32; i++)
                            await hub.PublishAsync("bulk." + i, largePayload);
                        sw.Stop();

                        Assert.True(
                            sw.Elapsed < TimeSpan.FromSeconds(2),
                            "publishing waited for slow subscriber I/O");
                        Assert.True(
                            PipeTestUtil.WaitUntil(
                                () => metrics.Snapshot().Events.Failed > 0,
                                5000),
                            "queue overflow was not observable through metrics");
                        Assert.True(
                            healthyReceived.Wait(5000),
                            "healthy subscriber received no event while another subscriber was slow");
                        Assert.Equal(2, hub.SubscriberCount);
                    }
                }
            }
        }

        [Fact]
        public async Task Publish_WithCancelledToken_DoesNotQueueEvent()
        {
            var name = PipeTestUtil.UniqueName();
            var metrics = new SimplePipeMetrics();

            using (var hub = new PipeEventHub(
                name,
                maxSubscribers: 2,
                PipeAccessPolicy.PlatformDefault,
                subscriberQueueCapacity: 4,
                PipeEventQueueOverflowPolicy.DropNewest,
                metrics))
            {
                hub.Start();

                var received = new ManualResetEventSlim(false);
                using (var client = new PipeEventClient(name))
                using (var cancellation = new CancellationTokenSource())
                {
                    client.OnEvent += message => received.Set();
                    client.Start();
                    Assert.True(
                        PipeTestUtil.WaitUntil(() => hub.SubscriberCount == 1, 5000),
                        "subscriber never connected");

                    cancellation.Cancel();
                    await hub.PublishAsync("cancelled", null, cancellation.Token);

                    Assert.False(received.Wait(500));
                    Assert.True(
                        PipeTestUtil.WaitUntil(
                            () => metrics.Snapshot().Events.Failed == 1,
                            2000));
                }
            }
        }

        [Fact]
        public async Task QueueOverflow_DisconnectPolicyRemovesSlowSubscriber()
        {
            var name = PipeTestUtil.UniqueName();

            using (var hub = new PipeEventHub(
                name,
                maxSubscribers: 2,
                PipeAccessPolicy.PlatformDefault,
                subscriberQueueCapacity: 1,
                PipeEventQueueOverflowPolicy.DisconnectSubscriber))
            {
                hub.Start();

                using (var slow = new NamedPipeClientStream(
                    ".",
                    name + ".events",
                    PipeDirection.In,
                    PipeOptions.Asynchronous))
                {
                    slow.Connect(3000);
                    Assert.True(
                        PipeTestUtil.WaitUntil(() => hub.SubscriberCount == 1, 5000),
                        "slow subscriber never connected");

                    var largePayload = new { text = new string('x', 512 * 1024) };
                    for (var i = 0; i < 32 && hub.SubscriberCount != 0; i++)
                        await hub.PublishAsync("bulk." + i, largePayload);

                    Assert.True(
                        PipeTestUtil.WaitUntil(() => hub.SubscriberCount == 0, 5000),
                        "queue overflow did not disconnect the slow subscriber");
                }
            }
        }
    }
}
