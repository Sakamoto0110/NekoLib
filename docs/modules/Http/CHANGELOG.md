# NekoLib.Http Changelog

**Document ID:** HTTP-CHANGELOG

**Schema version:** 1

**Kind:** reference

**Lifecycle:** current

**Subject:** consumer-visible evolution of the NekoLib.Http boundary

**Surface:** changelog

**Boundary:** http

**Authority role:** non-normative

**Mutation:** authored

**Indexing:** include

The [coordinated family changelog](../../../CHANGELOG.md) remains the release
summary. This file records HTTP-specific consumer impact without duplicating
package hashes or release provenance.

## 1.1.0

**Packages:** `NekoLib.Http`

**Compatibility class:** documentation-only

**Consumer impact:** The package now carries complete XML member documentation for both target assemblies; compiled signatures, accepted API baselines, dependencies, and runtime behavior are unchanged.

**Migration:** none

- XML comments define the catalog, client ownership, response evidence,
  serialization, relative-URI, size-bound, and consumer extension contracts.
- Immutable `1.1.0-local.9` is the qualifying package evidence for the XML
  delivery and stable release.

## 1.0.0

**Packages:** `NekoLib.Http`

**Compatibility class:** mixed

**Consumer impact:** One member left the compiled surface and three were added to an exception type; the remaining corrections are behavioral. Nothing that could be used in practice was removed, so no application source change is expected.

**Migration:** `docs/modules/Http/migrations/f1.md`

- `protected HttpEndpoint(string, HttpMethod, Type, Type)` became
  `private protected` and left both manifests. Nothing could use it:
  `CreateRequest` is `internal abstract`, so an external assembly deriving from
  `HttpEndpoint` never compiled, failing with `CS0534`. The constructor
  advertised an extension point that did not exist.
- An unresolvable response charset no longer throws. A response declaring a
  legacy code page such as `windows-1252` previously succeeded on `net481`, where
  the full code-page set ships, and threw a bare `ArgumentException` out of
  `SendAsync` on `net9.0`, where it does not — destroying the status, headers,
  and body the module exists to preserve. It now falls back to UTF-8 and returns
  the response intact. Applications needing byte-accurate legacy decoding
  register `CodePagesEncodingProvider` themselves; the module deliberately did
  not take a `System.Text.Encoding.CodePages` dependency.
- `HttpResponseContentTooLargeException` gained `StatusCode`, `ReasonPhrase`,
  and `Headers`, captured before the body is read, so an oversized `502`
  carrying `Retry-After` stays actionable. The constructor is `internal`, so
  widening it was not a public break and the three properties are additive.
- `HttpApiClientOptions` validation reports one exception type —
  `ArgumentException` naming the caller's `options` parameter — instead of a
  mixture of `InvalidOperationException` and `ArgumentOutOfRangeException`.
- Sending an endpoint that is not the registered instance now says exactly
  that, instead of claiming the name is unregistered.
