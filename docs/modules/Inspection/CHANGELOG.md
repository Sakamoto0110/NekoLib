# NekoLib.Inspection Changelog

**Document ID:** INSP-CHANGELOG

**Schema version:** 1

**Kind:** reference

**Lifecycle:** current

**Subject:** consumer-visible evolution of the NekoLib.Inspection boundary

**Surface:** changelog

**Boundary:** inspection

**Authority role:** non-normative

**Mutation:** authored

**Indexing:** include

The [coordinated family changelog](../../../CHANGELOG.md) remains the release
summary. This file records Inspection-specific consumer impact without
duplicating package hashes or release provenance.

## Unreleased

**Packages:** `NekoLib.Inspection`

**Compatibility class:** documentation-only

**Consumer impact:** XML comments now state the unbudgeted, potentially blocking nature of the synchronous owner read and the complete exception surface of process-wide activation. Compiled signatures, the accepted API baselines, and runtime behavior are unchanged.

**Migration:** none

- `CaptureState()` is documented as applying no completion budget and blocking
  for as long as a provider blocks, with `IInspectionSnapshotSource.CaptureSnapshot`
  named as the surface bounded evidence collectors must use. Its previous
  comment deferred to a rename that is not part of any consumer-visible
  contract.
- `EnableGlobal` documents that it can also throw `ObjectDisposedException` when
  the runtime is disposed while activation is completing, and can surface the
  `ArgumentException` raised when a recorder becomes disabled during
  installation. Both paths already existed and roll the installation back.
- These source comments have not been qualified in a new package candidate.
  Immutable `1.1.0-local.8` proves delivery of the prior XML bytes only.

## 1.0.0

**Packages:** `NekoLib.Inspection`

**Compatibility class:** mixed

**Consumer impact:** Four members gained the experimental `NEKOEXP0001` marker, so deliberate callers now see `CS0618` and must opt in narrowly. No type, member, signature, nullability annotation, default value, namespace, target, dependency, or friend declaration was removed or changed; the only compiled-surface delta is those four attributes. The remaining changes tighten pre-stable behavior.

**Migration:** `docs/modules/Inspection/migrations/f1.md`

- `InspectionRuntime.RegisterAction`, `TryInvokeAction`, `ActionKeys`, and
  `InspectionRuntimeDiagnostics.ActionCount` carry the exact marker
  `Experimental API NEKOEXP0001: compatibility is not guaranteed.` on both
  targets. The marker is release signaling, not an authorization boundary.
- Required identifiers reject blank values, and modules and provider or action
  components reject the reserved `::` delimiter. Identity comparison is
  explicitly ordinal and case-sensitive, and valid `module::key` output is
  unchanged. Operation names remain required but are not identity components.
- Provider invocation, `StateKeys()`, and the experimental `ActionKeys()` now
  enumerate in registration order rather than dictionary order, so a composition
  root can register essential evidence first.
- Each provider registration owns at most one outstanding budgeted invocation.
  Concurrent and repeated snapshots share it, a provider failure arriving after
  a caller timed out is captured as task data rather than lost, and a later
  capture starts fresh work once the task completes. The timeout still never
  cancels application code.
- Invalid capacity now reports `ParamName == "Capacity"`.
- `ClearOperations()` is inert after disposal, while an enabled clear of an
  already empty queue still increments the clear count and preserves lifetime
  totals, eviction count, and sequence state.
