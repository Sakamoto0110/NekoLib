using NekoLib.Http.Serialization;
using System;
using System.Net.Http;
using System.Text;

namespace NekoLib.Http
{
    /// <summary>Base metadata for an immutable typed HTTP endpoint.</summary>
    public abstract class HttpEndpoint
    {
        protected HttpEndpoint(string name, HttpMethod method, Type requestType, Type responseType)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Endpoint names cannot be empty.", nameof(name));

            Name = name;
            Method = method ?? throw new ArgumentNullException(nameof(method));
            RequestType = requestType ?? throw new ArgumentNullException(nameof(requestType));
            ResponseType = responseType ?? throw new ArgumentNullException(nameof(responseType));
        }

        public string Name { get; }

        public HttpMethod Method { get; }

        public Type RequestType { get; }

        public Type ResponseType { get; }

        internal abstract HttpRequestMessage CreateRequest(
            object? request,
            IHttpBodySerializer serializer);

        public static HttpEndpoint<TResponse> Get<TResponse>(
            string name,
            RelativeUri uri,
            Action<HttpRequestMessage>? configureRequest = null)
            => new HttpEndpoint<TResponse>(
                name,
                HttpMethod.Get,
                uri,
                configureRequest);

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

        public static HttpEndpoint<TRequest, HttpNoContent> Delete<TRequest>(
            string name,
            Func<TRequest, RelativeUri> createUri,
            Action<HttpRequestMessage, TRequest>? configureRequest = null)
            => Delete<TRequest, HttpNoContent>(name, createUri, configureRequest);

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

        public static HttpNoContent Value { get; } = new HttpNoContent();
    }
}
