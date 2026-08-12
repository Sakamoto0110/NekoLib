# NekoLib.Http

**Kind:** reference

**Lifecycle:** current

**Subject:** typed HTTP endpoint catalogs and consumer-owned execution

**Reference date:** 2026-08-12

**Reference commit:** working tree after `71bcc2e`

`NekoLib.Http` centralizes HTTP methods, relative routes and request/response
types without hiding the HTTP protocol. It is an opt-in `net481`/`net9.0`
package with no NekoLib project dependency.

## Core model

- `HttpEndpoint<TResponse>` describes a fixed relative request with no request
  value.
- `HttpEndpoint<TRequest, TResponse>` derives a relative URI, optional body and
  per-request headers from a typed request.
- `HttpApiCatalog` is instance-scoped, rejects duplicate names
  case-insensitively and becomes immutable after construction.
- `RelativeUriBuilder` escapes path segments, query names and query values
  independently. Endpoint routes cannot replace the configured scheme or host.
- `HttpApiClient` sends only endpoints registered in its catalog through a
  consumer-owned `HttpClient`.
- `HttpApiResponse<TResponse>` preserves status, reason phrase, HTTP version,
  headers and raw body. It exposes a typed value only for successful responses.

The module supplies `JsonHttpBodySerializer` through Newtonsoft.Json so the
same serializer semantics are available on both target families. A consumer
can replace it through `IHttpBodySerializer`.

## Example

```csharp
public static class CatEndpoints
{
    public static readonly HttpEndpoint<SearchRequest, CatImage[]> Search =
        HttpEndpoint.Get<SearchRequest, CatImage[]>(
            "cats.images.search",
            request => RelativeUriBuilder
                .Create("images", "search")
                .AddQuery("limit", request.Limit)
                .Build());

    public static HttpApiCatalog CreateCatalog() =>
        HttpApiCatalog.Create(builder => builder.Register(Search));
}

var httpClient = new HttpClient
{
    BaseAddress = new Uri("https://api.example.test/v1/")
};
httpClient.DefaultRequestHeaders.Add("x-api-key", configuredApiKey);

var api = new HttpApiClient(httpClient, CatEndpoints.CreateCatalog());
var response = await api.SendAsync(
    CatEndpoints.Search,
    new SearchRequest { Limit = 3 },
    cancellationToken);

if (response.IsSuccessStatusCode)
{
    CatImage[] images = response.RequireValue();
}
```

Static endpoint fields are immutable consumer metadata; the catalog and client
instances are composed per API client. `NekoLib.Http` owns no process-wide
registry.

## Ownership and policy boundaries

The consumer owns:

- `HttpClient`, handler and connection lifetime;
- absolute base address, authentication, certificates and proxy behavior;
- timeout and cancellation policy;
- any retry or resilience policy;
- provider-specific DTOs, error interpretation and application logging.

`HttpApiClient` never disposes the supplied client, logs request/response data,
retries a request or converts a non-success status into an exception. Network
and cancellation exceptions remain transport outcomes. Successful-body parsing
failures use `HttpResponseDeserializationException` without embedding the raw
body in the exception message.

The base address must be absolute and end in `/`. That requirement prevents
standard URI resolution from dropping the final base path segment. Endpoint
paths are always relative and are built from escaped segments.

## Bodies and bounds

POST, PUT and PATCH serialize the typed request by default. Their endpoint
factory accepts a body selector when route/query values and the wire DTO differ.
GET and DELETE send no body through their dedicated factories. `Create` remains
available for an explicit custom method/body combination.

Responses are buffered because callers receive both the raw and typed forms.
The default maximum is 1 MiB and can be changed through
`HttpApiClientOptions.MaxResponseContentBytes`. Exceeding it throws
`HttpResponseContentTooLargeException` before deserialization. This module is
not a streaming-download client.

`HttpNoContent` is the typed success value for operations whose body is not
part of the contract. `string` response endpoints return the bounded raw body
directly; other success types use the configured body serializer.

## Explicit non-goals

The module is not an API gateway, service discovery system, generated SDK,
dependency-injection wrapper, authentication framework, secret store, logging
pipeline, cache or automatic resilience layer. It does not interpret OAuth,
Pix, webhooks, idempotency, pagination or provider error envelopes.

## Verification

Deterministic tests use a controlled `HttpMessageHandler`; they do not access
the internet:

```powershell
dotnet test tests\NekoLib.Http.Tests\Unit\NekoLib.Http.Tests.Unit.csproj
```

The optional external provider scenario is documented in
[`runtime_tests/Http/TheCatApi/README.md`](../../../runtime_tests/Http/TheCatApi/README.md).
Its build and real-provider evidence are deliberately recorded separately.
