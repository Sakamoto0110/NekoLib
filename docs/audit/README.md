# Audit Index

**Kind:** reference

**Lifecycle:** current

**Subject:** audit artifact registry

**Reference date:** 2026-08-01

**Reference commit:** working tree after `c5a152f`

Audits are point-in-time evidence, not a live issue tracker. Their findings are
authoritative only for the recorded baseline. Current work, accepted decisions,
and active freezes belong in [`TODO.md`](../../TODO.md).

## Artifacts

| Artifact | Subject | Lifecycle / status | Reference date | Reference commit | Last reconciliation | Current state |
|---|---|---|---|---|---|---|
| [`data-first-pass.md`](data-first-pass.md) | Data first-pass review | historical | 2026-05-29, with pass 1.5 on 2026-05-31 | not recorded | 2026-08-01 | [`README.md`](../../README.md), [`TODO.md`](../../TODO.md) |
| [`data-stabilization-review-2026-08-01.md`](data-stabilization-review-2026-08-01.md) | Data deep stabilization review | historical; E1 implemented | 2026-08-01 | `628442a58cdf2e2374cc7e48fa10d394d3fc3b87` | 2026-08-02 | [`TODO.md`](../../TODO.md) Phase E4 |
| [`devices-first-pass.md`](devices-first-pass.md) | Devices first-pass review | historical | 2026-06-10 | not recorded | 2026-08-01 | [`README.md`](../../README.md), [`TODO.md`](../../TODO.md) |
| [`diagnostics-boundaries-review-2026-07-30.md`](diagnostics-boundaries-review-2026-07-30.md) | Diagnostics-sector boundaries and naming | current; three review-only decisions remain | 2026-07-30 | `1727a1cac3f66666b2df02bc618ad6ab45807a49` | 2026-08-01 | [`TODO.md`](../../TODO.md), [`README.md`](../../README.md) |
| [`navigation-audit.md`](navigation-audit.md) | Navigation passes 1-6 | historical | 2026-05-28 to 2026-06-03 | not recorded | 2026-08-01 | [Navigation reference](../../src/Navigation/NekoLib.Navigation/README.md), [`TODO.md`](../../TODO.md) |
| [`pipes-first-pass.md`](pipes-first-pass.md) | Pipes first-pass review | historical | 2026-06-04 | not recorded | 2026-08-01 | [`README.md`](../../README.md), [`TODO.md`](../../TODO.md) |
| [`repository-hygiene-phase-c-readiness-2026-08-01.md`](repository-hygiene-phase-c-readiness-2026-08-01.md) | Phase C readiness | historical; complete | 2026-08-01 | `88b07d83f037db6bf13eab34ac3f5abafba787b1` | 2026-08-01 | [`TODO.md`](../../TODO.md) |
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
