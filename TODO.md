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
