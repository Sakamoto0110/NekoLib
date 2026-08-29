# NekoLib.Pipes Manifest

**Document ID:** PIPE-MANIFEST

**Schema version:** 1

**Kind:** reference

**Lifecycle:** current

**Subject:** identity and documentation routing for the NekoLib.Pipes boundary

**Surface:** manifest

**Boundary:** pipes

**Authority role:** normative

**Mutation:** authored

**Indexing:** include

**Projects:** `src/Pipes/NekoLib.Pipes/NekoLib.Pipes.csproj`

**Packages:** `NekoLib.Pipes`

**Targets:** `net481`, `net9.0`

**Project dependencies:** none

**Package dependencies:** `Newtonsoft.Json`

**Solution membership:** included

**Distribution:** shipped-library

**Stability:** stable

**Experimental APIs:** none

**API baselines:** `eng/public-api/NekoLib.Pipes/net481.approved.txt`, `eng/public-api/NekoLib.Pipes/net9.0.approved.txt`

**Profiles:** `standard-library`, `transport`

**Technical reference:** `docs/modules/Pipes/REFERENCE.md`

**Related boundaries:** `watchdog`

**Source:** `src/Pipes/NekoLib.Pipes`

**Tests:** `tests/NekoLib.Pipes.Tests`

**Runtime scenarios:** `runtime_tests/Pipes/LongRunningRecovery/README.md`

**Package evidence:** `docs/stable-release-1.0.0.md`, `docs/audit/public-api-documentation-extensibility-review-2026-08-27.md`

This manifest routes the Pipes boundary. The project file and the compiled
assemblies remain implementation and API truth; the accepted manifests under
`eng/public-api/NekoLib.Pipes/` remain the stable public API oracle.

`Newtonsoft.Json` is a `net481`-conditioned package dependency. The `net9.0`
asset carries no direct package dependency and uses `System.Text.Json` from the
platform.

## Current surfaces

| Need | Owner |
|---|---|
| Consumer introduction | [`README.md`](README.md) |
| Technical contract | [`REFERENCE.md`](REFERENCE.md) |
| Chronological module history | [`HISTORY.md`](HISTORY.md) |
| Consumer-visible module evolution | [`CHANGELOG.md`](CHANGELOG.md) |
| Confirmed issues | [`ISSUES.md`](ISSUES.md) |
| Unconfirmed findings | [`FINDINGS.md`](FINDINGS.md) |
| Unpromoted ideas | [`docs/proposals/`](../../proposals/README.md), filtered by `Boundary: pipes` |
| Evidence requirements | [`VALIDATION_REQUIREMENTS.md`](VALIDATION_REQUIREMENTS.md) |
| Executed evidence | [`VALIDATIONS.md`](VALIDATIONS.md) |
| Historical audits | [`audits/`](audits/) |
| Consumer migrations | [`migrations/`](migrations/) |
| Accepted compiled API | [`eng/public-api/NekoLib.Pipes/`](../../../eng/public-api/NekoLib.Pipes/) |
| Source-adjacent portal | [`src/Pipes/NekoLib.Pipes/README.md`](../../../src/Pipes/NekoLib.Pipes/README.md) |

## Repository evidence routes

- Project and source: [`src/Pipes/NekoLib.Pipes/`](../../../src/Pipes/NekoLib.Pipes/)
- Focused tests: [`tests/NekoLib.Pipes.Tests/Unit/`](../../../tests/NekoLib.Pipes.Tests/Unit/)
- Runtime scenario: [`runtime_tests/Pipes/LongRunningRecovery/`](../../../runtime_tests/Pipes/LongRunningRecovery/)
- Coordinated changelog: [`CHANGELOG.md`](../../../CHANGELOG.md)
- Product direction and freezes: [`ROADMAP.md`](../../../ROADMAP.md)
- Promoted work: [`TODO.md`](../../../TODO.md)
- Stable release provenance: [`stable-release-1.0.0.md`](../../stable-release-1.0.0.md)
- Public API and release policy: [`public-api-release-policy.md`](../../public-api-release-policy.md)
- Verification taxonomy: [`tests/README.md`](../../../tests/README.md)

`Related boundaries` records the Watchdog consumer relationship for traversal
only. Pipes declares no project reference in either direction; Watchdog
references Pipes, not the reverse.
