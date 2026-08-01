using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NekoLib.Devices.Core.Abstractions;
using NekoLib.Devices.Core.Engine;
using NekoLib.Devices.Core.Protocols;
using NekoLib.Devices.Core.Transport;

namespace NekoLib.Devices.RuntimeTests.Com0Com
{
    internal static class Program
    {
        private const int ExchangeTimeoutMs = 1500;

        private static async Task<int> Main(string[] args)
        {
            var pcbAPort = ReadOption(args, "--pcb-a", "COM19");
            var pcbBPort = ReadOption(args, "--pcb-b", "COM20");

            try
            {
                RequirePort(pcbAPort);
                RequirePort(pcbBPort);

                Console.WriteLine($"NekoLib.Devices serial parity: PCB-A={pcbAPort}, PCB-B={pcbBPort}");
                await VerifyPcbA(pcbAPort).ConfigureAwait(false);
                await VerifyPcbB(pcbBPort).ConfigureAwait(false);
                Console.WriteLine("PASS: real COM-port transport and both independent protocol oracles succeeded.");
                return 0;
            }
            catch(Exception ex)
            {
                Console.Error.WriteLine("FAIL: " + ex);
                return 1;
            }
        }

        private static async Task VerifyPcbA(string portName)
        {
            Console.WriteLine("PCB-A: open/configuration, timeout, cancellation, framing, and reopen");
            var config = CreateSerialConfig(portName, "\r\n");

            using (var transport = new SerialCommTransport(portName))
            {
                transport.Configure(config);
                await transport.Open().ConfigureAwait(false);

                var openInfo = transport.PortInfo;
                Require(openInfo.Handshake == Handshake.None, "PCB-A handshake was not applied.");
                Require(openInfo.DtrEnable, "PCB-A DTR was not enabled.");
                Require(openInfo.RtsEnable, "PCB-A RTS was not enabled.");

                Require(await transport.ReadAll(120, 20).ConfigureAwait(false) == null,
                    "ReadAll should return null when no bytes arrive.");
                Require(await transport.ReadExact(1, 120).ConfigureAwait(false) == null,
                    "ReadExact should return null when no bytes arrive.");

                using (var cancellation = new CancellationTokenSource(120))
                {
                    var canceled = false;
                    try
                    {
                        await transport.ReadLine(2000, cancellation.Token).ConfigureAwait(false);
                    }
                    catch(OperationCanceledException)
                    {
                        canceled = true;
                    }

                    Require(canceled,
                        "ReadLine did not observe cancellation with an infinite configured SerialPort timeout.");
                }

                await transport.Write("SYS PING;").ConfigureAwait(false);
                Require(
                    string.Equals(await transport.ReadLine(ExchangeTimeoutMs).ConfigureAwait(false), "OK PONG;", StringComparison.Ordinal),
                    "PCB-A ReadLine PING did not return the expected protocol response.");

                await transport.Write(Encoding.ASCII.GetBytes("SYS PING;")).ConfigureAwait(false);
                var exact = await transport.ReadExact(10, ExchangeTimeoutMs).ConfigureAwait(false);
                Require(
                    exact != null && string.Equals(Encoding.ASCII.GetString(exact), "OK PONG;\r\n", StringComparison.Ordinal),
                    "PCB-A ReadExact PING did not preserve the exact response bytes.");

                await transport.Write("SYS ID;").ConfigureAwait(false);
                var all = await transport.ReadAll(ExchangeTimeoutMs, 50).ConfigureAwait(false);
                Require(
                    all != null && Encoding.GetEncoding(28591).GetString(all).StartsWith("OK PCB-A", StringComparison.Ordinal),
                    "PCB-A ReadAll ID did not return the expected protocol response.");

                await transport.Close().ConfigureAwait(false);
                Require(!transport.IsOpen, "PCB-A transport remained open after Close.");
                await transport.Open().ConfigureAwait(false);

                var protocol = new ProtocolRaw(config, Encoding.GetEncoding(28591));
                var engine = new HardwareEngine(transport, protocol);
                var operation = new HardwareOperation { Operation = "SYS PING" };
                operation.Args["RawText"] = "SYS PING;";

                var response = await engine.SendAsync(operation, ExchangeTimeoutMs).ConfigureAwait(false);
                Require(response.Success, "PCB-A HardwareEngine PING failed: " + response.Status);
                Require(
                    string.Equals(response.RawText, "OK PONG;\r\n", StringComparison.Ordinal),
                    "PCB-A HardwareEngine PING returned an unexpected payload.");

                await transport.Close().ConfigureAwait(false);
            }
        }

