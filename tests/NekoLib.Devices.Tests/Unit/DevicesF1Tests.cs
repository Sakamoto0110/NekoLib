using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NekoLib.Devices.Core.Abstractions;
using NekoLib.Devices.Core.Engine;
using NekoLib.Devices.Core.Protocols;
using NekoLib.Devices.Core.Transport;
using Xunit;

namespace NekoLib.Devices.Tests.Unit
{
    /// <summary>
    /// Regressions for the accepted F1-DEV dispositions. The operation-boundary
    /// tests use a real loopback TCP peer; none of this is serial evidence.
    /// </summary>
    public class DevicesF1Tests
    {
        // -------------------------------------------------------------
        // DEV-01 — operation boundary
        // -------------------------------------------------------------

        [Fact]
        public async Task SendAsync_DefaultOff_LateReplyIsDeliveredToTheNextOperation()
        {
            using (var peer = new LatePeer())
            {
                var engine = peer.CreateEngine();

                var first = await engine.SendAsync(Op("op1"), 200);
                await Task.Delay(700);
                var second = await engine.SendAsync(Op("op2"), 300);

                // Pins the default: without opting in, the receive buffer survives the
                // operation boundary and the late reply to op1 satisfies op2.
                Assert.False(first.Success);
                Assert.True(second.Success);
                Assert.Equal(LatePeer.LateReply, second.RawText);
            }
        }

        [Fact]
        public async Task SendAsync_CloseTransportOnNoResponse_KeepsTheLateReplyOutOfTheNextOperation()
        {
            using (var peer = new LatePeer())
            {
                var engine = peer.CreateEngine();
                engine.CloseTransportOnNoResponse = true;

                var first = await engine.SendAsync(Op("op1"), 200);
                await Task.Delay(700);
                var second = await engine.SendAsync(Op("op2"), 300);

                Assert.False(first.Success);
                Assert.False(second.Success);
                Assert.Null(second.RawText);
            }
        }

        [Fact]
        public async Task SendAsync_CloseTransportOnNoResponse_ClosesOnlyWhenNothingArrived()
        {
            var transport = new CountingTransport { Reply = Encoding.ASCII.GetBytes("OK") };
            var engine = new HardwareEngine(transport, RawProtocol())
            {
                CloseTransportOnNoResponse = true
            };

            var answered = await engine.SendAsync(Op("answered"), 100);
            Assert.True(answered.Success);
            Assert.Equal(0, transport.CloseCount);

            transport.Reply = null;
            var silent = await engine.SendAsync(Op("silent"), 100);
            Assert.False(silent.Success);
            Assert.Equal(1, transport.CloseCount);
        }

        // -------------------------------------------------------------
        // DEV-02 — configuration ownership
        // -------------------------------------------------------------

        [Fact]
        public async Task SendAsync_DoesNotMutateTheProtocolConfiguration()
        {
            var protocol = new ProtocolRaw(new SerialConfig { NewLine = "\r\n" });
            var transport = new CountingTransport
            {
                PortName = "tcp://127.0.0.1:9",
                Reply = Encoding.ASCII.GetBytes("OK")
            };
            var engine = new HardwareEngine(transport, protocol);

            await engine.SendAsync("tcp://127.0.0.1:9", Op("ping"), 100);

            Assert.Null(protocol.PortConfig.PortName);
            Assert.NotSame(protocol.PortConfig, transport.LastConfig);
        }

        [Fact]
        public async Task SendAsync_WithoutConfiguredPort_FallsBackToTheTransportEndpoint()
        {
            var protocol = new ProtocolRaw(new SerialConfig { NewLine = "\r\n" });
            var transport = new CountingTransport
            {
                PortName = "tcp://127.0.0.1:9",
                Reply = Encoding.ASCII.GetBytes("OK")
            };
            var engine = new HardwareEngine(transport, protocol);

            var response = await engine.SendAsync(Op("ping"), 100);

            Assert.True(response.Success);
            Assert.Equal("tcp://127.0.0.1:9", transport.OpenedPort);
            Assert.Null(protocol.PortConfig.PortName);
        }

        // -------------------------------------------------------------
        // DEV-03 — failure evidence
        // -------------------------------------------------------------

        [Fact]
        public async Task SendAsync_Failure_CarriesTheExceptionNotJustItsMessage()
        {
            var transport = new CountingTransport
            {
                PortName = "tcp://127.0.0.1:9",
                ThrowOnWrite = new ObjectDisposedException("FakeTransport")
            };
            var engine = new HardwareEngine(transport, RawProtocol());

            var response = await engine.SendAsync(Op("x"), 100);

            Assert.False(response.Success);
            Assert.NotNull(response.Failure);
            Assert.IsType<ObjectDisposedException>(response.Failure);
        }

        [Fact]
        public async Task SendAsync_Success_LeavesFailureNull()
        {
            var transport = new CountingTransport
            {
                PortName = "tcp://127.0.0.1:9",
                Reply = Encoding.ASCII.GetBytes("OK")
            };
            var engine = new HardwareEngine(transport, RawProtocol());

            var response = await engine.SendAsync(Op("x"), 100);

            Assert.True(response.Success);
            Assert.Null(response.Failure);
        }

