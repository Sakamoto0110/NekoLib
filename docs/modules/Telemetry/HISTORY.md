# NekoLib.Telemetry History

**Document ID:** TEL-HISTORY

**Schema version:** 1

**Kind:** reference

**Lifecycle:** current

**Subject:** factual chronological history of the NekoLib.Telemetry boundary

**Surface:** history

**Boundary:** telemetry

**Authority role:** evidence

**Mutation:** append-only

**Indexing:** include

## 2026-08-01 — TEL-HISTORY-001 — Telemetry became an independent capability

**Release:** none

- Phase D created `NekoLib.Telemetry` as a distinct capability over small Core
  contracts, owning correlated operation timings, checkpoints, outcomes,
  dimensions, measurements, and bounded snapshots. Version 1 was scoped
  deliberately: raw completed operations are kept in bounded memory and are not
  persisted, aggregated, exported, or sampled.

**Evidence:** [`../../history/architecture-roadmap-through-phase-d-2026-08-01.md`](../../history/architecture-roadmap-through-phase-d-2026-08-01.md), [`../../audit/diagnostics-boundaries-review-2026-07-30.md`](../../audit/diagnostics-boundaries-review-2026-07-30.md)

## 2026-08-01 — TEL-HISTORY-002 — Navigation became the first producer

**Release:** none

- Navigation began emitting one correlated `Navigation/page_switch` operation
  per navigation, with `page_ready` meaning the successful synchronous
  Navigation lifecycle rather than first paint. `NavigationTimingContext` lets
  the application report authentication completion. Navigation remains the only
  in-repository telemetry producer.

**Evidence:** [`../../history/architecture-roadmap-through-phase-d-2026-08-01.md`](../../history/architecture-roadmap-through-phase-d-2026-08-01.md), [`../Navigation/REFERENCE.md`](../Navigation/REFERENCE.md)

## 2026-08-09 — TEL-HISTORY-003 — Sustained and fault-driven runtime behavior observed

**Release:** none

- The shared Observability scenario gave Telemetry its own phase, checks, and
  result section on both target families: bounded retention under sustained
  completion, throwing-sink isolation with the next operation recording
  normally, and an explicit `abandoned-scope` check confirming that an operation
  started and never completed produces no sink write, no error, and nothing in
  the snapshot even after a forced collection.

**Evidence:** [`../../../runtime_tests/Observability/LongRunningRecovery/README.md`](../../../runtime_tests/Observability/LongRunningRecovery/README.md), [`VALIDATIONS.md`](VALIDATIONS.md)

## 2026-08-17 — TEL-HISTORY-004 — Stable public API dispositions completed

**Release:** pre-1.0.0

- The F1-TEL review accepted sixteen dispositions without changing either
  accepted API manifest. A malformed terminal payload stopped destroying its
  operation, a whitespace parent identifier stopped reading as a correlation
  link, and the sink array became a construction-time copy. The focused suite
  grew from 6 to 22 cases per target. One deliberate consequence was recorded:
  because the stopwatch is no longer stopped before the terminal payload is
  copied, `Duration` now includes that copy cost.

**Evidence:** [`audits/public-api-review-2026-08-17.md`](audits/public-api-review-2026-08-17.md), [`migrations/f1.md`](migrations/f1.md)

## 2026-08-17 — TEL-HISTORY-005 — Package candidate qualified

**Release:** 1.0.0-local.18

- An independent review of the accepted implementation found no additional
  defect or contract mismatch. The candidate package and its
  PackageReference-only consumers passed on both target families.

**Evidence:** [`audits/public-api-review-2026-08-17.md`](audits/public-api-review-2026-08-17.md), [`VALIDATIONS.md`](VALIDATIONS.md)

## 2026-08-21 — TEL-HISTORY-006 — Stable 1.0.0 released

**Release:** 1.0.0

- `NekoLib.Telemetry` joined the first coordinated stable NekoLib release with a
  materialized package hash and qualifying `1.0.0-local.22` evidence.

**Evidence:** [`../../stable-release-1.0.0.md`](../../stable-release-1.0.0.md), [`../../history/phase-f1-public-api-release-stability-2026-08-21.md`](../../history/phase-f1-public-api-release-stability-2026-08-21.md)

## 2026-08-28 — TEL-HISTORY-007 — Public XML documentation delivery qualified

**Release:** 1.1.0-local.8

- The family documentation campaign closed Telemetry's remaining public-member
  gaps and the corrected package flow proved package-owned XML files and
  PackageReference delivery for both target assets.

**Evidence:** [`../../audit/public-api-documentation-extensibility-review-2026-08-27.md`](../../audit/public-api-documentation-extensibility-review-2026-08-27.md), [`VALIDATIONS.md`](VALIDATIONS.md)

## 2026-08-29 — TEL-HISTORY-008 — Module-first documentation established

**Release:** unreleased documentation

- Telemetry received one canonical module boundary with a manifest, concise
  introduction, normative reference, separate history and changelog, an
  issues/findings split, a risk-derived validation contract, curated evidence
  decomposed from the shared Observability scenario, the colocated F1 audit and
  migration, and a pointer-only source portal. Logging, Telemetry, and
  Inspection remain three separate capabilities; no Observability boundary was
  created.

**Evidence:** [`MANIFEST.md`](MANIFEST.md), [`../../governance/agent-documentation-contract.md`](../../governance/agent-documentation-contract.md)
