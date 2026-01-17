using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hardware.Core.Abstractions
{
    /// <summary>
    /// Represents the supported hardware controller types.
    /// Each concrete protocol DLL will expose one controller model.
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
        /// Assigns a logger to the protocol implementation.
        /// </summary>
        HardwareLogHandler Log { get; set; }
    }

    /// <summary>
    /// Provides helper functions for clean diagnostic output formatting.
    /// </summary>
    public static class LogUtil
    {
        /// <summary>
        /// Converts a byte array into a spaced hex string.
        /// </summary>
        public static string Hex(byte[] data)
        {
            if(data == null) return "<null>";
            return BitConverter.ToString(data).Replace("-", " ");
        }

        /// <summary>
        /// Escapes control characters in string logs.
        /// </summary>
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
        public static byte Sum(params byte[] bytes) =>
            (byte)(bytes.Sum(x => x) & 0xFF);

        /// <summary>
        /// Computes XOR checksum across all bytes.
        /// </summary>
        public static byte Xor(params byte[] bytes)
        {
            byte v = 0;
            foreach(var b in bytes) v ^= b;
            return v;
        }
    }
    /// <summary>
    /// Represents the serial communication configuration used by a device.
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

        /// <summary>Per-read timeout used by SerialPort.</summary>
        public int ReadTimeout = 50;

        /// <summary>Write timeout used by SerialPort.</summary>
        public int WriteTimeout = 100;

        /// <summary>NewLine terminator used for ASCII protocols.</summary>
        public string NewLine;

        /// <summary>Optional default port name (e.g., "COM3").</summary>
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
    }
    public interface IHardwareProtocol
    {
        ControllerModel Model { get; }
        SerialConfig PortConfig { get; }
        byte[] BuildCommand(HardwareOperation op);


        HardwareResponse ParseResponse(byte[] reply, HardwareOperation op);
    }
}
