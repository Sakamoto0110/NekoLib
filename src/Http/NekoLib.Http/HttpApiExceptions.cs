using System;
using System.Net;

namespace NekoLib.Http
{
    public sealed class HttpResponseContentTooLargeException : Exception
    {
        internal HttpResponseContentTooLargeException(string endpointName, int maximumBytes)
            : base(
                $"Endpoint '{endpointName}' exceeded the configured response limit of " +
                $"{maximumBytes} bytes.")
        {
            EndpointName = endpointName;
            MaximumBytes = maximumBytes;
        }

        public string EndpointName { get; }

        public int MaximumBytes { get; }
    }

    public sealed class HttpResponseDeserializationException : Exception
    {
        internal HttpResponseDeserializationException(
            string endpointName,
            HttpStatusCode statusCode,
            Type responseType,
            Exception innerException)
            : base(
                $"Endpoint '{endpointName}' returned HTTP {(int)statusCode}, but its " +
                $"success body could not be read as '{responseType.FullName}'.",
                innerException)
        {
            EndpointName = endpointName;
            StatusCode = statusCode;
            ResponseType = responseType;
        }

        public string EndpointName { get; }

        public HttpStatusCode StatusCode { get; }

        public Type ResponseType { get; }
    }
}
