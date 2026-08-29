# NekoLib.Watchdog.Host Confirmed Issues

**Document ID:** WDGHOST-ISSUES

**Schema version:** 1

**Kind:** reference

**Lifecycle:** current

**Subject:** confirmed defects in the NekoLib.Watchdog.Host boundary

**Surface:** issues

**Boundary:** watchdog.host

**Authority role:** normative

**Mutation:** authored

**Indexing:** include

## Empty state — 2026-08-29

The module review at commit `16c217e7daa406ee3992b56ff907604e82313d1c` confirmed
no defect for this registry.

That statement is bounded by what the review did. It read the complete Host
source, the project file, the consumer targets file, the pack and probe scripts,
the three package-consumer projects, the Host-scoped focused tests, the deployed
sidecar scenario record, and the preserved contract review, and it reverified
all six WDHOST dispositions against current source. It executed no build, test,
scenario, or package run of its own, and it created no package.

Observations that are real but not verified as defects are recorded in
[`FINDINGS.md`](FINDINGS.md). Two of them are the most plausible candidates for
promotion: `WDGHOST-FINDING-001`, where the consumer framework version is never
checked before a payload is selected, and `WDGHOST-FINDING-002`, where an
explicit RID suppresses the unrecognized-`RuntimeIdentifier` rejection. Both are
deployment-contract questions rather than demonstrated failures, and each needs
a build that actually produces the wrong outcome before it could enter this
registry.

An empty registry is therefore not evidence that the boundary has zero defects.
It records that nothing met the bar of a verified defect at this baseline. A new
entry requires a stable `WDGHOST-ISSUE-nnn` identifier and direct evidence, and
scheduling any fix still requires explicit promotion to
[`TODO.md`](../../../TODO.md).
