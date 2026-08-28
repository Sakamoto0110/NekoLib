# NekoLib.Http

**Kind:** reference

**Lifecycle:** current

**Subject:** typed HTTP endpoint catalogs and consumer-owned execution

**Reference date:** 2026-08-17

**Implementation/package reference commits:** F1-HTTP implementation
`ea7c47623daa97a28e31e5c0e2825ef385305f2e`; coordinated package source
`63785cc8bb801f1d4a90ade6cffb7f0b42c6bc1b`.

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

## Endpoint identity

The catalog uses **two different identity models**, and the difference matters:

- **Registration and duplicate detection use the endpoint name**, compared
  case-insensitively. Registering `cats.search` and `Cats.Search` in one catalog
  throws.
- **`HttpApiClient.SendAsync` uses instance identity.** It sends only the exact
  endpoint object that was registered.

So a structurally identical endpoint built by a factory method is rejected even
when its name is registered, and the error says which of the two cases it is.
Hold your endpoints as static readonly fields — as in the example above — rather
than constructing them per call.

`HttpApiCatalog.Get(name)` and `Endpoints` return the non-generic `HttpEndpoint`,
which neither `SendAsync` overload accepts. They are introspection and
diagnostics surfaces — `Name`, `Method`, `RequestType`, `ResponseType` — not a
way to dispatch by name; a name-to-typed-endpoint lookup cannot be type-safe.

The endpoint hierarchy is **closed**. `HttpEndpoint` is public because it is the
element type of `Endpoints` and the parameter of `Register`, but request
construction is an internal contract and no external type can implement it.
Extend behaviour through the factories, `selectBody`, `configureRequest`, and
`IHttpBodySerializer`.

## Relative URIs

`RelativeUriBuilder` escapes every path segment and every query name and value
with `Uri.EscapeDataString`. That is what guarantees an endpoint route can never
replace the scheme or authority: `:` and `/` are escaped, and blank segments are
rejected, so neither an absolute URI nor a protocol-relative `//` prefix can be
built.

| Input | Result |
|---|---|
| `Create("v1/images")` | `v1%2Fimages` — a segment cannot inject path structure |
| `AddQuery("tag","a").AddQuery("tag","b")` | `?tag=a&tag=b` — repeated keys are preserved in order |
| `AddQuery("x", (string)null)` | the parameter is omitted entirely |
| `RelativeUri.FromPathSegments()` | `""` — targets the base address itself |

The one deliberate bypass is `configureRequest`, which hands you the raw
`HttpRequestMessage` and can therefore assign an absolute `RequestUri`. That is
your trust boundary, not the module's.

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

The bound is **inclusive at the limit**: a body of exactly
`MaxResponseContentBytes` succeeds and one byte more throws. It is applied to the
`Content-Length` header when present and again while streaming, so a lying or
absent header cannot bypass it. `HttpCompletionOption.ResponseHeadersRead` keeps
`HttpClient` from buffering the body first, which is what makes the bound real
rather than advisory.

`HttpResponseContentTooLargeException` carries `StatusCode`, `ReasonPhrase`, and
`Headers`, captured before the body was read — so a `502` with `Retry-After`
remains actionable even though its body was discarded.

## Request and response ownership

`HttpApiClient` disposes the request message it builds — and therefore its
content — and the response message, after materializing the body into a string.
It never disposes your `HttpClient`.

Because the response message is disposed, everything you need must come from
`HttpApiResponse`. There is no stream and no `byte[]` of the raw body.

### Content type

Serialized bodies are sent as `application/json; charset=utf-8`. A minority of
providers reject the charset parameter on JSON. `configureRequest` runs **after**
the body is assigned, so it can replace `message.Content` or reset its
`ContentType`.

### Character encoding

The response body is decoded using the charset declared in `Content-Type`, or
UTF-8 when none is declared.

**An unknown charset never throws.** .NET Framework ships the full code-page set
and .NET does not, so a response declaring `windows-1252` resolves on `net481`
and is unknown on `net9.0`. Throwing there would make the same response succeed
on one supported target and fail on the other, and would destroy the status,
headers, and body this module exists to preserve — so an unresolvable charset
falls back to UTF-8 and the response is returned intact.

For byte-accurate legacy decoding on `net9.0`, register the provider in your
application:

```csharp
Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
```

That is a process-wide opt-in the application owns; `NekoLib.Http` deliberately
takes no dependency on `System.Text.Encoding.CodePages`.

### Headers

`HttpApiResponse.Headers` merges response headers and content headers into one
case-insensitive, read-only dictionary, response values first when a name appears
in both. Multi-value headers keep their order, and content headers such as
`Content-Type` and `Content-Length` appear alongside the rest.

`HttpApiResponse.Value` is meaningful only when `HasValue` is true; use
`RequireValue()` for the checked accessor. A non-success response is never
deserialized, never throws, and keeps its raw body.

### Timeout and cancellation

Both remain transport outcomes and are never converted. Note one platform
difference: `HttpClient.Timeout` produces a `TaskCanceledException` whose inner
exception is a `TimeoutException` on `net9.0`, and which has no inner exception on
`net481`. Caller cancellation produces a `TaskCanceledException` on both.

### Serializer dependency

`JsonHttpBodySerializer(JsonSerializerSettings)` puts a `Newtonsoft.Json` type in
the compiled public surface, so the package's public contract is bound to
Newtonsoft 13.x. That is the deliberate cost of identical serializer semantics on
both target families; supply your own `IHttpBodySerializer` to avoid it.

### Writing a custom body serializer

Implement `IHttpBodySerializer` and assign it to
`HttpApiClientOptions.BodySerializer` before constructing the client. Its three
members have deliberately narrow responsibilities:

- `MediaType` must be a non-empty media type without a charset; request content
  is encoded as UTF-8 and the framework adds the charset.
- `Serialize(value, declaredType)` receives a non-null request body and its
  runtime type. This matters when an endpoint's `selectBody` returns a subtype or
  a wire DTO different from the request DTO.
- `Deserialize(content, declaredType)` receives the bounded, already decoded
  text of a successful response and the endpoint's `TResponse`. It must return a
  non-null value assignable to that type.

`string` and `HttpNoContent` responses bypass deserialization. Non-success
responses also bypass it and preserve their bounded raw body. Serialization
failures propagate from request construction; response deserialization failures
are wrapped in `HttpResponseDeserializationException` without embedding the raw
body. A serializer owns only its codec state: it must not send requests, retry,
log bodies, manage credentials, or dispose the consumer's `HttpClient`.

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

The F1 closure passed 29/29 deterministic tests on each target. The coordinated
clean package flow passed 1,538/1,538 full-solution tests and published all 16
packages. PackageReference-only WinForms and WPF consumers restored, built, and
ran on both target families with zero warnings; the multitarget, package,
deployment, publish, and clean probes also passed.

`NekoLib.Http.1.0.0-local.20.nupkg` contains `net481` and `net9.0` assets,
records package source commit `63785cc8bb801f1d4a90ade6cffb7f0b42c6bc1b`,
declares `Newtonsoft.Json 13.0.3` in both dependency groups, and has SHA-256
`545833DC1303B32ABF6C4A25FE753B9D8B19CA7555896C426D28F3133A1423D5`.

The scenario's missing-key path was executed on both targets and exited `3`
without sending a provider request. Real TheCatAPI runs then passed 10/10 with
exit `0` on `net9.0` and `net481`. Both created, queried, deleted and reconciled
their run-owned favourite with zero cleanup problems. This provider evidence is
separate from the deterministic and package evidence and does not transfer
credential, retry, or provider-policy ownership into the module.
