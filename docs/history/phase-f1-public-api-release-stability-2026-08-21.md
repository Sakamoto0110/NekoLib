# Phase F1 Public API and Release Stability Completion

**Kind:** roadmap/status

**Lifecycle:** historical

**Subject:** completed F1 public API finalization and first stable coordinated family release

**Reference date:** 2026-08-21

**Reference commit:** `2cc46c5acff5919d93b14da02f78bf3c0f221825`

**Current state:** [`ROADMAP.md`](../../ROADMAP.md), [`TODO.md`](../../TODO.md), and the [`1.0.0` stable release record](../stable-release-1.0.0.md)

## Outcome

F1 established the public API and release policy, added assembly-derived
baselines for every library target, finalized all shipped module surfaces, and
declared, materialized, tagged, published, and externally verified the first
stable coordinated NekoLib package family.

This record replaces the completed F1 work log that formerly occupied the live
`TODO.md`. Exact module decisions and evidence remain in the linked audits;
package hashes, commands, publication attempts, signatures, and external
consumer evidence remain in the stable release record. This summary does not
become current API or package authority.

## Policy and baseline foundation

- [`public-api-release-policy.md`](../public-api-release-policy.md) owns SemVer,
  stability classes, compatibility, deprecation, migration, baseline, and
  release rules.
- `eng/public-api/` contains the accepted assembly-derived manifests for all 15
  library packages across their supported targets.
- [`CHANGELOG.md`](../../CHANGELOG.md) and the migration guides own consumer
  transition; the module references own current documented contracts.

## Module finalization records

| F1 item | Accepted historical record | Current contract |
|---|---|---|
| F1-DATA | [`data-public-api-review-2026-08-17.md`](../audit/data-public-api-review-2026-08-17.md) | [Data reference](../../src/Data/NekoLib.Data/README.md) |
| F1-CORE | [`core-public-api-review-2026-08-17.md`](../audit/core-public-api-review-2026-08-17.md) | [Core reference](../../src/Core/NekoLib.Core/README.md) |
| F1-LOG | [`logging-public-api-review-2026-08-17.md`](../audit/logging-public-api-review-2026-08-17.md) | [Logging reference](../../src/Logging/NekoLib.Logging/README.md) |
| F1-TEL | [`telemetry-public-api-review-2026-08-17.md`](../audit/telemetry-public-api-review-2026-08-17.md) | [Telemetry reference](../../src/Telemetry/NekoLib.Telemetry/README.md) |
| F1-INSP | [`inspection-public-api-review-2026-08-17.md`](../audit/inspection-public-api-review-2026-08-17.md) | [Inspection reference](../../src/Inspection/NekoLib.Inspection/README.md) |
| F1-DIAG | [`diagnostics-public-api-review-2026-08-17.md`](../audit/diagnostics-public-api-review-2026-08-17.md) | [Diagnostics reference](../../src/Diagnostics/NekoLib.Diagnostics/README.md) |
| F1-WIN | [`diagnostics-windows-public-api-review-2026-08-17.md`](../audit/diagnostics-windows-public-api-review-2026-08-17.md) | [Diagnostics reference](../../src/Diagnostics/NekoLib.Diagnostics/README.md) |
| F1-HTTP | [`http-public-api-review-2026-08-17.md`](../audit/http-public-api-review-2026-08-17.md) | [HTTP reference](../../src/Http/NekoLib.Http/README.md) |
| F1-MVVM | [`public-api-review-2026-08-17.md`](../modules/Mvvm/audits/public-api-review-2026-08-17.md) | [Mvvm reference](../modules/Mvvm/REFERENCE.md) |
| F1-DEV | [`devices-public-api-review-2026-08-17.md`](../audit/devices-public-api-review-2026-08-17.md) | [Devices reference](../../src/Devices/NekoLib.Devices/README.md) |
| F1-PIPE | [`pipes-public-api-review-2026-08-18.md`](../audit/pipes-public-api-review-2026-08-18.md) | [Pipes reference](../../src/Pipes/NekoLib.Pipes/README.md) |
| F1-WDOG | [`watchdog-public-api-review-2026-08-18.md`](../audit/watchdog-public-api-review-2026-08-18.md) | [Watchdog reference](../../src/Watchdog/NekoLib.Watchdog/README.md) |
| F1-WDOG-HOST | [`watchdog-host-contract-review-2026-08-20.md`](../audit/watchdog-host-contract-review-2026-08-20.md) | [Watchdog Host reference](../../src/Watchdog/NekoLib.Watchdog.Host/README.md) |
| F1-NAV | [`navigation-public-api-review-2026-08-20.md`](../audit/navigation-public-api-review-2026-08-20.md) | [Navigation reference](../../src/Navigation/NekoLib.Navigation/README.md) |
| F1-NAV-WF | [`navigation-winforms-public-api-review-2026-08-21.md`](../audit/navigation-winforms-public-api-review-2026-08-21.md) | [Navigation reference](../../src/Navigation/NekoLib.Navigation/README.md) |
| F1-NAV-WPF | [`navigation-wpf-public-api-review-2026-08-21.md`](../audit/navigation-wpf-public-api-review-2026-08-21.md) | [Navigation reference](../../src/Navigation/NekoLib.Navigation/README.md) |

## Stable family closure

The qualifying clean package candidate came from
`7090e40eed7c6b888ce8da732f21cbe10f1a936c`. The materialized `1.0.0`
packages came from `db63529cafce11690a18a595e4abc6c0610b9b8e`; the annotated
`v1.0.0` tag points to that source. Trusted-publication workflow source
`2cc46c5acff5919d93b14da02f78bf3c0f221825` published the coordinated
family to NuGet.org, and package-only consumers restored and built against the
public feed.

The complete test counts, warning boundary, package/RID layout, aggregate and
individual hashes, failed-safe publication attempts, repository signatures,
GitHub Release assets, and external consumer results are preserved in
[`docs/stable-release-1.0.0.md`](../stable-release-1.0.0.md).

F1 completion did not promote automated CI, performance budgets, remote
evidence export, fleet management, non-Windows support, Navigation surface
regions, broad Inspection instrumentation, or changes to frozen Navigation
core components. Those directions now live in [`ROADMAP.md`](../../ROADMAP.md)
and concise unpromoted records under [`docs/proposals/`](../proposals/README.md).
