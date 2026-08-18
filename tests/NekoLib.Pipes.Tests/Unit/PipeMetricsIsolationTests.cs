using System;
using System.Threading;
using System.Threading.Tasks;
using NekoLib.Pipes.Tests.Unit.Fakes;
using Xunit;

namespace NekoLib.Pipes.Tests.Unit
{
    public sealed class PipeMetricsIsolationTests
    {
        [Fact]
        public async Task ThrowingMetrics_DoNotChangeRpcSuccessOrHandlerFailureResponse()
        {
            var name = PipeTestUtil.UniqueName();
            var metrics = new ThrowingMetrics();

            using (var server = new PipeServer(new PipeServerOptions
            {
                PipeName = name,
                EnableEvents = false,
                Metrics = metrics
            }))
            {
                var client = new PipeClient(new PipeClientOptions
                {
                    PipeName = name,
                    ConnectTimeout = TimeSpan.FromSeconds(3),
                    RequestTimeout = TimeSpan.FromSeconds(3),
                    Metrics = metrics
                });
                server.Map(
                    "ping",
                    (request, cancellationToken) =>
                        Task.FromResult(new PipeMessage { Ok = true }));
                server.Map(
                    "boom",
                    (request, cancellationToken) =>
                        throw new InvalidOperationException("local detail"));
                server.Start();

                var success = await client.SendAsync("ping");
                var failure = await client.SendAsync("boom");

                Assert.True(success.Ok);
                Assert.False(failure.Ok);
                Assert.Equal(PipeErrorCodes.Exception, failure.Error.Code);
                Assert.Equal("The handler failed.", failure.Error.Message);
            }
        }

        [Fact]
        public async Task ThrowingMetrics_DoNotFaultZeroSubscriberOrDeliveredEventPublication()
        {
            var name = PipeTestUtil.UniqueName();
            using (var hub = new PipeEventHub(name, 2, new ThrowingMetrics()))
            {
                hub.Start();
                await hub.PublishAsync("empty", new { value = 1 });

                var received = new ManualResetEventSlim(false);
                using (var client = new PipeEventClient(name))
                {
                    client.OnEvent += _ => received.Set();
                    client.Start();

                    Assert.True(
                        PipeTestUtil.WaitUntil(() => hub.SubscriberCount == 1, 5000),
                        "event client did not connect");

                    await hub.PublishAsync("delivered", new { value = 2 });
                    Assert.True(received.Wait(5000), "event was not delivered");
                    Assert.Equal(1, hub.SubscriberCount);
                }
            }
        }

        [Fact]
        public void SimplePipeMetrics_IsClosedForInheritance()
        {
            Assert.True(typeof(SimplePipeMetrics).IsSealed);
        }

        private sealed class ThrowingMetrics : IPipeMetrics
        {
            public void OnServerClientConnected(string pipeName) => Throw();
            public void OnServerClientDisconnected(string pipeName) => Throw();
            public void OnServerRequestReceived(string pipeName, string name) => Throw();
            public void OnServerResponseSent(
                string pipeName,
                string name,
                bool ok,
                TimeSpan elapsed) => Throw();
            public void OnServerEventPublished(
                string pipeName,
                string eventName,
                int subscribers,
                int success,
                int failed) => Throw();
            public void OnClientConnect(
                string pipeName,
                TimeSpan elapsed,
                bool ok,
                string errorCode) => Throw();
            public void OnClientRequest(string pipeName, string name) => Throw();
            public void OnClientResponse(
                string pipeName,
                string name,
                bool ok,
                TimeSpan elapsed,
                string errorCode) => Throw();
            public void OnError(string pipeName, string where, Exception ex) => Throw();
            public PipeMetricsSnapshot Snapshot() => throw new InvalidOperationException("snapshot");

            private static void Throw()
                => throw new InvalidOperationException("metrics");
        }
    }
}
