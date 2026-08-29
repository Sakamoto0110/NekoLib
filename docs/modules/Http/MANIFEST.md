# NekoLib.Http Manifest

**Document ID:** HTTP-MANIFEST

**Schema version:** 1

**Kind:** reference

**Lifecycle:** current

**Subject:** identity and documentation routing for the NekoLib.Http boundary

**Surface:** manifest

**Boundary:** http

**Authority role:** normative

**Mutation:** authored

**Indexing:** include

**Projects:** `src/Http/NekoLib.Http/NekoLib.Http.csproj`

**Packages:** `NekoLib.Http`

**Targets:** `net481`, `net9.0`

**Project dependencies:** none

**Package dependencies:** `Newtonsoft.Json`

**Solution membership:** included

**Distribution:** shipped-library

**Stability:** stable

**Experimental APIs:** none

**API baselines:** `eng/public-api/NekoLib.Http/net481.approved.txt`, `eng/public-api/NekoLib.Http/net9.0.approved.txt`

**Profiles:** `standard-library`, `external-provider`

**Technical reference:** `docs/modules/Http/REFERENCE.md`

**Related boundaries:** none

**Source:** `src/Http/NekoLib.Http`

**Tests:** `tests/NekoLib.Http.Tests`

**Runtime scenarios:** `runtime_tests/Http/TheCatApi`

**Package evidence:** `docs/stable-release-1.0.0.md`, `docs/audit/public-api-documentation-extensibility-review-2026-08-27.md`

This manifest routes one HTTP boundary: an opt-in typed endpoint catalog and a
client that executes it through a consumer-owned `HttpClient`. Project files and
compiled assemblies remain implementation and API truth, and the accepted
manifests remain the stable public API oracle.

Two identity facts are unusual for this family and are deliberate. `Project
dependencies` is `none`: HTTP references no NekoLib project, not even
`NekoLib.Core`. `Related boundaries` is also `none`: no repository module
consumes HTTP, so it has no adapter, producer, or consumer relationship to
document. Its only in-repository consumers are its own deterministic tests, its
own provider scenario, and the package-consumer probes. `Newtonsoft.Json` is a
real package dependency that reaches the compiled public surface through one
`JsonHttpBodySerializer` constructor overload.

## Current surfaces

| Need | Owner |
|---|---|
| Consumer introduction | [`README.md`](README.md) |
| Technical contract | [`REFERENCE.md`](REFERENCE.md) |
| Chronological module history | [`HISTORY.md`](HISTORY.md) |
| Consumer-visible module evolution | [`CHANGELOG.md`](CHANGELOG.md) |
| Confirmed issues | [`ISSUES.md`](ISSUES.md) |
| Unconfirmed findings | [`FINDINGS.md`](FINDINGS.md) |
| Unpromoted ideas | [`docs/proposals/`](../../proposals/README.md), filtered by `Boundary: http` |
| Evidence requirements | [`VALIDATION_REQUIREMENTS.md`](VALIDATION_REQUIREMENTS.md) |
| Executed evidence | [`VALIDATIONS.md`](VALIDATIONS.md) |
| Historical module audits | [`audits/`](audits/) |
| Consumer migrations | [`migrations/`](migrations/) |
| Accepted compiled API | [`eng/public-api/NekoLib.Http/`](../../../eng/public-api/NekoLib.Http) |

## Repository evidence routes

- Project and source: [`src/Http/NekoLib.Http/`](../../../src/Http/NekoLib.Http/)
- Deterministic tests: [`tests/NekoLib.Http.Tests/`](../../../tests/NekoLib.Http.Tests/)
- External provider scenario: [`runtime_tests/Http/TheCatApi/`](../../../runtime_tests/Http/TheCatApi/README.md)
- Package consumers: [`tests/NekoLib.PackageConsumers/`](../../../tests/NekoLib.PackageConsumers/)
- Coordinated changelog: [`CHANGELOG.md`](../../../CHANGELOG.md)
- Product direction and freezes: [`ROADMAP.md`](../../../ROADMAP.md)
- Promoted work: [`TODO.md`](../../../TODO.md)
- Stable release provenance: [`stable-release-1.0.0.md`](../../stable-release-1.0.0.md)
- XML package-delivery evidence: [`public-api-documentation-extensibility-review-2026-08-27.md`](../../audit/public-api-documentation-extensibility-review-2026-08-27.md)
- Public API and release policy: [`public-api-release-policy.md`](../../public-api-release-policy.md)
- Dated design input citing this boundary: [`payments-pix-design-review-2026-08-16.md`](../../audit/payments-pix-design-review-2026-08-16.md)

The deterministic tests use a controlled `HttpMessageHandler` and reach no
network. The TheCatAPI scenario is the only source of provider evidence, needs a
maintainer-owned key, and proves provider behavior only for the run it records.
The two are separate evidence layers and neither substitutes for the other.
Requirements describe the qualification contract; validation records identify
only evidence that actually ran, with its recorded gaps.
