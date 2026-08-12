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

        internal void Validate()
        {
            if (BodySerializer == null)
                throw new InvalidOperationException("A body serializer is required.");
            if (string.IsNullOrWhiteSpace(BodySerializer.MediaType))
                throw new InvalidOperationException("The body serializer media type is required.");
            if (MaxResponseContentBytes <= 0)
                throw new ArgumentOutOfRangeException(
                    nameof(MaxResponseContentBytes),
                    "The maximum response content size must be positive.");
        }
    }
}
