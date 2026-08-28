using NekoLib.Http.Serialization;
using System;

namespace NekoLib.Http
{
    /// <summary>
    /// Configures body serialization and the inclusive response-content byte
    /// bound for an <see cref="HttpApiClient"/>. Values are captured at construction.
    /// </summary>
    public sealed class HttpApiClientOptions
    {
        /// <summary>Default maximum buffered response body size: 1 MiB.</summary>
        public const int DefaultMaxResponseContentBytes = 1024 * 1024;

        /// <summary>Gets or sets the non-null body codec. The default is <see cref="JsonHttpBodySerializer"/>.</summary>
        public IHttpBodySerializer BodySerializer { get; set; }
            = new JsonHttpBodySerializer();

        /// <summary>Gets or sets the positive maximum buffered response body size in bytes.</summary>
        public int MaxResponseContentBytes { get; set; }
            = DefaultMaxResponseContentBytes;

        // Every invalid option is a problem with the argument the caller supplied,
        // so all three report the same exception type and name the same parameter.
        internal void Validate(string paramName)
        {
            if (BodySerializer == null)
                throw new ArgumentException("A body serializer is required.", paramName);
            if (string.IsNullOrWhiteSpace(BodySerializer.MediaType))
                throw new ArgumentException("The body serializer media type is required.", paramName);
            if (MaxResponseContentBytes <= 0)
                throw new ArgumentException(
                    "The maximum response content size must be positive.",
                    paramName);
        }
    }
}
