using NekoLib.Http.Serialization;
using System;
using System.Net.Http;
using System.Text;

namespace NekoLib.Http
{
    /// <summary>
    /// Base metadata for an immutable typed HTTP endpoint. The hierarchy is
    /// deliberately closed: request construction is an internal contract, so an
    /// external assembly cannot supply a working endpoint type. Extend behaviour
    /// through the endpoint factories, the body selector, the request
    /// configuration callback, and <see cref="IHttpBodySerializer"/>.
    /// </summary>
    public abstract class HttpEndpoint
    {
        private protected HttpEndpoint(string name, HttpMethod method, Type requestType, Type responseType)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Endpoint names cannot be empty.", nameof(name));

            Name = name;
            Method = method ?? throw new ArgumentNullException(nameof(method));
            RequestType = requestType ?? throw new ArgumentNullException(nameof(requestType));
            ResponseType = responseType ?? throw new ArgumentNullException(nameof(responseType));
        }

        /// <summary>Gets the non-blank catalog identity, compared case-insensitively during registration.</summary>
        public string Name { get; }

        /// <summary>Gets the HTTP method used to construct requests.</summary>
        public HttpMethod Method { get; }

        /// <summary>Gets the declared request type, or <see cref="HttpNoRequest"/> for a fixed endpoint.</summary>
        public Type RequestType { get; }

        /// <summary>Gets the declared typed-success response type.</summary>
        public Type ResponseType { get; }

        internal abstract HttpRequestMessage CreateRequest(
            object? request,
            IHttpBodySerializer serializer);

        /// <summary>Creates a fixed relative GET endpoint with no request value or body.</summary>
        /// <typeparam name="TResponse">Successful response type.</typeparam>
        /// <param name="name">Non-blank catalog identity.</param>
        /// <param name="uri">Fixed relative URI.</param>
        /// <param name="configureRequest">Optional callback invoked after message construction; it may override headers or the URI.</param>
        /// <returns>An immutable endpoint instance that must be registered and sent by instance identity.</returns>
        /// <exception cref="ArgumentException"><paramref name="name"/> is blank.</exception>
        /// <exception cref="ArgumentNullException"><paramref name="uri"/> is <c>null</c>.</exception>
        public static HttpEndpoint<TResponse> Get<TResponse>(
            string name,
            RelativeUri uri,
            Action<HttpRequestMessage>? configureRequest = null)
            => new HttpEndpoint<TResponse>(
                name,
                HttpMethod.Get,
                uri,
                configureRequest);

        /// <summary>Creates a typed GET endpoint whose relative URI is derived per request and which sends no body.</summary>
        /// <typeparam name="TRequest">Request value type.</typeparam>
        /// <typeparam name="TResponse">Successful response type.</typeparam>
        /// <param name="name">Non-blank catalog identity.</param>
        /// <param name="createUri">Required per-request relative URI factory.</param>
        /// <param name="configureRequest">Optional callback invoked after message construction.</param>
        /// <returns>An immutable typed endpoint.</returns>
        /// <exception cref="ArgumentException"><paramref name="name"/> is blank.</exception>
        /// <exception cref="ArgumentNullException"><paramref name="createUri"/> is <c>null</c>.</exception>
        public static HttpEndpoint<TRequest, TResponse> Get<TRequest, TResponse>(
            string name,
            Func<TRequest, RelativeUri> createUri,
            Action<HttpRequestMessage, TRequest>? configureRequest = null)
            => new HttpEndpoint<TRequest, TResponse>(
                name,
                HttpMethod.Get,
                createUri,
                null,
                configureRequest);

        /// <summary>Creates a typed POST endpoint whose request, or selected projection, is serialized as the body.</summary>
        /// <typeparam name="TRequest">Request value type.</typeparam>
        /// <typeparam name="TResponse">Successful response type.</typeparam>
        /// <param name="name">Non-blank catalog identity.</param>
        /// <param name="createUri">Required per-request relative URI factory.</param>
        /// <param name="selectBody">Optional body projection; when omitted, the request itself is serialized.</param>
        /// <param name="configureRequest">Optional callback invoked after body assignment.</param>
        /// <returns>An immutable typed endpoint.</returns>
        /// <exception cref="ArgumentException"><paramref name="name"/> is blank.</exception>
        /// <exception cref="ArgumentNullException"><paramref name="createUri"/> is <c>null</c>.</exception>
        public static HttpEndpoint<TRequest, TResponse> Post<TRequest, TResponse>(
            string name,
            Func<TRequest, RelativeUri> createUri,
            Func<TRequest, object?>? selectBody = null,
            Action<HttpRequestMessage, TRequest>? configureRequest = null)
            => WithBody<TRequest, TResponse>(
                name,
                HttpMethod.Post,
                createUri,
                selectBody,
                configureRequest);

