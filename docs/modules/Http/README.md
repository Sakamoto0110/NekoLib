# NekoLib.Http

**Document ID:** HTTP-INTRODUCTION

**Schema version:** 1

**Kind:** reference

**Lifecycle:** current

**Subject:** concise consumer introduction to the NekoLib.Http boundary

**Surface:** introduction

**Boundary:** http

**Authority role:** non-normative

**Mutation:** authored

**Indexing:** include

NekoLib.Http gives an application one place to declare its HTTP methods,
relative routes, and request and response types — without hiding the protocol
and without becoming a client framework. It is opt-in, targets `net481` and
`net9.0`, and references no other NekoLib project.

You declare endpoints as static readonly fields, register them in an immutable
`HttpApiCatalog`, and send them through an `HttpApiClient` wrapped around an
`HttpClient` you construct and own. `HttpApiResponse<T>` comes back carrying the
status, reason phrase, HTTP version, merged headers, the raw body, and — for a
successful response only — the typed value.

Three boundaries decide whether this module fits. **The consumer owns the
transport**: the `HttpClient`, its base address, authentication, certificates,
proxy, timeout, and any retry policy. The client never disposes it, never adds
credentials, and never retries. **Non-success is not an exception**: a `404` or
`502` is returned with its body intact so you keep the protocol evidence.
**Responses are buffered under a bound**, 1 MiB by default — this is not a
streaming-download client, and exceeding the bound throws rather than truncating.

The one thing worth knowing before writing the first catalog: registration
matches endpoints by **name**, but sending matches by **object identity**. A
structurally identical endpoint rebuilt per call is rejected even though its name
is registered. Hold endpoints as static fields.

Deterministic tests use a controlled message handler and reach no network. The
optional TheCatAPI scenario is the only provider evidence, needs a
maintainer-owned key, and is recorded separately.

Start with the [technical reference](REFERENCE.md) for identity, URI, ownership,
bound, encoding, and target contracts. Use the [module manifest](MANIFEST.md)
for package, API, evidence, audit, and migration routes.
