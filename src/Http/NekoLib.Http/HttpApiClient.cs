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
            effectiveOptions.Validate();
            _serializer = effectiveOptions.BodySerializer;
            _maxResponseContentBytes = effectiveOptions.MaxResponseContentBytes;
        }

        public Task<HttpApiResponse<TResponse>> SendAsync<TResponse>(
            HttpEndpoint<TResponse> endpoint,
            CancellationToken cancellationToken = default)
        {
            if (endpoint == null)
                throw new ArgumentNullException(nameof(endpoint));

            return SendCoreAsync<TResponse>(endpoint, null, cancellationToken);
        }

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
                throw new InvalidOperationException(
                    $"Endpoint '{endpoint.Name}' is not registered in this HTTP API catalog.");
            }

            cancellationToken.ThrowIfCancellationRequested();

            using (var requestMessage = endpoint.CreateRequest(request, _serializer))
            using (var responseMessage = await _httpClient.SendAsync(
                requestMessage,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken).ConfigureAwait(false))
            {
                var body = await ReadBodyAsync(
                    endpoint.Name,
                    responseMessage.Content,
                    cancellationToken).ConfigureAwait(false);
                var headers = CaptureHeaders(responseMessage);

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
            HttpContent? content,
            CancellationToken cancellationToken)
        {
            if (content == null)
                return string.Empty;

            if (content.Headers.ContentLength.HasValue &&
                content.Headers.ContentLength.Value > _maxResponseContentBytes)
            {
                throw new HttpResponseContentTooLargeException(
                    endpointName,
                    _maxResponseContentBytes);
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
                            _maxResponseContentBytes);
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
            return string.IsNullOrWhiteSpace(charset)
                ? Encoding.UTF8
                : Encoding.GetEncoding(charset!.Trim('"'));
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
