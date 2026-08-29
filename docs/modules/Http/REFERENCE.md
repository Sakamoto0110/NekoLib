# NekoLib.Http

**Document ID:** HTTP-REFERENCE

**Schema version:** 1

**Kind:** reference

**Lifecycle:** current

**Subject:** typed HTTP endpoint catalogs, consumer ownership, relative URI construction, bounded response evidence, serialization contracts, and target differences

**Surface:** technical-reference

**Boundary:** http

**Authority role:** normative

**Mutation:** authored

**Indexing:** include

`NekoLib.Http` centralizes HTTP methods, relative routes and request/response
types without hiding the HTTP protocol. It is an opt-in `net481`/`net9.0`
package with no NekoLib project dependency.

## Core model

- `HttpEndpoint<TResponse>` describes a fixed relative request with no request
  value.
- `HttpEndpoint<TRequest, TResponse>` derives a relative URI, optional body and
  per-request headers from a typed request.
- `HttpApiCatalog` is instance-scoped, rejects duplicate names
  case-insensitively and becomes immutable after construction. The builder is
  single-use: capturing it outside the `Create` callback and registering
  afterwards throws.
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

`Endpoints` is documented as registration-ordered. That order comes from
enumerating the catalog's backing dictionary and is not pinned by a test; see
[`HTTP-FINDING-001`](FINDINGS.md) before depending on it for anything but
display.

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

`RelativeUri` has no public constructor. The builder and
`RelativeUri.FromPathSegments` are the only ways to obtain one, which is what
makes the escaping guarantee hold rather than merely being the recommended path.

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

Declare `HttpNoContent` when a success may carry no body. A successful response
with an empty body and any other typed response type reaches the serializer as
an empty string and surfaces as `HttpResponseDeserializationException` — not as
a null or default value.

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

Both remain transport outcomes and are never converted. The token is observed
at four points: before the request message is built, on the send itself, before
the response body is read, and on every read of the body stream. That last one
is what makes cancelling a slow body read effective rather than advisory.

Note one platform difference: `HttpClient.Timeout` produces a `TaskCanceledException` whose inner
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

## Targets and thread safety

Both targets compile from one source set with no conditional compilation, and
the two accepted manifests are identical apart from the per-target framework
attribute. The only build-level difference is that `net481` reaches
`System.Net.Http` through a framework reference while `net9.0` gets it from the
shared framework. Two observable behaviors still differ by target, and both are
described above: legacy code-page availability during charset resolution, and
the inner exception on a `HttpClient.Timeout` cancellation.

`HttpApiCatalog`, `HttpEndpoint` and its generic subclasses, `RelativeUri`, and
`HttpApiResponse<T>` are immutable once constructed. `HttpApiClient` holds only
readonly state captured at construction and adds no synchronization, so
concurrent sends are as safe as the `HttpClient` underneath them.

Two things are **not** thread-safe and are yours to manage:

- **`RelativeUriBuilder` is mutable.** Finish building before sharing the
  result. `RelativeUri` itself is immutable and freely shareable.
- **Your endpoint delegates are invoked concurrently.** `createUri`,
  `selectBody`, and `configureRequest` run on whichever thread called
  `SendAsync`, with no lock around them. Sharing an endpoint as a static field —
  which this document recommends — means those delegates must tolerate being
  called from several threads at once. Keep them pure.

`HttpApiClientOptions` is a mutable carrier read once at client construction, so
mutating it afterwards cannot affect a live client. Its serializer is the one
exception: `JsonHttpBodySerializer(JsonSerializerSettings)` retains your settings
**instance**, so later mutation of that object does reach a live client. Give
each serializer its own settings instance if you keep a reference to it. See
[`HTTP-FINDING-002`](FINDINGS.md).

## Verification

Deterministic tests use a controlled `HttpMessageHandler`; they do not access
the internet:

```powershell
dotnet test tests\NekoLib.Http.Tests\Unit\NekoLib.Http.Tests.Unit.csproj
.\eng\verify-public-api.ps1 -PackageId NekoLib.Http
```

The optional external provider scenario is documented in
[`runtime_tests/Http/TheCatApi/README.md`](../../../runtime_tests/Http/TheCatApi/README.md).
It requires a maintainer-owned key, mutates a real third-party account, and
proves provider behavior only for the run it records. Deterministic and provider
evidence are separate layers and neither substitutes for the other.

[`VALIDATION_REQUIREMENTS.md`](VALIDATION_REQUIREMENTS.md) owns the qualifying
evidence contract and [`VALIDATIONS.md`](VALIDATIONS.md) records what actually
ran, with its gaps — including the package provenance and the provider runs this
section used to carry inline.

## Related surfaces

| Need | Owner |
|---|---|
| Identity, packages, targets, API oracles, evidence routes | [`MANIFEST.md`](MANIFEST.md) |
| Consumer introduction | [`README.md`](README.md) |
| Consumer-visible evolution | [`CHANGELOG.md`](CHANGELOG.md) |
| Chronology | [`HISTORY.md`](HISTORY.md) |
| Confirmed defects | [`ISSUES.md`](ISSUES.md) |
| Unconfirmed observations | [`FINDINGS.md`](FINDINGS.md) |
| Candidate-to-stable transition | [`migrations/f1.md`](migrations/f1.md) |
| Historical F1 review | [`audits/public-api-review-2026-08-17.md`](audits/public-api-review-2026-08-17.md) |
