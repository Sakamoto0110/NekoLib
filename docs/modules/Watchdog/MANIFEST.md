# NekoLib.Watchdog Manifest

**Document ID:** WDG-MANIFEST

**Schema version:** 1

**Kind:** reference

**Lifecycle:** current

**Subject:** identity and documentation routing for the NekoLib.Watchdog boundary

**Surface:** manifest

**Boundary:** watchdog

**Authority role:** normative

**Mutation:** authored

**Indexing:** include

**Projects:** `src/Watchdog/NekoLib.Watchdog/NekoLib.Watchdog.csproj`

**Packages:** `NekoLib.Watchdog`

**Targets:** `net481`, `net9.0-windows`

**Project dependencies:** `NekoLib.Core`, `NekoLib.Pipes`

**Package dependencies:** `Newtonsoft.Json`

**Solution membership:** included

**Distribution:** shipped-library

**Stability:** stable

**Experimental APIs:** none

**API baselines:** `eng/public-api/NekoLib.Watchdog/net481.approved.txt`, `eng/public-api/NekoLib.Watchdog/net9.0-windows.approved.txt`

**Profiles:** `standard-library`, `supervisor`

**Technical reference:** `docs/modules/Watchdog/REFERENCE.md`

**Related boundaries:** `pipes`, `watchdog.host`

**Source:** `src/Watchdog/NekoLib.Watchdog`

**Tests:** `tests/NekoLib.Watchdog.Tests`

**Runtime scenarios:** `runtime_tests/Watchdog/CrashRecovery/README.md`, `runtime_tests/Watchdog/Supervisor481/README.md`

**Package evidence:** `docs/stable-release-1.0.0.md`, `docs/audit/public-api-documentation-extensibility-review-2026-08-27.md`

This manifest routes the shipped `NekoLib.Watchdog` supervision library. The
project file and the compiled assemblies remain implementation and API truth;
the accepted manifests under `eng/public-api/NekoLib.Watchdog/` remain the
stable public API oracle.

`Newtonsoft.Json` is a `net481`-conditioned package dependency. The
`net9.0-windows` asset carries no direct package dependency and uses
`System.Text.Json` from the platform. Both targets reference `NekoLib.Core` for
the public `ILogSink`, `LogEntry`, and `ITelemetry` contracts and `NekoLib.Pipes`
for the control and event transport.

This boundary is the managed supervision library only. The separately
distributed `NekoLib.Watchdog.Host` sidecar owns deployment payloads, RID
selection, build and publish targets, executable arguments, and package-only
consumption; it is a different distribution with its own evidence and is not
covered by the API baselines above.

## Current surfaces

| Need | Owner |
|---|---|
| Consumer introduction | [`README.md`](README.md) |
| Technical contract | [`REFERENCE.md`](REFERENCE.md) |
| Chronological module history | [`HISTORY.md`](HISTORY.md) |
| Consumer-visible module evolution | [`CHANGELOG.md`](CHANGELOG.md) |
| Confirmed issues | [`ISSUES.md`](ISSUES.md) |
| Unconfirmed findings | [`FINDINGS.md`](FINDINGS.md) |
| Unpromoted ideas | [`docs/proposals/`](../../proposals/README.md), filtered by `Boundary: watchdog` |
| Evidence requirements | [`VALIDATION_REQUIREMENTS.md`](VALIDATION_REQUIREMENTS.md) |
| Executed evidence | [`VALIDATIONS.md`](VALIDATIONS.md) |
| Historical audits | [`audits/`](audits/) |
| Consumer migrations | [`migrations/`](migrations/) |
| Accepted compiled API | [`eng/public-api/NekoLib.Watchdog/`](../../../eng/public-api/NekoLib.Watchdog/) |
| Source-adjacent portal | [`src/Watchdog/NekoLib.Watchdog/README.md`](../../../src/Watchdog/NekoLib.Watchdog/README.md) |

## Repository evidence routes

- Project and source: [`src/Watchdog/NekoLib.Watchdog/`](../../../src/Watchdog/NekoLib.Watchdog/)
- Focused tests: [`tests/NekoLib.Watchdog.Tests/Unit/`](../../../tests/NekoLib.Watchdog.Tests/Unit/)
- Deployed-Host crash and recovery scenario: [`runtime_tests/Watchdog/CrashRecovery/`](../../../runtime_tests/Watchdog/CrashRecovery/)
- Interactive in-process supervisor scenario: [`runtime_tests/Watchdog/Supervisor481/`](../../../runtime_tests/Watchdog/Supervisor481/)
- Transport boundary: [`Pipes manifest`](../Pipes/MANIFEST.md)
- Deployment boundary: [`Watchdog Host reference`](../../../src/Watchdog/NekoLib.Watchdog.Host/README.md)
- Coordinated changelog: [`CHANGELOG.md`](../../../CHANGELOG.md)
- Product direction and freezes: [`ROADMAP.md`](../../../ROADMAP.md)
- Promoted work: [`TODO.md`](../../../TODO.md)
- Stable release provenance: [`stable-release-1.0.0.md`](../../stable-release-1.0.0.md)
- Public API and release policy: [`public-api-release-policy.md`](../../public-api-release-policy.md)
- Verification taxonomy: [`tests/README.md`](../../../tests/README.md)

`Related boundaries` records traversal relationships only. Watchdog references
Pipes and Core; neither references Watchdog. `NekoLib.Watchdog.Host` references
this library, not the reverse.

The focused suite under `tests/NekoLib.Watchdog.Tests/Unit/` covers both this
boundary and the Host executable. Host-scoped cases in that project are Host
evidence and do not qualify managed API claims here.
