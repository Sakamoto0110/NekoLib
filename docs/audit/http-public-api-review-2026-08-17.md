# HTTP Public API Review — 2026-08-17

**Kind:** audit

**Lifecycle:** historical

**Subject:** F1-HTTP compiled public surface, typed catalog and endpoint
identity, relative URI construction, request and response ownership,
serialization contracts, bounded response buffering, protocol-evidence
preservation, target parity, and compatibility boundaries

**Status:** all dispositions implemented, including both optional items, and
package-validated

**Reference date:** 2026-08-17

**Reference commit:** `e845165252c60c9ecff2e90221eac739a1631c68`

**Last reconciliation:** 2026-08-18 — package gate completed

**Current state:** [`TODO.md`](../../TODO.md) F1-HTTP

## Baseline and authority

This review covers committed `HEAD` on branch
`phase-e/sqlserver-and-orchestration`. The reviewed product source is unchanged
from `89f05b667be10104e8ef966ac9bebba7b7f13a23`; the two commits in between added
the F1-DIAG and F1-WIN review artifacts and their index entries. The worktree and
index were clean before this artifact was added, the branch was 25 commits ahead
of `origin/phase-e/sqlserver-and-orchestration`, and nothing was pushed.

The reviewed authority is the `NekoLib.Http` project, all nine of its source
files, its project file, its README, the two assembly-derived manifests under
[`eng/public-api/NekoLib.Http/`](../../eng/public-api/NekoLib.Http), the
dual-target deterministic tests, the
[public API and release policy](../public-api-release-policy.md), and the
[TheCatAPI scenario](../../runtime_tests/Http/TheCatApi/README.md) **as scenario
source only**.

This review changes no product source, test, API baseline, package, changelog,
migration guide, or roadmap item.

The G1 completion record and the `1.0.0-local.11` HTTP package are prior
evidence for the surface as it stands; they are not evidence for anything
proposed here.

## Scope

Included:

- all 14 compiled public type declarations and their members, on both targets;
- `HttpApiCatalog` and `HttpApiCatalogBuilder` immutability, duplicate-name
  rejection, lookup, and registration identity;
- typed endpoint request/response shapes, factory coverage, and the
  `HttpNoRequest`/`HttpNoContent` sentinels;
- `RelativeUri` and `RelativeUriBuilder` escaping, validation, query handling,
  and the prohibition on absolute routes;
- supplied `HttpClient` ownership, `BaseAddress` ownership, and request/response
  message ownership;
- serialization contracts, content type, and body selection;
- cancellation and timeout ownership and their per-target exception shapes;
- response-size enforcement, empty/malformed/oversized bodies, non-success
  evidence, header capture, and deserialization failures;
- target parity, package dependency boundary, README completeness, and migration
  impact.

Excluded:

- implementing any recommendation, editing product source or tests, or updating
  an accepted API manifest;
- credentials, certificates, OAuth, mTLS, provider policy, retries, resilience,
  webhooks, a process-wide registry, and any Logging/Telemetry/Diagnostics/Core
  dependency — all explicitly out of bounds;
- sending any external HTTP request, configuring any credential, or launching
  the TheCatAPI scenario. No network claim is made anywhere in this review;
- anything that would unfreeze G2 Payments, PicPay, Efí, or provider evidence.

## Package, ownership, and dependency boundary

`NekoLib.Http` targets `net481;net9.0`, enables `Nullable`, disables
`ImplicitUsings`, defines `NEKOLIB` plus the conditional symbols, has **no
NekoLib project reference**, and takes `Newtonsoft.Json 13.0.3` plus the net481
`System.Net.Http` framework reference
([`NekoLib.Http.csproj`](../../src/Http/NekoLib.Http/NekoLib.Http.csproj)).
It contains no conditional compilation, so both targets compile the same text.

The ownership split is stated well in the current README and holds up under
inspection: the consumer owns the `HttpClient`, base address, authentication,
certificates, proxy, timeout, cancellation policy, and any retry decision; the
module owns endpoint metadata, relative URI construction, request/response
message lifetime, bounded buffering, and evidence preservation. Nothing in the
assembly reads process-wide state, and there is no static mutable state
anywhere.

One boundary is real but unstated: **`Newtonsoft.Json` is part of the public
surface**, not just an implementation dependency.
`JsonHttpBodySerializer(JsonSerializerSettings)` puts a Newtonsoft type in the
compiled manifest on both targets, so the package's public contract is bound to
Newtonsoft 13.x. That was a deliberate G1 choice — identical serializer
semantics on both target families instead of `System.Text.Json` on net9 only —
and this review recommends keeping it, but the compatibility consequence belongs
in the documentation.