        [Fact]
        public async Task SendAsync_Cancellation_RemainsCancellationAndIsNotAFailedResponse()
        {
            var transport = new CountingTransport
            {
                PortName = "tcp://127.0.0.1:9",
                ThrowOnWrite = new OperationCanceledException()
            };
            var engine = new HardwareEngine(transport, RawProtocol());

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => engine.SendAsync(Op("x"), 100));
        }

        // -------------------------------------------------------------
        // DEV-09 — helper null handling
        // -------------------------------------------------------------

        [Fact]
        public void Checksum_NullInput_ThrowsArgumentNullExceptionConsistently()
        {
            Assert.Throws<ArgumentNullException>(() => Checksum.Sum(null));
            Assert.Throws<ArgumentNullException>(() => Checksum.Xor(null));
        }

        [Fact]
        public void Checksum_ComputesSumAndXor()
        {
            Assert.Equal((byte)0x06, Checksum.Sum(0x01, 0x02, 0x03));
            Assert.Equal((byte)0x00, Checksum.Xor(0x01, 0x02, 0x03));
        }

        // -------------------------------------------------------------
        // helpers
        // -------------------------------------------------------------

        private static IHardwareProtocol RawProtocol()
            => new ProtocolRaw(new SerialConfig { PortName = "tcp://127.0.0.1:9", NewLine = "\r\n" });

        private static HardwareOperation Op(string text)
            => new HardwareOperation
            {
                Operation = text,
                Args = new Dictionary<string, object> { ["RawText"] = text }
            };

        /// <summary>
        /// A real loopback peer that answers the first command far too late for its
        /// budget and never answers the second.
        /// </summary>
        private sealed class LatePeer : IDisposable
        {
            public const string LateReply = "LATE-REPLY-TO-OP1";

            private readonly TcpListener _listener;
            private readonly Task _serve;
            private readonly List<IDisposable> _owned = new List<IDisposable>();

            public LatePeer()
            {
                _listener = new TcpListener(IPAddress.Loopback, 0);
                _listener.Start();
                Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
                _serve = Serve();
            }

            public int Port { get; }

            public HardwareEngine CreateEngine()
            {
                var endpoint = "tcp://127.0.0.1:" + Port;
                var transport = new TcpCommTransport(endpoint);
                lock (_owned) _owned.Add(transport);
                return new HardwareEngine(
                    transport,
                    new ProtocolRaw(new SerialConfig { PortName = endpoint, NewLine = "\r\n" }));
            }

            private async Task Serve()
            {
                try
                {
                    while (true)
                    {
                        var client = await _listener.AcceptTcpClientAsync().ConfigureAwait(false);
                        lock (_owned) _owned.Add(client);
                        var stream = client.GetStream();
                        var buffer = new byte[256];

                        // First command: reply long after its budget expired.
                        await stream.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
                        await Task.Delay(600).ConfigureAwait(false);
                        var late = Encoding.ASCII.GetBytes(LateReply);
                        await stream.WriteAsync(late, 0, late.Length).ConfigureAwait(false);
                        await stream.FlushAsync().ConfigureAwait(false);

                        // Second command: deliberately unanswered.
                        await stream.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
                        await Task.Delay(2000).ConfigureAwait(false);
                    }
                }
                catch
                {
                }
            }

            public void Dispose()
            {
                try { _listener.Stop(); } catch { }
                lock (_owned)
                {
                    foreach (var owned in _owned)
                    {
                        try { owned.Dispose(); } catch { }
                    }
                }
                try { _serve.Wait(1000); } catch { }
            }
        }

        private sealed class CountingTransport : ICommTransport
        {
            public SerialConfig PortInfo { get; private set; } = new SerialConfig();
            public SerialConfig LastConfig { get; private set; }
            public HardwareLogHandler Log { get; set; }
            public string PortName { get; set; } = string.Empty;
            public bool IsOpen { get; private set; }
            public byte[] Reply { get; set; }
            public string OpenedPort { get; private set; }
            public int CloseCount { get; private set; }
            public Exception ThrowOnWrite { get; set; }

            public void Configure(SerialConfig cfg)
            {
                LastConfig = cfg;
                PortInfo = cfg;
            }

            public Task<ICommTransport> Open(string portName, CancellationToken ct = default)
            {
                OpenedPort = portName;
                PortName = portName;
                IsOpen = true;
                return Task.FromResult<ICommTransport>(this);
            }

            public Task<ICommTransport> Open(CancellationToken ct = default) => Open(PortName, ct);

            public Task Close()
            {
                CloseCount++;
                IsOpen = false;
                return Task.CompletedTask;
            }

            public Task Write(string text, CancellationToken ct = default)
                => Write(Encoding.ASCII.GetBytes(text), 0, -1, ct);

            public Task Write(byte[] data, int offset = 0, int count = -1, CancellationToken ct = default)
            {
                if (ThrowOnWrite != null) throw ThrowOnWrite;
                return Task.CompletedTask;
            }

            public Task<string> ReadLine(int timeoutMs = 2000, CancellationToken ct = default)
                => Task.FromResult<string>(null);

            public Task<byte[]> ReadExact(int length, int timeoutMs = 2000, CancellationToken ct = default)
                => Task.FromResult<byte[]>(null);

            public Task<byte[]> ReadAll(int timeoutMs = 2000, int quietPeriodMs = 100, CancellationToken ct = default)
                => Task.FromResult(Reply);
        }
    }
}
