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
    /// </summary>
    public interface ICommTransport
    {
        /// <summary>
        /// Gets a snapshot of the protocol/transport configuration.
        /// Serial transports apply every field. Stream transports use endpoint,
        /// newline and timeout fields while preserving the serial fields for
        /// compatibility with existing protocols.
        /// </summary>
        SerialConfig PortInfo { get; }

        /// <summary>
        /// Optional logger injected by <see cref="HardwareEngine"/>.
        /// </summary>
        HardwareLogHandler Log { get; set; }

        /// <summary>
        /// Applies the desired communication parameters to the underlying medium.
        /// Fields that have no meaning for a transport are preserved but ignored.
        /// </summary>
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
        Task<ICommTransport> Open(string portName, CancellationToken ct = default);

        /// <summary>
        /// Opens using the configured <see cref="SerialConfig.PortName"/>.
        /// </summary>
        Task<ICommTransport> Open(CancellationToken ct = default);

        /// <summary>
        /// Closes the transport.
        /// </summary>
        Task Close();

        /// <summary>
        /// Writes an ASCII text command.
        /// </summary>
        Task Write(string text, CancellationToken ct = default);

        /// <summary>
        /// Writes a binary command.
        /// </summary>
        Task Write(byte[] data, int offset = 0, int count = -1, CancellationToken ct = default);

        /// <summary>
        /// Reads a line terminated by <see cref="SerialConfig.NewLine"/>, or returns
        /// <c>null</c> if no line was received before <paramref name="timeoutMs"/> elapses.
        /// </summary>
        Task<string> ReadLine(int timeoutMs = 2000, CancellationToken ct = default);

        /// <summary>
        /// Reads exactly <paramref name="length"/> bytes, or returns null on timeout.
        /// </summary>
        Task<byte[]> ReadExact(int length, int timeoutMs = 2000, CancellationToken ct = default);

        /// <summary>
        /// Reads all available bytes until a quiet period or timeout occurs.
        /// </summary>
        Task<byte[]> ReadAll(int timeoutMs = 2000, int quietPeriodMs = 100, CancellationToken ct = default);
    }
}
