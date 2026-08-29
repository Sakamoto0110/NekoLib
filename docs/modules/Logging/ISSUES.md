# NekoLib.Logging Confirmed Issues

**Document ID:** LOG-ISSUES

**Schema version:** 1

**Kind:** reference

**Lifecycle:** current

**Subject:** confirmed defects in the NekoLib.Logging boundary

**Surface:** issues

**Boundary:** logging

**Authority role:** normative

**Mutation:** authored

**Indexing:** include

Only verified defects belong here. A recorded issue is not scheduled work until
it is explicitly promoted to [`TODO.md`](../../../TODO.md). Unconfirmed
observations belong in [`FINDINGS.md`](FINDINGS.md).

## LOG-ISSUE-001

**Status:** fixed

**Severity:** low

**Affected releases:** `1.0.0`

**Symptom:** Budget exhaustion did not reliably stop admission of later sink flushes. `FlushSink` reported failure and timeout with the same `false`, so the loop relied on the next iteration's `remaining <= TimeSpan.Zero` guard to stop. When a sink's wait expired a fraction of a millisecond before the pipeline stopwatch crossed the nominal boundary, `remaining` was still positive and the next sink was admitted after the budget was already spent, contradicting the documented single pipeline-wide bound.

**Trigger:** A flushable sink that exceeds its share of the budget by a sub-millisecond margin, with at least one later flushable sink registered.

**Evidence:** A pre-existing `net481` timing test failed during the `1.1.0-local.8` package attempt and reproduced in isolation. Commit `d6f2efdbe99f4a827293cdf4e8ed27c4096d134a`, titled `fix(logging): stop sink admission after timeout`, replaced the boolean result with a three-state `Completed`/`Failed`/`TimedOut` outcome and returns immediately on `TimedOut`. The corrected regression then passed 30 consecutive `net481` runs and the complete dual-target Logging suite. Recorded in [`../../audit/public-api-documentation-extensibility-review-2026-08-27.md`](../../audit/public-api-documentation-extensibility-review-2026-08-27.md).

**Workaround:** None needed for the returned value, which was already `false`. An application that must not have a further sink `Flush()` started after its budget expired could compose one flushable sink per logger.

**Intended fix:** Distinguish sink failure from budget exhaustion in the admission loop and stop admission immediately on exhaustion rather than on the next iteration's remaining-budget guard.

**Fix release:** unreleased; implemented in commit `d6f2efdbe99f4a827293cdf4e8ed27c4096d134a` and qualified in immutable candidate `1.1.0-local.8`

**Roadmap:** none — the fix is implemented and needs no promoted work; it ships with the next release of this package. The practical overrun was small, because each extra admitted sink received only the residual budget, but the extra sink's `Flush()` was started on a background thread and then abandoned.