## Compiled-surface inventory and recommended classification

| Type | Kind | Public members | Recommended class |
|---|---|---|---|
| `HttpApiCatalog` | sealed class | `Endpoints`, `Get`, static `Create` | Stable candidate |
| `HttpApiCatalogBuilder` | sealed class | `Register` | Stable candidate |
| `HttpApiClient` | sealed class | 1 ctor, 2 `SendAsync` overloads | Stable candidate |
| `HttpApiClientOptions` | sealed class | 1 ctor, 1 const, 2 properties | Stable candidate |
| `HttpApiResponse<TResponse>` | sealed class | 8 properties, `RequireValue` | Stable candidate |
| `HttpEndpoint` | abstract class | 1 protected ctor, 4 properties, 8 static factories | Stable candidate; see HTTP-08 |
| `HttpEndpoint<TResponse>` | sealed class | — | Stable candidate |
| `HttpEndpoint<TRequest, TResponse>` | sealed class | — | Stable candidate |
| `HttpNoContent` | sealed class | `Value` | Stable candidate |
| `HttpNoRequest` | sealed class | — | Stable candidate |
| `HttpResponseContentTooLargeException` | sealed exception | 2 properties | Stable candidate + additive |
| `HttpResponseDeserializationException` | sealed exception | 3 properties | Stable candidate |
| `RelativeUri` | sealed class | `Value`, `ToString`, static `FromPathSegments` | Stable candidate |
| `RelativeUriBuilder` | sealed class | 4 `AddQuery` overloads, `AppendPathSegment`, `Build`, static `Create` | Stable candidate |
| `IHttpBodySerializer` | interface | 3 members | Stable candidate |
| `JsonHttpBodySerializer` | sealed class | 2 ctors, 3 members | Stable candidate |

Totals: **16 public types across two namespaces**, identical on both targets
apart from the `TargetFramework` assembly attribute. Nothing is recommended for
removal, internalization, or the experimental class. The only proposed additions
are three evidence properties on `HttpResponseContentTooLargeException`
(HTTP-02). Everything else is behavioral or documentary.

The extension seams that matter are `IHttpBodySerializer`, the
`configureRequest` and `selectBody` delegates, and the caller-owned
`HttpMessageHandler` — all composition, not inheritance — which is why the
concrete types are correctly sealed.

## Downstream usage

- `tests/NekoLib.Http.Tests/Unit/` — 16 deterministic tests per target against a
  controlled `HttpMessageHandler`, no network.
- `runtime_tests/Http/TheCatApi/` — the optional external provider scenario,
  inspected as source only. It exercises `HttpEndpoint.Get`,
  `HttpEndpoint.Post`, `HttpEndpoint.Delete<TRequest>`, `RelativeUriBuilder`,
  `HttpApiCatalog.Create`, `HttpApiClient`, `HttpApiResponse`, and
  `RequireValue`. It was **not** built or run for this review and no provider
  claim is made.

No other repository code references the module. Per the release policy that
proves nothing about external use, and no removal below is justified by consumer
count.

## Observed facts, risks, and recommended dispositions

Findings marked *probe-confirmed* were reproduced with a disposable dual-target
console probe built against the `NekoLib.Http` project reference and run on
**both** `net481` and `net9.0`, using a controlled `HttpMessageHandler`. No
network request was made.

### HTTP-01 — A legacy charset decodes on `net481` and throws on `net9.0`

