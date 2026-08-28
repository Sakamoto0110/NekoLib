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

### `NEKOMKT-F009` — ship XML API documentation in managed-library packages

**Status:** promoted; all five family documentation subtasks completed. The
final integrated package gate remains closed pending separate authorization.

**Source:** external finding `F-009`, reconciled in the
[`NekoMarketplace evidence intake`](docs/audit/nekomarketplace-external-consumer-evidence-intake-2026-08-26.md),
residual findings `DOC-001` and `DOC-002` in the
[`public API documentation and extensibility review`](docs/audit/public-api-documentation-extensibility-review-2026-08-27.md),
and the owner's direct promotion and scope decision on 2026-08-27.

**Accepted scope:** complete member-level XML documentation and matching
consumer extension guidance for the packageable managed public API, generate the
XML assets, and include each matching file beside its target-framework assembly
in the package. Work is split into five independently executable family
subtasks. Their listing order is organizational only: no family is a prerequisite
for another, and separately authorized subtasks may proceed in any order.

This decision makes documentation coverage part of `NEKOMKT-F009`; it is not a
separate promoted item. The change must preserve product behavior, the accepted
public API, both target families, and unrelated warning identities.

**Shared documentation schema:**

1. Use the current Release assembly and both accepted TFM manifests as the exact
   public-member inventory for each managed package. Target-specific members
   must be documented only where they actually exist.
2. Give every public type and member effective XML documentation. Use
   `<inheritdoc />` only when the inherited contract is accurate; otherwise
   provide an explicit summary and the applicable parameter, type-parameter,
   return/value, exception, nullability, and target-specific semantics.
3. Describe consumer-visible contract rather than restating the signature:
   ownership, lifecycle, ordering, concurrency, cancellation, failure behavior,
   security/sensitive-data boundaries, compatibility, and resource cleanup are
   required where they materially apply.
4. Document every supported consumer implementation seam in the current module
   technical reference with composition guidance, invariants, a minimal usage or
   implementation example when useful, and an explicit statement of nearby
   capability interfaces that are not plug-in contracts.
5. Preserve deprecation replacements, experimental classification, both target
   families, current namespaces, and the accepted public API. This work does not
   authorize behavior changes, API additions/removals, baseline rewrites,
   warning suppression, or incidental module-first structural migration.
6. Write comments and documentation in English and keep the current authority
   split: source/assemblies own behavior and public shape, while current module
   references own the normative consumer contract.
7. Treat the existence of a source XML comment, generated XML member entry, XML
   documentation file, or zero `CS1591` count as inventory evidence only. None
   proves that the API is accurately, usefully, or completely documented. Read
   and judge every existing public comment in the authorized boundary against
   current source, tests, and the normative technical reference. Within an
   authorized family subtask, correct inaccurate, stale, vague, or incomplete
   documentation without requesting separate item-by-item permission. Stop only
   when there is material ambiguity about product/API intent or when the required
   correction would change behavior or the public contract.

**Family task list:**

| Subtask | Boundary | Planning baseline | Status |
|---|---|---:|---|
| `NEKOMKT-F009-NAV` | Navigation family: `NekoLib.Navigation`, `NekoLib.Navigation.WinForms`, and `NekoLib.Navigation.Wpf` | 368 residual unique `CS1591` diagnostics | completed 2026-08-28; zero residual unique `CS1591` diagnostics |
| `NEKOMKT-F009-DATA` | `NekoLib.Data` | 318 | completed 2026-08-28; zero residual unique `CS1591` diagnostics |
| `NEKOMKT-F009-CORE` | `NekoLib.Core` | 98 | completed 2026-08-28; zero residual unique `CS1591` diagnostics |
| `NEKOMKT-F009-PIPES` | `NekoLib.Pipes` | 94 | completed 2026-08-27; zero residual unique `CS1591` diagnostics |
| `NEKOMKT-F009-TAIL` | Remaining managed families: Watchdog, HTTP, Diagnostics including Diagnostics.Windows, Inspection, Logging, Mvvm, Devices, and Telemetry | 202 | completed 2026-08-27; zero residual unique `CS1591` diagnostics |

The counts are the post-review snapshot from 2026-08-27 and sum to 1,080. They
must be refreshed when a subtask starts and are not a completion denominator by
themselves. `NekoLib.Watchdog.Host` remains a tools/deployment package rather
than a managed public API family and is outside this member-coverage split.

**Dependencies and gates:**

1. Receive explicit implementation authorization for one or more named family
   subtasks. Authorization for one family does not open another family.
2. At subtask start, reconfirm the working tree, project/target matrix, current
   technical reference, Release assemblies, accepted manifests, and residual
   XML-documentation diagnostics for that exact boundary.
3. Preserve unrelated work and keep each family reviewable as a separate change.
   The Tail subtask may be split into smaller module commits without creating a
   mandatory module order.
4. If documentation exposes a source/reference conflict or an API design change
   such as the custom `GuardAttribute.RedirectTo` asymmetry, stop and route that
   decision separately. Do not fix behavior under documentation authorization.
5. Do not update accepted public API baselines automatically. A manifest mismatch
   is a blocking API finding, not documentation work.
6. Open the final integrated package gate only after all five family subtasks are
   closed. Reconfirm the complete packageable managed-library set, distinguish it
   from tool-only payloads, and use a new immutable package version.

**Per-family completion criteria:**

- an opt-in Release rebuild with `GenerateDocumentationFile=true` reports zero
  residual `CS1591` diagnostics for every assembly in the authorized family;
- every pre-existing and new public XML comment has been semantically reviewed;
  zero `CS1591` is necessary but is not sufficient for completion;
- public XML comments satisfy the shared schema, and the current technical
  reference explains every confirmed consumer implementation seam and its
  invariants;
- both supported target families build, the accepted public API baselines remain
  unchanged, and no new warning identity is introduced;
- `eng\verify-docs.ps1` and `git diff --check` pass; and
- the audit is reconciled with the completed family boundary and exact evidence.

**Completion criteria:**

- all five family subtasks are closed against refreshed member inventories;
- every targeted TFM package contains its assembly and matching XML API
  documentation file;
- a PackageReference consumer receives the XML documentation through the
  package rather than through repository source;
- documentation verification, build/package validation, warning-identity
  comparison, and `git diff --check` pass; and
- current packaging documentation records the shipped boundary and its
  validation evidence.

### `NEKOMKT-F026` — retire the legacy SQL-inverted QueryBuilder join overload

**Status:** promoted; release and implementation gates closed.

**Source:** external finding `F-026`, reconciled in the
[`NekoMarketplace evidence intake`](docs/audit/nekomarketplace-external-consumer-evidence-intake-2026-08-26.md),
the accepted
[`Data QueryBuilder decision`](docs/audit/data-type-adaptation-querybuilder-api-review-2026-08-26.md),
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

Any explicitly authorized `NEKOMKT-F009` family subtask is independently
eligible, with no required order among the five families. Its final integrated
package gate remains closed until all five are complete. `NEKOMKT-F026` remains
blocked by its release window regardless of ordering.

`DATA-ADAPT-QB-001` completed on 2026-08-27. Its accepted decisions,
implementation reconciliation, provider/package evidence, and closure are
preserved in the historical
[`Data type-adaptation and QueryBuilder review`](docs/audit/data-type-adaptation-querybuilder-api-review-2026-08-26.md).

Items in [`docs/proposals/`](docs/proposals/README.md), module findings or
issues, audit recommendations, and [`ROADMAP.md`](ROADMAP.md) intentions remain
outside this scheduler until their own formal promotion.
