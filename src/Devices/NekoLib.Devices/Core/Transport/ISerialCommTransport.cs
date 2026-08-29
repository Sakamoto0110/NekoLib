using NekoLib.Devices.Core.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NekoLib.Devices.Core.Transport
{
    /// <summary>
    /// Represents a hardware-agnostic asynchronous byte-stream transport.
    /// This abstraction allows any protocol to be executed over:
    /// - Serial ports
    /// - TCP streams
    /// - Named pipes
    /// - Virtual/fake transports for testing
    /// The interface does not transfer ownership and does not itself extend
    /// <see cref="IDisposable"/>; callers must dispose a disposable concrete transport.
    /// </summary>
    public interface ICommTransport
    {
        /// <summary>
        /// Gets a snapshot of the protocol/transport configuration.
        /// Serial transports apply every field. Stream transports apply the endpoint
        /// and newline, and preserve and report the remaining fields — including the
        /// read and write timeouts — without acting on them.
        /// </summary>
        SerialConfig PortInfo { get; }

        /// <summary>
        /// Optional logger injected by <see cref="NekoLib.Devices.Core.Engine.HardwareEngine"/>.
        /// </summary>
        HardwareLogHandler? Log { get; set; }

        /// <summary>
        /// Applies the desired communication parameters to the underlying medium.
        /// Fields that have no meaning for a transport are preserved but ignored.
        /// </summary>
        /// <param name="cfg">Caller-owned configuration to copy; implementations must not write back to it.</param>
        void Configure(SerialConfig cfg);

        /// <summary>
        /// Gets the current endpoint (for example, "COM3",
        /// "tcp://127.0.0.1:5001", or "\\.\pipe\pcb-a").
        /// </summary>
        string PortName { get; }

        /// <summary>
        /// Indicates whether the transport is currently open.
        /// </summary>
        bool IsOpen { get; }

        /// <summary>
        /// Opens the specified transport-specific endpoint, applying previous configuration.
        /// </summary>
        /// <param name="portName">Non-blank serial, TCP, named-pipe, or custom endpoint understood by the implementation.</param>
        /// <param name="ct">Cancellation token for connection admission and establishment.</param>
        /// <returns>This open transport instance.</returns>
        Task<ICommTransport> Open(string portName, CancellationToken ct = default);

        /// <summary>
        /// Opens using the configured <see cref="SerialConfig.PortName"/>.
        /// </summary>
        /// <param name="ct">Cancellation token for connection admission and establishment.</param>
        /// <returns>This open transport instance.</returns>
        Task<ICommTransport> Open(CancellationToken ct = default);

        /// <summary>
        /// Closes the transport.
        /// </summary>
        /// <returns>A task that completes after connection resources and receive state are released.</returns>
        Task Close();

        /// <summary>
        /// Writes an ASCII text command.
        /// </summary>
        /// <param name="text">Non-null text encoded as ASCII.</param>
        /// <param name="ct">Cancellation token for operation admission and writing.</param>
        /// <returns>A task that completes after the bytes are written to the transport.</returns>
        Task Write(string text, CancellationToken ct = default);

        /// <summary>
        /// Writes a binary command.
        /// </summary>
        /// <param name="data">Non-null caller-owned byte buffer.</param>
        /// <param name="offset">Zero-based starting offset.</param>
        /// <param name="count">Bytes to write, or -1 for the remainder of the buffer.</param>
        /// <param name="ct">Cancellation token for operation admission and writing.</param>
        /// <returns>A task that completes after the selected bytes are written.</returns>
        Task Write(byte[] data, int offset = 0, int count = -1, CancellationToken ct = default);

        /// <summary>
        /// Reads a line terminated by <see cref="SerialConfig.NewLine"/>, or returns
        /// <c>null</c> if no line was received before <paramref name="timeoutMs"/> elapses.
        /// </summary>
        /// <param name="timeoutMs">Non-negative receive timeout in milliseconds.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The ASCII line without its terminator, or <c>null</c> on timeout.</returns>
        Task<string?> ReadLine(int timeoutMs = 2000, CancellationToken ct = default);

        /// <summary>
        /// Reads exactly <paramref name="length"/> bytes, or returns null on timeout.
        /// </summary>
        /// <param name="length">Non-negative exact byte count.</param>
        /// <param name="timeoutMs">Non-negative receive timeout in milliseconds.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>A complete buffer of the requested length, or <c>null</c>; partial buffers are never returned.</returns>
        Task<byte[]?> ReadExact(int length, int timeoutMs = 2000, CancellationToken ct = default);

        /// <summary>
        /// Reads all available bytes until a quiet period or timeout occurs.
        /// </summary>
        /// <param name="timeoutMs">Non-negative budget for the first byte and total read in milliseconds.</param>
        /// <param name="quietPeriodMs">Non-negative silence window that starts only after the first byte arrives.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Collected bytes, or <c>null</c> when no byte arrives before timeout.</returns>
        Task<byte[]?> ReadAll(int timeoutMs = 2000, int quietPeriodMs = 100, CancellationToken ct = default);
    }
}
