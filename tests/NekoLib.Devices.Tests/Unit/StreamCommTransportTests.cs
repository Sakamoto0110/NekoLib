using System;
using System.IO;
using System.IO.Pipes;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using NekoLib.Devices.Core.Abstractions;
using NekoLib.Devices.Core.Transport;
using Xunit;

namespace NekoLib.Devices.Tests.Unit
{
    public class StreamCommTransportTests
    {
        [Fact]
        public void Configure_SerialOnlyFields_ArePreservedInSnapshot()
        {
            using (var transport = new TcpCommTransport("127.0.0.1", 5001))
            {
                var cfg = StreamConfig("\r\n");
                cfg.Handshake = System.IO.Ports.Handshake.RequestToSend;
                cfg.DtrEnable = true;
                cfg.RtsEnable = true;

                transport.Configure(cfg);
                var snapshot = transport.PortInfo;

                Assert.Equal(System.IO.Ports.Handshake.RequestToSend, snapshot.Handshake);
                Assert.True(snapshot.DtrEnable);
                Assert.True(snapshot.RtsEnable);
            }
        }

        [Fact]
        public async Task TcpTransport_FragmentedExchange_PreservesFramingAndExcessBytes()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();

            try
            {
                var port = ((IPEndPoint)listener.LocalEndpoint).Port;
                using (var transport = new TcpCommTransport("127.0.0.1", port))
                {
                    var cfg = StreamConfig(";\r\n");
                    transport.Configure(cfg);
                    var acceptTask = listener.AcceptTcpClientAsync();

                    await transport.Open();
                    using (var client = await acceptTask)
                    using (var server = client.GetStream())
                    {
                        Assert.Equal($"tcp://127.0.0.1:{port}", transport.PortName);
                        Assert.Equal(transport.PortName, cfg.PortName);

                        await transport.Write("PING;");
                        Assert.Equal("PING;", Encoding.ASCII.GetString(await ReadExact(server, 5)));

                        var response = Encoding.ASCII.GetBytes("OK PONG;\r\nSECOND");
                        await server.WriteAsync(response, 0, response.Length);
                        await server.FlushAsync();

                        Assert.Equal("OK PONG", await transport.ReadLine(1000));
                        Assert.Equal("SECOND", Encoding.ASCII.GetString(await transport.ReadExact(6, 1000)));
                    }

                    await transport.Close();
                    Assert.False(transport.IsOpen);
                }
            }
            finally
            {
                listener.Stop();
            }
        }

        [Fact]
        public async Task TcpTransport_ReadAll_FragmentedResponseWaitsForQuietPeriod()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();

