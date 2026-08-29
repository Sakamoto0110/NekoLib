# NekoLib.Pipes Confirmed Issues

**Document ID:** PIPE-ISSUES

**Schema version:** 1

**Kind:** reference

**Lifecycle:** current

**Subject:** confirmed defects in the NekoLib.Pipes boundary

**Surface:** issues

**Boundary:** pipes

**Authority role:** normative

**Mutation:** authored

**Indexing:** include

## Empty state — 2026-08-28

The module review at commit `cf2ef2f109e65063bd08259ad4b6336ee4a39885` confirmed
no defect for this registry.

That statement is bounded by what the review did. It read the complete Pipes
source, both accepted API manifests, the focused suite, the runtime scenario
record, and every preserved audit, and it reverified each historical hardening
lead against current source — but it executed no build, test, scenario, or
package run of its own. Observations that are real but unverified as defects are
recorded in [`FINDINGS.md`](FINDINGS.md), notably the unbacked-off accept retry
loop in `PIPE-FINDING-001`, which would need a reproduction before it could
enter this registry.

An empty registry is therefore not evidence that the boundary has zero defects.
It records that nothing met the bar of a verified defect at this baseline. A new
entry requires a stable `PIPE-ISSUE-nnn` identifier and direct evidence, and
scheduling any fix still requires explicit promotion to
[`TODO.md`](../../../TODO.md).
