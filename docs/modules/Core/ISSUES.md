# NekoLib.Core Confirmed Issues

**Document ID:** CORE-ISSUES

**Schema version:** 1

**Kind:** reference

**Lifecycle:** current

**Subject:** confirmed defects in the NekoLib.Core boundary

**Surface:** issues

**Boundary:** core

**Authority role:** normative

**Mutation:** authored

**Indexing:** include

No confirmed Core defect is recorded for baseline commit
`9e47ff6e4ded1b69b6c20765a7469b107c416e99`.

The source-first review reconciled every public symbol with both accepted API
manifests, the focused contract tests, current XML comments, the historical F1
review, and the concrete capability references. The pre-1.0 collection-aliasing
defects were fixed before the stable release, and the unresolved action design
was classified explicitly as experimental `NEKOEXP0001` instead of being
silently promoted to a stable contract.

[`TODO.md`](../../../TODO.md) contains no promoted Core work. Its current Data
release-window item, roadmap investigations, dependent-module findings, and the
broad Inspection freeze are not Core issues. Historical dispositions remain in
[`audits/public-api-review-2026-08-17.md`](audits/public-api-review-2026-08-17.md);
consumer impact remains in [`CHANGELOG.md`](CHANGELOG.md) and
[`migrations/f1.md`](migrations/f1.md).

This empty registry is bounded by the executed evidence in
[`VALIDATIONS.md`](VALIDATIONS.md). Unconfirmed observations belong in
[`FINDINGS.md`](FINDINGS.md), and an issue does not become scheduled work
without explicit promotion to the root scheduler.
