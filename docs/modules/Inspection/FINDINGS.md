# NekoLib.Inspection Findings

**Document ID:** INSP-FINDINGS

**Schema version:** 1

**Kind:** reference

**Lifecycle:** current

**Subject:** unconfirmed and non-normative observations about the NekoLib.Inspection boundary

**Surface:** findings

**Boundary:** inspection

**Authority role:** non-normative

**Mutation:** authored

**Indexing:** include

Everything here is non-normative. A finding becomes an issue only after it is
verified, and scheduled work only after explicit promotion to
[`TODO.md`](../../../TODO.md). Nothing here proposes lifting the instrumentation
or action freeze.

## INSP-FINDING-001

**Status:** open

**Confidence:** high

**Observation:** The runtime's outcome markers are ordinary strings placed into the same value space as provider and payload results, so a provider that legitimately returns the text `<null>`, `<snapshot timed out>`, or `<snapshot threw: X>` is indistinguishable from the runtime having produced that marker itself.

**Evidence:** Snapshot state is an `IReadOnlyDictionary<string, object>` whose values are either the provider's own object or one of the literal marker strings the runtime substitutes. Nothing tags, wraps, or types a marker. The same applies to `<payload threw: X>` in the operation payload slot.

**Hypothesis:** The collision is unlikely by accident but trivially reachable by a provider that echoes captured text, and the consequence lands where it matters least visibly: `NekoLib.Diagnostics` writes these values into a crash bundle, so a reader diagnosing an incident could attribute a provider's own output to a timeout that never happened, or the reverse. A typed or wrapped outcome would fix it and would change the snapshot value contract.

**Disposition:** Keep as a finding. Changing the marker representation is a consumer-visible contract change to `InspectionSnapshot.State` and needs an accepted decision. The reference now states that markers are untyped strings sharing the provider value space so consumers can decide whether they need their own disambiguation. No change is scheduled.

**Outcome link:** [`REFERENCE.md`](REFERENCE.md)

## INSP-FINDING-002

**Status:** open

**Confidence:** medium

**Observation:** The experimental signal for the four action members rides entirely on `ObsoleteAttribute(error: false)`, which the compiler reports as `CS0618` — the same diagnostic every ordinary deprecation uses. A consumer who suppresses `CS0618` project-wide for an unrelated deprecated API silently loses the only build-time signal that these members are experimental.

**Evidence:** All four members carry `[Obsolete("Experimental API NEKOEXP0001: compatibility is not guaranteed.", error: false)]` and nothing else. There is no dedicated analyzer, no distinct diagnostic id, and no runtime gate. External consumer evidence confirms the diagnostic does reach a real consumer's build, which is what makes suppression the remaining exposure rather than delivery.

**Hypothesis:** The marker text carries `NEKOEXP0001`, so a targeted suppression is possible and the migration guide demonstrates the narrow `#pragma warning disable CS0618` form. The risk is the blunt form: a project-level `NoWarn` is a normal thing for a team to add for an unrelated reason, and it would not be obvious that it also disarmed this.

**Disposition:** Keep as a finding. A dedicated analyzer or diagnostic id is a product decision with packaging consequences and is outside both the current freeze and current promoted work. The reference and the migration guide both show the narrow suppression form. No change is scheduled.

**Outcome link:** [`migrations/f1.md`](migrations/f1.md)

## INSP-FINDING-003

**Status:** open

**Confidence:** medium

**Observation:** A state provider that blocks permanently degrades its own slot for the lifetime of the process and holds a thread-pool thread indefinitely. The single-flight cache returns the same never-completing task to every later capture, so that provider's slot reports `<snapshot timed out>` forever and no fresh invocation is ever attempted.

**Evidence:** Each registration caches its in-flight task and starts a replacement only once the previous one has completed. A task that never completes therefore never releases the slot. The timeout bounds caller completion and explicitly does not cancel the delegate, so the blocked work is never abandoned by the runtime.

**Hypothesis:** This is the deliberate cost of not starting unbounded duplicate work against a slow provider, and for a merely slow provider it is the right trade. For a permanently blocked one it converts a transient timeout into a permanent evidence hole plus one leaked pool thread — and the leak is invisible in diagnostics, because the registry still counts the provider as healthy. No run has ever exercised a permanently blocked provider; the scenario's timeout fault uses a provider that eventually returns.

**Disposition:** Keep as a finding, not a defect: the behavior follows directly from the accepted single-flight decision, and unregistering the provider is an available application-side remedy. The qualification gap is carried explicitly as [`INSP-VALREQ-016`](VALIDATION_REQUIREMENTS.md). No change is scheduled.

**Outcome link:** [`VALIDATION_REQUIREMENTS.md`](VALIDATION_REQUIREMENTS.md)
