using NekoLib.Devices.Core.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NekoLib.Devices.Core.Protocols
{


    public abstract class HardwareProtocol 
    {
        public virtual string Template { get; protected set; }

    }

    /// <summary>
    /// Provides a simple "raw" hardware protocol that forwards 
    /// binary or ASCII commands directly to the transport layer.
    ///
    /// This protocol is useful for:
    /// <list type="bullet">
    ///     <item>New boards under testing</item>
    ///     <item>Unspecified/undocumented serial devices</item>
    ///     <item>Human-readable ASCII serial interfaces</item>
    ///     <item>Debugging binary protocols</item>
    /// </list>
    ///
    /// The protocol expects the user to configure:
    /// <list type="bullet">
    ///     <item>"RawBytes" → <see cref="byte[]"/></item>
    ///     <item>"RawText" → <see cref="string"/></item>
    /// </list>
    ///
    /// It performs no output interpretation and simply wraps the raw response
    /// into a <see cref="HardwareResponse"/>.
    ///
    /// "RawText" is always encoded/decoded as ASCII (v1). Binary or non-ASCII
    /// payloads must use "RawBytes" instead — this protocol does not offer a
    /// configurable text encoding.
    /// </summary>
    public sealed class ProtocolRaw : IHardwareProtocol, IProtocolWithLogging
    {
        /// <summary>
        /// Optional logger assigned by <see cref="Engine.HardwareEngine"/>.
        /// </summary>
        public HardwareLogHandler Log { get; set; }

        /// <inheritdoc/>
        public ControllerModel Model => ControllerModel.ControllerRaw;

        /// <inheritdoc/>
        public SerialConfig PortConfig => new SerialConfig
        {
            BaudRate = 115200,
            Parity = System.IO.Ports.Parity.None,
            DataBits = 8,
            StopBits = System.IO.Ports.StopBits.One,
            NewLine = string.Empty
        };

        /// <summary>
        /// Builds a raw command based on the presence of arguments:
        ///
        /// <list type="bullet">
        ///     <item>"RawBytes" → sends the byte buffer as-is</item>
        ///     <item>"RawText" → sends as ASCII</item>
        /// </list>
        ///
        /// Any other configuration results in an exception.
        /// </summary>
        /// <param name="op">Operation containing raw text or raw bytes.</param>
        /// <returns>Raw byte buffer ready for transport.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when neither RawBytes nor RawText was provided.
        /// </exception>
        public byte[] BuildCommand(HardwareOperation op)
        {
            if(op == null)
                throw new ArgumentNullException(nameof(op));

            if(op.Args == null)
                throw new ArgumentException("Operation arguments are required.", nameof(op));

            // Binary-mode
            if(op.Args.ContainsKey("RawBytes"))
            {
                if(!(op.Args["RawBytes"] is byte[] buff))
                    throw new ArgumentException("Arg[\"RawBytes\"] must be a byte array.", nameof(op));

                Log?.Invoke(LogLevel.Debug, $"[Raw] RAW BYTES CMD: {LogUtil.Hex(buff)}");
                return buff;
            }

            // ASCII mode
            if(op.Args.ContainsKey("RawText"))
            {
                var value = op.Args["RawText"];
                if(value != null && !(value is string))
                    throw new ArgumentException("Arg[\"RawText\"] must be a string.", nameof(op));

                var text = (string)value ?? string.Empty;
                Log?.Invoke(LogLevel.Debug, $"[Raw] RAW TEXT CMD: {LogUtil.Clean(text)}");
                return Encoding.ASCII.GetBytes(text);
            }

            throw new InvalidOperationException(
                "ProtocolRaw requires Arg[\"RawBytes\"] or Arg[\"RawText\"]");
        }

        /// <summary>
        /// Wraps the reply in a <see cref="HardwareResponse"/> without attempting
        /// to interpret or validate the content.
        /// </summary>
        /// <param name="reply">Raw bytes returned by the transport.</param>
        /// <param name="op">Original operation (unused).</param>
        /// <returns>The raw response.</returns>
        public HardwareResponse ParseResponse(byte[] reply, HardwareOperation op)
        {
            if(reply == null)
            {
                return new HardwareResponse
                {
                    Success = false,
                    Status = "NoResponse",
                    RawBytes = null,
                    RawText = null
                };
            }

            return new HardwareResponse
            {
                Success = true,
                Status = "Ok",
                RawBytes = reply,
                RawText = Encoding.ASCII.GetString(reply)
            };
        }
    }
}