        private static async Task VerifyPcbB(string portName)
        {
            Console.WriteLine("PCB-B: HardwareEngine binary PING, sequence, opcode, and CRC");
            var config = CreateSerialConfig(portName, "\r\n");

            using (var transport = new SerialCommTransport(portName))
            {
                var protocol = new ProtocolRaw(config);
                var engine = new HardwareEngine(transport, protocol);
                const byte sequence = 0x2A;
                var operation = new HardwareOperation { Operation = "PING" };
                operation.Args["RawBytes"] = EncodePcbBFrame(sequence, 0x10);

                var response = await engine.SendAsync(operation, ExchangeTimeoutMs).ConfigureAwait(false);
                Require(response.Success, "PCB-B HardwareEngine PING failed: " + response.Status);
                ValidatePcbBPong(response.RawBytes, sequence);

                await transport.Close().ConfigureAwait(false);
            }
        }

        private static SerialConfig CreateSerialConfig(string portName, string newLine)
        {
            return new SerialConfig
            {
                BaudRate = 115200,
                Parity = Parity.None,
                DataBits = 8,
                StopBits = StopBits.One,
                Handshake = Handshake.None,
                DtrEnable = true,
                RtsEnable = true,
                ReadTimeout = SerialPort.InfiniteTimeout,
                WriteTimeout = 2000,
                NewLine = newLine,
                PortName = portName
            };
        }

        private static byte[] EncodePcbBFrame(byte sequence, byte command)
        {
            var frame = new byte[] { 0xA5, 0x5A, 0x02, sequence, command, 0x00, 0x00 };
            var crc = ComputeCrc(frame, 2, 3);
            frame[5] = (byte)(crc >> 8);
            frame[6] = (byte)crc;
            return frame;
        }

        private static void ValidatePcbBPong(byte[]? frame, byte expectedSequence)
        {
            Require(frame != null, "PCB-B returned no frame.");
            Require(frame!.Length == 7, "PCB-B PONG frame length was not 7 bytes.");
            Require(frame[0] == 0xA5 && frame[1] == 0x5A, "PCB-B sync bytes were invalid.");
            Require(frame[2] == 0x02, "PCB-B body length was invalid.");
            Require(frame[3] == expectedSequence, "PCB-B sequence did not match the request.");
            Require(frame[4] == 0x90, "PCB-B response opcode was not PONG (0x90).");

            var expectedCrc = ComputeCrc(frame, 2, 3);
            var actualCrc = (ushort)((frame[5] << 8) | frame[6]);
            Require(actualCrc == expectedCrc, "PCB-B response CRC-16/CCITT-FALSE was invalid.");
        }

        private static ushort ComputeCrc(byte[] data, int offset, int count)
        {
            ushort crc = 0xFFFF;
            for (var i = offset; i < offset + count; i++)
            {
                crc ^= (ushort)(data[i] << 8);
                for (var bit = 0; bit < 8; bit++)
                    crc = (ushort)((crc & 0x8000) != 0 ? (crc << 1) ^ 0x1021 : crc << 1);
            }

            return crc;
        }

        private static string ReadOption(IReadOnlyList<string> args, string name, string fallback)
        {
            for (var i = 0; i < args.Count - 1; i++)
            {
                if(string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                    return args[i + 1];
            }

            return fallback;
        }

        private static void RequirePort(string portName)
        {
            var exists = SerialPort.GetPortNames()
                .Any(value => string.Equals(value, portName, StringComparison.OrdinalIgnoreCase));
            Require(exists, $"Required COM port '{portName}' is not installed.");
        }

        private static void Require(bool condition, string message)
        {
            if(!condition)
                throw new InvalidOperationException(message);
        }
    }
}
