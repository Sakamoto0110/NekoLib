using System;
using System.Collections.Generic;
using System.Net;

namespace NekoLib.Http
{
    /// <summary>
    /// Reports that a response body exceeded the configured byte bound while
    /// preserving status and headers captured before body disposal.
    /// </summary>
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

        /// <summary>Gets the registered endpoint name.</summary>
        public string EndpointName { get; }

        /// <summary>Gets the inclusive configured response-body byte limit.</summary>
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

    /// <summary>
    /// Wraps a non-cancellation failure while converting a successful bounded
    /// response body to the endpoint response type. The raw body is deliberately
    /// excluded from the exception message.
    /// </summary>
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

        /// <summary>Gets the registered endpoint name.</summary>
        public string EndpointName { get; }

        /// <summary>Gets the successful HTTP status whose body could not be converted.</summary>
        public HttpStatusCode StatusCode { get; }

        /// <summary>Gets the endpoint response type requested from the body serializer.</summary>
        public Type ResponseType { get; }
    }
}
