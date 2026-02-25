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
    /// Represents a hardware-agnostic asynchronous communication transport.
    /// This abstraction allows any protocol to be executed over:
    /// - Serial ports
    /// - TCP or UDP streams
    /// - Virtual/fake transports for testing
    /// </summary>
    public interface ICommTransport
    {
        /// <summary>
        /// Gets diagnostic information about the underlying transport configuration.
        /// </summary>
        SerialConfig PortInfo { get; }

        /// <summary>
        /// Optional logger injected by <see cref="HardwareEngine"/>.
        /// </summary>
        HardwareLogHandler Log { get; set; }

        /// <summary>
        /// Applies the desired communication parameters
        /// (baud, parity, newline, etc.) to the underlying medium.
        /// </summary>
        void Configure(SerialConfig cfg);

        /// <summary>
        /// Gets the current port name (e.g., "COM3").
        /// </summary>
        string PortName { get; }

        /// <summary>
        /// Indicates whether the transport is currently open.
        /// </summary>
        bool IsOpen { get; }

        /// <summary>
        /// Opens the specified port name, applying previous configuration.
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
        /// Reads a line terminated by <see cref="SerialConfig.NewLine"/>.
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
