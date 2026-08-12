using System;
using System.Collections.Generic;
using System.Net;

namespace NekoLib.Http
{
    /// <summary>
    /// Materialized HTTP outcome. Non-success statuses are returned rather than
    /// converted into exceptions so callers retain protocol evidence.
    /// </summary>
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

        public HttpStatusCode StatusCode { get; }

        public string? ReasonPhrase { get; }

        public Version HttpVersion { get; }

        public IReadOnlyDictionary<string, IReadOnlyList<string>> Headers { get; }

        public string Body { get; }

        public bool IsSuccessStatusCode
            => (int)StatusCode >= 200 && (int)StatusCode <= 299;

        public bool HasValue { get; }

        public TResponse? Value { get; }

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
