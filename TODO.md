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

**Last promotion decision:** 2026-08-27

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

No implementation is currently promoted.

`DATA-ADAPT-QB-001` completed on 2026-08-27. Its accepted decisions,
implementation reconciliation, provider/package evidence, and closure are
preserved in the historical
[`Data type-adaptation and QueryBuilder review`](docs/audit/data-type-adaptation-querybuilder-api-review-2026-08-26.md).

Items in [`docs/proposals/`](docs/proposals/README.md), module findings or
issues, audit recommendations, and [`ROADMAP.md`](ROADMAP.md) intentions remain
outside this scheduler until their own formal promotion.
