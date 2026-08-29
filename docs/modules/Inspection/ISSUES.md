# NekoLib.Inspection Confirmed Issues

**Document ID:** INSP-ISSUES

**Schema version:** 1

**Kind:** reference

**Lifecycle:** current

**Subject:** confirmed defects in the NekoLib.Inspection boundary

**Surface:** issues

**Boundary:** inspection

**Authority role:** normative

**Mutation:** authored

**Indexing:** include

Only verified defects belong here. A recorded issue is not scheduled work until
it is explicitly promoted to [`TODO.md`](../../../TODO.md). Unconfirmed
observations belong in [`FINDINGS.md`](FINDINGS.md).

The defects the F1-INSP review confirmed — non-deterministic provider and key
enumeration order, duplicate budgeted provider work with late failures going
unobserved, a wrong parameter name on invalid capacity, and a clear that still
mutated state after disposal — were all implemented before `1.0.0` and are
preserved in [`CHANGELOG.md`](CHANGELOG.md), [`HISTORY.md`](HISTORY.md), and the
historical
[`audits/public-api-review-2026-08-17.md`](audits/public-api-review-2026-08-17.md).

## INSP-ISSUE-001

**Status:** confirmed

**Severity:** low

**Affected releases:** `1.0.0`, and the current source baseline

**Symptom:** `CaptureSnapshot` can admit and invoke a state provider after its shared completion budget has already been consumed, so a provider that the documented contract says must be skipped with `<snapshot timed out>` instead runs and its value is recorded. The focused regression `CaptureSnapshot_SharedBudgetExpires_SkipsLaterProviders` fails intermittently for this reason, which also makes the Inspection suite non-deterministic.

**Trigger:** A provider that exhausts the budget by a sub-millisecond margin, with at least one later registered provider. `Task.Wait(remaining)` reports the timeout while the capture stopwatch has not yet crossed the nominal deadline, so the next iteration's `remaining <= TimeSpan.Zero` guard is still satisfied and the next provider is admitted.

**Evidence:** Observed three times in 35 focused-suite runs across both targets on 2026-08-29 at working-tree source based on `ac3da3dccb7be57230bae3de377109c617552af8`. One failure was captured with a TRX logger and positively identified: `NekoLib.Inspection.Tests.Unit.InspectionRuntimeTests.CaptureSnapshot_SharedBudgetExpires_SkipsLaterProviders`, `tests/NekoLib.Inspection.Tests/Unit/InspectionRuntimeTests.cs:279`, `Assert.Equal() Failure: Expected: <snapshot timed out>, Actual: 2`. Two further single-test failures with the same signature were observed but not captured by name. The mechanism is visible in `InspectionRuntime.CaptureSnapshot`, which uses `continue` after a timed-out wait and relies on the next iteration's remaining-budget guard to stop admission.

**Workaround:** None is needed for safety. The admitted provider receives only the residual budget, so the capture is not meaningfully unbounded, and a snapshot that contains a real provider value instead of a timeout marker is not worse evidence. A consumer that requires the skip to be deterministic can register at most one provider capable of exhausting the budget.

**Intended fix:** None accepted. The structurally identical defect in `Logger.Flush` was corrected in `d6f2efdbe99f4a827293cdf4e8ed27c4096d134a` by distinguishing a timed-out result from a failed one and returning immediately on exhaustion rather than relying on the next iteration's guard; the same shape would apply here. That is a recorded parallel, not an accepted decision.

**Fix release:** none

**Roadmap:** Not promoted. This documentation review is read-only with respect to product code, so no fix was implemented and nothing was added to [`TODO.md`](../../../TODO.md). Promotion requires an accepted decision by the owner. The qualifying requirement that this defect fails is [`INSP-VALREQ-005`](VALIDATION_REQUIREMENTS.md).
