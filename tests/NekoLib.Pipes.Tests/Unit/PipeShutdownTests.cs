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
            {
                var client = NewClient(name);
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
            {
                var client = NewClient(name);
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
        public async Task ShutdownAsync_EventSubscriberWithPendingWrite_ClosesAndDrainsOperation()
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
                await AwaitWithin(hub.ShutdownAsync(), 3000);
                sw.Stop();

                Assert.True(sw.Elapsed < TimeSpan.FromSeconds(3));
                Assert.Equal(0, hub.SubscriberCount);
                Assert.Equal(0, hub.ActiveSubscriberOperationCount);
            }
        }

        [Fact]
        public async Task ShutdownAsync_HandlerIgnoringCancellation_WaitsForAdmittedWork()
        {
            var name = PipeTestUtil.UniqueName();
            var entered = NewSignal();
            var release = NewSignal();

            using (var server = NewServer(name))
            {
                var client = NewClient(name);
                server.Map("wait", async (request, cancellationToken) =>
                {
                    entered.TrySetResult(true);
                    await release.Task;
                    return new PipeMessage { Ok = true };
                });
                server.Start();

                var requestTask = client.SendAsync("wait");
                await AwaitWithin(entered.Task, 5000);

                var shutdownTask = server.ShutdownAsync();
                Assert.False(shutdownTask.IsCompleted);
                Assert.True(server.ActiveClientOperationCount >= 1);

                release.TrySetResult(true);
                await AwaitWithin(shutdownTask, 5000);

                Assert.Equal(0, server.ActiveClientOperationCount);
                Assert.False((await requestTask).Ok);
            }
        }

        [Fact]
        public async Task Handler_CanInitiateShutdownWithoutAwaitingItsOwnOperation()
        {
            var name = PipeTestUtil.UniqueName();
            Task shutdownTask = null;

            using (var server = NewServer(name))
            {
                var client = NewClient(name);
                server.Map("stop", (request, cancellationToken) =>
                {
                    shutdownTask = server.ShutdownAsync();
                    return Task.FromResult(new PipeMessage { Ok = true });
                });
                server.Start();

                var response = await client.SendAsync("stop");
                await AwaitWithin(shutdownTask, 5000);

                Assert.False(response.Ok);
                Assert.Equal(PipeErrorCodes.ConnectionClosed, response.Error.Code);
                Assert.Equal(0, server.ActiveClientOperationCount);
            }
        }

        [Fact]
        public async Task ShutdownBeforeStart_IsTerminalForEveryStatefulEndpoint()
        {
            var server = NewServer(PipeTestUtil.UniqueName());
            await server.ShutdownAsync();
            Assert.Throws<ObjectDisposedException>(() => server.Start());
            Assert.Throws<ObjectDisposedException>(() => server.Map(
                "late",
                (request, cancellationToken) => Task.FromResult(new PipeMessage())));

            var hub = new PipeEventHub(PipeTestUtil.UniqueName(), 1);
            await hub.ShutdownAsync();
            Assert.Throws<ObjectDisposedException>(() => hub.Start());
            await Assert.ThrowsAsync<ObjectDisposedException>(
                () => hub.PublishAsync("late", null));

            var eventClient = new PipeEventClient(PipeTestUtil.UniqueName());
            await eventClient.ShutdownAsync();
            Assert.Throws<ObjectDisposedException>(() => eventClient.Start());
        }

        [Fact]
        public async Task ConcurrentServerStarts_AdmitExactlyOneStart()
        {
            var server = NewServer(PipeTestUtil.UniqueName());
            using (var gate = new ManualResetEventSlim(false))
            {
                var first = AttemptStart(server, gate);
                var second = AttemptStart(server, gate);
                gate.Set();

                var firstError = await first;
                var secondError = await second;

                Assert.True((firstError == null) ^ (secondError == null));
                Assert.IsType<InvalidOperationException>(firstError ?? secondError);
                await server.ShutdownAsync();
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

        private static Task<Exception> AttemptStart(
            PipeServer server,
            ManualResetEventSlim gate)
        {
            return Task.Run(() =>
            {
                gate.Wait();
                return Record.Exception(() => server.Start());
            });
        }
    }
}
