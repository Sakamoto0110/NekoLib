using NekoLib.Devices.Core.Abstractions;
using NekoLib.Devices.Core.Transport;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NekoLib.Devices.Core.Engine
{
    /// <summary>
    /// Coordinates communication between a <see cref="IHardwareProtocol"/> and a 
    /// <see cref="ICommTransport"/> (usually a serial port).
    /// 
    /// This engine handles:
    /// <list type="bullet">
    ///     <item>Applying correct protocol serial configuration</item>
    ///     <item>Opening the transport safely</item>
    ///     <item>Sending commands (binary or text)</item>
    ///     <item>Receiving raw replies</item>
    ///     <item>Delegating parsing to the active protocol</item>
    ///     <item>Logging at all stages</item>
    ///     <item>Returning a strongly typed <see cref="HardwareResponse"/></item>
    /// </list>
    ///
    /// The engine does not interpret device-specific logic — 
    /// that is entirely the protocol's responsibility.
    /// </summary>
    public sealed class HardwareEngine
    {
        private readonly ICommTransport _transport;
        private readonly IHardwareProtocol _protocol;
        private HardwareLogHandler _log;

        /// <summary>
        /// Logger for all engine, transport, and protocol operations.
        /// When set, applies automatically to transport and any protocol implementing
        /// <see cref="IProtocolWithLogging"/>.
        /// </summary>
        public HardwareLogHandler Log
        {
            get => _log;
            set
            {
                _log = value;

                // transport always supports logging
                _transport.Log = value;

                // protocol may or may not support it
                if(_protocol is IProtocolWithLogging p)
                    p.Log = value;
            }
        }

        /// <summary>
        /// Creates a new hardware engine for a transport+protocol pair.
        /// </summary>
        /// <param name="transport">Transport layer (serial, TCP, virtual).</param>
        /// <param name="protocol">Protocol implementation for the target controller.</param>
        public HardwareEngine(ICommTransport transport, IHardwareProtocol protocol)
        {
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            _protocol = protocol ?? throw new ArgumentNullException(nameof(protocol));

            // forward logs by default (engine logs everything)
            _transport.Log = (lvl, msg) => Log?.Invoke(lvl, msg);
            if(_protocol is IProtocolWithLogging p)
                p.Log = (lvl, msg) => Log?.Invoke(lvl, msg);
        }

        /// <summary>
        /// Sends a hardware operation using the configured port in the protocol's
        /// <see cref="SerialConfig"/>. 
        /// </summary>
        /// <param name="op">The protocol-defined operation to execute.</param>
        /// <param name="timeout">Overall timeout for receiving a reply.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>A parsed <see cref="HardwareResponse"/> instance.</returns>
        public async Task<HardwareResponse> SendAsync(
            HardwareOperation op,
            int timeout,
            CancellationToken ct = default)
        {
            if(op == null)
                throw new ArgumentNullException(nameof(op));

            if(timeout < 0)
                throw new ArgumentOutOfRangeException(nameof(timeout), "Timeout cannot be negative.");

            var cfg = _protocol.PortConfig;

            var sw = Stopwatch.StartNew();
            Log?.Invoke(LogLevel.Info, $"[{_protocol.Model}] Starting '{op.Operation}'");

            try
            {
                // ----------------------------------------
                // 1. Configure transport
                // ----------------------------------------
                Log?.Invoke(LogLevel.Debug, $"[{_protocol.Model}] Applying serial config");
                _transport.Configure(cfg);

                // ----------------------------------------
                // 2. Open using configured port name
                // ----------------------------------------
                if(string.IsNullOrWhiteSpace(cfg.PortName))
                    throw new InvalidOperationException(
                        $"Protocol {_protocol.Model} did not define a SerialConfig.PortName");

                Log?.Invoke(LogLevel.Info, $"[{_protocol.Model}] Opening port '{cfg.PortName}'");
                if(!_transport.IsOpen)
                {
                    await _transport.Open(cfg.PortName, ct);
                }
                else
                {
                    EnsureTransportOpenOnExpectedPort(cfg.PortName);
                    Log?.Invoke(LogLevel.Info, $"[{_protocol.Model}] Port is already open '{cfg.PortName}'");
                }

                // ----------------------------------------
                // 3. Build protocol-specific frame
                // ----------------------------------------
                Log?.Invoke(LogLevel.Debug, $"[{_protocol.Model}] Building command frame");
                var frame = _protocol.BuildCommand(op);

                Log?.Invoke(LogLevel.Raw,
                    $"[{_protocol.Model}] TX → {LogUtil.Hex(frame)}");

                // ----------------------------------------
                // 4. Send frame
                // ----------------------------------------
                await _transport.Write(frame, 0, frame.Length, ct);

                // ----------------------------------------
                // 5. Await reply
                // ----------------------------------------
                Log?.Invoke(LogLevel.Debug, $"[{_protocol.Model}] Awaiting response…");

                var rspBytes = await _transport.ReadAll(timeout, 50, ct);

                Log?.Invoke(LogLevel.Raw,
                    rspBytes != null
                        ? $"[{_protocol.Model}] RX ← {LogUtil.Hex(rspBytes)}"
                        : $"[{_protocol.Model}] RX ← <null>");

                // ----------------------------------------
                // 6. Let protocol parse it
                // ----------------------------------------
                var parsed = _protocol.ParseResponse(rspBytes, op);
                if(parsed == null)
                    throw new InvalidOperationException(
                        $"Protocol {_protocol.Model} returned a null response.");

                parsed.Request = parsed.Request ?? op;
                parsed.Elapsed = sw.Elapsed;

                Log?.Invoke(LogLevel.Info,
                    $"[{_protocol.Model}] Completed → Status={parsed.Status}, Success={parsed.Success}");

                return parsed;
            }
            catch(OperationCanceledException)
            {
                Log?.Invoke(LogLevel.Error, $"[{_protocol.Model}] CANCELED");
                throw;
            }
            catch(Exception ex)
            {
                Log?.Invoke(LogLevel.Error, $"[{_protocol.Model}] ERROR: {ex}");

                return new HardwareResponse
                {
                    Success = false,
                    Status = ex.Message,
                    Request = op,
                    Elapsed = sw.Elapsed
                };
            }
        }

        /// <summary>
        /// Sends a hardware operation using a specific port name
        /// instead of relying on the protocol's configured one.
        /// </summary>
        /// <param name="port">Port name to use (e.g., "COM7").</param>
        /// <param name="op">Protocol operation.</param>
        /// <param name="timeout">Receive timeout.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>A parsed <see cref="HardwareResponse"/> object.</returns>
        public async Task<HardwareResponse> SendAsync(
            string port,
            HardwareOperation op,
            int timeout,
            CancellationToken ct = default)
        {
            if(string.IsNullOrWhiteSpace(port))
                throw new ArgumentException("Port name is required.", nameof(port));

            if(op == null)
                throw new ArgumentNullException(nameof(op));

            if(timeout < 0)
                throw new ArgumentOutOfRangeException(nameof(timeout), "Timeout cannot be negative.");

            var cfg = _protocol.PortConfig;

            var sw = Stopwatch.StartNew();
            Log?.Invoke(LogLevel.Info, $"[{_protocol.Model}] Starting '{op.Operation}'");

            try
            {
                // ----------------------------------------
                // 1. Configure transport
                // ----------------------------------------
                Log?.Invoke(LogLevel.Debug, $"[{_protocol.Model}] Applying serial config");
                _transport.Configure(cfg);

                // ----------------------------------------
                // 2. Open port explicitly
                // ----------------------------------------
                Log?.Invoke(LogLevel.Info, $"[{_protocol.Model}] Opening port '{port}'");
                await _transport.Open(port, ct);

                // ----------------------------------------
                // 3. Build frame
                // ----------------------------------------
                Log?.Invoke(LogLevel.Debug, $"[{_protocol.Model}] Building command frame");
                var frame = _protocol.BuildCommand(op);

                Log?.Invoke(LogLevel.Raw,
                    $"[{_protocol.Model}] TX → {LogUtil.Hex(frame)}");

                await _transport.Write(frame, 0, frame.Length, ct);

                // ----------------------------------------
                // 4. Receive reply
                // ----------------------------------------
                Log?.Invoke(LogLevel.Debug, $"[{_protocol.Model}] Awaiting response…");

                var rspBytes = await _transport.ReadAll(timeout, 50, ct);

                Log?.Invoke(LogLevel.Raw,
                    rspBytes != null
                        ? $"[{_protocol.Model}] RX ← {LogUtil.Hex(rspBytes)}"
                        : $"[{_protocol.Model}] RX ← <null>");

                // ----------------------------------------
                // 5. Parse via protocol
                // ----------------------------------------
                var parsed = _protocol.ParseResponse(rspBytes, op);
                if(parsed == null)
                    throw new InvalidOperationException(
                        $"Protocol {_protocol.Model} returned a null response.");

                parsed.Request = parsed.Request ?? op;
                parsed.Elapsed = sw.Elapsed;

                Log?.Invoke(LogLevel.Info,
                    $"[{_protocol.Model}] Completed → Status={parsed.Status}, Success={parsed.Success}");

                return parsed;
            }
            catch(OperationCanceledException)
            {
                Log?.Invoke(LogLevel.Error, $"[{_protocol.Model}] CANCELED");
                throw;
            }
            catch(Exception ex)
            {
                Log?.Invoke(LogLevel.Error, $"[{_protocol.Model}] ERROR: {ex}");

                return new HardwareResponse
                {
                    Success = false,
                    Status = ex.Message,
                    Request = op,
                    Elapsed = sw.Elapsed
                };
            }
        }

        private void EnsureTransportOpenOnExpectedPort(string expectedPortName)
        {
            if(string.Equals(_transport.PortName, expectedPortName, StringComparison.OrdinalIgnoreCase))
                return;

            throw new InvalidOperationException(
                $"Transport is already open as '{_transport.PortName}' and cannot execute on '{expectedPortName}'.");
        }
    }
}
