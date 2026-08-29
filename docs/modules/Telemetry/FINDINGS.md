# NekoLib.Telemetry Findings

**Document ID:** TEL-FINDINGS

**Schema version:** 1

**Kind:** reference

**Lifecycle:** current

**Subject:** unconfirmed and non-normative observations about the NekoLib.Telemetry boundary

**Surface:** findings

**Boundary:** telemetry

**Authority role:** non-normative

**Mutation:** authored

**Indexing:** include

Everything here is non-normative. A finding becomes an issue only after it is
verified, and scheduled work only after explicit promotion to
[`TODO.md`](../../../TODO.md).

## TEL-FINDING-001

**Status:** open

**Confidence:** high

**Observation:** Checkpoints are unbounded per operation. `RecentOperationCapacity` bounds completed operations only; the checkpoint list inside a live operation has no cap, and a completed operation carries every checkpoint it accumulated into the retained window.

**Evidence:** The operation scope holds a plain `List<TelemetryCheckpoint>` that `Checkpoint` only ever appends to, and `Complete` copies the whole list into the retained `TelemetryOperation`. The F1-TEL review recorded this as `TEL-10`: a code fact plus one 50,000-checkpoint observation, with no long-running memory measurement taken.

**Hypothesis:** Ordinary producers checkpoint a handful of times per operation and never notice. A long-lived operation that checkpoints inside a loop grows without limit while it is in flight, and once completed it occupies one slot of the bounded window while carrying arbitrarily more memory than its neighbours — so the capacity bound stops describing the memory the window actually holds.

**Disposition:** Keep as a finding, not a defect. A per-operation cap would silently discard caller evidence and needs an accepted policy before it could be added; producers should keep checkpoints proportional to the operation. The qualification gap is carried explicitly as [`TEL-VALREQ-013`](VALIDATION_REQUIREMENTS.md). No change is scheduled.

**Outcome link:** [`audits/public-api-review-2026-08-17.md`](audits/public-api-review-2026-08-17.md)

## TEL-FINDING-002

**Status:** open

**Confidence:** high

**Observation:** A caller-supplied `operationId` is taken verbatim with no uniqueness, format, or length validation. Two concurrent or successive operations can carry the same identifier, and the pipeline neither rejects nor deduplicates them.

**Evidence:** `StartOperation` replaces only a null-or-whitespace identifier with a generated 32-character value and otherwise passes the caller's string straight through. Retention appends unconditionally; nothing anywhere compares identifiers. The same holds for `parentOperationId` once it is non-blank.

**Hypothesis:** A consumer that treats `OperationId` as a key — joining a snapshot to external records, or building a parent/child tree — can silently merge two unrelated operations. The pipeline's own guarantees never depend on identity, so the defect would surface only downstream, which is why no existing test would catch it.

**Disposition:** Keep as a finding. Generated identifiers are unique in practice and validating caller-supplied ones would be a behavioral break for a contract that has always been caller-owned. The reference now states that identifiers are caller-owned and unvalidated so consumers can decide whether they need their own constraint. No change is scheduled.

**Outcome link:** [`REFERENCE.md`](REFERENCE.md)

## TEL-FINDING-003

**Status:** open

**Confidence:** medium

**Observation:** The documented consequence of a sink that produces telemetry on the pipeline dispatching to it — unbounded recursion ending in a stack overflow rather than a deadlock — rests entirely on reading the code. No automated regression asserts it, and none reasonably can.

**Evidence:** The dispatch gate is a `lock`, so it is reentrant: a sink calling `Complete` on the same pipeline re-enters `Record` on the same thread rather than blocking. There is no depth counter, no reentrancy flag, and no guard. The F1-TEL review observed the blocking-sink cases `TEL-04` and `TEL-05` with an artificially blocking sink and explicitly recorded that the width of the window in a real application was not measured.

**Hypothesis:** The failure mode is worse than the alternatives it was chosen over: a `StackOverflowException` cannot be caught and terminates the process, so a misbehaving sink takes down the application rather than failing in isolation the way every other sink fault does. Whether that is reachable in practice depends on whether any real sink would ever produce telemetry unconditionally.

**Disposition:** Keep as a finding. Adding a reentrancy guard would change dispatch behavior and needs an accepted decision, and asserting a stack overflow in a test is not safe. The reference states the constraint prominently for sink authors, and [`TEL-VALREQ-014`](VALIDATION_REQUIREMENTS.md) records the characterization gap at RECOMMENDED. No change is scheduled.

**Outcome link:** [`REFERENCE.md`](REFERENCE.md)
