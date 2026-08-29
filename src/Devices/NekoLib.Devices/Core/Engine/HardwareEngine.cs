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
    /// <see cref="ICommTransport"/> (serial port, TCP stream, named pipe, or test double).
    /// 
    /// This engine handles:
    /// <list type="bullet">
    ///     <item>Applying the protocol's communication configuration</item>
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
    /// The caller retains transport ownership and must dispose a disposable
    /// concrete transport; the engine never closes it except when
    /// <see cref="CloseTransportOnNoResponse"/> is enabled for an operation boundary.
    /// </summary>
    public sealed class HardwareEngine
    {
        private readonly ICommTransport _transport;
        private readonly IHardwareProtocol _protocol;
        private readonly SemaphoreSlim _operationGate = new SemaphoreSlim(1, 1);
        private HardwareLogHandler? _log;

        /// <summary>
        /// When enabled, the engine closes the transport after an operation that
        /// received no bytes, so a late reply cannot be delivered as the next
        /// operation's response. The next send reopens, and opening clears the
        /// receive buffer.
        ///
        /// Off by default: a protocol with legitimate fire-and-forget commands, or
        /// one that receives unsolicited device traffic, would reconnect needlessly.
        /// When enabled, a failed reconnection surfaces as the next operation's
        /// failure instead of a missing response.
        /// </summary>
        public bool CloseTransportOnNoResponse { get; set; }

        /// <summary>
        /// Logger for all engine, transport, and protocol operations.
        /// When set, applies automatically to transport and any protocol implementing
        /// <see cref="IProtocolWithLogging"/>.
        /// </summary>
        public HardwareLogHandler? Log
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
        /// <param name="transport">Transport layer (serial, TCP, named pipe, or virtual).</param>
        /// <param name="protocol">Protocol implementation for the target controller.</param>
        /// <exception cref="ArgumentNullException">A dependency is <c>null</c>.</exception>
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
        /// Sends a hardware operation using the configured endpoint in the protocol's
        /// <see cref="SerialConfig"/>. 
        /// </summary>
        /// <param name="op">The protocol-defined operation to execute.</param>
        /// <param name="timeout">
        /// Receive budget in milliseconds. It bounds only the reply read; establishing
        /// the connection and writing the frame are bounded by the transport instead.
        /// </param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>
        /// A parsed response. Non-cancellation transport, engine, and protocol
        /// failures are converted to an unsuccessful response with
        /// <see cref="HardwareResponse.Failure"/> evidence.
        /// </returns>
        /// <exception cref="ArgumentNullException"><paramref name="op"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeout"/> is negative.</exception>
        /// <exception cref="OperationCanceledException"><paramref name="ct"/> is cancelled.</exception>
        public async Task<HardwareResponse> SendAsync(
            HardwareOperation op,
            int timeout,
            CancellationToken ct = default)
        {
            if(op == null)
                throw new ArgumentNullException(nameof(op));

            if(timeout < 0)
                throw new ArgumentOutOfRangeException(nameof(timeout), "Timeout cannot be negative.");

            return await ExecuteSerialized(null, op, timeout, ct).ConfigureAwait(false);
        }

        /// <summary>
        /// Sends a hardware operation using a specific transport endpoint
        /// instead of relying on the protocol's configured one.
        /// </summary>
        /// <param name="port">Endpoint to use (for example, "COM7" or "tcp://127.0.0.1:5001").</param>
        /// <param name="op">Protocol operation.</param>
        /// <param name="timeout">
        /// Receive budget in milliseconds. It bounds only the reply read; establishing
        /// the connection and writing the frame are bounded by the transport instead.
        /// </param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>
        /// A parsed response. Non-cancellation transport, engine, and protocol
        /// failures are converted to an unsuccessful response with
        /// <see cref="HardwareResponse.Failure"/> evidence.
        /// </returns>
        /// <exception cref="ArgumentException"><paramref name="port"/> is blank.</exception>
        /// <exception cref="ArgumentNullException"><paramref name="op"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeout"/> is negative.</exception>
        /// <exception cref="OperationCanceledException"><paramref name="ct"/> is cancelled.</exception>
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

            return await ExecuteSerialized(port, op, timeout, ct).ConfigureAwait(false);
        }

        private async Task<HardwareResponse> ExecuteSerialized(
            string? port,
            HardwareOperation op,
            int timeout,
            CancellationToken ct)
        {
            await _operationGate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                return await ExecuteCore(port, op, timeout, ct).ConfigureAwait(false);
            }
            finally
            {
                _operationGate.Release();
            }
        }

        private async Task<HardwareResponse> ExecuteCore(
            string? explicitPort,
            HardwareOperation op,
            int timeout,
            CancellationToken ct)
        {
            var sw = Stopwatch.StartNew();
            Log?.Invoke(LogLevel.Info, $"[{_protocol.Model}] Starting '{op.Operation}'");

            try
            {
                var cfg = _protocol.PortConfig;

                Log?.Invoke(LogLevel.Debug, $"[{_protocol.Model}] Applying transport config");

                // The protocol owns its configuration: hand the transport a copy so
                // an operation can never rewrite it.
                _transport.Configure(CopyOf(cfg));

                // Resolution order: the explicit endpoint, then the protocol config,
                // then whatever endpoint the transport was constructed with.
                var endpoint = !string.IsNullOrWhiteSpace(explicitPort)
                    ? explicitPort
                    : !string.IsNullOrWhiteSpace(cfg.PortName)
                        ? cfg.PortName
                        : _transport.PortName;

                if(string.IsNullOrWhiteSpace(endpoint))
                {
                    throw new InvalidOperationException(
                        $"Protocol {_protocol.Model} did not define SerialConfig.PortName or a transport endpoint");
                }

                var resolvedEndpoint = endpoint!;
                Log?.Invoke(LogLevel.Info, $"[{_protocol.Model}] Opening endpoint '{resolvedEndpoint}'");
                if(!_transport.IsOpen)
                {
                    await _transport.Open(resolvedEndpoint, ct).ConfigureAwait(false);
                }
                else
                {
                    EnsureTransportOpenOnExpectedPort(resolvedEndpoint);
                    Log?.Invoke(LogLevel.Info, $"[{_protocol.Model}] Endpoint is already open '{resolvedEndpoint}'");
                }

                Log?.Invoke(LogLevel.Debug, $"[{_protocol.Model}] Building command frame");
                var frame = _protocol.BuildCommand(op);

                Log?.Invoke(LogLevel.Raw, $"[{_protocol.Model}] TX → {LogUtil.Hex(frame)}");
                await _transport.Write(frame, 0, frame.Length, ct).ConfigureAwait(false);

                Log?.Invoke(LogLevel.Debug, $"[{_protocol.Model}] Awaiting response…");
                var rspBytes = await _transport.ReadAll(timeout, 50, ct).ConfigureAwait(false);

                if(rspBytes == null && CloseTransportOnNoResponse)
                    await CloseForCleanBoundary().ConfigureAwait(false);

                Log?.Invoke(LogLevel.Raw,
                    rspBytes != null
                        ? $"[{_protocol.Model}] RX ← {LogUtil.Hex(rspBytes)}"
                        : $"[{_protocol.Model}] RX ← <null>");

                var parsed = _protocol.ParseResponse(rspBytes, op);
                if(parsed == null)
                {
                    throw new InvalidOperationException(
                        $"Protocol {_protocol.Model} returned a null response.");
                }

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
                    Failure = ex,
                    Request = op,
                    Elapsed = sw.Elapsed
                };
            }
        }

        private async Task CloseForCleanBoundary()
        {
            Log?.Invoke(
                LogLevel.Info,
                $"[{_protocol.Model}] No response; closing the transport so the next " +
                "operation cannot inherit a late reply");

            try
            {
                await _transport.Close().ConfigureAwait(false);
            }
            catch(OperationCanceledException)
            {
                throw;
            }
            catch(Exception ex)
            {
                // Failing to close is not this operation's outcome; the next one will
                // report whatever state the transport is actually in.
                Log?.Invoke(LogLevel.Error, $"[{_protocol.Model}] CLOSE FAILED: {ex.Message}");
            }
        }

        private static SerialConfig CopyOf(SerialConfig source)
        {
            return new SerialConfig
            {
                BaudRate = source.BaudRate,
                Parity = source.Parity,
                DataBits = source.DataBits,
                StopBits = source.StopBits,
                Handshake = source.Handshake,
                DtrEnable = source.DtrEnable,
                RtsEnable = source.RtsEnable,
                ReadTimeout = source.ReadTimeout,
                WriteTimeout = source.WriteTimeout,
                NewLine = source.NewLine,
                PortName = source.PortName
            };
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
