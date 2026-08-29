# NekoLib.Telemetry Changelog

**Document ID:** TEL-CHANGELOG

**Schema version:** 1

**Kind:** reference

**Lifecycle:** current

**Subject:** consumer-visible evolution of the NekoLib.Telemetry boundary

**Surface:** changelog

**Boundary:** telemetry

**Authority role:** non-normative

**Mutation:** authored

**Indexing:** include

The [coordinated family changelog](../../../CHANGELOG.md) remains the release
summary. This file records Telemetry-specific consumer impact without
duplicating package hashes or release provenance.

## 1.0.0

**Packages:** `NekoLib.Telemetry`

**Compatibility class:** behavioral

**Consumer impact:** The pre-stable candidate was corrected before the first stable contract. No public type, member, signature, nullability annotation, default value, namespace, target, or dependency changed, both accepted API manifests are unchanged, and no source change is required to keep compiling.

**Migration:** `docs/modules/Telemetry/migrations/f1.md`

- `Complete` now materializes the caller's terminal dimensions and measurements
  *before* committing completion state. A malformed dictionary — a null key, or
  a throwing enumerator — previously left the operation marked terminal but
  never retained, never dispatched to a sink, and unable to be completed again,
  which silently destroyed the record. The exception still surfaces; the
  operation now survives it and a corrected retry records normally.
- One deliberate consequence of that ordering: the stopwatch is no longer
  stopped before the terminal payload is copied, so `Duration` now includes that
  copy cost. Stopping it earlier was rejected because a failed attempt would
  otherwise freeze the duration of an operation still in flight.
- `StartOperation` normalizes a null-or-whitespace `parentOperationId` to
  `null`, matching how a blank `operationId` is already replaced and restoring
  the contract that a root operation has no parent. A whitespace parent
  previously read as a real correlation link pointing nowhere.
- `TelemetryPipeline` copies the supplied sink array at construction, so a
  caller that mutates its own array can no longer re-target a live pipeline.
