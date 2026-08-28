using System;
namespace NekoLib.Pipes
{
    /// <summary>Represents structured error evidence carried by an unsuccessful pipe response.</summary>
    public sealed class PipeError
    {
        /// <summary>Initializes an empty error payload for object initialization and deserialization.</summary>
        public PipeError()
        {
        }

        /// <summary>
        /// Gets or sets the framework or application-defined machine-readable code.
        /// <see cref="PipeErrorCodes"/> is not a closed set.
        /// </summary>
        public string Code { get; set; } = "";

        /// <summary>Gets or sets the consumer-facing error message carried on the wire.</summary>
        public string Message { get; set; } = "";
    }
}