        /// <summary>Creates a typed PUT endpoint whose request, or selected projection, is serialized as the body.</summary>
        /// <typeparam name="TRequest">Request value type.</typeparam>
        /// <typeparam name="TResponse">Successful response type.</typeparam>
        /// <param name="name">Non-blank catalog identity.</param>
        /// <param name="createUri">Required per-request relative URI factory.</param>
        /// <param name="selectBody">Optional body projection; when omitted, the request itself is serialized.</param>
        /// <param name="configureRequest">Optional callback invoked after body assignment.</param>
        /// <returns>An immutable typed endpoint.</returns>
        /// <exception cref="ArgumentException"><paramref name="name"/> is blank.</exception>
        /// <exception cref="ArgumentNullException"><paramref name="createUri"/> is <c>null</c>.</exception>
        public static HttpEndpoint<TRequest, TResponse> Put<TRequest, TResponse>(
            string name,
            Func<TRequest, RelativeUri> createUri,
            Func<TRequest, object?>? selectBody = null,
            Action<HttpRequestMessage, TRequest>? configureRequest = null)
            => WithBody<TRequest, TResponse>(
                name,
                HttpMethod.Put,
                createUri,
                selectBody,
                configureRequest);

        /// <summary>Creates a typed PATCH endpoint whose request, or selected projection, is serialized as the body.</summary>
        /// <typeparam name="TRequest">Request value type.</typeparam>
        /// <typeparam name="TResponse">Successful response type.</typeparam>
        /// <param name="name">Non-blank catalog identity.</param>
        /// <param name="createUri">Required per-request relative URI factory.</param>
        /// <param name="selectBody">Optional body projection; when omitted, the request itself is serialized.</param>
        /// <param name="configureRequest">Optional callback invoked after body assignment.</param>
        /// <returns>An immutable typed endpoint.</returns>
        /// <exception cref="ArgumentException"><paramref name="name"/> is blank.</exception>
        /// <exception cref="ArgumentNullException"><paramref name="createUri"/> is <c>null</c>.</exception>
        public static HttpEndpoint<TRequest, TResponse> Patch<TRequest, TResponse>(
            string name,
            Func<TRequest, RelativeUri> createUri,
            Func<TRequest, object?>? selectBody = null,
            Action<HttpRequestMessage, TRequest>? configureRequest = null)
            => WithBody<TRequest, TResponse>(
                name,
                new HttpMethod("PATCH"),
                createUri,
                selectBody,
                configureRequest);

        /// <summary>Creates a typed DELETE endpoint with a typed success response and no request body.</summary>
        /// <typeparam name="TRequest">Request value type.</typeparam>
        /// <typeparam name="TResponse">Successful response type.</typeparam>
        /// <param name="name">Non-blank catalog identity.</param>
        /// <param name="createUri">Required per-request relative URI factory.</param>
        /// <param name="configureRequest">Optional callback invoked after message construction.</param>
        /// <returns>An immutable typed endpoint.</returns>
        /// <exception cref="ArgumentException"><paramref name="name"/> is blank.</exception>
        /// <exception cref="ArgumentNullException"><paramref name="createUri"/> is <c>null</c>.</exception>
        public static HttpEndpoint<TRequest, TResponse> Delete<TRequest, TResponse>(
            string name,
            Func<TRequest, RelativeUri> createUri,
            Action<HttpRequestMessage, TRequest>? configureRequest = null)
            => new HttpEndpoint<TRequest, TResponse>(
                name,
                HttpMethod.Delete,
                createUri,
                null,
                configureRequest);

        /// <summary>Creates a typed DELETE endpoint whose success value is <see cref="HttpNoContent.Value"/>.</summary>
        /// <typeparam name="TRequest">Request value type.</typeparam>
        /// <param name="name">Non-blank catalog identity.</param>
        /// <param name="createUri">Required per-request relative URI factory.</param>
        /// <param name="configureRequest">Optional callback invoked after message construction.</param>
        /// <returns>An immutable endpoint with <see cref="HttpNoContent"/> as its response type.</returns>
        /// <exception cref="ArgumentException"><paramref name="name"/> is blank.</exception>
        /// <exception cref="ArgumentNullException"><paramref name="createUri"/> is <c>null</c>.</exception>
        public static HttpEndpoint<TRequest, HttpNoContent> Delete<TRequest>(
            string name,
            Func<TRequest, RelativeUri> createUri,
            Action<HttpRequestMessage, TRequest>? configureRequest = null)
            => Delete<TRequest, HttpNoContent>(name, createUri, configureRequest);

