# NekoLib Promoted Work

**Document ID:** GLOBAL-TODO

**Schema version:** 1

**Kind:** roadmap/status

**Lifecycle:** current

**Subject:** formally promoted work, execution gates, dependencies, and completion criteria

**Surface:** work-scheduler

**Boundary:** global

**Authority role:** scheduler

**Mutation:** authored

**Indexing:** include

**Last promotion decision:** 2026-08-30

## Purpose and admission rule

This file owns only work that has been formally promoted. It may describe an
accepted implementation scope in enough detail to execute and close it, but it
does not store unpromoted ideas, historical completion narratives, package
hashes, or general product direction.

Use [`ROADMAP.md`](ROADMAP.md) for direction, intentions, guardrails, and
planning horizons. Use [`docs/proposals/`](docs/proposals/README.md) for concise
unpromoted ideas. Completed work moves to [`docs/history/`](docs/history/README.md)
or another appropriate evidence owner.

Promotion does not have to originate in `docs/proposals/`. A proposal, finding,
confirmed issue, audit, external-consumer record, or direct owner decision may
enter this file when all of the following are true:

1. the relevant current authority and evidence have been inspected;
2. the direction and boundary are formalized;
3. the owner has accepted the work intentionally;
4. dependencies, gates, and completion criteria are explicit; and
5. the source decision or evidence is linked without being copied here in full.

Unchecked items are not automatically authorized to start. An entry may retain
an explicit implementation gate after promotion.

## Current promoted work

### `GOV-PREMISES-001` — formalize scoped confidence premises

**Status:** promoted; implementation and working-tree validation complete;
commit remains open.

**Source:** the owner's direct decision on 2026-08-30 to formalize temporary,
scope-aware premises for extremely high-confidence code and automatically stop
relying on them when independent evidence contradicts the statement.

**Accepted scope:** define a separate premise policy, one-record-per-file JSON
schema and registry, example, and evaluator; cross-reference campaign IDs from
premise records without adding premise definitions to work-campaign schema
version 1; integrate equivalent consumption rules into Codex and Claude
documentation profiles. Do not create a real code premise without a separate,
evidence-backed acceptance decision.

**Dependencies and gates:**

1. Premises may narrow redundant investigation only; they cannot override
   source/evidence, suppress required validation, or authorize any mutation.
2. One qualifying contradiction suspends use; the configured count of distinct
   identities, at least two, or one critical contradiction derives `broken`.
3. Relevant code drift derives `stale`, not `broken`; expiry derives `expired`.
4. Preserve every contradiction and state transition. A broken ID cannot be
   reactivated; a newly accepted record may supersede it.

**Completion criteria:**

- policy, schema, example, registry, evaluator, indexes, and agent guidance
  agree on authority, scope, permitted uses, freshness, and lifecycle;
- evaluator fixtures demonstrate effective `challenged`, `broken`, and `stale`
  without treating the premise as truth;
- the expanded governance campaign deduplicates its finalizers after
  `eng/verify-premises.ps1`, `eng/verify-docs.ps1`,
  `eng/verify-skills.ps1`, and `git diff --check` pass; and
- no actual product-code premise, commit, push, package, or release is inferred
  from this governance implementation.

### `GOV-WORK-CAMPAIGNS-001` — formalize bounded work campaigns

**Status:** promoted; implementation and working-tree validation complete;
commit remains open.

**Source:** the owner's direct decision on 2026-08-30 to formalize the campaign
practice already used for multi-part documentation work.

**Accepted scope:** define one repository-wide policy and machine-readable
manifest for already-authorized multi-stage work; keep active state under
ignored `.local/work-campaigns/`; provide a safe plan-by-default runner that
selects and deduplicates declared finalizers; integrate the contract with agent
and documentation governance. `TODO.md` remains the only promoted-work
scheduler. Scoped premises are explicitly outside schema version 1.

**Dependencies and gates:**

1. Preserve every existing authority, freeze, validation, public API, package,
   release, commit, push, and destructive-action gate.
2. Keep manifest commands structured as executable plus arguments; do not
   evaluate shell command strings.
3. Treat mutable campaign state as non-authoritative and never as validation or
   completion evidence.
4. Validate Codex/Claude documentation-adapter interoperability after adding
   the shared campaign semantics.

**Completion criteria:**

- policy, schema, example, runner, indexes, and agent guidance agree on one
  execution contract;
- the example manifest validates and a real local campaign exercises planning,
  execution, fingerprint deduplication, and the clean-tree gate;
- `eng/verify-docs.ps1`, `eng/verify-skills.ps1`, and `git diff --check` pass;
  and
- the final diff separates campaign-owned changes from pre-existing work and is
  committed only after separate authorization.

### `NEKOMKT-F026` — retire the legacy SQL-inverted QueryBuilder join overload

**Status:** promoted; release and implementation gates closed.

**Source:** external finding `F-026`, reconciled in the
[`NekoMarketplace evidence intake`](docs/audit/nekomarketplace-external-consumer-evidence-intake-2026-08-26.md),
the accepted
[`Data QueryBuilder decision`](docs/modules/Data/audits/type-adaptation-querybuilder-api-review-2026-08-26.md),
and the owner's direct promotion decision on 2026-08-27.

**Accepted scope:** keep `JoinOn(...)` and `JoinTrusted(...)` as the canonical
structured/trusted join surfaces, then remove only the warning-only legacy
`Join(string Table, string OnExpression, string Type)` compatibility overload
when its existing compatibility promise permits the breaking change. This entry
does not reopen the completed QueryBuilder normalization design.

**Dependencies and gates:**

1. At least one released minor version must carry the current warning-only
   `Obsolete` marker and concrete replacement guidance.
2. A `2.0.0`-or-later public API window must be explicitly opened and reviewed.
3. Receive separate implementation authorization after both release gates are
   satisfied; changing the marker to `error: true` is removal-equivalent and is
   not an early substitute.
4. Preserve both target frameworks and the existing structured join semantics.

**Completion criteria:**

- the legacy overload is absent from source and both compiled public API
  baselines only inside the authorized next-major window;
- focused tests use and preserve `JoinOn(...)` and `JoinTrusted(...)` across
  both targets;
- the changelog and migration guide state the completed removal and replacement
  path; and
- full solution, public API, immutable package, PackageReference-consumer, and
  `git diff --check` validation pass.

`NEKOMKT-F026` remains blocked by its release window regardless of ordering.

`NEKOMKT-F009` completed on 2026-08-28. Its five-family documentation campaign,
package-delivery correction, immutable package evidence, consumer validation,
and closure are preserved in the historical
[`public API documentation and extensibility review`](docs/audit/public-api-documentation-extensibility-review-2026-08-27.md).

`DATA-ADAPT-QB-001` completed on 2026-08-27. Its accepted decisions,
implementation reconciliation, provider/package evidence, and closure are
preserved in the historical
[`Data type-adaptation and QueryBuilder review`](docs/modules/Data/audits/type-adaptation-querybuilder-api-review-2026-08-26.md).

Items in [`docs/proposals/`](docs/proposals/README.md), module findings or
issues, audit recommendations, and [`ROADMAP.md`](ROADMAP.md) intentions remain
outside this scheduler until their own formal promotion.
