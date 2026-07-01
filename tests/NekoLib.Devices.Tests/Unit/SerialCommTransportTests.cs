using System;
using System.IO.Ports;
using System.Threading.Tasks;
using NekoLib.Devices.Core.Abstractions;
using NekoLib.Devices.Core.Transport;
using Xunit;

namespace NekoLib.Devices.Tests.Unit
{
    public class SerialCommTransportTests
    {
        [Fact]
        public async Task Configure_NoExplicitPort_DoesNotPromoteSerialPortDefault()
        {
            using (var transport = new SerialCommTransport())
            {
                var cfg = BasicConfig();

                transport.Configure(cfg);

                Assert.Null(cfg.PortName);
                await Assert.ThrowsAsync<InvalidOperationException>(() => transport.Open());
            }
        }

        [Fact]
        public void Configure_AppliesSerialConfigAndPortName()
        {
            using (var transport = new SerialCommTransport())
            {
                var cfg = BasicConfig();
                cfg.BaudRate = 19200;
                cfg.Parity = Parity.Even;
                cfg.DataBits = 7;
                cfg.StopBits = StopBits.Two;
                cfg.ReadTimeout = 123;
                cfg.WriteTimeout = 456;
                cfg.NewLine = "\n";
                cfg.PortName = "COM42";

                transport.Configure(cfg);
                var info = transport.PortInfo;

                Assert.Equal(19200, info.BaudRate);
                Assert.Equal(Parity.Even, info.Parity);
                Assert.Equal(7, info.DataBits);
                Assert.Equal(StopBits.Two, info.StopBits);
                Assert.Equal(123, info.ReadTimeout);
                Assert.Equal(456, info.WriteTimeout);
                Assert.Equal("\n", info.NewLine);
                Assert.Equal("COM42", info.PortName);
            }
        }

        [Fact]
        public void Configure_ConstructorPort_IsFallbackWhenConfigHasNoPort()
        {
            using (var transport = new SerialCommTransport("COM7"))
            {
                var cfg = BasicConfig();

                transport.Configure(cfg);

                Assert.Equal("COM7", cfg.PortName);
                Assert.Equal("COM7", transport.PortInfo.PortName);
            }
        }

        [Fact]
        public async Task Open_BlankPort_ThrowsBeforeHardwareAccess()
        {
            using (var transport = new SerialCommTransport())
            {
                await Assert.ThrowsAsync<ArgumentException>(() => transport.Open(""));
                await Assert.ThrowsAsync<ArgumentException>(() => transport.Open("   "));
            }
        }

        [Fact]
        public async Task Write_InvalidInput_ThrowsBeforePortStateCheck()
        {
            using (var transport = new SerialCommTransport())
            {
                await Assert.ThrowsAsync<ArgumentNullException>(() => transport.Write((string)null));
                await Assert.ThrowsAsync<ArgumentNullException>(() => transport.Write((byte[])null));
                await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => transport.Write(new byte[2], -1, 1));
                await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => transport.Write(new byte[2], 3, 1));
                await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => transport.Write(new byte[2], 1, 2));
                await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => transport.Write(new byte[2], 0, -2));
            }
        }

        [Fact]
        public async Task ReadMethods_InvalidInput_ThrowsBeforePortStateCheck()
        {
            using (var transport = new SerialCommTransport())
            {
                await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => transport.ReadAll(-1));
                await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => transport.ReadAll(1, -1));
                await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => transport.ReadLine(-1));
                await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => transport.ReadExact(-1));
                await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => transport.ReadExact(1, -1));
            }
        }

        [Fact]
        public void Configure_InvalidBaudRate_Throws()
        {
            using (var transport = new SerialCommTransport())
            {
                var cfg = BasicConfig();
                cfg.BaudRate = 0;

                Assert.Throws<ArgumentOutOfRangeException>(() => transport.Configure(cfg));
            }
        }

        [Theory]
        [InlineData(4)]
        [InlineData(9)]
        public void Configure_InvalidDataBits_Throws(int dataBits)
        {
            using (var transport = new SerialCommTransport())
            {
                var cfg = BasicConfig();
                cfg.DataBits = dataBits;

                Assert.Throws<ArgumentOutOfRangeException>(() => transport.Configure(cfg));
            }
        }

        [Fact]
        public void Configure_StopBitsNone_Throws()
        {
            using (var transport = new SerialCommTransport())
            {
                var cfg = BasicConfig();
                cfg.StopBits = StopBits.None;

                Assert.Throws<ArgumentOutOfRangeException>(() => transport.Configure(cfg));
            }
        }

        [Fact]
        public void Configure_ReadTimeoutBelowInfinite_Throws()
        {
            using (var transport = new SerialCommTransport())
            {
                var cfg = BasicConfig();
                cfg.ReadTimeout = -2;

                Assert.Throws<ArgumentOutOfRangeException>(() => transport.Configure(cfg));
            }
        }

        [Fact]
        public void Configure_WriteTimeoutBelowInfinite_Throws()
        {
            using (var transport = new SerialCommTransport())
            {
                var cfg = BasicConfig();
                cfg.WriteTimeout = -2;

                Assert.Throws<ArgumentOutOfRangeException>(() => transport.Configure(cfg));
            }
        }

        [Fact]
        public async Task Disposed_MethodsThrowObjectDisposedException()
        {
            var transport = new SerialCommTransport();
            transport.Dispose();

            Assert.Throws<ObjectDisposedException>(() => transport.Configure(BasicConfig()));
            await Assert.ThrowsAsync<ObjectDisposedException>(() => transport.Open("COM1"));
            await Assert.ThrowsAsync<ObjectDisposedException>(() => transport.Open());
            await Assert.ThrowsAsync<ObjectDisposedException>(() => transport.Close());
            await Assert.ThrowsAsync<ObjectDisposedException>(() => transport.Write("x"));
            await Assert.ThrowsAsync<ObjectDisposedException>(() => transport.Write(new byte[] { 1 }));
            await Assert.ThrowsAsync<ObjectDisposedException>(() => transport.ReadAll());
            await Assert.ThrowsAsync<ObjectDisposedException>(() => transport.ReadLine());
            await Assert.ThrowsAsync<ObjectDisposedException>(() => transport.ReadExact(1));
        }

        private static SerialConfig BasicConfig()
        {
            return new SerialConfig
            {
                BaudRate = 9600,
                Parity = Parity.None,
                DataBits = 8,
                StopBits = StopBits.One,
                ReadTimeout = 50,
                WriteTimeout = 100
            };
        }
    }
}
