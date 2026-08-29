# NekoLib.Http Confirmed Issues

**Document ID:** HTTP-ISSUES

**Schema version:** 1

**Kind:** reference

**Lifecycle:** current

**Subject:** confirmed defects in the NekoLib.Http boundary

**Surface:** issues

**Boundary:** http

**Authority role:** normative

**Mutation:** authored

**Indexing:** include

No confirmed HTTP defect is recorded for the current source baseline.

This empty register is not proof that the module is defect-free. The defects the
F1-HTTP review confirmed — an unresolvable charset throwing on one target and
succeeding on the other, a size-bound failure discarding the protocol evidence it
was carrying, an unregistered-endpoint message that misreported an
instance-identity mismatch, and option validation reporting three different
exception types — were all implemented before `1.0.0` and are preserved in
[`CHANGELOG.md`](CHANGELOG.md), [`HISTORY.md`](HISTORY.md), and the historical
[`audits/public-api-review-2026-08-17.md`](audits/public-api-review-2026-08-17.md).

The absence of a defect here should be read against this boundary's evidence
limits rather than as broad assurance. Every deterministic test uses an
in-process message handler, so no recorded run has exercised a real socket, TLS,
proxy, redirect, compression, or HTTP/2 behavior, and the only provider evidence
is two bounded runs against one provider on one date. Those gaps are carried in
[`VALIDATION_REQUIREMENTS.md`](VALIDATION_REQUIREMENTS.md) and
[`VALIDATIONS.md`](VALIDATIONS.md).

Unconfirmed observations belong in [`FINDINGS.md`](FINDINGS.md), and only
accepted scheduled work belongs in [`../../../TODO.md`](../../../TODO.md).
