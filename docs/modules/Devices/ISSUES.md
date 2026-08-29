# NekoLib.Devices Confirmed Issues

**Document ID:** DEV-ISSUES

**Schema version:** 1

**Kind:** reference

**Lifecycle:** current

**Subject:** confirmed defects in the NekoLib.Devices boundary

**Surface:** issues

**Boundary:** devices

**Authority role:** normative

**Mutation:** authored

**Indexing:** include

Only verified defects belong here. An entry requires a stable ID and direct
evidence, and scheduling still requires explicit promotion to
[`TODO.md`](../../../TODO.md).

## Empty state — 2026-08-29

The module review at commit `84970a2ec8db25bb23a8e397d4f1325b0089de8c`
reverified every `DEV-01` through `DEV-15` disposition and the first-pass item
list against current source and confirmed **no open defect**. The dispositions
that changed code are implemented and, where the accepted work added a
regression, pinned by a test; the ones that remain are accepted design or
documented behaviour. That reverification is recorded in
[`FINDINGS.md`](FINDINGS.md).

The four observations that review raised are findings, not issues: each carries
either an explicit evidence limit or a disposition that would require a
behavioural or public API decision. Promoting one to this registry needs
execution evidence that does not exist today.

An empty registry is not proof that the module has no defects. The
[coverage table](VALIDATIONS.md) names the boundaries and targets where no
evidence exists at all — `net481` serial fault behaviour, sustained unread
input, and physical hardware — and a defect living there would not have been
observed by anything run so far.
