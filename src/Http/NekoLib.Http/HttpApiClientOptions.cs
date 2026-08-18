using NekoLib.Http.Serialization;
using System;

namespace NekoLib.Http
{
    public sealed class HttpApiClientOptions
    {
        public const int DefaultMaxResponseContentBytes = 1024 * 1024;

        public IHttpBodySerializer BodySerializer { get; set; }
            = new JsonHttpBodySerializer();

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
