using System;
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
                        WaitHandle.WaitAll(new[] { gotA.WaitHandle, gotB.WaitHandle }, 800);
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
    }
}
