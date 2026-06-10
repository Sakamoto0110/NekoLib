using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;
using NekoLib.Devices.Core.Abstractions;
using NekoLib.Devices.Core.Engine;
using NekoLib.Devices.Core.Transport;
using Xunit;

namespace NekoLib.Devices.Tests.Unit
{
    public class HardwareEngineTests
    {
        [Fact]
        public void Constructor_NullDependencies_Throw()
        {
            var transport = new FakeTransport();
            var protocol = new FakeProtocol();

            Assert.Throws<ArgumentNullException>(() => new HardwareEngine(null, protocol));
            Assert.Throws<ArgumentNullException>(() => new HardwareEngine(transport, null));
        }

        [Fact]
        public async Task SendAsync_ConfiguredPort_ExecutesInOrderAndParsesResponse()
        {
            var events = new List<string>();
            var transport = new FakeTransport(events)
            {
                Reply = new byte[] { 0x10, 0x20 }
            };
            var protocol = new FakeProtocol(events)
            {
                Config = { PortName = "COM9" },
                Command = new byte[] { 0xAA, 0xBB }
            };
            var op = new HardwareOperation { Operation = "status" };
            var engine = new HardwareEngine(transport, protocol);

            var response = await engine.SendAsync(op, 250);

            Assert.True(response.Success);
            Assert.Equal("Parsed", response.Status);
            Assert.Same(op, response.Request);
            Assert.Equal(transport.Reply, response.RawBytes);
            Assert.True(response.Elapsed >= TimeSpan.Zero);
            Assert.Equal("COM9", transport.OpenedPort);
            Assert.Equal(250, transport.ReadTimeoutMs);
            Assert.Equal(50, transport.QuietPeriodMs);
            Assert.Equal(new byte[] { 0xAA, 0xBB }, transport.Written);
            Assert.Equal(
                new[] { "Configure", "Open:COM9", "Build", "Write", "ReadAll", "Parse" },
                events);
        }

        [Fact]
        public async Task SendAsync_ExplicitPort_UsesProvidedPort()
        {
            var transport = new FakeTransport();
            var protocol = new FakeProtocol
            {
                Config = { PortName = "COM1" },
                Command = new byte[] { 0x01 }
            };
            var engine = new HardwareEngine(transport, protocol);

            await engine.SendAsync("COM7", new HardwareOperation { Operation = "ping" }, 100);

            Assert.Equal("COM7", transport.OpenedPort);
        }

        [Fact]
        public async Task SendAsync_ConfiguredPortAlreadyOpenOnSamePort_DoesNotReopen()
        {
            var events = new List<string>();
            var transport = new FakeTransport(events)
            {
                IsOpen = true,
                PortName = "COM9",
                Reply = new byte[] { 0x10 }
            };
            var protocol = new FakeProtocol(events)
            {
                Config = { PortName = "COM9" },
                Command = new byte[] { 0xAA }
            };
            var engine = new HardwareEngine(transport, protocol);

            var response = await engine.SendAsync(new HardwareOperation { Operation = "status" }, 100);

            Assert.True(response.Success);
            Assert.Null(transport.OpenedPort);
            Assert.Equal(
                new[] { "Configure", "Build", "Write", "ReadAll", "Parse" },
                events);
        }

        [Fact]
        public async Task SendAsync_ConfiguredPortAlreadyOpenOnDifferentPort_ReturnsFailedResponse()
        {
            var events = new List<string>();
            var transport = new FakeTransport(events)
            {
                IsOpen = true,
                PortName = "COM2"
            };
            var protocol = new FakeProtocol(events)
            {
                Config = { PortName = "COM9" },
                Command = new byte[] { 0xAA }
            };
            var op = new HardwareOperation { Operation = "status" };
            var engine = new HardwareEngine(transport, protocol);

            var response = await engine.SendAsync(op, 100);

            Assert.False(response.Success);
            Assert.Contains("COM2", response.Status);
            Assert.Contains("COM9", response.Status);
            Assert.Same(op, response.Request);
            Assert.Equal(new[] { "Configure" }, events);
            Assert.Null(transport.Written);
        }

        [Fact]
        public async Task SendAsync_OrdinaryException_ReturnsFailedResponse()
        {
            var transport = new FakeTransport
            {
                ExceptionToThrow = new InvalidOperationException("write failed")
            };
            var protocol = new FakeProtocol
            {
                Config = { PortName = "COM4" },
                Command = new byte[] { 0x01 }
            };
            var op = new HardwareOperation { Operation = "ping" };
            var engine = new HardwareEngine(transport, protocol);

            var response = await engine.SendAsync(op, 100);

            Assert.False(response.Success);
            Assert.Equal("write failed", response.Status);
            Assert.Same(op, response.Request);
            Assert.True(response.Elapsed >= TimeSpan.Zero);
        }

