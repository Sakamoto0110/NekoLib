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

        [Theory]
        [InlineData("PUT")]
        [InlineData("PATCH")]
        public async Task SendAsync_WriteVerbFactory_ConstructsExpectedMethodAndJsonBody(
            string method)
        {
            CapturedRequest captured = null;
            var handler = new DelegateHandler(async (request, cancellationToken) =>
            {
                captured = await CapturedRequest.CreateAsync(request);
                return JsonResponse(HttpStatusCode.OK, "{\"id\":7}");
            });
            HttpEndpoint<CreateRequest, CreatedResponse> endpoint = method == "PUT"
                ? HttpEndpoint.Put<CreateRequest, CreatedResponse>(
                    "cats.update.put",
                    request => RelativeUri.FromPathSegments("favourites", request.ImageId),
                    request => new CreateBody { ImageId = request.ImageId })
                : HttpEndpoint.Patch<CreateRequest, CreatedResponse>(
                    "cats.update.patch",
                    request => RelativeUri.FromPathSegments("favourites", request.ImageId),
                    request => new CreateBody { ImageId = request.ImageId });
            var client = CreateClient(handler, endpoint);

            var result = await client.SendAsync(endpoint, new CreateRequest
            {
                ImageId = "fav-7",
                OperationId = "unused"
            });

            Assert.Equal(method, captured.Method.Method);
            Assert.Equal("https://api.example.test/v1/favourites/fav-7", captured.Uri.AbsoluteUri);
            Assert.Equal("application/json", captured.ContentType);
            Assert.Equal("{\"ImageId\":\"fav-7\"}", captured.Body);
            Assert.Equal(7, result.RequireValue().Id);
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

        [Fact]
        public async Task SendAsync_UnknownCharset_FallsBackToUtf8AndPreservesEvidence()
        {
            var endpoint = HttpEndpoint.Get<string>(
                "cats.text",
                RelativeUri.FromPathSegments("text"));
            var handler = new DelegateHandler((request, cancellationToken) =>
            {
                var content = new StringContent("body-text", Encoding.UTF8);
                content.Headers.Remove("Content-Type");
                content.Headers.TryAddWithoutValidation(
                    "Content-Type",
                    "text/plain; charset=totally-unknown-charset");
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = content
                });
            });

            var response = await CreateClient(handler, endpoint).SendAsync(endpoint);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("body-text", response.Body);
            Assert.Equal("body-text", response.RequireValue());
        }

        [Fact]
        public async Task SendAsync_LegacyCodePageCharset_BehavesIdenticallyOnBothTargets()
        {
            // net481 resolves windows-1252 and net9.0 does not. Whichever way this
            // runtime falls, the call must return protocol evidence rather than
            // throwing out of SendAsync.
            var endpoint = HttpEndpoint.Get<string>(
                "cats.legacy",
                RelativeUri.FromPathSegments("legacy"));
            var handler = new DelegateHandler((request, cancellationToken) =>
            {
                var content = new StringContent("legacy", Encoding.ASCII);
                content.Headers.Remove("Content-Type");
                content.Headers.TryAddWithoutValidation(
                    "Content-Type",
                    "text/plain; charset=windows-1252");
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = content
                });
            });

            var response = await CreateClient(handler, endpoint).SendAsync(endpoint);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("legacy", response.Body);
        }

        [Fact]
        public async Task SendAsync_ContentExceedsLimit_ExposesStatusReasonAndHeaders()
        {
            var endpoint = HttpEndpoint.Get<string>(
                "cats.big",
                RelativeUri.FromPathSegments("big"));
            var handler = new DelegateHandler((request, cancellationToken) =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.BadGateway)
                {
                    ReasonPhrase = "Bad Gateway",
                    Content = new StringContent(new string('x', 5000), Encoding.UTF8, "text/plain")
                };
                response.Headers.TryAddWithoutValidation("retry-after", "30");
                return Task.FromResult(response);
            });

            var client = new HttpApiClient(
                NewHttpClient(handler),
                HttpApiCatalog.Create(builder => builder.Register(endpoint)),
                new HttpApiClientOptions { MaxResponseContentBytes = 100 });

            var error = await Assert.ThrowsAsync<HttpResponseContentTooLargeException>(
                () => client.SendAsync(endpoint));

            Assert.Equal("cats.big", error.EndpointName);
            Assert.Equal(100, error.MaximumBytes);
            Assert.Equal(HttpStatusCode.BadGateway, error.StatusCode);
            Assert.Equal("Bad Gateway", error.ReasonPhrase);
            Assert.Equal(new[] { "30" }, error.Headers["Retry-After"]);
        }

        [Fact]
        public async Task SendAsync_EndpointNameRegisteredWithAnotherInstance_SaysSo()
        {
            var registered = HttpEndpoint.Get<string>(
                "cats.shape",
                RelativeUri.FromPathSegments("shape"));
            var lookalike = HttpEndpoint.Get<string>(
                "cats.shape",
                RelativeUri.FromPathSegments("shape"));
            var unknown = HttpEndpoint.Get<string>(
                "cats.absent",
                RelativeUri.FromPathSegments("absent"));

            var client = CreateClient(
                new DelegateHandler((request, cancellationToken) =>
                    Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK))),
                registered);

            var sameName = await Assert.ThrowsAsync<InvalidOperationException>(
                () => client.SendAsync(lookalike));
            Assert.Contains("a different endpoint instance was supplied", sameName.Message);

            var absent = await Assert.ThrowsAsync<InvalidOperationException>(
                () => client.SendAsync(unknown));
            Assert.Contains("is not registered", absent.Message);
        }

        [Fact]
        public async Task SendAsync_ResponseHeaders_MergeResponseAndContentHeaders()
        {
            var endpoint = HttpEndpoint.Get<string>(
                "cats.headers",
                RelativeUri.FromPathSegments("headers"));
            var handler = new DelegateHandler((request, cancellationToken) =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("body", Encoding.UTF8, "text/plain")
                };
                response.Headers.TryAddWithoutValidation("x-probe", "one");
                response.Headers.TryAddWithoutValidation("x-probe", "two");
                return Task.FromResult(response);
            });

            var response = await CreateClient(handler, endpoint).SendAsync(endpoint);

            Assert.Equal(new[] { "one", "two" }, response.Headers["X-PROBE"]);
            Assert.True(response.Headers.ContainsKey("Content-Type"));
        }

        [Fact]
        public async Task SendAsync_NonSuccess_HasNoValueAndRequireValueThrows()
        {
            var endpoint = HttpEndpoint.Get<string>(
                "cats.missing",
                RelativeUri.FromPathSegments("missing"));
            var handler = new DelegateHandler((request, cancellationToken) =>
                Task.FromResult(JsonResponse(HttpStatusCode.NotFound, "{\"error\":\"nope\"}")));

            var response = await CreateClient(handler, endpoint).SendAsync(endpoint);

            Assert.False(response.IsSuccessStatusCode);
            Assert.False(response.HasValue);
            Assert.Null(response.Value);
            Assert.Equal("{\"error\":\"nope\"}", response.Body);
            Assert.Throws<InvalidOperationException>(() => response.RequireValue());
        }

        [Theory]
        [InlineData(100, true)]
        [InlineData(101, false)]
        public async Task SendAsync_ResponseSizeBound_IsInclusiveAtTheLimit(int length, bool allowed)
        {
            var endpoint = HttpEndpoint.Get<string>(
                "cats.bound." + length,
                RelativeUri.FromPathSegments("bound"));
            var handler = new DelegateHandler((request, cancellationToken) =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(new string('x', length), Encoding.UTF8, "text/plain")
                };
                response.Content.Headers.ContentLength = null;
                return Task.FromResult(response);
            });

            var client = new HttpApiClient(
                NewHttpClient(handler),
                HttpApiCatalog.Create(builder => builder.Register(endpoint)),
                new HttpApiClientOptions { MaxResponseContentBytes = 100 });

            if (allowed)
            {
                var response = await client.SendAsync(endpoint);
                Assert.Equal(length, response.Body.Length);
            }
            else
            {
                await Assert.ThrowsAsync<HttpResponseContentTooLargeException>(
                    () => client.SendAsync(endpoint));
            }
        }

        [Fact]
        public void Constructor_InvalidOptions_ThrowArgumentExceptionNamingOptions()
        {
            var endpoint = HttpEndpoint.Get<string>(
                "cats.options",
                RelativeUri.FromPathSegments("options"));
            var catalog = HttpApiCatalog.Create(builder => builder.Register(endpoint));

            var size = Assert.Throws<ArgumentException>(() => new HttpApiClient(
                NewHttpClient(new DelegateHandler((request, cancellationToken) =>
                    Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)))),
                catalog,
                new HttpApiClientOptions { MaxResponseContentBytes = 0 }));
            Assert.Equal("options", size.ParamName);

            var serializer = Assert.Throws<ArgumentException>(() => new HttpApiClient(
                NewHttpClient(new DelegateHandler((request, cancellationToken) =>
                    Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)))),
                catalog,
                new HttpApiClientOptions { BodySerializer = null }));
            Assert.Equal("options", serializer.ParamName);
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
