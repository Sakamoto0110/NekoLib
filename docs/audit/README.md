# Audit Index

**Kind:** reference

**Lifecycle:** current

**Subject:** audit artifact registry

**Reference date:** 2026-08-17

**Reference commit:** working tree after `6480c9e`

Audits are point-in-time evidence, not a live issue tracker. Their findings are
authoritative only for the recorded baseline. Current work, accepted decisions,
and active freezes belong in [`TODO.md`](../../TODO.md).

## Artifacts

| Artifact | Subject | Lifecycle / status | Reference date | Reference commit | Last reconciliation | Current state |
|---|---|---|---|---|---|---|
| [`core-public-api-review-2026-08-17.md`](core-public-api-review-2026-08-17.md) | Core F1 public API finalization | historical; all dispositions implemented | 2026-08-17 | `0ad3840b29d749c25e157ae15db450bf82d17011` | 2026-08-17 | [Core reference](../../src/Core/NekoLib.Core/README.md), [`TODO.md`](../../TODO.md) F1-CORE |
| [`data-first-pass.md`](data-first-pass.md) | Data first-pass review | historical | 2026-05-29, with pass 1.5 on 2026-05-31 | not recorded | 2026-08-01 | [`README.md`](../../README.md), [`TODO.md`](../../TODO.md) |
| [`data-public-api-review-2026-08-17.md`](data-public-api-review-2026-08-17.md) | Data F1 public API finalization | historical; all seven dispositions implemented | 2026-08-17 | `87b34b061f5db6cf50a28d3187070940b851e1be` | 2026-08-17 | [Data reference](../../src/Data/NekoLib.Data/README.md), [`TODO.md`](../../TODO.md) F1-DATA |
| [`data-stabilization-review-2026-08-01.md`](data-stabilization-review-2026-08-01.md) | Data deep stabilization review | historical; E1 implemented | 2026-08-01 | `628442a58cdf2e2374cc7e48fa10d394d3fc3b87` | 2026-08-02 | [`TODO.md`](../../TODO.md) Phase E4 |
| [`devices-first-pass.md`](devices-first-pass.md) | Devices first-pass review | historical | 2026-06-10 | not recorded | 2026-08-01 | [`README.md`](../../README.md), [`TODO.md`](../../TODO.md) |
| [`diagnostics-boundaries-review-2026-07-30.md`](diagnostics-boundaries-review-2026-07-30.md) | Diagnostics-sector boundaries and naming | historical; E6 dispositions promoted | 2026-07-30 | `1727a1cac3f66666b2df02bc618ad6ab45807a49` | 2026-08-08 | [`TODO.md`](../../TODO.md) Phase E6, [`README.md`](../../README.md) |
| [`logging-public-api-review-2026-08-17.md`](logging-public-api-review-2026-08-17.md) | Logging F1 public API finalization | historical; all dispositions implemented and package-validated | 2026-08-17 | `c7967e784914b56863a1b2da97cfafecb32ea494` | 2026-08-17 | [Logging reference](../../src/Logging/NekoLib.Logging/README.md), [`TODO.md`](../../TODO.md) F1-LOG |
| [`navigation-adapter-review-2026-08-03.md`](navigation-adapter-review-2026-08-03.md) | Navigation WinForms/WPF adapter review | historical; all eleven findings closed | 2026-08-03 | `ae1781086b3858cdc9cb025473ed18e3445ee1eb` | 2026-08-04 | [`TODO.md`](../../TODO.md) Phase E2 |
| [`navigation-audit.md`](navigation-audit.md) | Navigation passes 1-6 | historical | 2026-05-28 to 2026-06-03 | not recorded | 2026-08-01 | [Navigation reference](../../src/Navigation/NekoLib.Navigation/README.md), [`TODO.md`](../../TODO.md) |
| [`navigation-design-time-2026-08-06.md`](navigation-design-time-2026-08-06.md) | Navigation design-time loadability | current; both findings implemented, one residual gap unscheduled | 2026-08-06 | `5418cb27f8da669a060ac382fa277c59d2322769` | reconciled 2026-08-08 into E2 | [Navigation reference](../../src/Navigation/NekoLib.Navigation/README.md) |
| [`payments-pix-design-review-2026-08-16.md`](payments-pix-design-review-2026-08-16.md) | Payments/Pix product boundary and first provider model | current; review complete, implementation decision pending | 2026-08-16 | `f73ba4a2fd01b66f5df6c172ba15d6d39d01a072` | none | [`TODO.md`](../../TODO.md) Phase G2 |
| [`pipes-first-pass.md`](pipes-first-pass.md) | Pipes first-pass review | historical | 2026-06-04 | not recorded | 2026-08-01 | [`README.md`](../../README.md), [`TODO.md`](../../TODO.md) |
| [`pipes-ipc-hardening-review-2026-08-08.md`](pipes-ipc-hardening-review-2026-08-08.md) | Pipes and Watchdog IPC hardening | historical; accepted dispositions promoted to E5 | 2026-08-08 | `941e17e8224dff3b34b7495d7bd0f7cf12c8f4ed` | 2026-08-08 | [`TODO.md`](../../TODO.md) Phase E5 |
| [`repository-hygiene-phase-c-readiness-2026-08-01.md`](repository-hygiene-phase-c-readiness-2026-08-01.md) | Phase C readiness | historical; complete | 2026-08-01 | `88b07d83f037db6bf13eab34ac3f5abafba787b1` | 2026-08-01 | [`TODO.md`](../../TODO.md) |
| [`telemetry-public-api-review-2026-08-17.md`](telemetry-public-api-review-2026-08-17.md) | Telemetry F1 public API finalization | current; review complete, decision pending | 2026-08-17 | `6480c9e57a42af3490eeda55b0f66400e75782cd` | none | [`TODO.md`](../../TODO.md) F1-TEL |
| [`watchdog-first-pass.md`](watchdog-first-pass.md) | Watchdog first-pass review | historical | 2026-06-04 | not recorded | 2026-08-01 | [`README.md`](../../README.md), [`TODO.md`](../../TODO.md) |

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
