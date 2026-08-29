# NekoLib.Logging Findings

**Document ID:** LOG-FINDINGS

**Schema version:** 1

**Kind:** reference

**Lifecycle:** current

**Subject:** unconfirmed and non-normative observations about the NekoLib.Logging boundary

**Surface:** findings

**Boundary:** logging

**Authority role:** non-normative

**Mutation:** authored

**Indexing:** include

Everything here is non-normative. A finding becomes an issue only after it is
verified, and scheduled work only after explicit promotion to
[`TODO.md`](../../../TODO.md).

## LOG-FINDING-001

**Status:** open

**Confidence:** medium

**Observation:** `Dispose` performs its unbounded final flush while holding the pipeline gate, and `GetRecentEntries` takes that same gate. A snapshot reader that races an in-progress disposal therefore blocks for as long as the slowest sink blocks in `Flush()`, with no budget available to it.

**Evidence:** `Logger.Dispose` wraps the terminal flush and sink disposal in `lock (_gate)`; `Logger.GetRecentEntries` acquires `_gate` before copying the queue. The reference already records that the final flush carries no time budget and that the snapshot stays readable *after* disposal; neither source nor documentation addresses the window *during* disposal.

**Hypothesis:** The interaction matters mainly for `NekoLib.Diagnostics`, which reads `ILogSnapshotSource` under a per-contributor budget. If a fatal fault arrives while shutdown is blocked in a sink, the log contributor could consume its budget waiting on the gate and yield no log evidence, even though the pipeline retains entries. The width of that window depends entirely on sink behavior and has not been measured.

**Disposition:** Keep as a finding. The documented mitigation already exists — call `Flush(budget)` before `Dispose` and treat `false` as "persistence not confirmed" — and the reference now states the blocking window explicitly. Changing the disposal locking model would alter the terminal-flush contract and requires a reproduced defect plus review; no change is scheduled.

**Outcome link:** [`REFERENCE.md`](REFERENCE.md)

## LOG-FINDING-002

**Status:** open

**Confidence:** high

**Observation:** `RollingFileLogSink` keys its process-wide serialization gate on the normalized path in a static dictionary that is never pruned, so the table grows by one small entry for every distinct path the process ever uses, for the lifetime of the loaded assembly.

**Evidence:** The static `PathGates` dictionary is only ever added to; the sink is not `IDisposable` and has no unregistration path. The F1-LOG review recorded this as `LOG-12`, a code fact rather than a measured leak.

**Hypothesis:** Ordinary applications use a small fixed set of log paths and never notice. An application that generates per-session, per-date, or per-tenant paths at runtime accumulates entries proportional to the number of distinct paths. No long-running measurement has ever bounded that growth.

**Disposition:** Keep as a finding, not a defect. Pruning would require reference counting a sink that deliberately holds no handle and is deliberately not disposable. Applications that mint paths dynamically should reuse sink instances. No change is scheduled.

**Outcome link:** [`audits/public-api-review-2026-08-17.md`](audits/public-api-review-2026-08-17.md)

## LOG-FINDING-003

**Status:** open

**Confidence:** medium

**Observation:** The "one process owns a log path" rule is derived from the `FileShare.Read` mode in source and has never been exercised across real processes. A second process writing the same path fails with a sharing violation that the pipeline absorbs, so that process logs nothing and surfaces no error.

**Evidence:** `RollingFileLogSink.Write` opens the append stream with `FileShare.Read`; `Logger.Log` swallows every sink exception. The F1-LOG review recorded `LOG-15` explicitly as resting on the share mode and the sink's own class comment, with no cross-process test executed. The shared Observability scenario injects a *file lock* within one process and does not start a second writer process.

**Hypothesis:** The behavior is almost certainly as documented, but the consequence — silent total log loss for the losing process — is the kind of failure a deployment discovers only in production, and it is the one Logging failure mode with no executed evidence at its real boundary.

**Disposition:** Keep as a finding and carry the qualification gap explicitly as `LOG-VALREQ-013`. Do not treat the current documentation as executed evidence. Give each process its own path.

**Outcome link:** [`VALIDATION_REQUIREMENTS.md`](VALIDATION_REQUIREMENTS.md)