**Confirmed, probe-confirmed on both targets.** `ResolveEncoding` calls
`Encoding.GetEncoding(charset)` directly
([`HttpApiClient.cs:211`](../../src/Http/NekoLib.Http/HttpApiClient.cs#L211)).
.NET Framework ships the full code-page set; .NET 9 ships only the Unicode and
ASCII families unless `CodePagesEncodingProvider` is registered.

```text
net481:  Encoding.GetEncoding("windows-1252") -> Windows-1252
net9.0:  Encoding.GetEncoding("windows-1252") THREW ArgumentException
```

`windows-1252` is one of the most common legacy response charsets. The same
server response therefore succeeds on one supported target and throws on the
other, from a module whose README states that the two target families are peers.
The exception is a bare `ArgumentException` that escapes `SendAsync`, so the
caller loses the status code, reason phrase, headers, and body — the exact
protocol evidence this module exists to preserve.

**Recommended disposition:** never let charset resolution throw. Resolve the
declared charset when the runtime knows it; otherwise fall back to UTF-8 and
return the response with its full evidence intact. Document that on `net9.0`
byte-accurate legacy code-page decoding requires the **application** to call
`Encoding.RegisterProvider(CodePagesEncodingProvider.Instance)` with its own
package reference — a process-wide opt-in the consumer already owns.

**Explicitly not recommended:** adding
`System.Text.Encoding.CodePages` to `NekoLib.Http`. That is a new package
dependency on a module whose defining property is having almost none, and it
would change the restore graph for every consumer to fix a case the application
can fix for itself. If the decision gate wants byte-accurate legacy decoding
inside the package, that is a dependency decision and must be raised as a
blocker rather than folded into F1-HTTP.

### HTTP-02 — An oversized response destroys the protocol evidence it was returned with

**Confirmed, probe-confirmed on both targets.** `HttpApiResponse` exists so that
"non-success statuses are returned rather than converted into exceptions so
callers retain protocol evidence"
([`HttpApiResponse.cs:7`](../../src/Http/NekoLib.Http/HttpApiResponse.cs#L7)).
But `ReadBodyAsync` throws before any response object is built
([`HttpApiClient.cs:172`](../../src/Http/NekoLib.Http/HttpApiClient.cs#L172)),
and `HttpResponseContentTooLargeException` carries only the endpoint name and the
configured limit.

Probe, a `502 Bad Gateway` with `retry-after: 30` and an oversized body:

```text
HttpResponseContentTooLargeException exposes EndpointName='big' MaximumBytes=100;
status/headers unavailable
```

The status code, reason phrase, and headers were all already materialized and
were thrown away. A caller that must distinguish "the gateway is failing, retry
after 30s" from "this endpoint returns too much data" cannot.

**Recommended disposition:** add `StatusCode`, `ReasonPhrase`, and `Headers` to
`HttpResponseContentTooLargeException`. These are additive properties on a sealed
exception — no compatibility cost — and they restore the module's own stated
contract.

**Rejected alternative:** returning a truncated `HttpApiResponse` instead of
throwing. That would silently hand back a body the caller cannot distinguish from
a complete one, which is worse than a loud failure.

### HTTP-03 — `HttpEndpoint` advertises an extension point that does not exist

**Confirmed by compilation.** `HttpEndpoint` is `public abstract` with a
`protected` constructor in the manifest
([`HttpEndpoint.cs:11`](../../src/Http/NekoLib.Http/HttpEndpoint.cs#L11)),
but `CreateRequest` is `internal abstract`
([`HttpEndpoint.cs:30`](../../src/Http/NekoLib.Http/HttpEndpoint.cs#L30)).

Compiling a derived type in a separate assembly:

```text
error CS0534: 'ExternalEndpoint' does not implement inherited abstract member
'HttpEndpoint.CreateRequest(object?, IHttpBodySerializer)'
```

The hierarchy is closed. The public abstract type is correct — it is the element
type of `HttpApiCatalog.Endpoints` and the parameter of `Register` — but the
`protected` constructor in the public surface promises derivation that is
impossible.

**Recommended disposition:** document that the endpoint hierarchy is
deliberately closed and that extension happens through `selectBody`,
`configureRequest`, `HttpEndpoint.Create`, and `IHttpBodySerializer`. Optionally
tighten the constructor to `private protected`, which removes it from both
manifests; the policy classifies accessibility reduction as breaking, but the
impact here is provably zero because no external type can derive today. The
recommendation is **document, and tighten only if the decision gate wants the
manifest to stop advertising a seam that cannot be used**.

### HTTP-04 — Registration identity is by reference; lookup identity is by case-insensitive name

**Confirmed, probe-confirmed on both targets.** `HttpApiCatalogBuilder` keys by
name with `StringComparer.OrdinalIgnoreCase`
([`HttpApiCatalog.cs:56`](../../src/Http/NekoLib.Http/HttpApiCatalog.cs#L56)),
while `HttpApiCatalog.Contains` uses a `HashSet<HttpEndpoint>` with default
reference equality
([`HttpApiCatalog.cs:46`](../../src/Http/NekoLib.Http/HttpApiCatalog.cs#L46)).

```text
catalog.Get("SHAPE") -> case-insensitive hit, same instance True
identical-but-distinct endpoint instance -> rejected:
  Endpoint 'shape' is not registered in this HTTP API catalog.
```

Both behaviors are defensible on their own — reference identity is what makes
`SendAsync` type-safe — but together they are a trap for anyone who builds
endpoints from a factory method instead of holding static readonly fields. The
error message says the endpoint "is not registered", which is misleading when an
endpoint with that exact name *is* registered.

**Recommended disposition:** document the two identity models explicitly, and
improve the message so it distinguishes "no endpoint with this name" from "a
different instance is registered under this name". Do **not** switch to name
equality: that would let a caller send an endpoint whose route or body selector
differs from the registered one.

### HTTP-05 — `HttpApiCatalog.Get(string)` returns something no `SendAsync` overload accepts

**Confirmed.** `Get` returns the non-generic `HttpEndpoint`
([`HttpApiCatalog.cs:35`](../../src/Http/NekoLib.Http/HttpApiCatalog.cs#L35)),
while both `SendAsync` overloads require `HttpEndpoint<TResponse>` or
`HttpEndpoint<TRequest, TResponse>`. The value is usable for introspection —
`Name`, `Method`, `RequestType`, `ResponseType` — and nothing else without a
cast.

**Recommended disposition:** document `Get` and `Endpoints` as introspection and
diagnostics surfaces, with typed endpoint references remaining the supported way
to send. No API change: a name-to-typed-endpoint lookup cannot be type-safe, and
removing `Get` would take away a legitimate introspection capability.

### HTTP-06 — Relative URI edge behaviors are correct but entirely undocumented

**Confirmed, probe-confirmed on both targets.**

```text
duplicate query keys        -> 'images/search?tag=a&tag=b'
null query value            -> parameter omitted entirely
segment containing '/'      -> 'v1%2Fimages'
FromPathSegments() (empty)  -> Value='', request URI 'https://probe.test/v1/'
```

Each of these is a deliberate and defensible choice: repeated keys are preserved
in order because the query is a list rather than a dictionary; a null value means
"omit"; `Uri.EscapeDataString` escapes `/` so a segment can never inject extra
path structure — which is exactly what keeps routes relative; and an empty
segment list yields an empty relative URI that targets the base address.

The last one is also an inconsistency worth naming: every individual segment is
rejected when blank
([`RelativeUri.cs:55`](../../src/Http/NekoLib.Http/RelativeUri.cs#L55)),
yet a builder with no segments at all silently produces `""`.

**Recommended disposition:** document all four behaviors in the README. Keep the
empty-URI behavior — targeting the base address is meaningful — but say so
explicitly rather than leaving it as an accident of `string.Join`.

### HTTP-07 — The absolute-route prohibition is enforced by escaping, and the escape hatch is `configureRequest`

**Confirmed.** `RelativeUri`'s constructor is `internal`
([`RelativeUri.cs:14`](../../src/Http/NekoLib.Http/RelativeUri.cs#L14)),
so every instance comes from the builder, and every segment and query component
passes through `Uri.EscapeDataString`. A scheme, an authority, or a
protocol-relative `//` prefix cannot survive that: blank segments are rejected
and `:` and `/` are escaped. This is a genuinely strong property and should be
stated as a guarantee.

The one deliberate bypass is `configureRequest`, which hands the caller the raw
`HttpRequestMessage` and can therefore assign an absolute `RequestUri`.

**Recommended disposition:** document the guarantee and name `configureRequest`
as the caller-owned trust boundary that suspends it. No code change.

### HTTP-08 — Request and response message ownership is correct but unstated

**Confirmed.** `SendCoreAsync` disposes both the request message — and therefore
its content — and the response message, after fully materializing the body into a
string
([`HttpApiClient.cs:90`](../../src/Http/NekoLib.Http/HttpApiClient.cs#L90)).
`HttpCompletionOption.ResponseHeadersRead` keeps `HttpClient` from buffering the
body before the size bound is applied, which is what makes
`MaxResponseContentBytes` meaningful rather than advisory.

A consequence worth stating: because the response message is disposed, anything a
caller wants from it must come from `HttpApiResponse`. A stream or a
`byte[]` of the raw body is not available, and on `net9.0` a body in an
unavailable code page is therefore lossy (see HTTP-01).

**Recommended disposition:** document the ownership and the
`ResponseHeadersRead` rationale. No code change.

### HTTP-09 — Timeout and cancellation surface differently on the two targets

**Confirmed, probe-confirmed on both targets.**

```text
net9.0:  HttpClient.Timeout -> TaskCanceledException, inner=TimeoutException
net481:  HttpClient.Timeout -> TaskCanceledException, inner=none
both:    caller cancellation -> TaskCanceledException, is OperationCanceledException=True
```

This is a platform difference, not a module defect, and the module correctly
leaves both as transport outcomes rather than converting them. But a consumer
writing "was this a timeout or a caller cancellation?" can only distinguish them
by the inner exception on `net9.0`.

**Recommended disposition:** document it. Do not normalize the exception shape:
wrapping would replace a well-known BCL contract with a NekoLib-specific one and
would hide the platform difference rather than explain it.

### HTTP-10 — Serialized bodies always carry `charset=utf-8`

**Confirmed, probe-confirmed on both targets.**

```text
POST content-type -> 'application/json; charset=utf-8', body '{"id":"1"}'
```

`SerializeBody` uses `new StringContent(serialized, Encoding.UTF8, MediaType)`
([`HttpEndpoint.cs:139`](../../src/Http/NekoLib.Http/HttpEndpoint.cs#L139)),
which appends the charset parameter. A minority of providers reject
`application/json; charset=utf-8`, and RFC 8259 makes the parameter meaningless
for JSON.

`configureRequest` runs **after** the body is assigned
([`HttpEndpoint.cs:214`](../../src/Http/NekoLib.Http/HttpEndpoint.cs#L214)), so a
caller can replace `message.Content` or reset its `ContentType`.

**Recommended disposition:** document the emitted content type, the delegate
ordering that makes it overridable, and the override recipe. No code change: the
charset is correct HTTP, and removing it would be a behavioral change for every
existing consumer.

### HTTP-11 — Header capture merges response and content headers, and duplicates concatenate

**Confirmed, probe-confirmed on both targets.**

```text
captured headers -> Content-Length=[4] Content-Type=[text/plain; charset=utf-8]
                    x-probe=[one|two]
```

`CaptureHeaders` builds one case-insensitive dictionary from
`response.Headers` and then `response.Content.Headers`, concatenating when a name
appears in both
([`HttpApiClient.cs:219`](../../src/Http/NekoLib.Http/HttpApiClient.cs#L219)).
Multi-value headers are preserved in order. The result is wrapped in a
`ReadOnlyDictionary`, so it is genuinely immutable.

**Recommended disposition:** document the merge, the case-insensitive lookup, the
ordering (response headers before content headers), and that content headers such
as `Content-Type` and `Content-Length` appear in the same dictionary. No code
change.

### HTTP-12 — Response bounds, non-success shapes, and options validation are exactly right

**Confirmed, probe-confirmed on both targets.** Recorded as positive findings to
preserve, because the dispositions above must not disturb them:

```text
body 100 bytes, limit 100 -> ok, body length 100
body 101 bytes, limit 100 -> HttpResponseContentTooLargeException
404 -> IsSuccessStatusCode=False HasValue=False Value null=True
       Body='{"error":"nope"}' RequireValue -> InvalidOperationException
options MaxResponseContentBytes = 0 -> ArgumentOutOfRangeException
options BodySerializer = null       -> InvalidOperationException
```

The bound is inclusive at the limit and exclusive above it, on both the
`Content-Length` pre-check and the streaming path; a non-success response is
never deserialized and never throws, and its raw body survives; and
`HttpApiClientOptions` is validated once and its values copied into the client
([`HttpApiClient.cs:48`](../../src/Http/NekoLib.Http/HttpApiClient.cs#L48)), so
later mutation of the caller's options object cannot re-target a live client —
matching the accepted `Logger` and `TelemetryPipeline` read-once decisions.

The one blemish is cosmetic: two invalid option values produce two different
exception types, and neither names the `options` parameter.

**Recommended disposition:** preserve all of the above and add regressions that
pin them. Optionally normalize the two validation exceptions to
`ArgumentException(nameof(options))`; recommended as **document-only** unless the
decision gate wants the consistency, because the current types are not wrong.

### HTTP-13 — The nullability of `HttpApiResponse.Value` is invisible to the API baseline

**Confirmed.** The source declares `public TResponse? Value { get; }`
([`HttpApiResponse.cs:46`](../../src/Http/NekoLib.Http/HttpApiResponse.cs#L46)),
but both manifests record `public TResponse Value { get; }` — the reflected
representation does not carry the annotation for an unconstrained type
parameter.

This is a limitation of the accepted baseline tool, not a defect in the module,
and it means a future change to that annotation would pass
`verify-public-api.ps1` silently. It is worth recording once, here, so the gap is
known rather than discovered later.

**Recommended disposition:** record the limitation in this review and state the
intended contract in the README: `Value` is meaningful only when `HasValue` is
true, and `RequireValue()` is the checked accessor. No code or tooling change is
proposed — changing the manifest generator is outside F1-HTTP.

### HTTP-14 — Newtonsoft.Json is a public dependency, not an implementation detail

**Confirmed.** `JsonHttpBodySerializer(JsonSerializerSettings settings)` appears
in both manifests, so `Newtonsoft.Json` types are part of the compiled public
contract
([`JsonHttpBodySerializer.cs:19`](../../src/Http/NekoLib.Http/Serialization/JsonHttpBodySerializer.cs#L19)).
The README explains *why* Newtonsoft was chosen but not that the choice is
consumer-visible and version-binding.

**Recommended disposition:** document it as an accepted boundary and keep the
dependency. Replacing it with `System.Text.Json` on `net9.0` only would break the
stated dual-target parity and change the public surface per target; hiding the
settings overload would remove a legitimate configuration seam.

### HTTP-15 — The README is good and has specific, enumerable gaps

**Confirmed.** [`src/Http/NekoLib.Http/README.md`](../../src/Http/NekoLib.Http/README.md)
is one of the better module references in the repository: the ownership section,
the non-goals, and the bounds discussion are all accurate. It does not cover the
two identity models (HTTP-04), the introspection-only `Get` (HTTP-05), the four
relative-URI edge behaviors (HTTP-06), the absolute-route guarantee and its
`configureRequest` bypass (HTTP-07), message ownership and
`ResponseHeadersRead` (HTTP-08), per-target timeout shapes (HTTP-09), the emitted
content type and delegate ordering (HTTP-10), header merging (HTTP-11), charset
resolution and the `net9.0` code-page limitation (HTTP-01), the closed endpoint
hierarchy (HTTP-03), or the Newtonsoft public boundary (HTTP-14).

It also carries package and scenario evidence for `1.0.0-local.11`, which will
need updating when the F1-HTTP package gate closes.

**Recommended disposition:** extend the existing README with those sections
rather than rewriting it, and refresh its reference commit and package evidence
when Codex closes the package gate.

### HTTP-16 — Coverage is solid on the happy paths and absent on every finding above

**Confirmed.** 16 deterministic tests per target cover typed GET, POST body
selection, the write-verb factories, non-success, no-content, unregistered
endpoints, the size limit, cancellation, malformed success bodies, base-address
validation, duplicate catalog names, post-build registration, and URI escaping.

Nothing covers charset resolution, header merging or duplicate header values,
duplicate query keys, a null query value, an empty relative URI, reference
identity versus name identity, `RequireValue` on a non-success response,
`HttpNoContent` on a non-2xx status, options validation, or `Get`/`Endpoints`
introspection.

**Recommended disposition:** add focused dual-target regressions for every
behavior the accepted dispositions touch or pin, including the HTTP-12 positives.

## Target parity

The two manifests differ only by the `TargetFramework` assembly attribute. Every
public type, member, and signature is identical, and the source has no
conditional compilation. The only target-specific project input is the `net481`
`System.Net.Http` framework reference, which is a compilation requirement rather
than an API difference.

Two **runtime** behaviors differ despite the identical surface, and both are
platform facts rather than module defects:

- available character encodings (HTTP-01) — a real divergence that the HTTP-01
  disposition converts from "throws on one target" into "degrades identically on
  both";
- the `HttpClient.Timeout` exception shape (HTTP-09) — documentation only.

Every probe in this review ran on both targets and the results agreed except
where explicitly noted above.

## Likely migration cost

| Disposition | Compiled surface | Behavior | Consumer action |
|---|---|---|---|
| HTTP-01 charset fallback | none | unknown charset no longer throws | none; register `CodePagesEncodingProvider` in the app for byte-accurate legacy decoding on `net9.0` |
| HTTP-02 exception evidence | **additive** | none | none; opt in to the new properties |
| HTTP-04 message wording | none | clearer error text | none |
| HTTP-03 tighten ctor *(optional)* | **breaking in the manifest** | none | none in practice; derivation is already impossible |
| HTTP-12 normalize validation *(optional)* | none | exception type changes | update `catch` clauses if any pin the type |
| HTTP-05 to HTTP-11, HTTP-13, HTTP-14 | none | none | none |

A `docs/migrations/f1-http.md` guide is required only if HTTP-03's optional
tightening or HTTP-12's optional normalization is accepted. If the recommended
set alone is accepted, the changes are additive and behavior-restoring, and a
changelog entry is sufficient. The review recommends writing the guide anyway for
symmetry with the other F1 modules, because HTTP-01 changes an observable failure
mode.

## Core-contract conflict

None, and none is possible: the module references no NekoLib project. No
recommendation adds a project reference, a Core dependency, a Logging, Telemetry,
Diagnostics, or Inspection dependency, credentials, certificates, retries,
resilience policy, or a process-wide registry. Nothing here touches the G2
Payments freeze.

The one dependency question raised — `System.Text.Encoding.CodePages` — is
explicitly **not** recommended and is flagged in HTTP-01 as a decision the gate
would have to take as a separate dependency decision.

## Rejected alternatives

- **Adding `System.Text.Encoding.CodePages` to the package.** Rejected: a new
  dependency on a deliberately dependency-light module, to fix something the
  application can fix process-wide for itself.
- **Wrapping timeout and cancellation in a NekoLib exception type.** Rejected:
  it would replace a well-known BCL contract and hide a platform difference the
  documentation can simply state.
- **Returning a truncated response instead of throwing when the limit is
  exceeded.** Rejected: an indistinguishable partial body is worse than a loud
  failure.
- **Switching `HttpApiCatalog.Contains` to name equality.** Rejected: it would
  let a caller send an endpoint whose route or body selector differs from the
  registered one.
- **Removing `HttpApiCatalog.Get(string)`.** Rejected: introspection is a
  legitimate use, and a type-safe name lookup is not expressible.
- **`System.Text.Json` on `net9.0`.** Rejected: it would break the stated
  dual-target serializer parity and produce a per-target public surface.
- **Removing `charset=utf-8` from serialized bodies.** Rejected: correct HTTP,
  and `configureRequest` already overrides it.
- **Adding a `byte[] RawBody` or a response stream to `HttpApiResponse`.**
  Rejected: this is explicitly not a streaming-download client, and bounded
  buffering is the point.
- **A retry, resilience, credential, OAuth, certificate, pagination, or
  provider-envelope feature of any kind.** Out of bounds by the campaign and by
  the module's own non-goals.
- **A process-wide catalog or `HttpClient` registry.** Out of bounds and
  contrary to the instance-scoped design.

## Proposed implementation block after acceptance

If the dispositions are accepted, one narrow commit should:

1. record the accepted decisions in `TODO.md` F1-HTTP with package-pending
   evidence and leave the checkbox unchecked;
2. implement HTTP-01 (non-throwing charset resolution), HTTP-02 (additive
   exception evidence), and HTTP-04 (message wording) in
   `src/Http/NekoLib.Http/`;
3. add the focused dual-target regressions described in HTTP-16, including
   pinning the HTTP-12 positives;
4. extend `src/Http/NekoLib.Http/README.md` with the sections listed in HTTP-15
   and refresh its reference commit;
5. add `docs/migrations/f1-http.md` for the HTTP-01 failure-mode change;
6. update `CHANGELOG.md` and `docs/README.md`;
7. update both `NekoLib.Http` manifests for the three additive exception
   properties;
8. append a reconciliation section here without rewriting the snapshot above.

## Review validation

Commands run on Windows at the reference commit:

```text
dotnet test tests/NekoLib.Http.Tests/Unit/NekoLib.Http.Tests.Unit.csproj
  net481:  16 passed, 0 failed, 0 skipped
  net9.0:  16 passed, 0 failed, 0 skipped

diff eng/public-api/NekoLib.Http/net481.approved.txt
     eng/public-api/NekoLib.Http/net9.0.approved.txt
  TargetFramework assembly attribute only

git grep '#if|#else|#endif' -- src/Http
  no match (no conditional compilation on either target)
```

A disposable dual-target console probe was built against the `NekoLib.Http`
project reference and run on both `net481` and `net9.0` outside the repository,
then deleted. It used a controlled `HttpMessageHandler` and made no network
request. It measured encoding availability, malformed-charset behavior, the size
bound at and above the limit, evidence available on the too-large exception,
empty relative URIs, duplicate query keys, segment escaping, catalog identity,
request content type, header capture, timeout and cancellation shapes, and
options validation. A separate compilation confirmed `CS0534` for an external
`HttpEndpoint` subclass. No repository file changed.

## Residual validation limits

- **No external HTTP request was sent, no credential was configured, and the
  TheCatAPI scenario was neither built nor launched.** Nothing in this review is
  provider evidence. The scenario was read as source only.
- All probes used an in-process `HttpMessageHandler`, so no real socket, TLS,
  proxy, redirect, compression, or HTTP/2 behavior was exercised.
- No package was produced and no package-consumer probe was run.
- The full solution was not rebuilt or tested for this review.
- HTTP-01's `net9.0` behavior was measured through `Encoding.GetEncoding`
  directly and through a response carrying an unresolvable charset; a real
  provider returning `windows-1252` was not exercised.
- The API-baseline nullability gap in HTTP-13 was inferred from the manifest
  text; the generator itself was not modified or tested.

## Decision gate

HTTP-01, HTTP-02, and HTTP-04 are recommended as accepted work. HTTP-03's
constructor tightening and HTTP-12's validation normalization are optional and
need an explicit yes or no. HTTP-05 to HTTP-11 and HTTP-13 to HTTP-15 are
recommended as documentation-only. HTTP-16 is recommended as test-only. The
`System.Text.Encoding.CodePages` dependency is explicitly **not** recommended and
would be a separate dependency decision. Nothing here may be implemented until
the consolidated F1 decision gate accepts or modifies these dispositions.

## Reconciliation — 2026-08-17: dispositions accepted and implemented

The observed facts, probe output, and original recommendations above are the
snapshot and are unchanged. This section records the decision-gate outcome and
the implementation.

### Accepted

All sixteen dispositions were accepted as recommended, **including both optional
items**. HTTP-01, HTTP-02, HTTP-03, HTTP-04, and HTTP-12's optional
normalization landed as code; HTTP-05 through HTTP-11 and HTTP-13 through HTTP-15
landed as new sections in the existing module reference; HTTP-16 landed as
thirteen focused regressions.

The `System.Text.Encoding.CodePages` dependency raised in HTTP-01 was **not**
taken, exactly as recommended. The application registers
`CodePagesEncodingProvider` when it needs byte-accurate legacy decoding, and the
module reference says so.

### Implementation

- **HTTP-01.** `ResolveEncoding` catches `ArgumentException` and
  `NotSupportedException` and falls back to UTF-8, so an unresolvable charset
  degrades identically on both targets instead of throwing on one.
- **HTTP-02.** `CaptureHeaders` now runs **before** the body is read, and both
  size-bound throw sites pass the status, reason phrase, and captured headers
  into `HttpResponseContentTooLargeException`. Its constructor is `internal`, so
  widening it is not a public break; the three new properties are additive.
- **HTTP-03.** The `HttpEndpoint` constructor became `private protected`. The
  nested generic endpoints are in the same assembly and still bind to it.
- **HTTP-04.** `HttpApiCatalog` gained an `internal ContainsName`, and the
  client's error distinguishes an unregistered name from a registered name
  supplied through a different instance.
- **HTTP-12.** `Validate` takes the caller's parameter name and reports all three
  invalid-option cases as `ArgumentException`.

### Validation

```text
dotnet build src/Http/NekoLib.Http/NekoLib.Http.csproj
  net481 and net9.0: 0 warnings, 0 errors

dotnet test tests/NekoLib.Http.Tests/Unit/NekoLib.Http.Tests.Unit.csproj
  net481:  29 passed, 0 failed, 0 skipped
  net9.0:  29 passed, 0 failed, 0 skipped

eng/verify-public-api.ps1 -PackageId NekoLib.Http
  diff was exactly the accepted delta, then updated and re-verified:
    -  protected HttpEndpoint(string, HttpMethod, Type, Type)
    +  public IReadOnlyDictionary<string, IReadOnlyList<string>> Headers { get; }
    +  public string? ReasonPhrase { get; }
    +  public HttpStatusCode StatusCode { get; }

eng/verify-docs.ps1        passed
git diff --check           clean
```

Thirteen regressions were added: unknown-charset fallback, the `windows-1252`
case behaving identically on both targets, oversized-response evidence, the
mismatched-instance message, header merging with duplicate values, non-success
shape and `RequireValue`, the size bound at and one byte above the limit, option
validation naming `options`, repeated query keys, omitted null query values,
segment escaping, and the empty relative URI.

The `windows-1252` regression is deliberately written to assert the **outcome**
rather than the encoding: `net481` resolves the code page and `net9.0` falls back,
and the point is that neither throws.

### Residual limits carried forward

Every limit recorded in the original snapshot still applies:

- **no external HTTP request was sent, no credential was configured, and the
  TheCatAPI scenario was neither built nor run.** Nothing here is provider
  evidence;
- all tests use an in-process `HttpMessageHandler`, so no real socket, TLS,
  proxy, redirect, compression, or HTTP/2 behaviour was exercised;
- no full-solution build or test run was performed for this module block;
- no package was produced and no PackageReference consumer probe was run.

The module reference still carries package and scenario evidence from the pre-F1
`1.0.0-local.11` artifact, now labelled as such. Refreshing it belongs to the
coordinated package campaign.

## Package reconciliation — 2026-08-18

The implementation landed in
`ea7c47623daa97a28e31e5c0e2825ef385305f2e`. The coordinated clean
`1.0.0-local.20` campaign passed 1,538/1,538 tests, rebuilt with 464 warnings
and zero errors, introduced no warning identity, and passed all
PackageReference-only consumer, multitarget, package, deployment, publish, and
clean probes.

`NekoLib.Http.1.0.0-local.20.nupkg` records repository commit
`63785cc8bb801f1d4a90ade6cffb7f0b42c6bc1b`, contains
`lib/net481/NekoLib.Http.dll` and `lib/net9.0/NekoLib.Http.dll`, and declares
`Newtonsoft.Json 13.0.3` in both dependency groups. Its SHA-256 is
`545833DC1303B32ABF6C4A25FE753B9D8B19CA7555896C426D28F3133A1423D5`.
The module reference now carries this F1 evidence instead of the stale
`local.11` package record.

No external request was sent, no credential was configured, and the TheCatAPI
scenario was not run. F1-HTTP is complete, and this review is historical.
