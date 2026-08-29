# NekoLib.Watchdog.Host Manifest

**Document ID:** WDGHOST-MANIFEST

**Schema version:** 1

**Kind:** reference

**Lifecycle:** current

**Subject:** identity and documentation routing for the NekoLib.Watchdog.Host boundary

**Surface:** manifest

**Boundary:** watchdog.host

**Authority role:** normative

**Mutation:** authored

**Indexing:** include

**Projects:** `src/Watchdog/NekoLib.Watchdog.Host/NekoLib.Watchdog.Host.csproj`

**Packages:** `NekoLib.Watchdog.Host`

**Targets:** `net481`, `net9.0-windows`

**Project dependencies:** `NekoLib.Watchdog`

**Package dependencies:** none

**Solution membership:** included

**Distribution:** deployment-package

**Stability:** stable

**Experimental APIs:** none

**API baselines:** none

**Profiles:** `deployment-package`

**Technical reference:** `docs/modules/WatchdogHost/REFERENCE.md`

**Related boundaries:** `watchdog`

**Source:** `src/Watchdog/NekoLib.Watchdog.Host`

**Tests:** `tests/NekoLib.Watchdog.Tests`, `tests/NekoLib.PackageConsumers`

**Runtime scenarios:** `runtime_tests/Watchdog/CrashRecovery/README.md`

**Package evidence:** `docs/stable-release-1.0.0.md`, `docs/modules/WatchdogHost/audits/contract-review-2026-08-20.md`

This manifest routes the `NekoLib.Watchdog.Host` deployment package. The project
file, the package targets file, and the produced package remain implementation
and layout truth.

**There is no API baseline, and that is correct.** The Host compiles to a
`WinExe` that exports zero public types, so an assembly-derived manifest would be
empty and would baseline nothing. Its public contract is instead the deployment
and protocol surface owned by [`REFERENCE.md`](REFERENCE.md): payload roots,
consumer MSBuild properties, deployment destination and replacement, protocol v1,
exit codes, and fatal evidence. Do not create an `eng/public-api` directory for
this package.

The Host is `IsPackable` only when `NekoLibWatchdogHostPayloadRoot` supplies
pre-published RID payloads, so `dotnet pack NekoLib.sln` deliberately omits it.
`eng/pack-local.ps1` is the canonical entry point that publishes the three
payloads before packing. The package carries no `lib/` asset and therefore no
managed XML documentation; the family documentation gate excludes it by design.

This boundary is the deployment sidecar only. The supervision behavior it hosts
— configuration, lifecycle, control, log forwarding, crash evidence, hotkeys,
and the security model — belongs to the separate `NekoLib.Watchdog` library
boundary and is not restated here.

## Current surfaces

| Need | Owner |
|---|---|
| Consumer introduction | [`README.md`](README.md) |
| Deployment and protocol contract | [`REFERENCE.md`](REFERENCE.md) |
| Chronological module history | [`HISTORY.md`](HISTORY.md) |
| Consumer-visible module evolution | [`CHANGELOG.md`](CHANGELOG.md) |
| Confirmed issues | [`ISSUES.md`](ISSUES.md) |
| Unconfirmed findings | [`FINDINGS.md`](FINDINGS.md) |
| Unpromoted ideas | [`docs/proposals/`](../../proposals/README.md), filtered by `Boundary: watchdog.host` |
| Evidence requirements | [`VALIDATION_REQUIREMENTS.md`](VALIDATION_REQUIREMENTS.md) |
| Executed evidence | [`VALIDATIONS.md`](VALIDATIONS.md) |
| Historical audits | [`audits/`](audits/) |
| Consumer migrations | [`migrations/`](migrations/) |
| Source-adjacent portal | [`src/Watchdog/NekoLib.Watchdog.Host/README.md`](../../../src/Watchdog/NekoLib.Watchdog.Host/README.md) |

## Repository evidence routes

- Project, entry point, parser, and fatal log: [`src/Watchdog/NekoLib.Watchdog.Host/`](../../../src/Watchdog/NekoLib.Watchdog.Host/)
- Consumer deployment targets: [`NekoLib.Watchdog.Host.Package.targets`](../../../src/Watchdog/NekoLib.Watchdog.Host/NekoLib.Watchdog.Host.Package.targets)
- Focused parser and fatal-log tests: [`tests/NekoLib.Watchdog.Tests/Unit/`](../../../tests/NekoLib.Watchdog.Tests/Unit/)
- Package-only consumer probes: [`tests/NekoLib.PackageConsumers/`](../../../tests/NekoLib.PackageConsumers/)
- Canonical pack and probe flow: [`eng/pack-local.ps1`](../../../eng/pack-local.ps1), [`eng/test-local-packages.ps1`](../../../eng/test-local-packages.ps1)
- Deployed-Host runtime scenario: [`runtime_tests/Watchdog/CrashRecovery/`](../../../runtime_tests/Watchdog/CrashRecovery/)
- Supervision boundary: [`Watchdog manifest`](../Watchdog/MANIFEST.md)
- Coordinated changelog: [`CHANGELOG.md`](../../../CHANGELOG.md)
- Product direction and freezes: [`ROADMAP.md`](../../../ROADMAP.md)
- Promoted work: [`TODO.md`](../../../TODO.md)
- Stable release provenance: [`stable-release-1.0.0.md`](../../stable-release-1.0.0.md)
- Public API and release policy: [`public-api-release-policy.md`](../../public-api-release-policy.md)
- Verification taxonomy: [`tests/README.md`](../../../tests/README.md)

`Related boundaries` records traversal only. This package references
`NekoLib.Watchdog`; the library does not reference it back, and a consumer must
reference both directly.

The listed test paths are shared with the Watchdog library boundary. Only the
Host-scoped cases in `tests/NekoLib.Watchdog.Tests/Unit/` — argument parsing and
fatal-log bounding — and the three `WatchdogHost*` package consumers are
evidence for this boundary.
