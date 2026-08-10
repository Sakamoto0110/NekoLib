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
    /// <summary>
    /// The original interactive parity run against the independent
    /// NekoPcbEmulator, moved out of <c>Program</c> unchanged.
    /// <para/>
    /// This path is the one with a 2026-08-01 interactive pass, and it is kept
    /// byte-for-byte so that evidence keeps describing what the binary does:
    /// same options, same console lines, same exit codes (<c>0</c> and
    /// <c>1</c>), same checks in the same order. The E3-DEV modes were added
    /// beside it, never on top of it.
    /// <para/>
    /// What makes it worth keeping separate is what it proves: the emulator is
    /// an <b>independent</b> implementation of both wire protocols, in another
    /// repository, with no reference to NekoLib. Nothing the scenario-owned peer
    /// does can replace that, because a peer this project wrote agreeing with a
    /// validator this project wrote is not independent agreement.
    /// </summary>
    internal static class OracleParity
    {
        private const int ExchangeTimeoutMs = 1500;

        public static int Run(string[] args)
        {
            return RunAsync(args).GetAwaiter().GetResult();
        }

        private static async Task<int> RunAsync(string[] args)
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
                operation.Args["RawBytes"] = PcbB.EncodeRequest(sequence, PcbB.CommandPing);

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

        private static void ValidatePcbBPong(byte[]? frame, byte expectedSequence)
        {
            Require(frame != null, "PCB-B returned no frame.");
            Require(frame!.Length == 7, "PCB-B PONG frame length was not 7 bytes.");
            Require(frame[0] == 0xA5 && frame[1] == 0x5A, "PCB-B sync bytes were invalid.");
            Require(frame[2] == 0x02, "PCB-B body length was invalid.");
            Require(frame[3] == expectedSequence, "PCB-B sequence did not match the request.");
            Require(frame[4] == 0x90, "PCB-B response opcode was not PONG (0x90).");

            var expectedCrc = PcbB.Crc(frame, 2, 3);
            var actualCrc = (ushort)((frame[5] << 8) | frame[6]);
            Require(actualCrc == expectedCrc, "PCB-B response CRC-16/CCITT-FALSE was invalid.");
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
