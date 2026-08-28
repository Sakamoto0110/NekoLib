using System;
using System.Collections.Generic;
using System.Net;

namespace NekoLib.Http
{
    /// <summary>
    /// Materialized HTTP outcome. Non-success statuses are returned rather than
    /// converted into exceptions so callers retain protocol evidence.
    /// </summary>
    /// <typeparam name="TResponse">Typed value produced only for successful responses.</typeparam>
    public sealed class HttpApiResponse<TResponse>
    {
        internal HttpApiResponse(
            HttpStatusCode statusCode,
            string? reasonPhrase,
            Version httpVersion,
            IReadOnlyDictionary<string, IReadOnlyList<string>> headers,
            string body,
            bool hasValue,
            TResponse? value)
        {
            StatusCode = statusCode;
            ReasonPhrase = reasonPhrase;
            HttpVersion = httpVersion;
            Headers = headers;
            Body = body;
            HasValue = hasValue;
            Value = value;
        }

        /// <summary>Gets the HTTP status code.</summary>
        public HttpStatusCode StatusCode { get; }

        /// <summary>Gets the server-supplied reason phrase, when present.</summary>
        public string? ReasonPhrase { get; }

        /// <summary>Gets the response HTTP version.</summary>
        public Version HttpVersion { get; }

        /// <summary>Gets merged response and content headers in a case-insensitive read-only dictionary.</summary>
        public IReadOnlyDictionary<string, IReadOnlyList<string>> Headers { get; }

        /// <summary>Gets the bounded raw response body after charset decoding and optional BOM removal.</summary>
        public string Body { get; }

        /// <summary>Gets whether <see cref="StatusCode"/> is in the 2xx range.</summary>
        public bool IsSuccessStatusCode
            => (int)StatusCode >= 200 && (int)StatusCode <= 299;

        /// <summary>Gets whether <see cref="Value"/> contains a typed success value.</summary>
        public bool HasValue { get; }

        /// <summary>Gets the typed success value when <see cref="HasValue"/> is true; otherwise the default value.</summary>
        public TResponse? Value { get; }

        /// <summary>Returns the typed success value after checking <see cref="HasValue"/>.</summary>
        /// <returns>The materialized success value.</returns>
        /// <exception cref="InvalidOperationException">The response has no typed success value.</exception>
        public TResponse RequireValue()
        {
            if (!HasValue)
            {
                throw new InvalidOperationException(
                    $"HTTP response {(int)StatusCode} does not contain a typed success value.");
            }

            return Value!;
        }
    }
}
