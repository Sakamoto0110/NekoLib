# NekoLib.Watchdog.Host History

**Document ID:** WDGHOST-HISTORY

**Schema version:** 1

**Kind:** reference

**Lifecycle:** current

**Subject:** factual chronological history of the NekoLib.Watchdog.Host boundary

**Surface:** history

**Boundary:** watchdog.host

**Authority role:** evidence

**Mutation:** append-only

**Indexing:** include

Entries are appended in ascending date order and are not rewritten to match
later architecture. Each entry links preserved evidence rather than restating
it.

## 2026-06-04 — WDGHOST-HISTORY-001 — First-pass audit covered the Host entry point

**Release:** none

- The first Watchdog review inspected the Host as a minimal entry point:
  argument parsing into `WatchdogOptions`, runtime construction, `Start`, and
  `WaitForExit`, with best-effort fatal reporting that could not replace the
  original startup failure.
- It left one Host item open: fatal evidence used a relative path and therefore
  landed in whatever directory happened to be current.

**Evidence:** [`../Watchdog/audits/initial-audit.md`](../Watchdog/audits/initial-audit.md)

## 2026-08-01 — WDGHOST-HISTORY-002 — Coordinated local packaging validated

**Release:** none

- By the close of the repository-hygiene phase the canonical pack flow was
  running end to end: `eng/pack-local.ps1` published immutable local packages
  under one coordinated family version and passed `PackageReference`-only
  consumer probes. The Host participates in that flow as a tools/build
  deployment package rather than a library, with isolated `net481`, `win-x86`,
  and `win-x64` payloads.
- The dual-target project topology recorded at the same baseline lists
  `NekoLib.Watchdog.Host` as `net481; net9.0-windows`, which is still its
  target set.

**Evidence:** [`../../history/phase-c-repository-hygiene-2026-08-01.md`](../../history/phase-c-repository-hygiene-2026-08-01.md), [`../../history/architecture-roadmap-through-phase-d-2026-08-01.md`](../../history/architecture-roadmap-through-phase-d-2026-08-01.md)

## 2026-08-11 — WDGHOST-HISTORY-003 — Deployed-sidecar scenario reached its package gate

**Release:** none

- The unattended crash and recovery scenario exercised the real deployment
  boundary: a controller started a scenario application, the application
  bootstrapped the deployed sidecar, and later generations were started by the
  Host rather than by the controller.
- One `net9.0-windows` run against immutable package `1.0.0-local.10` verified
  the package identity from its nuspec, hashed the package, and required the
  deployed Host bytes to match one exact payload entry. The run records
  `supportsPackageClaim: true` and `belowSpecifiedWindow: true`.

**Evidence:** [`../../../runtime_tests/Watchdog/CrashRecovery/README.md`](../../../runtime_tests/Watchdog/CrashRecovery/README.md)

## 2026-08-20 — WDGHOST-HISTORY-004 — F1-WDOG-HOST contract review and its six accepted dispositions

**Release:** none

- The contract review classified the deployment surface, confirmed that the Host
  exports zero public types, and recorded that no compiled API manifest should be
  created for it. All six accepted dispositions were implemented: internal
  protocol v1 with a required launch version, a version check, and the
  `attached:v1:<pid>:<token>` identity; direct-only deployment with the
  `buildTransitive` asset and its global import guard removed; bounded per-user
  fatal evidence replacing the unbounded relative log; fail-fast validation of an
  explicit `--workdir`; a dedicated Host contract document; and an expanded
  package-only evidence campaign.
- The expanded campaign added exact required and forbidden layout, direct
  ownership, byte identity between package and deployed payloads, AnyCPU
  `net481`, explicit x86 and default x64 selection, unsupported-RID failure,
  replacement, build, publish, clean, deployment opt-out and re-enable, protocol
  mismatch, and real package-backed startup and shutdown on both target
  families. Immutable candidate `1.0.0-local.21` was produced from a clean
  commit without `-AllowDirty`.

**Evidence:** [`audits/contract-review-2026-08-20.md`](audits/contract-review-2026-08-20.md), [`migrations/f1.md`](migrations/f1.md), [`../../history/phase-f1-public-api-release-stability-2026-08-21.md`](../../history/phase-f1-public-api-release-stability-2026-08-21.md)

## 2026-08-21 — WDGHOST-HISTORY-005 — First stable family baseline

**Release:** 1.0.0

- `NekoLib.Watchdog.Host` 1.0.0 entered the first stable coordinated family
  baseline. Its deployment contract, rather than a compiled API manifest, is the
  baselined surface under the public API and release policy. It was published
  with the family as the only main package without a symbol package.

**Evidence:** [`../../stable-release-1.0.0.md`](../../stable-release-1.0.0.md)

## 2026-08-29 — WDGHOST-HISTORY-006 — Module-first documentation migration

**Release:** none

- The boundary moved to `docs/modules/WatchdogHost/` as a distinct
  `deployment-package` boundary separate from the `NekoLib.Watchdog` library.
  The source-adjacent reference became the canonical `REFERENCE.md` with a
  pointer-only portal left in the source tree, and the F1-WDOG-HOST migration
  guide and the contract review moved under the module with their bodies,
  baselines, and original paths preserved.
- The full review reconciled the reference against the project file, the package
  targets file, the entry point, the argument parser, the fatal log, and the
  pack and probe scripts, and recorded the derived evidence contract, the
  curated executed evidence, and four non-normative findings. The manifest
  records that the absence of an API baseline is deliberate. No project file,
  package target, payload layout, protocol, or package version changed.

**Evidence:** [`MANIFEST.md`](MANIFEST.md), [`REFERENCE.md`](REFERENCE.md), [`FINDINGS.md`](FINDINGS.md), [`VALIDATIONS.md`](VALIDATIONS.md)