        [Fact]
        public async Task SendAsync_Cancellation_Propagates()
        {
            var transport = new FakeTransport
            {
                ExceptionToThrow = new OperationCanceledException()
            };
            var protocol = new FakeProtocol
            {
                Config = { PortName = "COM4" },
                Command = new byte[] { 0x01 }
            };
            var engine = new HardwareEngine(transport, protocol);

            await Assert.ThrowsAsync<OperationCanceledException>(
                () => engine.SendAsync(new HardwareOperation { Operation = "ping" }, 100));
        }

        [Fact]
        public async Task SendAsync_MissingConfiguredPort_ReturnsFailedResponse()
        {
            var transport = new FakeTransport();
            var protocol = new FakeProtocol();
            var engine = new HardwareEngine(transport, protocol);

            var response = await engine.SendAsync(new HardwareOperation { Operation = "ping" }, 100);

            Assert.False(response.Success);
            Assert.Contains("PortName", response.Status);
        }

        [Fact]
        public async Task SendAsync_NullOperation_Throws()
        {
            var engine = new HardwareEngine(new FakeTransport(), new FakeProtocol());

            await Assert.ThrowsAsync<ArgumentNullException>(() => engine.SendAsync(null, 100));
            await Assert.ThrowsAsync<ArgumentNullException>(() => engine.SendAsync("COM1", null, 100));
        }

        private sealed class FakeProtocol : IHardwareProtocol
        {
            private readonly List<string> _events;

            public FakeProtocol()
                : this(new List<string>())
            {
            }

            public FakeProtocol(List<string> events)
            {
                _events = events;
                Config = new SerialConfig
                {
                    BaudRate = 9600,
                    Parity = Parity.None,
                    DataBits = 8,
                    StopBits = StopBits.One
                };
            }

            public ControllerModel Model => ControllerModel.ControllerRaw;

            public SerialConfig PortConfig => Config;

            public SerialConfig Config { get; set; }

            public byte[] Command { get; set; } = new byte[] { 0x01 };

            public byte[] BuildCommand(HardwareOperation op)
            {
                _events.Add("Build");
                return Command;
            }

            public HardwareResponse ParseResponse(byte[] reply, HardwareOperation op)
            {
                _events.Add("Parse");
                return new HardwareResponse
                {
                    Success = true,
                    Status = "Parsed",
                    RawBytes = reply
                };
            }
        }

        private sealed class FakeTransport : ICommTransport
        {
            private readonly List<string> _events;

            public FakeTransport()
                : this(new List<string>())
            {
            }

            public FakeTransport(List<string> events)
            {
                _events = events;
            }

            public SerialConfig PortInfo { get; private set; }

            public HardwareLogHandler Log { get; set; }

            public string PortName { get; set; }

            public bool IsOpen { get; set; }

            public byte[] Reply { get; set; }

            public byte[] Written { get; private set; }

            public string OpenedPort { get; private set; }

            public int ReadTimeoutMs { get; private set; }

            public int QuietPeriodMs { get; private set; }

            public Exception ExceptionToThrow { get; set; }

            public void Configure(SerialConfig cfg)
            {
                _events.Add("Configure");
                PortInfo = cfg;
            }

            public Task<ICommTransport> Open(string portName, CancellationToken ct = default)
            {
                _events.Add("Open:" + portName);
                OpenedPort = portName;
                PortName = portName;
                IsOpen = true;
                return Task.FromResult<ICommTransport>(this);
            }

            public Task<ICommTransport> Open(CancellationToken ct = default)
            {
                return Open(PortName, ct);
            }

            public Task Close()
            {
                IsOpen = false;
                return Task.CompletedTask;
            }

            public Task Write(string text, CancellationToken ct = default)
            {
                throw new NotSupportedException();
            }

            public Task Write(byte[] data, int offset = 0, int count = -1, CancellationToken ct = default)
            {
                _events.Add("Write");
                if(ExceptionToThrow != null)
                    throw ExceptionToThrow;

                if(count < 0)
                    count = data.Length - offset;

                Written = new byte[count];
                Array.Copy(data, offset, Written, 0, count);
                return Task.CompletedTask;
            }

            public Task<string> ReadLine(int timeoutMs = 2000, CancellationToken ct = default)
            {
                throw new NotSupportedException();
            }

            public Task<byte[]> ReadExact(int length, int timeoutMs = 2000, CancellationToken ct = default)
            {
                throw new NotSupportedException();
            }

            public Task<byte[]> ReadAll(int timeoutMs = 2000, int quietPeriodMs = 100, CancellationToken ct = default)
            {
                _events.Add("ReadAll");
                ReadTimeoutMs = timeoutMs;
                QuietPeriodMs = quietPeriodMs;
                return Task.FromResult(Reply);
            }
        }
    }
}
