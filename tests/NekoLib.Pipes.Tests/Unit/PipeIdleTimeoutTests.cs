using System;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using NekoLib.Pipes.Tests.Unit.Fakes;
using Xunit;

namespace NekoLib.Pipes.Tests.Unit
{
    /// <summary>
    /// Tests for the corrected <c>ClientIdleTimeout</c> semantics: it now measures
    /// inactivity *between* requests and resets on activity (a true idle timeout),
    /// instead of capping the whole session. Uses a raw persistent connection +
    /// internal <c>PipeFraming</c> (via InternalsVisibleTo) because the per-call
    /// <c>PipeClient</c> never holds a connection open across the timeout.
    /// </summary>
    public class PipeIdleTimeoutTests
    {
        private static PipeMessage Req(string name)
            => new PipeMessage { Id = Guid.NewGuid(), Type = "req", Name = name, Ok = true };

        private static PipeServer StartPingServer(string name, TimeSpan idle)
        {
            var server = new PipeServer(new PipeServerOptions
            {
                PipeName = name,
                EnableEvents = false,
                ClientIdleTimeout = idle
            });
            server.Map("ping", (req, ct) => Task.FromResult(new PipeMessage { Ok = true }));
            server.Start();
            return server;
        }

        private static NamedPipeClientStream ConnectRaw(string name)
        {
            var raw = new NamedPipeClientStream(".", name, PipeDirection.InOut, PipeOptions.Asynchronous);
            raw.Connect(3000);
            return raw;
        }

        [Fact]
        public async Task ActiveConnection_StaysAlive_PastIdleTimeout()
        {
            var name = PipeTestUtil.UniqueName();

            using (var server = StartPingServer(name, TimeSpan.FromMilliseconds(1000)))
            using (var raw = ConnectRaw(name))
            {
                // A request every ~200 ms (< 1000 ms idle) over ~1 s: activity keeps
                // resetting the timer, so the connection must survive past one timeout.
                for (int i = 0; i < 5; i++)
                {
                    await PipeFraming.WriteAsync(raw, Req("ping"), default);
                    var resp = await PipeFraming.ReadAsync(raw, default);
                    Assert.True(resp.Ok, "request " + i + " failed");
                    await Task.Delay(200);
                }
            }
        }

        [Fact]
        public async Task IdleConnection_IsDisconnected_AfterIdleTimeout()
        {
            var name = PipeTestUtil.UniqueName();

            using (var server = StartPingServer(name, TimeSpan.FromMilliseconds(500)))
            using (var raw = ConnectRaw(name))
            {
                // One active request works.
                await PipeFraming.WriteAsync(raw, Req("ping"), default);
                var ok = await PipeFraming.ReadAsync(raw, default);
                Assert.True(ok.Ok);

                // Stay idle well past the timeout — the server drops the connection.
                await Task.Delay(1500);

                // The next exchange must fail (server closed its end).
                var ex = await Record.ExceptionAsync(async () =>
                {
                    await PipeFraming.WriteAsync(raw, Req("ping"), default);
                    await PipeFraming.ReadAsync(raw, default);
                });

                Assert.NotNull(ex);
            }
        }
    }
}
