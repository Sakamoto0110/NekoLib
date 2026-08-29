# NekoLib.Core History

**Document ID:** CORE-HISTORY

**Schema version:** 1

**Kind:** reference

**Lifecycle:** current

**Subject:** factual chronological history of the NekoLib.Core boundary

**Surface:** history

**Boundary:** core

**Authority role:** evidence

**Mutation:** append-only

**Indexing:** include

## 2026-06-09 — CORE-HISTORY-001 — Shared contract foundation created

**Release:** none

- Phase A established `NekoLib.Core` as a dual-target, Windows-independent
  contract foundation and placed it in the solution. Later phases refined the
  initial diagnostic vocabulary into the current independent capability model.

**Evidence:** [`../../history/architecture-roadmap-through-phase-d-2026-08-01.md`](../../history/architecture-roadmap-through-phase-d-2026-08-01.md)

## 2026-08-01 — CORE-HISTORY-002 — Current capability boundary established

**Release:** none

- Phase D separated Logging, Telemetry, Inspection, Diagnostics, and
  Diagnostics.Windows into distinct capabilities. Core retained only the small
  shared contracts, models, null objects, `Disposable.Empty`, and the optional
  Inspection provider slot; it acquired no authored package or project
  dependency.

**Evidence:** [`../../history/architecture-roadmap-through-phase-d-2026-08-01.md`](../../history/architecture-roadmap-through-phase-d-2026-08-01.md)

## 2026-08-17 — CORE-HISTORY-003 — Stable public API dispositions completed

**Release:** 1.0.0-local.16

- The F1 review retained the contract and null-object surfaces, made
  `TelemetryCheckpoint`, `TelemetryOperation`, and `InspectionSnapshot`
  defensively protect their outer collections, and marked only
  `IInspectionRecorder.RegisterAction` as experimental `NEKOEXP0001`. The
  accepted manifests remained identical between targets apart from the newly
  accepted marker, and the immutable candidate and PackageReference consumers
  were qualified without unfreezing broad Inspection instrumentation.

**Evidence:** [`audits/public-api-review-2026-08-17.md`](audits/public-api-review-2026-08-17.md), [`migrations/f1.md`](migrations/f1.md)

## 2026-08-21 — CORE-HISTORY-004 — Stable 1.0.0 released

**Release:** 1.0.0

- `NekoLib.Core` joined the first coordinated stable NekoLib release with both
  target assemblies, accepted API baselines, package provenance, and a recorded
  package hash. `NEKOEXP0001` remained outside the stable contract.

**Evidence:** [`../../stable-release-1.0.0.md`](../../stable-release-1.0.0.md), [`../../history/phase-f1-public-api-release-stability-2026-08-21.md`](../../history/phase-f1-public-api-release-stability-2026-08-21.md)

## 2026-08-28 — CORE-HISTORY-005 — XML documentation and extension contracts completed

**Release:** unreleased documentation

- Every public member received reviewed XML documentation, both generated XML
  files carried the same 109 documented member entries, and the current
  technical reference classified all eleven public interfaces and their
  explicit-composition obligations. The work changed documentation only and did
  not stabilize or expand the experimental action boundary.

**Evidence:** [`../../audit/public-api-documentation-extensibility-review-2026-08-27.md`](../../audit/public-api-documentation-extensibility-review-2026-08-27.md)

## 2026-08-29 — CORE-HISTORY-006 — Module-first documentation established

**Release:** unreleased documentation

- Core received one canonical module boundary with a manifest, concise
  introduction, normative technical reference, history and changelog, separate
  issues and findings, risk-derived validation requirements, executed-evidence
  records, the colocated F1 audit and migration, and a pointer-only source
  portal. No product behavior or public API changed.

**Evidence:** [`MANIFEST.md`](MANIFEST.md), [`../../governance/agent-documentation-contract.md`](../../governance/agent-documentation-contract.md)
