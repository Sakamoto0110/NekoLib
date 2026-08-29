# NekoLib.Diagnostics Manifest

**Document ID:** DIAG-MANIFEST

**Schema version:** 1

**Kind:** reference

**Lifecycle:** current

**Subject:** identity and documentation routing for the NekoLib.Diagnostics family boundary

**Surface:** manifest

**Boundary:** diagnostics

**Authority role:** normative

**Mutation:** authored

**Indexing:** include

**Projects:** `src/Diagnostics/NekoLib.Diagnostics/NekoLib.Diagnostics.csproj`, `src/Diagnostics/NekoLib.Diagnostics.Windows/NekoLib.Diagnostics.Windows.csproj`

**Packages:** `NekoLib.Diagnostics`, `NekoLib.Diagnostics.Windows`

**Targets:** `net481`, `net9.0`, `net9.0-windows`

**Project dependencies:** `NekoLib.Core`, `NekoLib.Diagnostics`

**Package dependencies:** none

**Solution membership:** included

**Distribution:** shipped-library

**Stability:** stable

**Experimental APIs:** none

**API baselines:** `eng/public-api/NekoLib.Diagnostics/net481.approved.txt`, `eng/public-api/NekoLib.Diagnostics/net9.0.approved.txt`, `eng/public-api/NekoLib.Diagnostics.Windows/net481.approved.txt`, `eng/public-api/NekoLib.Diagnostics.Windows/net9.0-windows.approved.txt`

**Profiles:** `standard-library`, `stateful-runtime`, `platform-adapter`

**Technical reference:** `docs/modules/Diagnostics/REFERENCE.md`

**Related boundaries:** `core`, `inspection`, `logging`, `telemetry`, `watchdog`

**Source:** `src/Diagnostics/NekoLib.Diagnostics`, `src/Diagnostics/NekoLib.Diagnostics.Windows`

**Tests:** `tests/NekoLib.Diagnostics.Tests`

**Runtime scenarios:** none

**Package evidence:** `docs/stable-release-1.0.0.md`, `docs/audit/public-api-documentation-extensibility-review-2026-08-27.md`

This manifest routes one Diagnostics family boundary containing the
cross-platform incident-orchestration package and its Windows adapter. Project
files and compiled assemblies remain implementation and API truth; accepted
manifests remain the stable public API oracle.

## Current surfaces

| Need | Owner |
|---|---|
| Consumer introduction | [`README.md`](README.md) |
| Technical contract | [`REFERENCE.md`](REFERENCE.md) |
| Chronological module history | [`HISTORY.md`](HISTORY.md) |
| Consumer-visible module evolution | [`CHANGELOG.md`](CHANGELOG.md) |
| Confirmed issues | [`ISSUES.md`](ISSUES.md) |
| Unconfirmed findings | [`FINDINGS.md`](FINDINGS.md) |
| Unpromoted ideas | [`docs/proposals/`](../../proposals/README.md), filtered by `Boundary: diagnostics` |
| Evidence requirements | [`VALIDATION_REQUIREMENTS.md`](VALIDATION_REQUIREMENTS.md) |
| Executed evidence | [`VALIDATIONS.md`](VALIDATIONS.md) |
| Historical module audits | [`audits/`](audits/) |
| Cross-cutting Diagnostics-sector audit | [`diagnostics-boundaries-review-2026-07-30.md`](../../audit/diagnostics-boundaries-review-2026-07-30.md) |
| Consumer migrations | [`migrations/`](migrations/) |
| Accepted compiled API | [`eng/public-api/NekoLib.Diagnostics*/`](../../../eng/public-api/) |

## Repository evidence routes

- Projects and source: [`src/Diagnostics/`](../../../src/Diagnostics/)
- Focused tests: [`tests/NekoLib.Diagnostics.Tests/`](../../../tests/NekoLib.Diagnostics.Tests/)
- Package consumers: [`tests/NekoLib.PackageConsumers/`](../../../tests/NekoLib.PackageConsumers/)
- Coordinated changelog: [`CHANGELOG.md`](../../../CHANGELOG.md)
- Product direction and confidentiality horizon: [`ROADMAP.md`](../../../ROADMAP.md)
- Promoted work: [`TODO.md`](../../../TODO.md)
- Stable release provenance: [`stable-release-1.0.0.md`](../../stable-release-1.0.0.md)
- XML package-delivery evidence: [`public-api-documentation-extensibility-review-2026-08-27.md`](../../audit/public-api-documentation-extensibility-review-2026-08-27.md)
- Public API and release policy: [`public-api-release-policy.md`](../../public-api-release-policy.md)

The module registers reconcile current source and accepted API with preserved
audit, migration, release, test, package-consumer, and package evidence.
Requirements describe the qualification contract; validation records identify
only evidence that actually ran and retain their recorded gaps.
