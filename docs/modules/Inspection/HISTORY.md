# NekoLib.Inspection History

**Document ID:** INSP-HISTORY

**Schema version:** 1

**Kind:** reference

**Lifecycle:** current

**Subject:** factual chronological history of the NekoLib.Inspection boundary

**Surface:** history

**Boundary:** inspection

**Authority role:** evidence

**Mutation:** append-only

**Indexing:** include

## 2026-07-27 — INSP-HISTORY-001 — Opt-in runtime and process-wide slot established

**Release:** none

- The capability shipped first under the `DebugUtils` name: a Core contract, a
  concrete runtime with an operation ring buffer, pull-based state providers,
  and an invokable command channel. The same day, Core took ownership of a
  process-wide NO-OP default slot and the runtime gained deterministic global
  activation and teardown, Navigation opted into the slot, and the runtime got
  direct dual-target tests. Broad module instrumentation was frozen from the
  start, deliberately rather than by omission.

**Evidence:** [`../../history/architecture-roadmap-through-phase-d-2026-08-01.md`](../../history/architecture-roadmap-through-phase-d-2026-08-01.md), [`../../../ROADMAP.md`](../../../ROADMAP.md)

## 2026-08-01 — INSP-HISTORY-002 — Renamed to Inspection and split by direction

**Release:** none

- Phase D renamed the capability to `NekoLib.Inspection` as a clean break with
  no compatibility surface, and split the contract by direction: modules receive
  the push/register `IInspectionRecorder`, while readers receive the read-only
  `IInspectionSnapshotSource`. Diagnostics became a consumer of the read-only
  side only and therefore cannot invoke actions. The broad rollout stayed
  frozen, with Navigation the only producer.

**Evidence:** [`../../history/architecture-roadmap-through-phase-d-2026-08-01.md`](../../history/architecture-roadmap-through-phase-d-2026-08-01.md), [`../../audit/diagnostics-boundaries-review-2026-07-30.md`](../../audit/diagnostics-boundaries-review-2026-07-30.md)

## 2026-08-09 — INSP-HISTORY-003 — Sustained and fault-driven runtime behavior observed

**Release:** none

- The shared Observability scenario gave Inspection its own phase, checks, and
  result section on both target families: bounded recording under sustained
  load, a throwing provider isolated into a marker while healthy providers stayed
  in the same snapshot, a slow provider marked as timed out with the capture
  still returning inside its budget, and process-wide teardown restoring the
  Core null recorder before a fresh installation succeeded. The scenario
  deliberately references no action API.

**Evidence:** [`../../../runtime_tests/Observability/LongRunningRecovery/README.md`](../../../runtime_tests/Observability/LongRunningRecovery/README.md), [`VALIDATIONS.md`](VALIDATIONS.md)

## 2026-08-17 — INSP-HISTORY-004 — Experimental boundary and passive contract finalized

**Release:** pre-1.0.0

- The F1-INSP review accepted eight dispositions. Four members —
  `RegisterAction`, `TryInvokeAction`, `ActionKeys`, and
  `InspectionRuntimeDiagnostics.ActionCount` — gained the exact `NEKOEXP0001`
  marker on both targets, making this the one boundary in the capability family
  whose accepted API manifests actually changed. Identity validation rejected
  blank and delimiter-ambiguous values, provider and key enumeration became
  registration-ordered, each registration gained one outstanding budgeted
  invocation with late-failure observation and no cancellation, invalid capacity
  began reporting `Capacity`, and post-disposal clear became inert while enabled
  empty clears still counted. The focused suite grew from 13 to 40 cases per
  target.

**Evidence:** [`audits/public-api-review-2026-08-17.md`](audits/public-api-review-2026-08-17.md), [`migrations/f1.md`](migrations/f1.md)

## 2026-08-17 — INSP-HISTORY-005 — Package candidate qualified

**Release:** 1.0.0-local.19

- The scoped baseline update produced exactly the four accepted
  `ObsoleteAttribute` additions per target manifest and nothing else. The
  candidate package and its PackageReference-only consumers passed on both
  target families.

**Evidence:** [`audits/public-api-review-2026-08-17.md`](audits/public-api-review-2026-08-17.md), [`VALIDATIONS.md`](VALIDATIONS.md)

## 2026-08-21 — INSP-HISTORY-006 — Stable 1.0.0 released

**Release:** 1.0.0

- `NekoLib.Inspection` joined the first coordinated stable NekoLib release with
  a materialized package hash and qualifying `1.0.0-local.22` evidence. The
  release record states that `IInspectionRecorder.RegisterAction` remains
  explicitly experimental under `NEKOEXP0001` while the rest of the accepted
  surface is stable.

**Evidence:** [`../../stable-release-1.0.0.md`](../../stable-release-1.0.0.md), [`../../history/phase-f1-public-api-release-stability-2026-08-21.md`](../../history/phase-f1-public-api-release-stability-2026-08-21.md)

## 2026-08-26 — INSP-HISTORY-007 — Experimental signal confirmed to reach a real consumer

**Release:** 1.0.0

- An external consumer building against the published package observed the
  compiler diagnostic on the experimental action members. The evidence intake
  preserved that as a positive record: the experimental status is not only
  documented, it is delivered through the package and surfaces at the consumer's
  build.

**Evidence:** [`../../audit/nekomarketplace-external-consumer-evidence-intake-2026-08-26.md`](../../audit/nekomarketplace-external-consumer-evidence-intake-2026-08-26.md)

## 2026-08-28 — INSP-HISTORY-008 — Public XML documentation delivery qualified

**Release:** 1.1.0-local.8

- The family documentation campaign closed Inspection's remaining public-member
  gaps and the corrected package flow proved package-owned XML files and
  PackageReference delivery for both target assets.

**Evidence:** [`../../audit/public-api-documentation-extensibility-review-2026-08-27.md`](../../audit/public-api-documentation-extensibility-review-2026-08-27.md), [`VALIDATIONS.md`](VALIDATIONS.md)

## 2026-08-29 — INSP-HISTORY-009 — Module-first documentation established

**Release:** unreleased documentation

- Inspection received one canonical module boundary with a manifest, concise
  introduction, normative reference, separate history and changelog, an
  issues/findings split, a risk-derived validation contract, curated evidence
  decomposed from the shared Observability scenario, the colocated F1 audit and
  migration, and a pointer-only source portal. The broad instrumentation and
  action freeze was preserved and its ownership corrected from `TODO.md` to
  `ROADMAP.md`. Logging, Telemetry, and Inspection remain three separate
  capabilities; no Observability boundary was created.

**Evidence:** [`MANIFEST.md`](MANIFEST.md), [`../../governance/agent-documentation-contract.md`](../../governance/agent-documentation-contract.md)
