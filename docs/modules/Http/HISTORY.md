# NekoLib.Http History

**Document ID:** HTTP-HISTORY

**Schema version:** 1

**Kind:** reference

**Lifecycle:** current

**Subject:** factual chronological history of the NekoLib.Http boundary

**Surface:** history

**Boundary:** http

**Authority role:** evidence

**Mutation:** append-only

**Indexing:** include

## 2026-08-16 — HTTP-HISTORY-001 — Typed HTTP catalog delivered

**Release:** 1.0.0-local.11

- Phase G1 delivered the bounded typed catalog: immutable endpoints, an
  instance-scoped catalog, escaped relative URI construction, a client over a
  consumer-owned `HttpClient`, bounded buffered responses that preserve
  non-success protocol evidence, and a Newtonsoft-backed body serializer chosen
  so both target families share one serializer semantic. The module took no
  NekoLib project dependency.

**Evidence:** [`../../history/phase-g1-http-integration-2026-08-16.md`](../../history/phase-g1-http-integration-2026-08-16.md), [`VALIDATIONS.md`](VALIDATIONS.md)

## 2026-08-16 — HTTP-HISTORY-002 — External provider boundary established

**Release:** 1.0.0-local.11

- The optional TheCatAPI scenario became the module's only provider evidence. It
  requires a maintainer-owned key, exits without sending a request when the key
  is absent, scopes its only provider-side mutation to a generated run-owned
  identifier, and reconciles cleanup in a `finally` block. Its artifacts record
  outcomes and timing and never contain the key, credential headers, or
  request/response bodies.

**Evidence:** [`../../../runtime_tests/Http/TheCatApi/README.md`](../../../runtime_tests/Http/TheCatApi/README.md), [`VALIDATIONS.md`](VALIDATIONS.md)

## 2026-08-16 — HTTP-HISTORY-003 — Recorded as design input for an unpromoted product idea

**Release:** 1.0.0-local.11

- The Payments/Pix design review examined this boundary and concluded that HTTP
  deliberately does not interpret Pix, OAuth, webhooks, idempotency, provider
  error envelopes, or reconciliation, and that changing it would weaken both
  modules. That conclusion is dated design input for an unpromoted proposal; it
  authorized no HTTP change and none was made.

**Evidence:** [`../../audit/payments-pix-design-review-2026-08-16.md`](../../audit/payments-pix-design-review-2026-08-16.md), [`../../proposals/payments-pix.md`](../../proposals/payments-pix.md)

## 2026-08-17 — HTTP-HISTORY-004 — Stable public API dispositions completed

**Release:** pre-1.0.0

- The F1-HTTP review accepted all sixteen dispositions, including both optional
  items. Unresolvable charsets stopped throwing so a response degrades
  identically on both targets; headers moved ahead of the body read so a
  size-bound failure keeps its status, reason phrase, and headers; the
  `HttpEndpoint` constructor became `private protected`, removing an extension
  point that never compiled; the unregistered-endpoint error began
  distinguishing an unknown name from a registered name supplied through a
  different instance; and option validation began reporting all three invalid
  cases as `ArgumentException` naming the caller's parameter. The proposed
  `System.Text.Encoding.CodePages` dependency was explicitly declined. The
  deterministic suite grew from 16 to 29 executed cases per target.

**Evidence:** [`audits/public-api-review-2026-08-17.md`](audits/public-api-review-2026-08-17.md), [`migrations/f1.md`](migrations/f1.md)

## 2026-08-18 — HTTP-HISTORY-005 — Package candidate qualified

**Release:** 1.0.0-local.20

- The coordinated clean package campaign qualified the F1 implementation and
  replaced the stale pre-F1 `1.0.0-local.11` package record carried by the module
  reference. The candidate declared `Newtonsoft.Json 13.0.3` in both dependency
  groups and passed every PackageReference-only consumer probe.

**Evidence:** [`audits/public-api-review-2026-08-17.md`](audits/public-api-review-2026-08-17.md), [`VALIDATIONS.md`](VALIDATIONS.md)

## 2026-08-21 — HTTP-HISTORY-006 — Stable 1.0.0 released

**Release:** 1.0.0

- `NekoLib.Http` joined the first coordinated stable NekoLib release with a
  materialized package hash and qualifying `1.0.0-local.22` evidence.

**Evidence:** [`../../stable-release-1.0.0.md`](../../stable-release-1.0.0.md), [`../../history/phase-f1-public-api-release-stability-2026-08-21.md`](../../history/phase-f1-public-api-release-stability-2026-08-21.md)

## 2026-08-28 — HTTP-HISTORY-007 — Extension contract documented and XML delivery qualified

**Release:** 1.1.0-local.8

- The family documentation campaign identified `IHttpBodySerializer` as this
  boundary's supported consumer extension seam, added its media-type,
  request/response typing, failure, ownership, and bypass rules to the module
  reference, closed the remaining public-member documentation gaps, and proved
  package-owned XML files and PackageReference delivery for both target assets.

**Evidence:** [`../../audit/public-api-documentation-extensibility-review-2026-08-27.md`](../../audit/public-api-documentation-extensibility-review-2026-08-27.md), [`VALIDATIONS.md`](VALIDATIONS.md)

## 2026-08-29 — HTTP-HISTORY-008 — Module-first documentation established

**Release:** unreleased documentation

- HTTP received one canonical module boundary with a manifest, concise
  introduction, normative reference, separate history and changelog, an
  issues/findings split, a risk-derived validation contract that keeps
  deterministic and provider evidence apart, the colocated F1 audit and
  migration, and a pointer-only source portal. The package, test, and provider
  evidence previously embedded in the reference moved to the executed-evidence
  register, and the reference dropped the historical reference date that a
  current technical reference may not carry.

**Evidence:** [`MANIFEST.md`](MANIFEST.md), [`../../governance/agent-documentation-contract.md`](../../governance/agent-documentation-contract.md)
