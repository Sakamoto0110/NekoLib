# NekoLib.Data Manifest

**Document ID:** DATA-MANIFEST

**Schema version:** 1

**Kind:** reference

**Lifecycle:** current

**Subject:** identity and documentation routing for the NekoLib.Data boundary

**Surface:** manifest

**Boundary:** data

**Authority role:** normative

**Mutation:** authored

**Indexing:** include

**Projects:** `src/Data/NekoLib.Data/NekoLib.Data.csproj`

**Packages:** `NekoLib.Data`

**Targets:** `net481`, `net9.0`

**Project dependencies:** none

**Package dependencies:** none

**Solution membership:** included

**Distribution:** shipped-library

**Stability:** stable

**Experimental APIs:** none

**API baselines:** `eng/public-api/NekoLib.Data/net481.approved.txt`, `eng/public-api/NekoLib.Data/net9.0.approved.txt`

**Profiles:** `standard-library`, `stateful-runtime`, `external-provider`

**Technical reference:** `docs/modules/Data/REFERENCE.md`

**Related boundaries:** none

**Source:** `src/Data/NekoLib.Data`

**Tests:** `tests/NekoLib.Data.Tests`

**Runtime scenarios:** `runtime_tests/Data/FarmDatabase`, `runtime_tests/Data/SqlServer`

**Package evidence:** `docs/stable-release-1.0.0.md`, `docs/audit/public-api-documentation-extensibility-review-2026-08-27.md`

This manifest routes the Data boundary. Project files and compiled assemblies
remain implementation and API truth, and the accepted manifests remain the
stable public API oracle. The module has no project or package dependency; its
database providers and provider configuration are consumer-owned.

The two compiled surfaces are intentionally not identical. `net9.0` adds the
asynchronous streaming capability and modern DTO trimming annotations;
`net481` contains neither a placeholder streaming contract nor a dependency
that emulates it. Every other target difference requires an explicit public-API
disposition before either accepted manifest changes.

## Current surfaces

| Need | Owner |
|---|---|
| Consumer introduction | [`README.md`](README.md) |
| Technical contract | [`REFERENCE.md`](REFERENCE.md) |
| Chronological module history | [`HISTORY.md`](HISTORY.md) |
| Consumer-visible module evolution | [`CHANGELOG.md`](CHANGELOG.md) |
| Confirmed issues | [`ISSUES.md`](ISSUES.md) |
| Unconfirmed findings | [`FINDINGS.md`](FINDINGS.md) |
| Unpromoted ideas | [`docs/proposals/`](../../proposals/README.md), filtered by `Boundary: data` |
| Evidence requirements | [`VALIDATION_REQUIREMENTS.md`](VALIDATION_REQUIREMENTS.md) |
| Executed evidence | [`VALIDATIONS.md`](VALIDATIONS.md) |
| Historical audits | [`audits/`](audits/) |
| Consumer migrations | [`migrations/`](migrations/) |
| Accepted compiled API | [`eng/public-api/NekoLib.Data/`](../../../eng/public-api/NekoLib.Data/) |
| Source-adjacent portal | [`src/Data/NekoLib.Data/README.md`](../../../src/Data/NekoLib.Data/README.md) |

## Repository evidence routes

- Project and source: [`src/Data/NekoLib.Data/`](../../../src/Data/NekoLib.Data/)
- Focused tests: [`tests/NekoLib.Data.Tests/Unit/`](../../../tests/NekoLib.Data.Tests/Unit/)
- Runtime scenarios: [`runtime_tests/Data/`](../../../runtime_tests/Data/)
- Coordinated changelog: [`CHANGELOG.md`](../../../CHANGELOG.md)
- Product direction and freezes: [`ROADMAP.md`](../../../ROADMAP.md)
- Promoted work: [`TODO.md`](../../../TODO.md)
- Stable release provenance: [`stable-release-1.0.0.md`](../../stable-release-1.0.0.md)
- Public API and release policy: [`public-api-release-policy.md`](../../public-api-release-policy.md)
- Verification taxonomy: [`tests/README.md`](../../../tests/README.md)
