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

**Status:** fixed

**Severity:** low

**Affected releases:** reported in `1.0.0` and still open in the `1.4.0` release record

**Symptom:** `CaptureSnapshot` could invoke a later provider after an earlier wait exhausted the shared budget. The regression `CaptureSnapshot_SharedBudgetExpires_SkipsLaterProviders` intermittently observed a provider value where it expected `<snapshot timed out>`.

**Trigger:** A provider wait reported a timeout while the stopwatch still showed a positive fraction of a millisecond remaining. The loop consulted the stopwatch again and admitted the next provider.

**Evidence:** The [original regression record](VALIDATIONS.md#insp-valevid-20260829-002) preserves the 35 runs and the captured assertion. The [fix verification](VALIDATIONS.md#insp-valevid-20261127-001) covers both targets.

**Workaround:** On affected 1.x releases, even one slow provider can time out and allow a subsequent fast callback to run. If deterministic skipping is essential, limit the snapshot to one registered provider. With multiple providers, callbacks must tolerate invocation after another provider times out.

**Intended fix:** Treat the first timed-out wait as budget exhaustion for the rest of that capture. Record a timeout marker for every remaining provider.

**Fix release:** planned for 2.0.0, unreleased

**Roadmap:** Fix implemented for [`INSP-VALREQ-005`](VALIDATION_REQUIREMENTS.md#insp-valreq-005).
