#if NET9
using System.Text.Json;
#else
using Newtonsoft.Json.Linq;
#endif
using System;
 
namespace NekoLib.Pipes
{

    /// <summary>
    /// Represents one JSON RPC request, response, or event envelope. The wire
    /// discriminator values used by the library are <c>req</c>, <c>res</c>, and
    /// <c>evt</c> respectively.
    /// </summary>
    public sealed class PipeMessage
    {
        /// <summary>Initializes an empty envelope for object initialization and deserialization.</summary>
        public PipeMessage()
        {
        }

        /// <summary>Gets or sets the request correlation identifier copied to its response.</summary>
        public Guid Id { get; set; }

        /// <summary>Gets or sets the wire discriminator: <c>req</c>, <c>res</c>, or <c>evt</c>.</summary>
        public string Type { get; set; } = "";

        /// <summary>Gets or sets the RPC operation or event name.</summary>
        public string Name { get; set; } = "";

        /// <summary>Gets or sets whether the envelope represents a successful outcome.</summary>
        public bool Ok { get; set; } = true;

#if NET9
        /// <summary>
        /// Gets or sets the optional target-specific JSON payload. On <c>net9.0</c>
        /// the public DOM type is <see cref="JsonElement"/>.
        /// </summary>
        public JsonElement? Data { get; set; }
#else
        /// <summary>
        /// Gets or sets the optional target-specific JSON payload. On <c>net481</c>
        /// the public DOM type is <see cref="Newtonsoft.Json.Linq.JToken"/>.
        /// </summary>
        public Newtonsoft.Json.Linq.JToken? Data { get; set; }
#endif

        /// <summary>Gets or sets structured error evidence for an unsuccessful response.</summary>
        public PipeError? Error { get; set; }
    }

  




}


