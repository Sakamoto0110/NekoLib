using System;
using System.Threading;
using System.Threading.Tasks;
using NekoLib.Pipes.Tests.Unit.Fakes;
using Xunit;

namespace NekoLib.Pipes.Tests.Unit
{
    public sealed class PipeConfigurationTests
    {
        [Theory]
        [InlineData("pipe")]
        [InlineData("connect")]
        [InlineData("request")]
        [InlineData("message")]
        public void Client_InvalidConfiguration_ThrowsAtConstruction(string field)
        {
            var options = new PipeClientOptions
            {
                PipeName = PipeTestUtil.UniqueName()
            };

            switch (field)
            {
                case "pipe":
                    options.PipeName = "  ";
                    break;
                case "connect":
                    options.ConnectTimeout = TimeSpan.Zero;
                    break;
                case "request":
                    options.RequestTimeout = Timeout.InfiniteTimeSpan;
                    break;
                case "message":
                    options.MaxMessageBytes = 0;
                    break;
            }

            Assert.ThrowsAny<ArgumentException>(() => new PipeClient(options));
        }

        [Theory]
        [InlineData("pipe")]
        [InlineData("clients")]
        [InlineData("idle")]
        [InlineData("subscribers")]
        [InlineData("queue")]
        [InlineData("overflow")]
        [InlineData("message")]
        public void Server_InvalidConfiguration_ThrowsAtConstruction(string field)
        {
            var options = new PipeServerOptions
            {
                PipeName = PipeTestUtil.UniqueName()
            };

            switch (field)
            {
                case "pipe":
                    options.PipeName = "";
                    break;
                case "clients":
                    options.MaxClients = 0;
                    break;
                case "idle":
                    options.ClientIdleTimeout = TimeSpan.Zero;
                    break;
                case "subscribers":
                    options.MaxEventSubscribers = 0;
                    break;
                case "queue":
                    options.EventSubscriberQueueCapacity = 0;
                    break;
                case "overflow":
                    options.EventQueueOverflowPolicy = (PipeEventQueueOverflowPolicy)int.MaxValue;
                    break;
                case "message":
                    options.MaxMessageBytes = -1;
                    break;
            }

            Assert.ThrowsAny<ArgumentException>(() => new PipeServer(options));
        }

        [Fact]
        public void EventEndpoints_InvalidConfiguration_ThrowsImmediately()
        {
            Assert.Throws<ArgumentException>(() => new PipeEventClient(" "));
            Assert.Throws<ArgumentException>(() => new PipeEventHub(" ", 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PipeEventHub("valid", 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PipeEventHub(
                "valid",
                1,
                PipeAccessPolicy.PlatformDefault,
                0,
                PipeEventQueueOverflowPolicy.DropNewest));
        }

        [Fact]
        public void EventClient_InvalidLiveTimeoutOrDelay_ThrowsFromSetter()
        {
            var client = new PipeEventClient(PipeTestUtil.UniqueName());

            Assert.Throws<ArgumentOutOfRangeException>(
                () => client.ConnectTimeout = TimeSpan.Zero);
            Assert.Throws<ArgumentOutOfRangeException>(
                () => client.ConnectTimeout = TimeSpan.MaxValue);
            Assert.Throws<ArgumentOutOfRangeException>(
                () => client.ReconnectDelay = TimeSpan.FromMilliseconds(-1));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => client.ReconnectDelay = TimeSpan.MaxValue);
        }

        [Fact]
        public async Task Constructors_CaptureOptionsAndIgnoreLaterMutation()
        {
            var originalName = PipeTestUtil.UniqueName();
            var serverOptions = new PipeServerOptions
            {
                PipeName = originalName,
                EnableEvents = false,
                AccessPolicy = PipeAccessPolicy.PlatformDefault
            };
            var clientOptions = new PipeClientOptions
            {
                PipeName = originalName,
                ConnectTimeout = TimeSpan.FromSeconds(3),
                RequestTimeout = TimeSpan.FromSeconds(3)
            };

            using (var server = new PipeServer(serverOptions))
            {
                var client = new PipeClient(clientOptions);

                serverOptions.PipeName = " ";
                serverOptions.MaxClients = 0;
                serverOptions.ClientIdleTimeout = TimeSpan.Zero;
                serverOptions.EnableEvents = true;
                serverOptions.AccessPolicy = (PipeAccessPolicy)int.MaxValue;
                clientOptions.PipeName = " ";
                clientOptions.ConnectTimeout = TimeSpan.Zero;
                clientOptions.RequestTimeout = TimeSpan.Zero;
                clientOptions.MaxMessageBytes = 0;

                server.Map(
                    "ping",
                    (request, cancellationToken) =>
                        Task.FromResult(new PipeMessage { Ok = true }));
                server.Start();

                var response = await client.SendAsync("ping");

                Assert.True(response.Ok);
                Assert.Null(server.Events);
                Assert.Equal(PipeAccessPolicy.PlatformDefault, server.AccessPolicy);
            }
        }
    }
}
