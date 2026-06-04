using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using NekoLib.Pipes.Tests.Unit.Fakes;
using Xunit;

namespace NekoLib.Pipes.Tests.Unit
{
    /// <summary>
    /// Black-box round-trip tests for the RPC path: a real in-process
    /// <see cref="PipeServer"/> + <see cref="PipeClient"/> over a named pipe.
    /// These exercise framing, dispatch, request/response correlation, and the
    /// two built-in error responses (not_found, exception) through the public
    /// API only — locking the current behavior before the net481 cancellation
    /// fix (audit H2) touches the framing layer.
    /// </summary>
    public class PipeRpcTests
    {
        private static PipeServer StartServer(string name, Action<PipeServer> configure)
        {
            var server = new PipeServer(new PipeServerOptions
            {
                PipeName = name,
                EnableEvents = false
            });
            configure(server);
            server.Start();
            return server;
        }

        private static PipeClient Client(string name)
            => new PipeClient(new PipeClientOptions
            {
                PipeName = name,
                ConnectTimeout = TimeSpan.FromSeconds(3),
                RequestTimeout = TimeSpan.FromSeconds(3)
            });

        [Fact]
        public async Task Request_RoundTrips_AndEchoesPayload()
        {
            var name = PipeTestUtil.UniqueName();

            using (var server = StartServer(name, s =>
                s.Map("echo", (req, ct) => Task.FromResult(new PipeMessage { Ok = true, Data = req.Data }))))
            using (var client = Client(name))
            {
                var resp = await client.SendAsync("echo", new { value = 42 });

                Assert.True(resp.Ok);
                Assert.Equal("res", resp.Type);
                Assert.Equal("echo", resp.Name);

                var text = PipeTestUtil.DataText(resp);
                Assert.Contains("value", text);
                Assert.Contains("42", text);
            }
        }

        [Fact]
        public async Task UnknownCommand_ReturnsNotFoundError()
        {
            var name = PipeTestUtil.UniqueName();

            using (var server = StartServer(name, _ => { /* no handlers */ }))
            using (var client = Client(name))
            {
                var resp = await client.SendAsync("does_not_exist");

                Assert.False(resp.Ok);
                Assert.NotNull(resp.Error);
                Assert.Equal("not_found", resp.Error.Code);
            }
        }

        [Fact]
        public async Task HandlerThatThrows_ReturnsExceptionError()
        {
            var name = PipeTestUtil.UniqueName();

            using (var server = StartServer(name, s =>
                s.Map("boom", async (req, ct) =>
                {
                    await Task.Yield();
                    throw new InvalidOperationException("kaboom");
                })))
            using (var client = Client(name))
            {
                var resp = await client.SendAsync("boom");

                Assert.False(resp.Ok);
                Assert.NotNull(resp.Error);
                Assert.Equal("exception", resp.Error.Code);
                Assert.Contains("kaboom", resp.Error.Message);
            }
        }

        [Fact]
        public async Task OversizeResponse_ReturnsStructuredError_NotDroppedConnection()
        {
            // The handler returns a response larger than the 1 MB frame limit (via a
            // big Error.Message — no TFM-specific Data construction needed). Before the
            // M5 fix this dropped the connection; now the client gets a clean error.
            var name = PipeTestUtil.UniqueName();

            using (var server = StartServer(name, s =>
                s.Map("big", (req, ct) => Task.FromResult(new PipeMessage
                {
                    Ok = true,
                    Error = new PipeError { Code = "", Message = new string('x', 1_200_000) }
                }))))
            using (var client = Client(name))
            {
                var resp = await client.SendAsync("big");

                Assert.False(resp.Ok);
                Assert.NotNull(resp.Error);
                Assert.Equal("response_too_large", resp.Error.Code);
            }
        }

        [Fact]
        public async Task MultipleSequentialRequests_AllSucceed()
        {
            var name = PipeTestUtil.UniqueName();

            using (var server = StartServer(name, s =>
                s.Map("ping", (req, ct) => Task.FromResult(new PipeMessage { Ok = true }))))
            using (var client = Client(name))
            {
                for (int i = 0; i < 5; i++)
                {
                    var resp = await client.SendAsync("ping");
                    Assert.True(resp.Ok);
                    Assert.Equal("ping", resp.Name);
                }
            }
        }

        [Fact]
        public async Task RequestTimeout_IsEnforced_WhenHandlerStalls()
        {
            // Before the H2 fix this would block indefinitely on net481 (the framing
            // layer ignored the CancellationToken there); net9 already honored it.
            var name = PipeTestUtil.UniqueName();

            using (var server = StartServer(name, s =>
                s.Map("slow", async (req, ct) =>
                {
                    await Task.Delay(5000, ct);   // far longer than the client's RequestTimeout
                    return new PipeMessage { Ok = true };
                })))
            using (var client = new PipeClient(new PipeClientOptions
            {
                PipeName = name,
                ConnectTimeout = TimeSpan.FromSeconds(3),
                RequestTimeout = TimeSpan.FromMilliseconds(500)
            }))
            {
                var sw = Stopwatch.StartNew();
                await Assert.ThrowsAnyAsync<OperationCanceledException>(
                    () => client.SendAsync("slow"));
                sw.Stop();

                Assert.True(sw.ElapsedMilliseconds < 3000,
                    "request did not time out promptly (took " + sw.ElapsedMilliseconds + " ms)");
            }
        }

        [Fact]
        public async Task Response_CorrelatesToRequest()
        {
            // A successful SendAsync internally asserts response.Id == request.Id
            // (PipeClient.ValidateCorrelation throws otherwise), so completing
            // without throwing is the correlation guarantee.
            var name = PipeTestUtil.UniqueName();

            using (var server = StartServer(name, s =>
                s.Map("ok", (req, ct) => Task.FromResult(new PipeMessage { Ok = true }))))
            using (var client = Client(name))
            {
                var ex = await Record.ExceptionAsync(() => client.SendAsync("ok"));
                Assert.Null(ex);
            }
        }
    }
}
