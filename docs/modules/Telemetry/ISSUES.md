# NekoLib.Telemetry Confirmed Issues

**Document ID:** TEL-ISSUES

**Schema version:** 1

**Kind:** reference

**Lifecycle:** current

**Subject:** confirmed defects in the NekoLib.Telemetry boundary

**Surface:** issues

**Boundary:** telemetry

**Authority role:** normative

**Mutation:** authored

**Indexing:** include

No confirmed Telemetry defect is recorded for the current source baseline.

This empty register is not proof that the module is defect-free. The three
behavioral defects the F1-TEL review confirmed — a malformed terminal payload
destroying its operation, a whitespace parent identifier reading as a
correlation link, and a caller-mutable sink array re-targeting a live pipeline —
were all implemented before `1.0.0` and are preserved in
[`CHANGELOG.md`](CHANGELOG.md), [`HISTORY.md`](HISTORY.md), and the historical
[`audits/public-api-review-2026-08-17.md`](audits/public-api-review-2026-08-17.md).
The independent pre-package review of that implementation found no additional
defect or contract mismatch.

Unconfirmed observations belong in [`FINDINGS.md`](FINDINGS.md), and only
accepted scheduled work belongs in [`../../../TODO.md`](../../../TODO.md).
