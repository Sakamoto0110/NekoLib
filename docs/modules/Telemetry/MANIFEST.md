# NekoLib.Telemetry Manifest

**Document ID:** TEL-MANIFEST

**Schema version:** 1

**Kind:** reference

**Lifecycle:** current

**Subject:** identity and documentation routing for the NekoLib.Telemetry boundary

**Surface:** manifest

**Boundary:** telemetry

**Authority role:** normative

**Mutation:** authored

**Indexing:** include

**Projects:** `src/Telemetry/NekoLib.Telemetry/NekoLib.Telemetry.csproj`

**Packages:** `NekoLib.Telemetry`

**Targets:** `net481`, `net9.0`

**Project dependencies:** `NekoLib.Core`

**Package dependencies:** none

**Solution membership:** included

**Distribution:** shipped-library

**Stability:** stable

**Experimental APIs:** none

**API baselines:** `eng/public-api/NekoLib.Telemetry/net481.approved.txt`, `eng/public-api/NekoLib.Telemetry/net9.0.approved.txt`

**Profiles:** `standard-library`, `stateful-runtime`

**Technical reference:** `docs/modules/Telemetry/REFERENCE.md`

**Related boundaries:** `core`, `diagnostics`, `inspection`, `logging`, `navigation`

**Source:** `src/Telemetry/NekoLib.Telemetry`

**Tests:** `tests/NekoLib.Telemetry.Tests`

**Runtime scenarios:** `runtime_tests/Observability/LongRunningRecovery`

**Package evidence:** `docs/stable-release-1.0.0.md`, `docs/audit/public-api-documentation-extensibility-review-2026-08-27.md`

This manifest routes one Telemetry boundary: the concrete in-process operation
timing pipeline. Telemetry is a capability distinct from Logging and Inspection;
the three share a composition root and a runtime scenario, not an authority.
Project files and compiled assemblies remain implementation and API truth, and
the accepted manifests remain the stable public API oracle.

## Current surfaces

| Need | Owner |
|---|---|
| Consumer introduction | [`README.md`](README.md) |
| Technical contract | [`REFERENCE.md`](REFERENCE.md) |
| Chronological module history | [`HISTORY.md`](HISTORY.md) |
| Consumer-visible module evolution | [`CHANGELOG.md`](CHANGELOG.md) |
| Confirmed issues | [`ISSUES.md`](ISSUES.md) |
| Unconfirmed findings | [`FINDINGS.md`](FINDINGS.md) |
| Unpromoted ideas | [`docs/proposals/`](../../proposals/README.md), filtered by `Boundary: telemetry` |
| Evidence requirements | [`VALIDATION_REQUIREMENTS.md`](VALIDATION_REQUIREMENTS.md) |
| Executed evidence | [`VALIDATIONS.md`](VALIDATIONS.md) |
| Historical module audits | [`audits/`](audits/) |
| Consumer migrations | [`migrations/`](migrations/) |
| Accepted compiled API | [`eng/public-api/NekoLib.Telemetry/`](../../../eng/public-api/NekoLib.Telemetry) |

## Repository evidence routes

- Project and source: [`src/Telemetry/NekoLib.Telemetry/`](../../../src/Telemetry/NekoLib.Telemetry/)
- Focused tests: [`tests/NekoLib.Telemetry.Tests/`](../../../tests/NekoLib.Telemetry.Tests/)
- Shared runtime scenario: [`runtime_tests/Observability/LongRunningRecovery/`](../../../runtime_tests/Observability/LongRunningRecovery/README.md)
- Package consumers: [`tests/NekoLib.PackageConsumers/`](../../../tests/NekoLib.PackageConsumers/)
- Core telemetry contracts: [`src/Core/NekoLib.Core/README.md`](../../../src/Core/NekoLib.Core/README.md)
- First in-repository producer: [`docs/modules/Navigation/REFERENCE.md`](../Navigation/REFERENCE.md)
- Coordinated changelog: [`CHANGELOG.md`](../../../CHANGELOG.md)
- Product direction and freezes: [`ROADMAP.md`](../../../ROADMAP.md)
- Promoted work: [`TODO.md`](../../../TODO.md)
- Stable release provenance: [`stable-release-1.0.0.md`](../../stable-release-1.0.0.md)
- XML package-delivery evidence: [`public-api-documentation-extensibility-review-2026-08-27.md`](../../audit/public-api-documentation-extensibility-review-2026-08-27.md)
- Public API and release policy: [`public-api-release-policy.md`](../../public-api-release-policy.md)

The shared Observability scenario exercises Logging, Telemetry, and Inspection in
one process. Its Telemetry checks are Telemetry evidence; its Logging and
Inspection checks are not. Requirements describe the qualification contract, and
validation records identify only evidence that actually ran with its recorded
gaps.