        /// <summary>
        /// Creates a typed endpoint for an explicit method and optional body
        /// projection. Unlike the dedicated body factories, a null
        /// <paramref name="selectBody"/> sends no body.
        /// </summary>
        /// <typeparam name="TRequest">Request value type.</typeparam>
        /// <typeparam name="TResponse">Successful response type.</typeparam>
        /// <param name="name">Non-blank catalog identity.</param>
        /// <param name="method">HTTP method.</param>
        /// <param name="createUri">Required per-request relative URI factory.</param>
        /// <param name="selectBody">Optional body projection; null means no body.</param>
        /// <param name="configureRequest">Optional callback invoked after any body assignment.</param>
        /// <returns>An immutable typed endpoint.</returns>
        /// <exception cref="ArgumentException"><paramref name="name"/> is blank.</exception>
        /// <exception cref="ArgumentNullException"><paramref name="method"/> or <paramref name="createUri"/> is <c>null</c>.</exception>
        public static HttpEndpoint<TRequest, TResponse> Create<TRequest, TResponse>(
            string name,
            HttpMethod method,
            Func<TRequest, RelativeUri> createUri,
            Func<TRequest, object?>? selectBody = null,
            Action<HttpRequestMessage, TRequest>? configureRequest = null)
            => new HttpEndpoint<TRequest, TResponse>(
                name,
                method,
                createUri,
                selectBody,
                configureRequest);

        private static HttpEndpoint<TRequest, TResponse> WithBody<TRequest, TResponse>(
            string name,
            HttpMethod method,
            Func<TRequest, RelativeUri> createUri,
            Func<TRequest, object?>? selectBody,
            Action<HttpRequestMessage, TRequest>? configureRequest)
            => new HttpEndpoint<TRequest, TResponse>(
                name,
                method,
                createUri,
                selectBody ?? (request => request),
                configureRequest);

        internal static HttpContent SerializeBody(
            object value,
            IHttpBodySerializer serializer)
        {
            var serialized = serializer.Serialize(value, value.GetType());
            return new StringContent(
                serialized,
                Encoding.UTF8,
                serializer.MediaType);
        }
    }

    /// <summary>An endpoint with a fixed relative URI and no request value.</summary>
    /// <typeparam name="TResponse">Successful response type.</typeparam>
    public sealed class HttpEndpoint<TResponse> : HttpEndpoint
    {
        private readonly RelativeUri _uri;
        private readonly Action<HttpRequestMessage>? _configureRequest;

        internal HttpEndpoint(
            string name,
            HttpMethod method,
            RelativeUri uri,
            Action<HttpRequestMessage>? configureRequest)
            : base(name, method, typeof(HttpNoRequest), typeof(TResponse))
        {
            _uri = uri ?? throw new ArgumentNullException(nameof(uri));
            _configureRequest = configureRequest;
        }

        internal override HttpRequestMessage CreateRequest(
            object? request,
            IHttpBodySerializer serializer)
        {
            var message = new HttpRequestMessage(Method, _uri.Value);
            _configureRequest?.Invoke(message);
            return message;
        }
    }

    /// <summary>An endpoint whose URI and optional body are derived from a typed request.</summary>
    /// <typeparam name="TRequest">Request value type.</typeparam>
    /// <typeparam name="TResponse">Successful response type.</typeparam>
    public sealed class HttpEndpoint<TRequest, TResponse> : HttpEndpoint
    {
        private readonly Func<TRequest, RelativeUri> _createUri;
        private readonly Func<TRequest, object?>? _selectBody;
        private readonly Action<HttpRequestMessage, TRequest>? _configureRequest;

        internal HttpEndpoint(
            string name,
            HttpMethod method,
            Func<TRequest, RelativeUri> createUri,
            Func<TRequest, object?>? selectBody,
            Action<HttpRequestMessage, TRequest>? configureRequest)
            : base(name, method, typeof(TRequest), typeof(TResponse))
        {
            _createUri = createUri ?? throw new ArgumentNullException(nameof(createUri));
            _selectBody = selectBody;
            _configureRequest = configureRequest;
        }

        internal override HttpRequestMessage CreateRequest(
            object? request,
            IHttpBodySerializer serializer)
        {
            if (!(request is TRequest typedRequest))
            {
                throw new ArgumentException(
                    $"Endpoint '{Name}' requires a '{typeof(TRequest).FullName}' request.",
                    nameof(request));
            }

            var uri = _createUri(typedRequest)
                ?? throw new InvalidOperationException(
                    $"Endpoint '{Name}' produced no relative URI.");
            var message = new HttpRequestMessage(Method, uri.Value);

            if (_selectBody != null)
            {
                var body = _selectBody(typedRequest)
                    ?? throw new InvalidOperationException(
                        $"Endpoint '{Name}' produced a null request body.");
                message.Content = SerializeBody(body, serializer);
            }

            _configureRequest?.Invoke(message, typedRequest);
            return message;
        }
    }

    /// <summary>Marker used by fixed endpoints that accept no request value.</summary>
    public sealed class HttpNoRequest
    {
        private HttpNoRequest()
        {
        }
    }

    /// <summary>Typed success value for endpoints whose response has no content.</summary>
    public sealed class HttpNoContent
    {
        private HttpNoContent()
        {
        }

        /// <summary>Gets the shared typed marker returned for every successful no-content endpoint.</summary>
        public static HttpNoContent Value { get; } = new HttpNoContent();
    }
}
