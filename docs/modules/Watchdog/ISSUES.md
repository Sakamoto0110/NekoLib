# NekoLib.Watchdog Confirmed Issues

**Document ID:** WDG-ISSUES

**Schema version:** 1

**Kind:** reference

**Lifecycle:** current

**Subject:** confirmed defects in the NekoLib.Watchdog boundary

**Surface:** issues

**Boundary:** watchdog

**Authority role:** normative

**Mutation:** authored

**Indexing:** include

## Empty state — 2026-08-29

The module review at commit `c1d6aadcea773529220b7850f0e18d1dd0e3c1f0` confirmed
no defect for this registry.

That statement is bounded by what the review did. It read the complete Watchdog
library source, both accepted API manifests, the focused dual-target suite, both
runtime-scenario records, the package-consumer probes, and every preserved
audit, and it reverified each historical lead — the first-pass items, the twelve
WDOG findings, and the two IPC leads that the Pipes boundary routed here —
against current source. It executed no build, test, scenario, or package run of
its own.

Observations that are real but not verified as defects are recorded in
[`FINDINGS.md`](FINDINGS.md). The one that would most plausibly become an issue
is `WDG-FINDING-001`: the single-instance guard is created in the Windows global
kernel-object namespace, and no run has ever exercised that under a restricted
standard-user token. It needs a reproduction before it could enter this
registry.

An empty registry is therefore not evidence that the boundary has zero defects.
It records that nothing met the bar of a verified defect at this baseline. A new
entry requires a stable `WDG-ISSUE-nnn` identifier and direct evidence, and
scheduling any fix still requires explicit promotion to
[`TODO.md`](../../../TODO.md).
