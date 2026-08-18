using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text;
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
            {
                var client = Client(name);
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
            {
                var client = Client(name);
                var resp = await client.SendAsync("does_not_exist");

                Assert.False(resp.Ok);
                Assert.NotNull(resp.Error);
                Assert.Equal(PipeErrorCodes.NotFound, resp.Error.Code);
            }
        }

        [Fact]
        public async Task HandlerThatThrows_ReturnsSanitizedExceptionError()
        {
            var name = PipeTestUtil.UniqueName();

            using (var server = StartServer(name, s =>
                s.Map("boom", async (req, ct) =>
                {
                    await Task.Yield();
                    throw new InvalidOperationException("kaboom");
                })))
            {
                var client = Client(name);
                var resp = await client.SendAsync("boom");

                Assert.False(resp.Ok);
                Assert.NotNull(resp.Error);
                Assert.Equal(PipeErrorCodes.Exception, resp.Error.Code);
                Assert.Equal("The handler failed.", resp.Error.Message);
                Assert.DoesNotContain("kaboom", resp.Error.Message);
            }
        }

        [Fact]
        public async Task TryReadAsync_CleanEndOfStream_ReturnsNull()
        {
            using (var stream = new MemoryStream())
            {
                var message = await PipeFraming.TryReadAsync(stream, default);

                Assert.Null(message);
            }
        }

        [Fact]
        public async Task TryReadAsync_TruncatedFrame_ThrowsEndOfStreamException()
        {
            var declaredLength = BitConverter.GetBytes(10);
            var bytes = new byte[declaredLength.Length + 3];
            Buffer.BlockCopy(declaredLength, 0, bytes, 0, declaredLength.Length);
            bytes[4] = (byte)'{';
            bytes[5] = (byte)'}';
            bytes[6] = (byte)' ';

            using (var stream = new MemoryStream(bytes))
            {
                await Assert.ThrowsAsync<EndOfStreamException>(
                    () => PipeFraming.TryReadAsync(stream, default));
            }
        }

        [Fact]
        public async Task TryReadAsync_MalformedJson_ThrowsParseException()
        {
            var payload = Encoding.UTF8.GetBytes("{not-json}");
            var length = BitConverter.GetBytes(payload.Length);
            var bytes = new byte[length.Length + payload.Length];
            Buffer.BlockCopy(length, 0, bytes, 0, length.Length);
            Buffer.BlockCopy(payload, 0, bytes, length.Length, payload.Length);

            using (var stream = new MemoryStream(bytes))
            {
                await Assert.ThrowsAnyAsync<Exception>(
                    () => PipeFraming.TryReadAsync(stream, default));
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
            {
                var client = Client(name);
                var resp = await client.SendAsync("big");

                Assert.False(resp.Ok);
                Assert.NotNull(resp.Error);
                Assert.Equal(PipeErrorCodes.ResponseTooLarge, resp.Error.Code);
            }
        }

        [Fact]
        public async Task ConfiguredMaxMessageBytes_IsHonored()
        {
            // Lowering MaxMessageBytes well below a normally-fine response proves the
            // configured cap threads through framing: an 8 KB response (fine at the 1 MB
            // default) is now rejected as response_too_large.
            var name = PipeTestUtil.UniqueName();

            using (var server = new PipeServer(new PipeServerOptions
            {
                PipeName = name,
                EnableEvents = false,
                MaxMessageBytes = 2048
            }))
            {
                var client = new PipeClient(new PipeClientOptions
                {
                    PipeName = name,
                    ConnectTimeout = TimeSpan.FromSeconds(3),
                    RequestTimeout = TimeSpan.FromSeconds(3),
                    MaxMessageBytes = 2048
                });
                server.Map("big", (req, ct) => Task.FromResult(new PipeMessage
                {
                    Ok = true,
                    Error = new PipeError { Code = "", Message = new string('x', 8000) }
                }));
                server.Start();

                var resp = await client.SendAsync("big");

                Assert.False(resp.Ok);
                Assert.NotNull(resp.Error);
                Assert.Equal(PipeErrorCodes.ResponseTooLarge, resp.Error.Code);
            }
        }

        [Fact]
        public async Task ConfiguredMaxMessageBytes_AllowsLargerResponse()
        {
            // With both sides raising the cap, a 2 MB response (over the 1 MB default)
            // round-trips on both TFMs. Regression guard: a missed net9 client call site
            // had left the read pinned to the 1 MB default.
            var name = PipeTestUtil.UniqueName();
            const int big = 2 * 1024 * 1024;
            const int cap = 8 * 1024 * 1024;

            using (var server = new PipeServer(new PipeServerOptions
            {
                PipeName = name,
                EnableEvents = false,
                MaxMessageBytes = cap
            }))
            {
                var client = new PipeClient(new PipeClientOptions
                {
                    PipeName = name,
                    ConnectTimeout = TimeSpan.FromSeconds(3),
                    RequestTimeout = TimeSpan.FromSeconds(15),
                    MaxMessageBytes = cap
                });
                server.Map("big", (req, ct) => Task.FromResult(new PipeMessage
                {
                    Ok = true,
                    Error = new PipeError { Code = "", Message = new string('x', big) }
                }));
                server.Start();

                var resp = await client.SendAsync("big");

                Assert.True(resp.Ok);
                Assert.NotNull(resp.Error);
                Assert.True(resp.Error.Message.Length >= big, "large payload did not round-trip");
            }
        }

        [Fact]
        public async Task MultipleSequentialRequests_AllSucceed()
        {
            var name = PipeTestUtil.UniqueName();

            using (var server = StartServer(name, s =>
                s.Map("ping", (req, ct) => Task.FromResult(new PipeMessage { Ok = true }))))
            {
                var client = Client(name);
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
            {
                var client = new PipeClient(new PipeClientOptions
                {
                    PipeName = name,
                    ConnectTimeout = TimeSpan.FromSeconds(3),
                    RequestTimeout = TimeSpan.FromMilliseconds(500)
                });
                var sw = Stopwatch.StartNew();
                await Assert.ThrowsAnyAsync<OperationCanceledException>(
                    () => client.SendAsync("slow"));
                sw.Stop();

                Assert.True(sw.ElapsedMilliseconds < 3000,
                    "request did not time out promptly (took " + sw.ElapsedMilliseconds + " ms)");
            }
        }

        [Fact]
        public async Task ConcurrentRequests_AllSucceed()
        {
            // Exercises concurrent Dispatch reads of the handler map (audit M2).
            var name = PipeTestUtil.UniqueName();

            using (var server = StartServer(name, s =>
                s.Map("ping", (req, ct) => Task.FromResult(new PipeMessage { Ok = true }))))
            {
                const int n = 12;
                var tasks = new Task<bool>[n];
                for (int i = 0; i < n; i++)
                {
                    tasks[i] = Task.Run(async () =>
                    {
                        var client = Client(name);
                        var resp = await client.SendAsync("ping");
                        return resp.Ok;
                    });
                }

                bool[] results = await Task.WhenAll(tasks);
                Assert.All(results, ok => Assert.True(ok));
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
            {
                var client = Client(name);
                var ex = await Record.ExceptionAsync(() => client.SendAsync("ok"));
                Assert.Null(ex);
            }
        }

        [Fact]
        public void FrameworkErrorCodes_AreStableWireStrings()
        {
            Assert.Equal("not_found", PipeErrorCodes.NotFound);
            Assert.Equal("exception", PipeErrorCodes.Exception);
            Assert.Equal("response_too_large", PipeErrorCodes.ResponseTooLarge);
            Assert.Equal("connection_closed", PipeErrorCodes.ConnectionClosed);
        }

        [Fact]
        public async Task CleanEofBeforeResponse_ReturnsConnectionClosedError()
        {
            var name = PipeTestUtil.UniqueName();
            var client = Client(name);
            Task<PipeMessage> responseTask;

            using (var server = NewRawServer(name))
            {
                var serverTask = Task.Run(async () =>
                {
                    await Task.Run(() => server.WaitForConnection());
                    Assert.NotNull(await PipeFraming.TryReadAsync(server, default));
                });

                responseTask = client.SendAsync("close");
                await serverTask;
            }

            var response = await responseTask;

            Assert.False(response.Ok);
            Assert.Equal(PipeErrorCodes.ConnectionClosed, response.Error.Code);
        }

        [Fact]
        public async Task ResponseWithWrongCorrelation_ThrowsInvalidOperationException()
        {
            var name = PipeTestUtil.UniqueName();
            using (var server = NewRawServer(name))
            {
                var serverTask = Task.Run(async () =>
                {
                    await Task.Run(() => server.WaitForConnection());
                    var request = await PipeFraming.TryReadAsync(server, default);
                    await PipeFraming.WriteAsync(
                        server,
                        new PipeMessage
                        {
                            Id = Guid.NewGuid(),
                            Type = "res",
                            Name = request.Name,
                            Ok = true
                        },
                        default);
                });
                var client = Client(name);

                await Assert.ThrowsAsync<InvalidOperationException>(
                    () => client.SendAsync("mismatch"));
                await serverTask;
            }
        }

        private static NamedPipeServerStream NewRawServer(string name)
        {
            return new NamedPipeServerStream(
                name,
                PipeDirection.InOut,
                1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous);
        }
    }
}
