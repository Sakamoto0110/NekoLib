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
| F1-DATA | [`public-api-review-2026-08-17.md`](../modules/Data/audits/public-api-review-2026-08-17.md) | [Data reference](../modules/Data/REFERENCE.md) |
| F1-CORE | [`public-api-review-2026-08-17.md`](../modules/Core/audits/public-api-review-2026-08-17.md) | [Core reference](../modules/Core/REFERENCE.md) |
| F1-LOG | [`public-api-review-2026-08-17.md`](../modules/Logging/audits/public-api-review-2026-08-17.md) | [Logging reference](../modules/Logging/REFERENCE.md) |
| F1-TEL | [`public-api-review-2026-08-17.md`](../modules/Telemetry/audits/public-api-review-2026-08-17.md) | [Telemetry reference](../modules/Telemetry/REFERENCE.md) |
| F1-INSP | [`public-api-review-2026-08-17.md`](../modules/Inspection/audits/public-api-review-2026-08-17.md) | [Inspection reference](../modules/Inspection/REFERENCE.md) |
| F1-DIAG | [`public-api-review-2026-08-17.md`](../modules/Diagnostics/audits/public-api-review-2026-08-17.md) | [Diagnostics reference](../modules/Diagnostics/REFERENCE.md) |
| F1-WIN | [`windows-public-api-review-2026-08-17.md`](../modules/Diagnostics/audits/windows-public-api-review-2026-08-17.md) | [Diagnostics reference](../modules/Diagnostics/REFERENCE.md) |
| F1-HTTP | [`public-api-review-2026-08-17.md`](../modules/Http/audits/public-api-review-2026-08-17.md) | [HTTP reference](../modules/Http/REFERENCE.md) |
| F1-MVVM | [`public-api-review-2026-08-17.md`](../modules/Mvvm/audits/public-api-review-2026-08-17.md) | [Mvvm reference](../modules/Mvvm/REFERENCE.md) |
| F1-DEV | [`public-api-review-2026-08-17.md`](../modules/Devices/audits/public-api-review-2026-08-17.md) | [Devices reference](../modules/Devices/REFERENCE.md) |
| F1-PIPE | [`public-api-review-2026-08-18.md`](../modules/Pipes/audits/public-api-review-2026-08-18.md) | [Pipes reference](../modules/Pipes/REFERENCE.md) |
| F1-WDOG | [`public-api-review-2026-08-18.md`](../modules/Watchdog/audits/public-api-review-2026-08-18.md) | [Watchdog reference](../modules/Watchdog/REFERENCE.md) |
| F1-WDOG-HOST | [`contract-review-2026-08-20.md`](../modules/WatchdogHost/audits/contract-review-2026-08-20.md) | [Watchdog Host reference](../modules/WatchdogHost/REFERENCE.md) |
| F1-NAV | [`public-api-review-2026-08-20.md`](../modules/Navigation/audits/public-api-review-2026-08-20.md) | [Navigation reference](../modules/Navigation/REFERENCE.md) |
| F1-NAV-WF | [`winforms-public-api-review-2026-08-21.md`](../modules/Navigation/audits/winforms-public-api-review-2026-08-21.md) | [Navigation reference](../modules/Navigation/REFERENCE.md) |
| F1-NAV-WPF | [`wpf-public-api-review-2026-08-21.md`](../modules/Navigation/audits/wpf-public-api-review-2026-08-21.md) | [Navigation reference](../modules/Navigation/REFERENCE.md) |

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
