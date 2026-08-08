using System;
using System.Diagnostics;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using NekoLib.Pipes.Tests.Unit.Fakes;
using Xunit;

namespace NekoLib.Pipes.Tests.Unit
{
    public sealed class PipeShutdownTests
    {
        [Fact]
        public async Task Dispose_CooperativeHandler_DrainsBeforeReturning()
        {
            var name = PipeTestUtil.UniqueName();
            var entered = NewSignal();

            using (var server = NewServer(name))
            using (var client = NewClient(name))
            {
                server.Map("wait", async (request, cancellationToken) =>
                {
                    entered.TrySetResult(true);
                    await Task.Delay(Timeout.Infinite, cancellationToken);
                    return new PipeMessage { Ok = true };
                });
                server.Start();

                var requestTask = client.SendAsync("wait");
                await AwaitWithin(entered.Task, 5000);

                server.Dispose();
                await AwaitWithin(server.ShutdownCompletion, 3000);

                Assert.Equal(0, server.ActiveClientOperationCount);
                Assert.False((await requestTask).Ok);
            }
        }

        [Fact]
        public async Task Dispose_HandlerIgnoringCancellation_ReturnsBoundedAndDefersResourceCleanup()
        {
            var name = PipeTestUtil.UniqueName();
            var entered = NewSignal();
            var release = NewSignal();

            using (var server = NewServer(name))
            using (var client = NewClient(name))
            {
                server.Map("wait", async (request, cancellationToken) =>
                {
                    entered.TrySetResult(true);
                    await release.Task;
                    return new PipeMessage { Ok = true };
                });
                server.Start();

                var requestTask = client.SendAsync("wait");
                await AwaitWithin(entered.Task, 5000);

                var sw = Stopwatch.StartNew();
                server.Dispose();
                sw.Stop();

                Assert.True(sw.Elapsed < TimeSpan.FromSeconds(3));
                Assert.Equal(1, server.ActiveClientOperationCount);
                Assert.False(server.ShutdownCompletion.IsCompleted);

                release.TrySetResult(true);
                await AwaitWithin(server.ShutdownCompletion, 5000);

                Assert.Equal(0, server.ActiveClientOperationCount);
                Assert.False((await requestTask).Ok);
            }
        }

        [Fact]
        public async Task Dispose_PendingAccept_ClosesAndDrainsOperation()
        {
            var server = NewServer(PipeTestUtil.UniqueName());
            server.Start();
            Assert.True(
                PipeTestUtil.WaitUntil(() => server.ActiveClientOperationCount > 0, 5000),
                "server did not admit its pending accept");

            var sw = Stopwatch.StartNew();
            server.Dispose();
            await AwaitWithin(server.ShutdownCompletion, 3000);
            sw.Stop();

            Assert.True(sw.Elapsed < TimeSpan.FromSeconds(3));
            Assert.Equal(0, server.ActiveClientOperationCount);
        }

        [Fact]
        public async Task Dispose_EventSubscriberWithPendingWrite_ClosesAndDrainsOperation()
        {
            var name = PipeTestUtil.UniqueName();
            var hub = new PipeEventHub(name, maxSubscribers: 2);
            hub.Start();

            using (var client = new NamedPipeClientStream(
                ".",
                name + ".events",
                PipeDirection.In,
                PipeOptions.Asynchronous))
            {
                client.Connect(3000);
                Assert.True(
                    PipeTestUtil.WaitUntil(
                        () => hub.SubscriberCount == 1 && hub.ActiveSubscriberOperationCount > 0,
                        5000),
                    "event subscriber did not connect");

                await hub.PublishAsync(
                    "large",
                    new { text = new string('x', 512 * 1024) });

                var sw = Stopwatch.StartNew();
                hub.Dispose();
                await AwaitWithin(hub.ShutdownCompletion, 3000);
                sw.Stop();

                Assert.True(sw.Elapsed < TimeSpan.FromSeconds(3));
                Assert.Equal(0, hub.SubscriberCount);
                Assert.Equal(0, hub.ActiveSubscriberOperationCount);
            }
        }

        private static PipeServer NewServer(string name)
            => new PipeServer(new PipeServerOptions
            {
                PipeName = name,
                EnableEvents = false,
                ClientIdleTimeout = TimeSpan.FromSeconds(10)
            });

        private static PipeClient NewClient(string name)
            => new PipeClient(new PipeClientOptions
            {
                PipeName = name,
                ConnectTimeout = TimeSpan.FromSeconds(3),
                RequestTimeout = TimeSpan.FromSeconds(10)
            });

        private static TaskCompletionSource<bool> NewSignal()
            => new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        private static async Task AwaitWithin(Task task, int timeoutMs)
        {
            var winner = await Task.WhenAny(task, Task.Delay(timeoutMs));
            Assert.Same(task, winner);
            await task;
        }
    }
}
