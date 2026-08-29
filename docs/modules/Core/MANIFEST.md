# NekoLib.Core Manifest

**Document ID:** CORE-MANIFEST

**Schema version:** 1

**Kind:** reference

**Lifecycle:** current

**Subject:** identity and documentation routing for the NekoLib.Core boundary

**Surface:** manifest

**Boundary:** core

**Authority role:** normative

**Mutation:** authored

**Indexing:** include

**Projects:** `src/Core/NekoLib.Core/NekoLib.Core.csproj`

**Packages:** `NekoLib.Core`

**Targets:** `net481`, `net9.0`

**Project dependencies:** none

**Package dependencies:** none

**Solution membership:** included

**Distribution:** shipped-library

**Stability:** stable

**Experimental APIs:** `NEKOEXP0001`

**API baselines:** `eng/public-api/NekoLib.Core/net481.approved.txt`, `eng/public-api/NekoLib.Core/net9.0.approved.txt`

**Profiles:** `standard-library`

**Technical reference:** `docs/modules/Core/REFERENCE.md`

**Related boundaries:** `logging`, `telemetry`, `inspection`, `diagnostics`, `navigation`, `watchdog`

**Source:** `src/Core/NekoLib.Core`

**Tests:** `tests/NekoLib.Core.Tests`

**Runtime scenarios:** none

**Package evidence:** `docs/stable-release-1.0.0.md`, `docs/audit/public-api-documentation-extensibility-review-2026-08-27.md`

This manifest routes the shared contract boundary. Core contains contracts,
small structurally read-only models, null objects, `Disposable.Empty`, and the
explicit Inspection provider slot. It does not own the concrete Logging,
Telemetry, Inspection, Diagnostics, Navigation, or Watchdog implementations.
Project files and compiled assemblies remain implementation and API truth; the
accepted manifests remain the stable public API oracle.

## Current surfaces

| Need | Owner |
|---|---|
| Consumer introduction | [`README.md`](README.md) |
| Technical contract | [`REFERENCE.md`](REFERENCE.md) |
| Chronological module history | [`HISTORY.md`](HISTORY.md) |
| Consumer-visible module evolution | [`CHANGELOG.md`](CHANGELOG.md) |
| Confirmed issues | [`ISSUES.md`](ISSUES.md) |
| Unconfirmed findings | [`FINDINGS.md`](FINDINGS.md) |
| Unpromoted ideas | [`docs/proposals/`](../../proposals/README.md), filtered by `Boundary: core` |
| Evidence requirements | [`VALIDATION_REQUIREMENTS.md`](VALIDATION_REQUIREMENTS.md) |
| Executed evidence | [`VALIDATIONS.md`](VALIDATIONS.md) |
| Historical module audits | [`audits/`](audits/) |
| Consumer migrations | [`migrations/`](migrations/) |
| Accepted compiled API | [`eng/public-api/NekoLib.Core/`](../../../eng/public-api/NekoLib.Core/) |

## Repository evidence routes

- Project and source: [`src/Core/NekoLib.Core/`](../../../src/Core/NekoLib.Core/)
- Focused tests: [`tests/NekoLib.Core.Tests/`](../../../tests/NekoLib.Core.Tests/)
- Package consumers: [`tests/NekoLib.PackageConsumers/`](../../../tests/NekoLib.PackageConsumers/)
- Coordinated changelog: [`CHANGELOG.md`](../../../CHANGELOG.md)
- Product direction and Inspection freeze: [`ROADMAP.md`](../../../ROADMAP.md)
- Promoted work: [`TODO.md`](../../../TODO.md)
- Stable release provenance: [`stable-release-1.0.0.md`](../../stable-release-1.0.0.md)
- XML package-delivery evidence: [`public-api-documentation-extensibility-review-2026-08-27.md`](../../audit/public-api-documentation-extensibility-review-2026-08-27.md)
- Public API and release policy: [`public-api-release-policy.md`](../../public-api-release-policy.md)
- Concrete capability contracts: [`Logging`](../Logging/REFERENCE.md), [`Telemetry`](../Telemetry/REFERENCE.md), [`Inspection`](../Inspection/REFERENCE.md)

Core has no standalone runtime scenario because it contains no pipeline,
transport, persistence, platform hook, or background runtime. Runtime evidence
for a concrete implementation belongs to that implementation's boundary.
Package and PackageReference evidence remain separate from source, build, test,
and API evidence.
