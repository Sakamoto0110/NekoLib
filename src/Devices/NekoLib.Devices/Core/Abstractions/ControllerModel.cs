using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NekoLib.Devices.Core.Abstractions
{
    /// <summary>
    /// Represents the supported hardware controller types.
    /// Each protocol implementation reports one controller model.
    /// </summary>
    public enum ControllerModel
    {
        /// <summary>Raw board with no defined protocol. Used for generic ASCII/Binary devices.</summary>
        ControllerRaw,

        /// <summary>Legacy 2018 motor controller.</summary>
        Controller2018,

        /// <summary>Modern 2023 motor controller with DIP addressing.</summary>
        Controller2023,

        /// <summary>Locker controller (standard AA-EB-22-02 frame structure).</summary>
        ControllerLocker,

        /// <summary>Locker-Ker controller (framed checksum-based protocol).</summary>
        ControllerLockerKer,

        /// <summary>Industrial scale / balance protocol.</summary>
        ControllerBalanca,

        /// <summary>UPUS modular I/O and motor controller.</summary>
        ControllerUpus
    }
    /// <summary>
    /// Defines the logging severity for hardware operations.
    /// </summary>
    public enum LogLevel
    {
        /// <summary>General operation tracing.</summary>
        Info,

        /// <summary>Detailed protocol-level information.</summary>
        Debug,

        /// <summary>Fatal errors or communication failures.</summary>
        Error,

        /// <summary>Raw frame-level data dump.</summary>
        Raw
    }
    /// <summary>
    /// Delegate used for capturing hardware protocol and transport logs.
    /// </summary>
    /// <param name="level">Severity of the log entry.</param>
    /// <param name="message">Associated message.</param>
    public delegate void HardwareLogHandler(LogLevel level, string message);
    /// <summary>
    /// Optional contract indicating a protocol supports injected logging.
    /// </summary>
    public interface IProtocolWithLogging
    {
        /// <summary>
        /// Gets or sets the optional synchronous logger assigned by the
        /// composition root or <see cref="Engine.HardwareEngine"/>.
        /// </summary>
        HardwareLogHandler? Log { get; set; }
    }

    /// <summary>
    /// Provides helper functions for clean diagnostic output formatting.
    /// </summary>
    public static class LogUtil
    {
        /// <summary>
        /// Converts a byte array into a spaced hex string.
        /// </summary>
        /// <param name="data">Bytes to format, or <c>null</c> to return the literal <c>&lt;null&gt;</c>.</param>
        /// <returns>Uppercase hexadecimal byte pairs separated by spaces.</returns>
        public static string Hex(byte[] data)
        {
            if(data == null) return "<null>";
            return BitConverter.ToString(data).Replace("-", " ");
        }

        /// <summary>
        /// Escapes control characters in string logs.
        /// </summary>
        /// <param name="s">Text to clean, or <c>null</c> to return the literal <c>&lt;null&gt;</c>.</param>
        /// <returns>The text with carriage returns and line feeds escaped.</returns>
        public static string Clean(string s)
        {
            if(s == null) return "<null>";
            return s.Replace("\r", "\\r").Replace("\n", "\\n");
        }
    }
    /// <summary>
    /// Implements common checksum strategies used in hardware protocols.
    /// </summary>
    public static class Checksum
    {
        /// <summary>
        /// Computes additive checksum mod 256.
        /// </summary>
        /// <param name="bytes">Bytes to include in the checksum.</param>
        /// <returns>The low eight bits of the additive sum.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="bytes"/> is <c>null</c>.</exception>
        public static byte Sum(params byte[] bytes)
        {
            if(bytes == null) throw new ArgumentNullException(nameof(bytes));

            return (byte)(bytes.Sum(x => x) & 0xFF);
        }

        /// <summary>
        /// Computes XOR checksum across all bytes.
        /// </summary>
        /// <param name="bytes">Bytes to include in the checksum.</param>
        /// <returns>The XOR of all supplied bytes, or zero for an empty array.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="bytes"/> is <c>null</c>.</exception>
        public static byte Xor(params byte[] bytes)
        {
            if(bytes == null) throw new ArgumentNullException(nameof(bytes));

            byte v = 0;
            foreach(var b in bytes) v ^= b;
            return v;
        }
    }
    /// <summary>
    /// Represents communication configuration used by a device.
    /// The type retains its serial-shaped fields for API compatibility; non-serial
    /// transports use the endpoint, newline, and timeout fields that apply to them.
    /// </summary>
    public class SerialConfig
    {
        /// <summary>Baud rate used by the serial port.</summary>
        public int BaudRate;

        /// <summary>Parity mode.</summary>
        public Parity Parity;

        /// <summary>Number of data bits.</summary>
        public int DataBits;

        /// <summary>Number of stop bits.</summary>
        public StopBits StopBits;

        /// <summary>Hardware/software flow-control mode.</summary>
        public Handshake Handshake;

        /// <summary>Whether the Data Terminal Ready line is enabled.</summary>
        public bool DtrEnable;

        /// <summary>Whether the Request To Send line is enabled.</summary>
        public bool RtsEnable;

        /// <summary>Per-read timeout used by SerialPort.</summary>
        public int ReadTimeout = 50;

        /// <summary>Write timeout used by SerialPort.</summary>
        public int WriteTimeout = 100;

        /// <summary>NewLine terminator used for ASCII protocols.</summary>
        public string NewLine;

        /// <summary>
        /// Optional default transport endpoint (for example, "COM3",
        /// "tcp://127.0.0.1:5001", or "\\.\pipe\pcb-a").
        /// </summary>
        public string PortName;
    }

    /// <summary>
    /// Universal protocol operation object.
    /// Each implementation defines its own semantics for "Operation" + "Args".
    /// </summary>
    public class HardwareOperation
    {
        /// <summary>
        /// Protocol-defined operation code (textual hex for UPUS, symbolic for others).
        /// </summary>
        public string Operation;

        /// <summary>
        /// Key-value parameters to be encoded into the protocol frame.
        /// </summary>
        public Dictionary<string, object> Args = new Dictionary<string, object>();
    }
    /// <summary>
    /// Represents the outcome of a hardware command execution.
    /// </summary>
    public class HardwareResponse
    {
        /// <summary>Indicates whether the protocol considers this a successful response.</summary>
        public bool Success;

        /// <summary>Status string describing the result (e.g., "Ok", "Timeout").</summary>
        public string Status;

        /// <summary>Raw text received (ASCII protocols only).</summary>
        public string RawText;

        /// <summary>Raw binary received (binary protocols).</summary>
        public byte[] RawBytes;

        /// <summary>Elapsed time between send and receive.</summary>
        public TimeSpan Elapsed;

        /// <summary>Optional original request.</summary>
        public HardwareOperation Request;

        /// <summary>User-friendly interpreted representation.</summary>
        public string PrettyText;

        /// <summary>
        /// Transport or engine exception behind an unsuccessful response, when one
        /// occurred. <see cref="Status"/> stays the protocol-facing string; this
        /// carries the evidence a caller needs to tell a device outcome apart from a
        /// programming error. Null for protocol-level failures and for success.
        /// </summary>
        public Exception? Failure;
    }
    /// <summary>
    /// Defines the supported device-specific extension seam used by
    /// <see cref="Engine.HardwareEngine"/> to build commands and interpret replies.
    /// Implementations own framing, correlation, protocol status semantics, and
    /// any device-specific validation; the engine owns transport orchestration.
    /// </summary>
    public interface IHardwareProtocol
    {
        /// <summary>Gets the controller identity reported by this protocol.</summary>
        ControllerModel Model { get; }

        /// <summary>
        /// Gets the protocol-owned transport configuration. Consumers and the
        /// engine must treat this mutable object as read-only while an operation is in flight.
        /// </summary>
        SerialConfig PortConfig { get; }

        /// <summary>Builds the complete byte frame for one protocol operation.</summary>
        /// <param name="op">Caller-owned operation to encode.</param>
        /// <returns>The complete frame passed to the configured transport.</returns>
        byte[] BuildCommand(HardwareOperation op);


        /// <summary>
        /// Interprets a transport reply. <paramref name="reply"/> is null when the
        /// transport received nothing within its budget.
        /// </summary>
        /// <param name="reply">Complete reply bytes, or <c>null</c> when no bytes arrived before timeout.</param>
        /// <param name="op">The original operation associated with the reply.</param>
        /// <returns>A protocol-owned response; protocol failures should normally be represented by the response rather than thrown.</returns>
        HardwareResponse ParseResponse(byte[]? reply, HardwareOperation op);
    }
}
