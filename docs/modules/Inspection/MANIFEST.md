# NekoLib.Inspection Manifest

**Document ID:** INSP-MANIFEST

**Schema version:** 1

**Kind:** reference

**Lifecycle:** current

**Subject:** identity and documentation routing for the NekoLib.Inspection boundary

**Surface:** manifest

**Boundary:** inspection

**Authority role:** normative

**Mutation:** authored

**Indexing:** include

**Projects:** `src/Inspection/NekoLib.Inspection/NekoLib.Inspection.csproj`

**Packages:** `NekoLib.Inspection`

**Targets:** `net481`, `net9.0`

**Project dependencies:** `NekoLib.Core`

**Package dependencies:** none

**Solution membership:** included

**Distribution:** shipped-library

**Stability:** stable

**Experimental APIs:** `NEKOEXP0001`

**API baselines:** `eng/public-api/NekoLib.Inspection/net481.approved.txt`, `eng/public-api/NekoLib.Inspection/net9.0.approved.txt`

**Profiles:** `standard-library`, `stateful-runtime`

**Technical reference:** `docs/modules/Inspection/REFERENCE.md`

**Related boundaries:** `core`, `diagnostics`, `logging`, `navigation`, `telemetry`

**Source:** `src/Inspection/NekoLib.Inspection`

**Tests:** `tests/NekoLib.Inspection.Tests`

**Runtime scenarios:** `runtime_tests/Observability/LongRunningRecovery`

**Package evidence:** `docs/stable-release-1.0.0.md`, `docs/audit/public-api-documentation-extensibility-review-2026-08-27.md`

This manifest routes one Inspection boundary: the opt-in passive in-process
runtime behind the Core Inspection contracts. Inspection is a capability
distinct from Logging and Telemetry; the three share a composition root and a
runtime scenario, not an authority. Project files and compiled assemblies remain
implementation and API truth, and the accepted manifests remain the stable public
API oracle.

The package is `stable` while four of its members carry the experimental
`NEKOEXP0001` marker. That combination is deliberate: the passive recording,
provider, snapshot, diagnostics, and lifecycle surface is stable, and only the
concrete action registration and invocation members are pre-stable. The marker
is release signaling and is not an authorization boundary. The broad
instrumentation and action rollout remains frozen in
[`ROADMAP.md`](../../../ROADMAP.md).

## Current surfaces

| Need | Owner |
|---|---|
| Consumer introduction | [`README.md`](README.md) |
| Technical contract | [`REFERENCE.md`](REFERENCE.md) |
| Chronological module history | [`HISTORY.md`](HISTORY.md) |
| Consumer-visible module evolution | [`CHANGELOG.md`](CHANGELOG.md) |
| Confirmed issues | [`ISSUES.md`](ISSUES.md) |
| Unconfirmed findings | [`FINDINGS.md`](FINDINGS.md) |
| Unpromoted ideas | [`docs/proposals/`](../../proposals/README.md), filtered by `Boundary: inspection` |
| Evidence requirements | [`VALIDATION_REQUIREMENTS.md`](VALIDATION_REQUIREMENTS.md) |
| Executed evidence | [`VALIDATIONS.md`](VALIDATIONS.md) |
| Historical module audits | [`audits/`](audits/) |
| Consumer migrations | [`migrations/`](migrations/) |
| Accepted compiled API | [`eng/public-api/NekoLib.Inspection/`](../../../eng/public-api/NekoLib.Inspection) |
| Active instrumentation and action freeze | [`ROADMAP.md`](../../../ROADMAP.md) |

## Repository evidence routes

- Project and source: [`src/Inspection/NekoLib.Inspection/`](../../../src/Inspection/NekoLib.Inspection/)
- Focused tests: [`tests/NekoLib.Inspection.Tests/`](../../../tests/NekoLib.Inspection.Tests/)
- Shared runtime scenario: [`runtime_tests/Observability/LongRunningRecovery/`](../../../runtime_tests/Observability/LongRunningRecovery/README.md)
- Package consumers: [`tests/NekoLib.PackageConsumers/`](../../../tests/NekoLib.PackageConsumers/)
- Core Inspection contracts and the process-wide slot: [`src/Core/NekoLib.Core/README.md`](../../../src/Core/NekoLib.Core/README.md)
- Only in-repository producer: [`docs/modules/Navigation/REFERENCE.md`](../Navigation/REFERENCE.md)
- Read-only evidence consumer: [`docs/modules/Diagnostics/REFERENCE.md`](../Diagnostics/REFERENCE.md)
- Coordinated changelog: [`CHANGELOG.md`](../../../CHANGELOG.md)
- Product direction and freezes: [`ROADMAP.md`](../../../ROADMAP.md)
- Promoted work: [`TODO.md`](../../../TODO.md)
- Stable release provenance: [`stable-release-1.0.0.md`](../../stable-release-1.0.0.md)
- XML package-delivery evidence: [`public-api-documentation-extensibility-review-2026-08-27.md`](../../audit/public-api-documentation-extensibility-review-2026-08-27.md)
- Public API and release policy: [`public-api-release-policy.md`](../../public-api-release-policy.md)

The shared Observability scenario exercises Logging, Telemetry, and Inspection in
one process. Its Inspection checks are Inspection evidence; its Logging and
Telemetry checks are not. Requirements describe the qualification contract, and
validation records identify only evidence that actually ran with its recorded
gaps.
