# Audit Index

**Kind:** reference

**Lifecycle:** current

**Subject:** audit artifact registry

**Reference date:** 2026-08-26

**Reference commit:** working tree after `0fa1a321c85c541cc3e32c39e5607de881032b5a`

Audits are point-in-time evidence, not a live issue tracker. Their findings are
authoritative only for the recorded baseline. Current work, accepted decisions,
and active freezes belong in [`TODO.md`](../../TODO.md).

This registry may route an audit to its module-first location after a reviewed
structural move; the moved audit retains its original path and baseline.

## Artifacts

| Artifact | Subject | Lifecycle / status | Reference date | Reference commit | Last reconciliation | Current state |
|---|---|---|---|---|---|---|
| [`core-public-api-review-2026-08-17.md`](core-public-api-review-2026-08-17.md) | Core F1 public API finalization | historical; all dispositions implemented | 2026-08-17 | `0ad3840b29d749c25e157ae15db450bf82d17011` | 2026-08-17 | [Core reference](../../src/Core/NekoLib.Core/README.md), [`TODO.md`](../../TODO.md) F1-CORE |
| [`data-first-pass.md`](data-first-pass.md) | Data first-pass review | historical | 2026-05-29, with pass 1.5 on 2026-05-31 | not recorded | 2026-08-01 | [`README.md`](../../README.md), [`TODO.md`](../../TODO.md) |
| [`data-public-api-review-2026-08-17.md`](data-public-api-review-2026-08-17.md) | Data F1 public API finalization | historical; all seven dispositions implemented | 2026-08-17 | `87b34b061f5db6cf50a28d3187070940b851e1be` | 2026-08-17 | [Data reference](../../src/Data/NekoLib.Data/README.md), [`TODO.md`](../../TODO.md) F1-DATA |
| [`data-stabilization-review-2026-08-01.md`](data-stabilization-review-2026-08-01.md) | Data deep stabilization review | historical; E1 implemented | 2026-08-01 | `628442a58cdf2e2374cc7e48fa10d394d3fc3b87` | 2026-08-02 | [`TODO.md`](../../TODO.md) Phase E4 |
| [`devices-first-pass.md`](devices-first-pass.md) | Devices first-pass review | historical | 2026-06-10 | not recorded | 2026-08-01 | [`README.md`](../../README.md), [`TODO.md`](../../TODO.md) |
| [`devices-public-api-review-2026-08-17.md`](devices-public-api-review-2026-08-17.md) | Devices F1 public API, operation-boundary, and transport-contract finalization | historical; all dispositions implemented and package-validated | 2026-08-17 | `a6af985245180bf1d5aa4581dbeb3352fee3e885` | 2026-08-18 | [Devices reference](../../src/Devices/NekoLib.Devices/README.md), [`TODO.md`](../../TODO.md) F1-DEV |
| [`diagnostics-boundaries-review-2026-07-30.md`](diagnostics-boundaries-review-2026-07-30.md) | Diagnostics-sector boundaries and naming | historical; E6 dispositions promoted | 2026-07-30 | `1727a1cac3f66666b2df02bc618ad6ab45807a49` | 2026-08-08 | [`TODO.md`](../../TODO.md) Phase E6, [`README.md`](../../README.md) |
| [`diagnostics-public-api-review-2026-08-17.md`](diagnostics-public-api-review-2026-08-17.md) | Diagnostics F1 public API and incident-evidence finalization | historical; all dispositions implemented, lifecycle race corrected, and package-validated | 2026-08-17 | `89f05b667be10104e8ef966ac9bebba7b7f13a23` | 2026-08-18 | [Diagnostics reference](../../src/Diagnostics/NekoLib.Diagnostics/README.md), [`TODO.md`](../../TODO.md) F1-DIAG |
| [`diagnostics-windows-public-api-review-2026-08-17.md`](diagnostics-windows-public-api-review-2026-08-17.md) | Diagnostics.Windows F1 public API, crash-hook and minidump finalization | historical; all dispositions implemented and package-validated | 2026-08-17 | `ef533e2bca9ae8f86a8ecec7ae4d7bcf778077bf` | 2026-08-18 | [Diagnostics reference](../../src/Diagnostics/NekoLib.Diagnostics/README.md), [`TODO.md`](../../TODO.md) F1-WIN |
| [`logging-public-api-review-2026-08-17.md`](logging-public-api-review-2026-08-17.md) | Logging F1 public API finalization | historical; all dispositions implemented and package-validated | 2026-08-17 | `c7967e784914b56863a1b2da97cfafecb32ea494` | 2026-08-17 | [Logging reference](../../src/Logging/NekoLib.Logging/README.md), [`TODO.md`](../../TODO.md) F1-LOG |
| [`http-public-api-review-2026-08-17.md`](http-public-api-review-2026-08-17.md) | HTTP F1 public API, catalog identity, and response-evidence finalization | historical; all dispositions implemented and package-validated | 2026-08-17 | `e845165252c60c9ecff2e90221eac739a1631c68` | 2026-08-18 | [HTTP reference](../../src/Http/NekoLib.Http/README.md), [`TODO.md`](../../TODO.md) F1-HTTP |
| [`inspection-public-api-review-2026-08-17.md`](inspection-public-api-review-2026-08-17.md) | Inspection F1 public API and behavior finalization | historical; all dispositions implemented and package-validated | 2026-08-17 | `7c4d449ec3a6854b0561c8514701a1ec31fe3c35` | 2026-08-17 | [Inspection reference](../../src/Inspection/NekoLib.Inspection/README.md), [`TODO.md`](../../TODO.md) F1-INSP |
| [`public-api-review-2026-08-17.md`](../modules/Mvvm/audits/public-api-review-2026-08-17.md) | Mvvm F1 public API, coercion, notification, and nullability finalization | historical; all dispositions implemented and package-validated | 2026-08-17 | `c9c4321e9fe67c0aeadcb7afda36347368fce457` | 2026-08-18 | [Mvvm reference](../modules/Mvvm/REFERENCE.md), [`TODO.md`](../../TODO.md) F1-MVVM |
| [`navigation-adapter-review-2026-08-03.md`](navigation-adapter-review-2026-08-03.md) | Navigation WinForms/WPF adapter review | historical; all eleven findings closed | 2026-08-03 | `ae1781086b3858cdc9cb025473ed18e3445ee1eb` | 2026-08-04 | [`TODO.md`](../../TODO.md) Phase E2 |
| [`navigation-audit.md`](navigation-audit.md) | Navigation passes 1-6 | historical | 2026-05-28 to 2026-06-03 | not recorded | 2026-08-01 | [Navigation reference](../../src/Navigation/NekoLib.Navigation/README.md), [`TODO.md`](../../TODO.md) |
| [`navigation-design-time-2026-08-06.md`](navigation-design-time-2026-08-06.md) | Navigation design-time loadability | current; both findings implemented, one residual gap unscheduled | 2026-08-06 | `5418cb27f8da669a060ac382fa277c59d2322769` | reconciled 2026-08-08 into E2 | [Navigation reference](../../src/Navigation/NekoLib.Navigation/README.md) |
| [`navigation-public-api-review-2026-08-20.md`](navigation-public-api-review-2026-08-20.md) | Navigation F1 public API, facade, registration, lifecycle, history, guard, surface, diagnostics, nullability, target, and package review | historical; all twelve dispositions implemented | 2026-08-20 | `9706a2c165d3bc4bcfac810319a829f42845eb95` | 2026-08-20 | [Navigation reference](../../src/Navigation/NekoLib.Navigation/README.md), [`TODO.md`](../../TODO.md) F1-NAV |
| [`navigation-winforms-public-api-review-2026-08-21.md`](navigation-winforms-public-api-review-2026-08-21.md) | Navigation.WinForms F1 adapter, host, surface, ownership, disposal, compatibility, and nullability review | historical; all six dispositions implemented | 2026-08-21 | `aefd2b8985f626abe1a02e78094bf48cfdf6494e` | 2026-08-21 | [Navigation reference](../../src/Navigation/NekoLib.Navigation/README.md), [`TODO.md`](../../TODO.md) F1-NAV-WF |
| [`navigation-wpf-public-api-review-2026-08-21.md`](navigation-wpf-public-api-review-2026-08-21.md) | Navigation.Wpf F1 adapter, host, surface, ownership, disposal, compatibility, and nullability review | historical; all six dispositions implemented | 2026-08-21 | `aefd2b8985f626abe1a02e78094bf48cfdf6494e` | 2026-08-21 | [Navigation reference](../../src/Navigation/NekoLib.Navigation/README.md), [`TODO.md`](../../TODO.md) F1-NAV-WPF |
| [`nekomarketplace-external-consumer-evidence-intake-2026-08-26.md`](nekomarketplace-external-consumer-evidence-intake-2026-08-26.md) | NekoMarketplace E2E evidence intake for the published NekoLib 1.0.0 package family | historical; intake complete, current reconciliation pending | 2026-08-26 | `0fa1a321c85c541cc3e32c39e5607de881032b5a` | none | Evidence intake only; [`TODO.md`](../../TODO.md) unchanged |
| [`payments-pix-design-review-2026-08-16.md`](payments-pix-design-review-2026-08-16.md) | Payments/Pix product boundary and first provider model | current; review complete, implementation decision pending | 2026-08-16 | `f73ba4a2fd01b66f5df6c172ba15d6d39d01a072` | none | [`TODO.md`](../../TODO.md) Phase G2 |
| [`pipes-first-pass.md`](pipes-first-pass.md) | Pipes first-pass review | historical | 2026-06-04 | not recorded | 2026-08-01 | [`README.md`](../../README.md), [`TODO.md`](../../TODO.md) |
| [`pipes-ipc-hardening-review-2026-08-08.md`](pipes-ipc-hardening-review-2026-08-08.md) | Pipes and Watchdog IPC hardening | historical; accepted dispositions promoted to E5 | 2026-08-08 | `941e17e8224dff3b34b7495d7bd0f7cf12c8f4ed` | 2026-08-08 | [`TODO.md`](../../TODO.md) Phase E5 |
| [`pipes-public-api-review-2026-08-18.md`](pipes-public-api-review-2026-08-18.md) | Pipes F1 public API, RPC/event, lifecycle, framing, metrics, security-policy, error, target, and package review | historical; all eight dispositions implemented | 2026-08-18 | `2db588ac6fef271851e70c04f7b8b82cd8e004a7` | 2026-08-18 | [Pipes reference](../../src/Pipes/NekoLib.Pipes/README.md), [`TODO.md`](../../TODO.md) F1-PIPE |
| [`repository-hygiene-phase-c-readiness-2026-08-01.md`](repository-hygiene-phase-c-readiness-2026-08-01.md) | Phase C readiness | historical; complete | 2026-08-01 | `88b07d83f037db6bf13eab34ac3f5abafba787b1` | 2026-08-01 | [`TODO.md`](../../TODO.md) |
| [`telemetry-public-api-review-2026-08-17.md`](telemetry-public-api-review-2026-08-17.md) | Telemetry F1 public API finalization | historical; all dispositions implemented and package-validated | 2026-08-17 | `6480c9e57a42af3490eeda55b0f66400e75782cd` | 2026-08-17 | [Telemetry reference](../../src/Telemetry/NekoLib.Telemetry/README.md), [`TODO.md`](../../TODO.md) F1-TEL |
| [`watchdog-first-pass.md`](watchdog-first-pass.md) | Watchdog first-pass review | historical | 2026-06-04 | not recorded | 2026-08-01 | [`README.md`](../../README.md), [`TODO.md`](../../TODO.md) |
| [`watchdog-public-api-review-2026-08-18.md`](watchdog-public-api-review-2026-08-18.md) | Watchdog F1 public API, application/advanced-runtime, lifecycle, process, IPC, evidence, target, security, and package review | historical; all eight dispositions implemented | 2026-08-18 | `075bb7520dedd80dc853d6dac57c53e9e5b8aea7` | 2026-08-18 | [Watchdog reference](../../src/Watchdog/NekoLib.Watchdog/README.md), [`TODO.md`](../../TODO.md) F1-WDOG |
| [`watchdog-host-contract-review-2026-08-20.md`](watchdog-host-contract-review-2026-08-20.md) | Watchdog Host F1 deployment package, payload, build/publish, bootstrap argument, protocol, security, and release-evidence review | historical; all six dispositions implemented and package-validated | 2026-08-20 | `3ec2c63e2d60d96a8462c1a91483dea863015c01` | 2026-08-20 | [Host reference](../../src/Watchdog/NekoLib.Watchdog.Host/README.md), [`TODO.md`](../../TODO.md) F1-WDOG-HOST |

`not recorded` is intentional: an audit's first appearance in Git does not
prove which commit its author reviewed.

## Snapshot rules

- Keep observed facts and original recommendations intact.
- Add later outcomes under a dated reconciliation heading.
- Do not label a historical section `Latest`, `Current Status`, or
  `Status (current)`.
- Do not treat an unreconciled old finding as open work.
- Promote a finding only after current code confirms it, a direction is
  accepted, and the work is intentionally added to `TODO.md`.