            try
            {
                var port = ((IPEndPoint)listener.LocalEndpoint).Port;
                using (var transport = new TcpCommTransport($"tcp://127.0.0.1:{port}"))
                {
                    transport.Configure(StreamConfig("\r\n"));
                    var acceptTask = listener.AcceptTcpClientAsync();

                    await transport.Open();
                    using (var client = await acceptTask)
                    using (var server = client.GetStream())
                    {
                        await server.WriteAsync(new byte[] { 1, 2 }, 0, 2);
                        await server.FlushAsync();
                        await Task.Delay(30);
                        await server.WriteAsync(new byte[] { 3, 4 }, 0, 2);
                        await server.FlushAsync();

                        Assert.Equal(
                            new byte[] { 1, 2, 3, 4 },
                            await transport.ReadAll(1000, 80));
                    }
                }
            }
            finally
            {
                listener.Stop();
            }
        }

        [Fact]
        public async Task NamedPipeTransport_FragmentedExchange_PreservesFramingAndExcessBytes()
        {
            var pipeName = "nekolib-devices-" + Guid.NewGuid().ToString("N");
            using (var server = CreatePipeServer(pipeName))
            using (var transport = new NamedPipeCommTransport(
                $@"pipe://\\.\pipe\{pipeName}"))
            {
                var cfg = StreamConfig(";\r\n");
                transport.Configure(cfg);
                var acceptTask = Task.Run(() => server.WaitForConnection());

                await AwaitStage(transport.Open(), "client open");
                await AwaitStage(acceptTask, "server accept");

                Assert.Equal($@"\\.\pipe\{pipeName}", transport.PortName);
                Assert.Equal(transport.PortName, cfg.PortName);

                var serverReadTask = ReadExact(server, 5);
                await AwaitStage(transport.Write("PING;"), "client write");
                Assert.Equal(
                    "PING;",
                    Encoding.ASCII.GetString(await AwaitStage(serverReadTask, "server read")));

                var response = Encoding.ASCII.GetBytes("OK PONG;\r\nSECOND");
                await AwaitStage(server.WriteAsync(response, 0, response.Length), "server write");
                await AwaitStage(server.FlushAsync(), "server flush");

                Assert.Equal("OK PONG", await AwaitStage(transport.ReadLine(1000), "client line"));
                Assert.Equal(
                    "SECOND",
                    Encoding.ASCII.GetString(
                        await AwaitStage(transport.ReadExact(6, 1000), "client excess")));

                await AwaitStage(transport.Close(), "client close");
            }
        }

        [Fact]
        public async Task NamedPipeTransport_ReadAll_FragmentedResponseWaitsForQuietPeriod()
        {
            var pipeName = "nekolib-devices-" + Guid.NewGuid().ToString("N");
            using (var server = CreatePipeServer(pipeName))
            using (var transport = new NamedPipeCommTransport(pipeName))
            {
                transport.Configure(StreamConfig("\r\n"));
                var acceptTask = Task.Run(() => server.WaitForConnection());

                await transport.Open();
                await acceptTask;

                await server.WriteAsync(new byte[] { 1, 2 }, 0, 2);
                await server.FlushAsync();
                await Task.Delay(30);
                await server.WriteAsync(new byte[] { 3, 4 }, 0, 2);
                await server.FlushAsync();

                Assert.Equal(
                    new byte[] { 1, 2, 3, 4 },
                    await transport.ReadAll(1000, 80));
            }
        }

        [Fact]
        public async Task StreamTransports_ReadTimeout_ReturnsNullWithoutClosingConnection()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();

            try
            {
                var port = ((IPEndPoint)listener.LocalEndpoint).Port;
                using (var transport = new TcpCommTransport("127.0.0.1", port))
                {
                    transport.Configure(StreamConfig("\r\n"));
                    var acceptTask = listener.AcceptTcpClientAsync();

                    await transport.Open();
                    using (var client = await acceptTask)
                    using (var server = client.GetStream())
                    {
                        Assert.Null(await transport.ReadAll(50, 10));
                        Assert.True(transport.IsOpen);

                        await server.WriteAsync(new byte[] { 0x42 }, 0, 1);
                        await server.FlushAsync();

                        Assert.Equal(
                            new byte[] { 0x42 },
                            await transport.ReadExact(1, 1000));
                    }
                }
            }
            finally
            {
                listener.Stop();
            }
        }

        [Fact]
        public void StreamTransports_InvalidEndpoints_ThrowClearErrors()
        {
            Assert.Throws<ArgumentException>(() => new TcpCommTransport("not-an-endpoint"));
            Assert.Throws<ArgumentOutOfRangeException>(() => new TcpCommTransport("localhost", 0));
            Assert.Throws<ArgumentException>(() => new NamedPipeCommTransport(@"\\.\wrong\name"));
        }

        [Fact]
        public async Task StreamTransports_DisposedMethodsThrowObjectDisposedException()
        {
            var tcp = new TcpCommTransport("127.0.0.1", 5001);
            tcp.Dispose();

            Assert.Throws<ObjectDisposedException>(() => tcp.Configure(StreamConfig("\r\n")));
            await Assert.ThrowsAsync<ObjectDisposedException>(() => tcp.Open());
            await Assert.ThrowsAsync<ObjectDisposedException>(() => tcp.Write(new byte[] { 1 }));
            await Assert.ThrowsAsync<ObjectDisposedException>(() => tcp.ReadAll());
            await Assert.ThrowsAsync<ObjectDisposedException>(() => tcp.Close());
        }

        private static NamedPipeServerStream CreatePipeServer(string pipeName)
        {
            return new NamedPipeServerStream(
                pipeName,
                PipeDirection.InOut,
                1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous | PipeOptions.WriteThrough);
        }

        private static SerialConfig StreamConfig(string newLine)
        {
            return new SerialConfig
            {
                BaudRate = 115200,
                Parity = System.IO.Ports.Parity.None,
                DataBits = 8,
                StopBits = System.IO.Ports.StopBits.One,
                ReadTimeout = 50,
                WriteTimeout = 100,
                NewLine = newLine
            };
        }

        private static async Task<byte[]> ReadExact(Stream stream, int length)
        {
            var result = new byte[length];
            int offset = 0;

            while(offset < result.Length)
            {
                int read = await stream.ReadAsync(result, offset, result.Length - offset);
                if(read <= 0)
                    throw new EndOfStreamException();

                offset += read;
            }

            return result;
        }

        private static async Task AwaitStage(Task task, string stage)
        {
            var completed = await Task.WhenAny(task, Task.Delay(3000));
            if(!ReferenceEquals(completed, task))
                throw new TimeoutException($"Timed out during {stage}.");

            await task;
        }

        private static async Task<T> AwaitStage<T>(Task<T> task, string stage)
        {
            await AwaitStage((Task)task, stage);
            return await task;
        }
    }
}
