using System;
using System.Threading;
using System.Threading.Tasks;
using NekoLib.Pipes.Tests.Unit.Fakes;
using Xunit;

namespace NekoLib.Pipes.Tests.Unit
{
    /// <summary>
    /// Resilience tests for <see cref="PipeEventClient"/> (audit M4): it used to
    /// die silently on a dropped/restarted hub or a throwing handler. It now
    /// auto-reconnects and isolates handler faults.
    /// </summary>
    public class PipeEventClientTests
    {
        private static PipeServer NewEventServer(string name)
            => new PipeServer(new PipeServerOptions
            {
                PipeName = name,
                EnableEvents = true,
                MaxEventSubscribers = 4
            });

        private static async Task PublishUntil(PipeServer server, Func<bool> condition, int attempts = 20)
        {
            for (int i = 0; i < attempts && !condition(); i++)
            {
                await server.Events.PublishAsync("e", new { hello = "world", i });
                await Task.Delay(150);
            }
        }

        [Fact]
        public async Task Subscriber_AutoReconnects_AfterServerRestart()
        {
            var name = PipeTestUtil.UniqueName();
            int received = 0;

            using (var client = new PipeEventClient(name) { ReconnectDelay = TimeSpan.FromMilliseconds(200) })
            {
                client.OnEvent += _ => Interlocked.Increment(ref received);

                // --- Server #1 ---
                using (var server1 = NewEventServer(name))
                {
                    server1.Start();
                    client.Start();

                    Assert.True(
                        PipeTestUtil.WaitUntil(() => server1.Events.SubscriberCount >= 1, 5000),
                        "client never connected to server #1");

                    await PublishUntil(server1, () => received >= 1);
                    Assert.True(received >= 1, "no event from server #1");
                }
                // server #1 disposed — the client should now reconnect.

                // --- Server #2 on the same pipe name ---
                using (var server2 = NewEventServer(name))
                {
                    server2.Start();

                    Assert.True(
                        PipeTestUtil.WaitUntil(() => server2.Events.SubscriberCount >= 1, 8000),
                        "client did not auto-reconnect to server #2");

                    int before = received;
                    await PublishUntil(server2, () => received > before);
                    Assert.True(received > before, "no event received after reconnect");
                }
            }
        }

        [Fact]
        public async Task ThrowingHandler_DoesNotStopOtherHandlers()
        {
            var name = PipeTestUtil.UniqueName();
            var secondHandlerRan = new ManualResetEventSlim(false);

            using (var server = NewEventServer(name))
            {
                server.Start();

                using (var client = new PipeEventClient(name))
                {
                    client.OnEvent += _ => throw new InvalidOperationException("bad handler");
                    client.OnEvent += _ => secondHandlerRan.Set();
                    client.Start();

                    Assert.True(
                        PipeTestUtil.WaitUntil(() => server.Events.SubscriberCount >= 1, 5000),
                        "client never connected");

                    for (int i = 0; i < 10 && !secondHandlerRan.IsSet; i++)
                    {
                        await server.Events.PublishAsync("e", new { i });
                        secondHandlerRan.Wait(300);
                    }

                    Assert.True(secondHandlerRan.IsSet,
                        "the second handler did not run despite the first throwing");
                }
            }
        }
    }
}
