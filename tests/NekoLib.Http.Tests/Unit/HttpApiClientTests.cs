using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace NekoLib.Http.Tests.Unit
{
    public sealed class HttpApiClientTests
    {
        [Fact]
        public async Task SendAsync_Get_ReturnsTypedValueAndProtocolEvidence()
        {
            CapturedRequest captured = null;
            var handler = new DelegateHandler(async (request, cancellationToken) =>
            {
                captured = await CapturedRequest.CreateAsync(request);
                var response = JsonResponse(HttpStatusCode.OK, "{\"id\":\"cat-1\"}");
                response.Headers.Add("x-provider", "cats");
                return response;
            });
            var endpoint = HttpEndpoint.Get<SearchRequest, CatResponse>(
                "cats.search",
                request => RelativeUriBuilder
                    .Create("images", "search")
                    .AddQuery("limit", request.Limit)
                    .Build());
            var client = CreateClient(handler, endpoint);

            var result = await client.SendAsync(endpoint, new SearchRequest { Limit = 3 });

            Assert.Equal(HttpStatusCode.OK, result.StatusCode);
            Assert.True(result.IsSuccessStatusCode);
            Assert.True(result.HasValue);
            Assert.Equal("cat-1", result.RequireValue().Id);
            Assert.Equal("cats", Assert.Single(result.Headers["x-provider"]));
            Assert.Equal("{\"id\":\"cat-1\"}", result.Body);
            Assert.Equal(HttpMethod.Get, captured.Method);
            Assert.Equal("https://api.example.test/v1/images/search?limit=3", captured.Uri.AbsoluteUri);
            Assert.Null(captured.Body);
        }

        [Fact]
        public async Task SendAsync_Post_SerializesSelectedBodyAndConfiguresRequestOnce()
        {
            var calls = 0;
            CapturedRequest captured = null;
            var handler = new DelegateHandler(async (request, cancellationToken) =>
            {
                calls++;
                captured = await CapturedRequest.CreateAsync(request);
                return JsonResponse(HttpStatusCode.Created, "{\"id\":42}");
            });
            var endpoint = HttpEndpoint.Post<CreateRequest, CreatedResponse>(
                "cats.favourites.create",
                request => RelativeUri.FromPathSegments("favourites"),
                request => new CreateBody { ImageId = request.ImageId },
                (message, request) => message.Headers.Add("idempotency-key", request.OperationId));
            var client = CreateClient(handler, endpoint);

            var result = await client.SendAsync(endpoint, new CreateRequest
            {
                ImageId = "abc",
                OperationId = "op-1"
            });

            Assert.Equal(1, calls);
            Assert.Equal(HttpMethod.Post, captured.Method);
            Assert.Equal("application/json", captured.ContentType);
            Assert.Equal("op-1", Assert.Single(captured.Headers["idempotency-key"]));
            Assert.Equal("{\"ImageId\":\"abc\"}", captured.Body);
            Assert.Equal(42, result.RequireValue().Id);
        }

        [Fact]
        public async Task SendAsync_NonSuccess_ReturnsRawBodyWithoutDeserializingIt()
        {
            var handler = new DelegateHandler((request, cancellationToken) =>
                Task.FromResult(JsonResponse(HttpStatusCode.BadRequest, "not-json")));
            var endpoint = HttpEndpoint.Get<CatResponse>(
                "cats.invalid",
                RelativeUri.FromPathSegments("invalid"));
            var client = CreateClient(handler, endpoint);

            var result = await client.SendAsync(endpoint);

            Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
            Assert.False(result.IsSuccessStatusCode);
            Assert.False(result.HasValue);
            Assert.Equal("not-json", result.Body);
            Assert.Throws<InvalidOperationException>(() => result.RequireValue());
        }

        [Fact]
        public async Task SendAsync_NoContent_ReturnsTypedNoContentValue()
        {
            var handler = new DelegateHandler((request, cancellationToken) =>
                Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent)));
            var endpoint = HttpEndpoint.Delete<DeleteRequest>(
                "cats.favourites.delete",
                request => RelativeUriBuilder
                    .Create("favourites")
                    .AppendPathSegment(request.Id)
                    .Build());
            var client = CreateClient(handler, endpoint);

            var result = await client.SendAsync(
                endpoint,
                new DeleteRequest { Id = "fav/1" });

            Assert.Equal(HttpStatusCode.NoContent, result.StatusCode);
            Assert.Same(HttpNoContent.Value, result.RequireValue());
        }

        [Fact]
        public async Task SendAsync_UnregisteredEndpoint_ThrowsBeforeTransport()
        {
            var calls = 0;
            var handler = new DelegateHandler((request, cancellationToken) =>
            {
                calls++;
                return Task.FromResult(JsonResponse(HttpStatusCode.OK, "{}"));
            });
            var registered = HttpEndpoint.Get<CatResponse>(
                "cats.registered",
                RelativeUri.FromPathSegments("registered"));
            var unregistered = HttpEndpoint.Get<CatResponse>(
                "cats.unregistered",
                RelativeUri.FromPathSegments("unregistered"));
            var client = CreateClient(handler, registered);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                client.SendAsync(unregistered));
            Assert.Equal(0, calls);
        }

        [Fact]
        public async Task SendAsync_ContentExceedsLimit_ThrowsBoundedException()
        {
            var handler = new DelegateHandler((request, cancellationToken) =>
                Task.FromResult(JsonResponse(HttpStatusCode.OK, "123456789")));
            var endpoint = HttpEndpoint.Get<string>(
                "cats.large",
                RelativeUri.FromPathSegments("large"));
            var httpClient = NewHttpClient(handler);
            var catalog = HttpApiCatalog.Create(builder => builder.Register(endpoint));
            var client = new HttpApiClient(httpClient, catalog, new HttpApiClientOptions
            {
                MaxResponseContentBytes = 8
            });

            var error = await Assert.ThrowsAsync<HttpResponseContentTooLargeException>(() =>
                client.SendAsync(endpoint));

            Assert.Equal("cats.large", error.EndpointName);
            Assert.Equal(8, error.MaximumBytes);
        }

        [Fact]
        public async Task SendAsync_Cancellation_PropagatesToHandler()
        {
            var entered = new TaskCompletionSource<bool>();
            var handler = new DelegateHandler(async (request, cancellationToken) =>
            {
                entered.TrySetResult(true);
                await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
                return JsonResponse(HttpStatusCode.OK, "{} ");
            });
            var endpoint = HttpEndpoint.Get<CatResponse>(
                "cats.cancel",
                RelativeUri.FromPathSegments("cancel"));
            var client = CreateClient(handler, endpoint);
            using (var cts = new CancellationTokenSource())
            {
                var send = client.SendAsync(endpoint, cts.Token);
                await entered.Task;
                cts.Cancel();

                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => send);
            }
        }

        [Fact]
        public async Task SendAsync_MalformedSuccessBody_ThrowsWithoutIncludingBodyInMessage()
        {
            const string sensitiveBody = "secret-response-body";
            var handler = new DelegateHandler((request, cancellationToken) =>
                Task.FromResult(JsonResponse(HttpStatusCode.OK, sensitiveBody)));
            var endpoint = HttpEndpoint.Get<CatResponse>(
                "cats.malformed",
                RelativeUri.FromPathSegments("malformed"));
            var client = CreateClient(handler, endpoint);

            var error = await Assert.ThrowsAsync<HttpResponseDeserializationException>(() =>
                client.SendAsync(endpoint));

            Assert.Equal("cats.malformed", error.EndpointName);
            Assert.Equal(HttpStatusCode.OK, error.StatusCode);
            Assert.DoesNotContain(sensitiveBody, error.ToString());
        }

        [Fact]
        public void Constructor_BaseAddressWithoutTrailingSlash_Throws()
        {
            var endpoint = HttpEndpoint.Get<string>(
                "cats.search",
                RelativeUri.FromPathSegments("images", "search"));
            var catalog = HttpApiCatalog.Create(builder => builder.Register(endpoint));
            var httpClient = new System.Net.Http.HttpClient(new DelegateHandler(
                (request, cancellationToken) =>
                    Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK))))
            {
                BaseAddress = new Uri("https://api.example.test/v1")
            };

            Assert.Throws<ArgumentException>(() => new HttpApiClient(httpClient, catalog));
        }

        private static HttpApiClient CreateClient(
            HttpMessageHandler handler,
            params HttpEndpoint[] endpoints)
        {
            var catalog = HttpApiCatalog.Create(builder =>
            {
                foreach (var endpoint in endpoints)
                    builder.Register(endpoint);
            });
            return new HttpApiClient(NewHttpClient(handler), catalog);
        }

        private static System.Net.Http.HttpClient NewHttpClient(HttpMessageHandler handler)
            => new System.Net.Http.HttpClient(handler)
            {
                BaseAddress = new Uri("https://api.example.test/v1/")
            };

        private static HttpResponseMessage JsonResponse(HttpStatusCode status, string body)
            => new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };

        private sealed class DelegateHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _send;

            public DelegateHandler(
                Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send)
            {
                _send = send;
            }

            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
                => _send(request, cancellationToken);
        }

        private sealed class CapturedRequest
        {
            public HttpMethod Method { get; private set; }
            public Uri Uri { get; private set; }
            public string Body { get; private set; }
            public string ContentType { get; private set; }
            public Dictionary<string, IReadOnlyList<string>> Headers { get; private set; }

            public static async Task<CapturedRequest> CreateAsync(HttpRequestMessage request)
            {
                var headers = new Dictionary<string, IReadOnlyList<string>>(
                    StringComparer.OrdinalIgnoreCase);
                foreach (var header in request.Headers)
                    headers[header.Key] = new List<string>(header.Value);

                return new CapturedRequest
                {
                    Method = request.Method,
                    Uri = request.RequestUri,
                    Body = request.Content == null
                        ? null
                        : await request.Content.ReadAsStringAsync(),
                    ContentType = request.Content?.Headers.ContentType?.MediaType,
                    Headers = headers
                };
            }
        }

        private sealed class SearchRequest
        {
            public int Limit { get; set; }
        }

        private sealed class CreateRequest
        {
            public string ImageId { get; set; }
            public string OperationId { get; set; }
        }

        private sealed class CreateBody
        {
            public string ImageId { get; set; }
        }

        private sealed class DeleteRequest
        {
            public string Id { get; set; }
        }

        private sealed class CatResponse
        {
            [JsonProperty("id")]
            public string Id { get; set; }
        }

        private sealed class CreatedResponse
        {
            [JsonProperty("id")]
            public int Id { get; set; }
        }
    }
}
