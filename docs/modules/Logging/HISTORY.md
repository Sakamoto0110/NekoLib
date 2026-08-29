# NekoLib.Logging History

**Document ID:** LOG-HISTORY

**Schema version:** 1

**Kind:** reference

**Lifecycle:** current

**Subject:** factual chronological history of the NekoLib.Logging boundary

**Surface:** history

**Boundary:** logging

**Authority role:** evidence

**Mutation:** append-only

**Indexing:** include

## 2026-08-01 — LOG-HISTORY-001 — Logging became an independent capability

**Release:** none

- Phase D separated the concrete logging pipeline from Diagnostics and renamed
  the earlier `NekoLib.Logger` project to `NekoLib.Logging`. The package took
  ownership of severity filtering, ordered sink dispatch, bounded recent
  entries, the bounded flush, and rolling-file persistence, while Core kept the
  small writer and read/flush contracts. Phase D used a deliberate clean break;
  `NekoLib.Logger` was not retained as a compatibility surface.

**Evidence:** [`../../history/architecture-roadmap-through-phase-d-2026-08-01.md`](../../history/architecture-roadmap-through-phase-d-2026-08-01.md), [`../../audit/diagnostics-boundaries-review-2026-07-30.md`](../../audit/diagnostics-boundaries-review-2026-07-30.md)

## 2026-08-09 — LOG-HISTORY-002 — Sustained and fault-driven runtime behavior observed

**Release:** none

- The shared Observability scenario gave Logging its own phase, checks, and
  result section, and executed them on both target families: sustained ordered
  delivery under concurrent writers, throwing-sink isolation, a locked file,
  and a blocked flush that returned inside its bound. The run also recorded, as
  a note rather than an assertion, that `TimestampUtc` and delivery order can
  disagree under concurrent writers.

**Evidence:** [`../../../runtime_tests/Observability/LongRunningRecovery/README.md`](../../../runtime_tests/Observability/LongRunningRecovery/README.md), [`VALIDATIONS.md`](VALIDATIONS.md)

## 2026-08-17 — LOG-HISTORY-003 — Stable public API dispositions completed

**Release:** pre-1.0.0

- The F1-LOG review accepted nineteen dispositions without changing either
  accepted API manifest. `DebugLogSink` stopped compiling itself away in
  Release builds, null entries became an argument error, the sink array became
  a construction-time copy, `Flush` gained failure isolation with budget-based
  admission and observed its own abandoned faults, and disposal became the
  authority for the terminal flush. The focused suite grew from 9 to 30 cases
  per target.

**Evidence:** [`audits/public-api-review-2026-08-17.md`](audits/public-api-review-2026-08-17.md), [`migrations/f1.md`](migrations/f1.md)

## 2026-08-17 — LOG-HISTORY-004 — Disposal race corrected and package candidate qualified

**Release:** 1.0.0-local.17

- An independent packaging review found that `Dispose` published its terminal
  state before the final flush had taken the pipeline gate, letting a
  concurrent `Flush` report success early or reach already disposed sinks.
  Disposal admission moved under the gate, the documented total-budget wording
  was aligned with the implemented admission rule, and both cases gained
  regressions. The candidate package and its PackageReference consumers passed.

**Evidence:** [`audits/public-api-review-2026-08-17.md`](audits/public-api-review-2026-08-17.md), [`VALIDATIONS.md`](VALIDATIONS.md)

## 2026-08-21 — LOG-HISTORY-005 — Stable 1.0.0 released

**Release:** 1.0.0

- `NekoLib.Logging` joined the first coordinated stable NekoLib release with a
  materialized package hash and qualifying `1.0.0-local.22` evidence.

**Evidence:** [`../../stable-release-1.0.0.md`](../../stable-release-1.0.0.md), [`../../history/phase-f1-public-api-release-stability-2026-08-21.md`](../../history/phase-f1-public-api-release-stability-2026-08-21.md)

## 2026-08-28 — LOG-HISTORY-006 — Flush admission corrected and XML delivery qualified

**Release:** 1.1.0-local.8

- A `net481` regression failure exposed a sub-millisecond admission hole: an
  exhausted sink task could return just before the stopwatch crossed the
  nominal boundary, leaving the loop's remaining-budget guard satisfied and
  admitting one more sink after the budget was spent. `Flush` now reports
  completion, failure, and exhaustion separately and returns immediately on
  exhaustion, restoring the documented single pipeline-wide bound with no
  public API change. The corrected regression passed 30 consecutive `net481`
  runs and the complete dual-target suite. The same candidate proved
  package-owned XML documentation and PackageReference delivery.

**Evidence:** [`../../audit/public-api-documentation-extensibility-review-2026-08-27.md`](../../audit/public-api-documentation-extensibility-review-2026-08-27.md), [`CHANGELOG.md`](CHANGELOG.md)

## 2026-08-29 — LOG-HISTORY-007 — Module-first documentation established

**Release:** unreleased documentation

- Logging received one canonical module boundary with a manifest, concise
  introduction, normative reference, separate history and changelog, an
  issues/findings split, a risk-derived validation contract, curated evidence
  decomposed from the shared Observability scenario, the colocated F1 audit and
  migration, and a pointer-only source portal. Logging, Telemetry, and
  Inspection remain three separate capabilities; no Observability boundary was
  created.

**Evidence:** [`MANIFEST.md`](MANIFEST.md), [`../../governance/agent-documentation-contract.md`](../../governance/agent-documentation-contract.md)
