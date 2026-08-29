# NekoLib.Diagnostics Findings

**Document ID:** DIAG-FINDINGS

**Schema version:** 1

**Kind:** reference

**Lifecycle:** current

**Subject:** unconfirmed and non-normative observations about the NekoLib.Diagnostics family

**Surface:** findings

**Boundary:** diagnostics

**Authority role:** non-normative

**Mutation:** authored

**Indexing:** include

## DIAG-FINDING-001

**Status:** open

**Confidence:** medium

**Observation:** Crash bundle directories use a UTC timestamp with millisecond resolution. Multiple installed handlers configured with the same `CrashRootDirectory` receive one process-level report sequentially and can select the same directory name, allowing their `crash.txt`, `crash.dmp`, or tail artifacts to merge or overwrite.

**Evidence:** `CrashHandler.CreateCrashDirectory()` formats `crash-yyyy-MM-dd_HH-mm-ss-fffZ`; the process-wide registry snapshots every installed handler and reports to them in registration order. The 2026-08-17 public-API audit retained the collision as an unreproduced theoretical risk.

**Hypothesis:** The collision requires at least two handlers sharing a root and landing inside one millisecond; ordinary single-handler composition does not trigger it.

**Disposition:** Keep this as a finding, not a confirmed issue. Prefer one process-owned handler or distinct roots when composing multiple handlers. A directory-allocation change requires a reproduced defect and compatibility review because the path format is observable; no change is scheduled.

**Outcome link:** [`REFERENCE.md#bundle-layout`](REFERENCE.md#bundle-layout)

### Security-policy horizon

Encryption at rest, retention and deletion schedules, ACLs, upload transport,
dump access, and consumer-specific secrecy policy are application/deployment
decisions. External review evidence does not establish a NekoLib defect or
authorize a product policy change. [`NEKOMKT-F022`](../../../ROADMAP.md) remains
the explicit product/security decision horizon. Reopen this boundary only with
an accepted requirement and promote implementation through
[`../../../TODO.md`](../../../TODO.md).
