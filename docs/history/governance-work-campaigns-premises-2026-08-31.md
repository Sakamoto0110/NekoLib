# Work Campaign and Scoped Premise Governance Completion

**Document ID:** GLOBAL-GOVERNANCE-CAMPAIGNS-PREMISES-HISTORY

**Schema version:** 1

**Kind:** roadmap/status

**Lifecycle:** historical

**Subject:** completed work-campaign coordination and scoped-premise governance

**Surface:** roadmap

**Boundary:** global

**Authority role:** non-normative

**Mutation:** snapshot

**Indexing:** include

**Reference date:** 2026-08-31

**Reference commit:** `94d799d4897ecb6627be61131549af31a4dab730`

**Current state:** [`work-campaign-policy.md`](../governance/work-campaign-policy.md), [`premise-policy.md`](../governance/premise-policy.md), and [`TODO.md`](../../TODO.md)

## Outcome

`GOV-WORK-CAMPAIGNS-001` and `GOV-PREMISES-001` completed at the reference
commit. NekoLib now has one shared contract for bounded multi-stage campaigns
and one separate contract for falsifiable, automatically suspended reasoning
premises. Both systems preserve the existing source, public API, validation,
package, release, and authorization boundaries.

The campaign implementation consists of the normative policy, JSON schema,
canonical example, plan-by-default runner, ignored local manifests, and a
fingerprint ledger that deduplicates successful finalizers. The premise
implementation consists of the normative policy, one-record-per-file schema
and registry, canonical example, and evaluator that derives challenged, broken,
stale, and expired states without treating confidence as evidence.

## Validation boundary

The active local governance campaign exercised manifest validation, planning,
execution, fingerprint deduplication, and the post-commit clean-tree gate. Its
final pre-commit fingerprint passed:

- `eng/verify-docs.ps1`;
- `eng/verify-skills.ps1`;
- `eng/verify-premises.ps1`; and
- `git diff --check` over the campaign-owned paths.

These checks establish documentation/schema integrity, registered skill
topology, premise evaluator behavior, and diff hygiene. They do not constitute
product builds, tests, compiled API, runtime, interactive, package, release, or
push evidence; none of those layers changed or ran for this governance work.

## Preserved boundaries

- Active manifests and mutable state remain ignored under
  `.local/work-campaigns/` and are not repository evidence.
- `TODO.md` remains the only promoted-work scheduler.
- A campaign consumes authority and never creates it.
- A premise may reduce redundant investigation only while effectively active;
  it never overrides current evidence or suppresses required validation.
- No actual product-code premise was created by this work.
