using NekoLib.Http.Serialization;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NekoLib.Http
{
    /// <summary>
    /// Sends registered typed endpoints through a consumer-owned
    /// <see cref="System.Net.Http.HttpClient"/>. This type does not dispose the
    /// client and does not add authentication, retries, logging or global state.
    /// </summary>
    public sealed class HttpApiClient
    {
        private readonly System.Net.Http.HttpClient _httpClient;
        private readonly HttpApiCatalog _catalog;
        private readonly IHttpBodySerializer _serializer;
        private readonly int _maxResponseContentBytes;

        /// <summary>
        /// Creates a typed client over a caller-owned <see cref="System.Net.Http.HttpClient"/>,
        /// immutable catalog, and captured option values. The supplied HTTP client
        /// and serializer remain caller-owned and are never disposed here.
        /// </summary>
        /// <param name="httpClient">Client with an absolute base address ending in <c>/</c>.</param>
        /// <param name="catalog">Immutable endpoint catalog.</param>
        /// <param name="options">Options to capture, or <c>null</c> for defaults.</param>
        /// <exception cref="ArgumentNullException"><paramref name="httpClient"/> or <paramref name="catalog"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException">The base address or options are invalid.</exception>
        public HttpApiClient(
            System.Net.Http.HttpClient httpClient,
            HttpApiCatalog catalog,
            HttpApiClientOptions? options = null)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));

            if (_httpClient.BaseAddress == null || !_httpClient.BaseAddress.IsAbsoluteUri)
            {
                throw new ArgumentException(
                    "The consumer-owned HttpClient must have an absolute BaseAddress.",
                    nameof(httpClient));
            }
            if (!_httpClient.BaseAddress.AbsoluteUri.EndsWith("/", StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "HttpClient.BaseAddress must end with '/' so relative endpoint paths " +
                    "cannot replace its final segment.",
                    nameof(httpClient));
            }

            var effectiveOptions = options ?? new HttpApiClientOptions();
            effectiveOptions.Validate(nameof(options));
            _serializer = effectiveOptions.BodySerializer;
            _maxResponseContentBytes = effectiveOptions.MaxResponseContentBytes;
        }

        /// <summary>Sends a registered fixed endpoint and materializes its bounded response.</summary>
        /// <typeparam name="TResponse">Successful response type declared by the endpoint.</typeparam>
        /// <param name="endpoint">The exact endpoint instance registered in this client's catalog.</param>
        /// <param name="cancellationToken">Token governing request construction checks, transport, and response reading.</param>
        /// <returns>The asynchronous materialized HTTP outcome.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="endpoint"/> is <c>null</c>.</exception>
        /// <exception cref="InvalidOperationException">The supplied endpoint instance is not registered in this catalog.</exception>
        /// <exception cref="HttpResponseContentTooLargeException">The response body exceeds the configured byte bound.</exception>
        /// <exception cref="HttpResponseDeserializationException">A successful body cannot be converted to <typeparamref name="TResponse"/>.</exception>
        /// <exception cref="OperationCanceledException">The caller or consumer-owned HTTP client cancels the operation.</exception>
        public Task<HttpApiResponse<TResponse>> SendAsync<TResponse>(
            HttpEndpoint<TResponse> endpoint,
            CancellationToken cancellationToken = default)
        {
            if (endpoint == null)
                throw new ArgumentNullException(nameof(endpoint));

            return SendCoreAsync<TResponse>(endpoint, null, cancellationToken);
        }

        /// <summary>
        /// Sends a registered typed endpoint, deriving its URI, optional body,
        /// and request customization from <paramref name="request"/>.
        /// </summary>
        /// <typeparam name="TRequest">Endpoint request type.</typeparam>
        /// <typeparam name="TResponse">Successful response type.</typeparam>
        /// <param name="endpoint">The exact endpoint instance registered in this client's catalog.</param>
        /// <param name="request">Non-null caller-owned request value.</param>
        /// <param name="cancellationToken">Token governing request construction checks, transport, and response reading.</param>
        /// <returns>The asynchronous materialized HTTP outcome.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="endpoint"/> or <paramref name="request"/> is <c>null</c>.</exception>
        /// <exception cref="InvalidOperationException">The endpoint is unregistered or an endpoint callback produces no required URI or body.</exception>
        /// <exception cref="HttpResponseContentTooLargeException">The response body exceeds the configured byte bound.</exception>
        /// <exception cref="HttpResponseDeserializationException">A successful body cannot be converted to <typeparamref name="TResponse"/>.</exception>
        /// <exception cref="OperationCanceledException">The caller or consumer-owned HTTP client cancels the operation.</exception>
        public Task<HttpApiResponse<TResponse>> SendAsync<TRequest, TResponse>(
            HttpEndpoint<TRequest, TResponse> endpoint,
            TRequest request,
            CancellationToken cancellationToken = default)
        {
            if (endpoint == null)
                throw new ArgumentNullException(nameof(endpoint));
            if (ReferenceEquals(request, null))
                throw new ArgumentNullException(nameof(request));

            return SendCoreAsync<TResponse>(endpoint, request, cancellationToken);
        }

        private async Task<HttpApiResponse<TResponse>> SendCoreAsync<TResponse>(
            HttpEndpoint endpoint,
            object? request,
            CancellationToken cancellationToken)
        {
            if (!_catalog.Contains(endpoint))
            {
                // Registration identity is by instance, so a structurally identical
                // endpoint built by a factory is not the registered one. Saying only
                // "not registered" when that name is present is misleading.
                throw new InvalidOperationException(
                    _catalog.ContainsName(endpoint.Name)
                        ? $"Endpoint '{endpoint.Name}' is registered in this HTTP API " +
                          "catalog, but a different endpoint instance was supplied. Send " +
                          "the instance that was registered."
                        : $"Endpoint '{endpoint.Name}' is not registered in this HTTP API catalog.");
            }

            cancellationToken.ThrowIfCancellationRequested();

            using (var requestMessage = endpoint.CreateRequest(request, _serializer))
            using (var responseMessage = await _httpClient.SendAsync(
                requestMessage,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken).ConfigureAwait(false))
            {
                // Headers are captured before the body is read so that a response
                // exceeding the bound can still carry its protocol evidence out
                // through HttpResponseContentTooLargeException.
                var headers = CaptureHeaders(responseMessage);
                var body = await ReadBodyAsync(
                    endpoint.Name,
                    responseMessage,
                    headers,
                    cancellationToken).ConfigureAwait(false);

                if (!responseMessage.IsSuccessStatusCode)
                {
                    return new HttpApiResponse<TResponse>(
                        responseMessage.StatusCode,
                        responseMessage.ReasonPhrase,
                        responseMessage.Version,
                        headers,
                        body,
                        false,
                        default);
                }

                if (typeof(TResponse) == typeof(HttpNoContent))
                {
                    return new HttpApiResponse<TResponse>(
                        responseMessage.StatusCode,
                        responseMessage.ReasonPhrase,
                        responseMessage.Version,
                        headers,
                        body,
                        true,
                        (TResponse)(object)HttpNoContent.Value);
                }

                if (typeof(TResponse) == typeof(string))
                {
                    return new HttpApiResponse<TResponse>(
                        responseMessage.StatusCode,
                        responseMessage.ReasonPhrase,
                        responseMessage.Version,
                        headers,
                        body,
                        true,
                        (TResponse)(object)body);
                }

                try
                {
                    var value = (TResponse)_serializer.Deserialize(body, typeof(TResponse));
                    return new HttpApiResponse<TResponse>(
                        responseMessage.StatusCode,
                        responseMessage.ReasonPhrase,
                        responseMessage.Version,
                        headers,
                        body,
                        true,
                        value);
                }
                catch (Exception ex) when (!(ex is OperationCanceledException))
                {
                    throw new HttpResponseDeserializationException(
                        endpoint.Name,
                        responseMessage.StatusCode,
                        typeof(TResponse),
                        ex);
                }
            }
        }

        private async Task<string> ReadBodyAsync(
            string endpointName,
            HttpResponseMessage response,
            IReadOnlyDictionary<string, IReadOnlyList<string>> headers,
            CancellationToken cancellationToken)
        {
            var content = response.Content;
            if (content == null)
                return string.Empty;

            if (content.Headers.ContentLength.HasValue &&
                content.Headers.ContentLength.Value > _maxResponseContentBytes)
            {
                throw new HttpResponseContentTooLargeException(
                    endpointName,
                    _maxResponseContentBytes,
                    response.StatusCode,
                    response.ReasonPhrase,
                    headers);
            }

            cancellationToken.ThrowIfCancellationRequested();
            using (var source = await content.ReadAsStreamAsync().ConfigureAwait(false))
            using (var buffer = new MemoryStream())
            {
                var chunk = new byte[8192];
                while (true)
                {
                    var read = await source.ReadAsync(
                        chunk,
                        0,
                        chunk.Length,
                        cancellationToken).ConfigureAwait(false);
                    if (read == 0)
                        break;

                    if (buffer.Length + read > _maxResponseContentBytes)
                    {
                        throw new HttpResponseContentTooLargeException(
                            endpointName,
                            _maxResponseContentBytes,
                            response.StatusCode,
                            response.ReasonPhrase,
                            headers);
                    }

                    buffer.Write(chunk, 0, read);
                }

                cancellationToken.ThrowIfCancellationRequested();
                var encoding = ResolveEncoding(content);
                var text = encoding.GetString(buffer.ToArray());
                return text.Length > 0 && text[0] == '\uFEFF'
                    ? text.Substring(1)
                    : text;
            }
        }

        private static Encoding ResolveEncoding(HttpContent content)
        {
            var charset = content.Headers.ContentType?.CharSet;
            if (string.IsNullOrWhiteSpace(charset))
                return Encoding.UTF8;

            try
            {
                return Encoding.GetEncoding(charset!.Trim('"').Trim());
            }
            catch (Exception ex) when (ex is ArgumentException || ex is NotSupportedException)
            {
                // The declared charset is unknown to this runtime. .NET Framework
                // ships the full code-page set and .NET does not, so throwing here
                // made the same response succeed on one supported target and fail on
                // the other - destroying the status, headers and body this module
                // exists to preserve. Applications needing byte-accurate legacy
                // decoding register CodePagesEncodingProvider themselves.
                return Encoding.UTF8;
            }
        }

        private static IReadOnlyDictionary<string, IReadOnlyList<string>> CaptureHeaders(
            HttpResponseMessage response)
        {
            var headers = new Dictionary<string, IReadOnlyList<string>>(
                StringComparer.OrdinalIgnoreCase);

            foreach (var header in response.Headers)
                headers[header.Key] = header.Value.ToArray();

            if (response.Content != null)
            {
                foreach (var header in response.Content.Headers)
                {
                    if (headers.TryGetValue(header.Key, out var existing))
                    {
                        headers[header.Key] = existing.Concat(header.Value).ToArray();
                    }
                    else
                    {
                        headers[header.Key] = header.Value.ToArray();
                    }
                }
            }

            return new ReadOnlyDictionary<string, IReadOnlyList<string>>(headers);
        }
    }
}
