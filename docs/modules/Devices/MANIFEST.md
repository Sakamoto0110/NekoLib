# NekoLib.Devices Manifest

**Document ID:** DEV-MANIFEST

**Schema version:** 1

**Kind:** reference

**Lifecycle:** current

**Subject:** identity and documentation routing for the NekoLib.Devices boundary

**Surface:** manifest

**Boundary:** devices

**Authority role:** normative

**Mutation:** authored

**Indexing:** include

**Projects:** `src/Devices/NekoLib.Devices/NekoLib.Devices.csproj`

**Packages:** `NekoLib.Devices`

**Targets:** `net481`, `net9.0`

**Project dependencies:** none

**Package dependencies:** `System.IO.Ports`, `Microsoft.Bcl.AsyncInterfaces`

**Solution membership:** included

**Distribution:** shipped-library

**Stability:** stable

**Experimental APIs:** none

**API baselines:** `eng/public-api/NekoLib.Devices/net481.approved.txt`, `eng/public-api/NekoLib.Devices/net9.0.approved.txt`

**Profiles:** `standard-library`, `transport`

**Technical reference:** `docs/modules/Devices/REFERENCE.md`

**Related boundaries:** none

**Source:** `src/Devices/NekoLib.Devices`

**Tests:** `tests/NekoLib.Devices.Tests`

**Runtime scenarios:** `runtime_tests/Devices/Com0Com/README.md`

**Package evidence:** `docs/stable-release-1.0.0.md`, `docs/modules/Devices/audits/public-api-review-2026-08-17.md`

This manifest routes the Devices boundary. The project file and the compiled
assemblies remain implementation and API truth; the accepted manifests under
`eng/public-api/NekoLib.Devices/` remain the stable public API oracle.

Both package dependencies are target-conditioned. `System.IO.Ports` applies to
`net9.0` and `Microsoft.Bcl.AsyncInterfaces` to `net481`; neither is declared
for both targets.

`Related boundaries` is `none`. The named-pipe transport uses `System.IO.Pipes`
directly and does not route through the `pipes` boundary.

## Current surfaces

| Need | Owner |
|---|---|
| Consumer introduction | [`README.md`](README.md) |
| Technical contract | [`REFERENCE.md`](REFERENCE.md) |
| Chronological module history | [`HISTORY.md`](HISTORY.md) |
| Consumer-visible module evolution | [`CHANGELOG.md`](CHANGELOG.md) |
| Confirmed issues | [`ISSUES.md`](ISSUES.md) |
| Unconfirmed findings | [`FINDINGS.md`](FINDINGS.md) |
| Unpromoted ideas | [`docs/proposals/`](../../proposals/README.md), filtered by `Boundary: devices` |
| Evidence requirements | [`VALIDATION_REQUIREMENTS.md`](VALIDATION_REQUIREMENTS.md) |
| Executed evidence | [`VALIDATIONS.md`](VALIDATIONS.md) |
| Historical audits | [`audits/`](audits/) |
| Consumer migrations | [`migrations/`](migrations/) |
| Accepted compiled API | [`eng/public-api/NekoLib.Devices/`](../../../eng/public-api/NekoLib.Devices/) |
| Source-adjacent portal | [`src/Devices/NekoLib.Devices/README.md`](../../../src/Devices/NekoLib.Devices/README.md) |

## Repository evidence routes

- Project and source: [`src/Devices/NekoLib.Devices/`](../../../src/Devices/NekoLib.Devices/)
- Focused tests: [`tests/NekoLib.Devices.Tests/Unit/`](../../../tests/NekoLib.Devices.Tests/Unit/)
- Runtime scenario: [`runtime_tests/Devices/Com0Com/`](../../../runtime_tests/Devices/Com0Com/)
- Coordinated changelog: [`CHANGELOG.md`](../../../CHANGELOG.md)
- Product direction and freezes: [`ROADMAP.md`](../../../ROADMAP.md)
- Promoted work: [`TODO.md`](../../../TODO.md)
- Stable release provenance: [`stable-release-1.0.0.md`](../../stable-release-1.0.0.md)
- Public API and release policy: [`public-api-release-policy.md`](../../public-api-release-policy.md)
- Verification taxonomy: [`tests/README.md`](../../../tests/README.md)
