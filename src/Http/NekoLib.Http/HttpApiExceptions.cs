using System;
using System.Collections.Generic;
using System.Net;

namespace NekoLib.Http
{
    public sealed class HttpResponseContentTooLargeException : Exception
    {
        internal HttpResponseContentTooLargeException(
            string endpointName,
            int maximumBytes,
            HttpStatusCode statusCode,
            string? reasonPhrase,
            IReadOnlyDictionary<string, IReadOnlyList<string>> headers)
            : base(
                $"Endpoint '{endpointName}' returned HTTP {(int)statusCode} and exceeded " +
                $"the configured response limit of {maximumBytes} bytes.")
        {
            EndpointName = endpointName;
            MaximumBytes = maximumBytes;
            StatusCode = statusCode;
            ReasonPhrase = reasonPhrase;
            Headers = headers;
        }

        public string EndpointName { get; }

        public int MaximumBytes { get; }

        /// <summary>Status of the response whose body exceeded the bound.</summary>
        public HttpStatusCode StatusCode { get; }

        /// <summary>Reason phrase of that response, when the server supplied one.</summary>
        public string? ReasonPhrase { get; }

        /// <summary>
        /// Response and content headers captured before the body was read, so
        /// protocol evidence such as <c>Retry-After</c> survives the failure.
        /// </summary>
        public IReadOnlyDictionary<string, IReadOnlyList<string>> Headers { get; }
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
