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

**Last promotion decision:** 2026-08-26

## Purpose and admission rule

This file owns only work that has been formally promoted. It may describe the
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

### Data type adaptation and QueryBuilder API normalization

**Promotion ID:** DATA-ADAPT-QB-001

**Aliases:** G3

**State:** in progress; QueryBuilder API and validated write-side type-adaptation slices complete; read-side temporal policy pending

**Decision date:** 2026-08-26

**Decision record:** [`data-type-adaptation-querybuilder-api-review-2026-08-26.md`](docs/audit/data-type-adaptation-querybuilder-api-review-2026-08-26.md)

**Origin:** clarified NekoMarketplace `P-001` plus the confirmed
`F-003`/`NOTE-012`/`NOTE-013` QueryBuilder correction. The bounded reconciliation
is appended to the
[`NekoMarketplace evidence intake`](docs/audit/nekomarketplace-external-consumer-evidence-intake-2026-08-26.md).

#### Accepted implementation scope

- [x] Add gateway-instance `TypePromotionPolicy` values `Disabled`,
  `ExplicitOnly` (default), and `SchemaValidated`. `Disabled` blocks even
  field-explicit promotion; schema validates but does not authorize under
  `ExplicitOnly`; only registered, proven-lossless conversions may be
  schema-authorized.
- [ ] Separate input promotion, provider representation adaptation/decay, and
  read materialization. Prefer exact binding, prohibit unrestricted conversion
  guessing and post-dispatch retry, and fail locally with sanitized evidence.
  The write side is implemented; read materialization still requires temporal
  reporting and per-field loss authorization.
- [x] Finalize the public loss-policy spelling while preserving the accepted
  semantics: lossy fallback is disabled by default, requires an explicit
  per-field rule, and always reports; strict mode rejects the fallback chain at
  the first incompatible preferred representation.
- [x] Add an instance-scoped, observational `OnTypeAdaptation` hook for the
  implemented write path. It never
  authorizes a conversion, reports once per logical adaptation, isolates hook
  failures, and excludes values, SQL, parameters, connection strings,
  credentials, and unfiltered provider exception text.
- [x] Add provider profiles plus `SchemaDiscoveryMode.Disabled`, `Lazy`
  (default), and `Preload`. Lazy discovery is pre-transaction, thread-safe,
  single-flight, cached for the gateway lifetime, refreshable after migrations,
  and never parses raw SQL to infer schema.
- [x] Normalize the base structured fluent API around
  `InsertInto(table).Value(...)`, `Update(table).Set(...)`, structured
  `Where(...)`, `JoinOn(...)`, and explicitly trusted `WhereTrusted(...)` and
  `JoinTrusted(...)` escape hatches. `QueryBuilder` remains independent from
  schema, providers, commands, binders, and hooks.
- [x] Extend logical parameter descriptors with neutral promotion/type metadata
  without changing that independence boundary.
- [x] Preserve the existing logical-parameter model across named and positional
  providers. Final SQL occurrence order remains authoritative for positional
  binding. Future adaptation must still run and report only once when one
  logical value creates multiple physical parameters.
- [x] Retain replaced stable overloads as compatibility shims that delegate to
  the new model. Mark them with
  `[Obsolete("This overload is retained for compatibility and will be removed in the next major version. Use <replacement> instead.", error: false)]`.
  The real message must name the concrete replacement. Keep the shims for at
  least one released minor and remove them only in `2.0.0` or later; never turn
  this accepted deprecation into `error: true`.
- [x] Cover both targets, named and OleDb positional binding, repeated logical
  parameters, subqueries, idempotent builds, all promotion modes, lossy/strict
  decisions, hook redaction/isolation, schema-cache concurrency/failure,
  unknown-provider failure, no-dispatch/no-retry guarantees, compiled API
  baselines, migration, changelog, package consumers, and real providers.

The first mandatory deprecation set is the current dictionary-based
`InsertInto` and `Update`, condition-template `Where`, and raw-expression
`Join` overloads. Existing structured `WhereIn`, `WhereNotIn`, `WhereBetween`,
and `WhereLike` methods are not deprecated by this decision.

The QueryBuilder slice selected `QueryOperator`, `QueryJoinType`,
`WhereTrusted(...)`, `JoinOn(...)`, and `JoinTrusted(...)`. Final loss-policy
and type-adaptation names still require a bounded API review before their own
implementation. That review may choose names and exact signatures but may not
reopen the accepted conversion, loss, diagnostic, dispatch,
positional-ordering, or compatibility semantics.

#### QueryBuilder slice reconciliation — 2026-08-26

- The owner separately authorized QueryBuilder-only implementation. No
  promotion, decay, schema, hook, gateway, binder, or neutral adaptation
  descriptor was added.
- Both target builds and the full solution test run pass. Focused regressions
  cover structured operators and nulls, compatibility delegation, build
  idempotence, subquery rewriting, and named versus positional parameter order.
- The compiled `NekoLib.Data` API baselines, current Data reference, migration
  guide, changelog, repository-owned runtime scenario sources, and package
  consumers now use or describe the canonical surface.
- Disposable package `1.1.0-local.1`, built with `-AllowDirty`, passed the full
  package smoke suite for both consumer targets and both the new and deprecated
  shapes. It is local implementation evidence, not clean release provenance.
- No real database provider was executed by this slice. Type-adaptation and
  real-provider validation remain open below.

#### Gate and execution order

**WRITE-SIDE IMPLEMENTATION AUTHORIZED 2026-08-27.** The owner expanded the
second slice beyond neutral descriptors and explicitly authorized promotion,
decay, schema discovery, and hooks. The working tree now implements those
features on structured command parameters. Read-side policy and real-provider
closure remain in the promoted scope but are not silently implied by this
authorization. The remaining order is:

1. [x] finalize public type-adaptation names and introduce neutral logical
   parameter descriptors;
2. [x] add write-side promotion, decay/loss, sanitized failure, and hook
   behavior;
3. [x] add provider profiles and lazy/preload schema discovery with refresh;
4. [x] validate package consumers and real SQL Server, SQLite, and Access
   providers; and
5. [ ] design and implement read-side temporal reporting/loss authorization,
   then close the remaining completion evidence.

#### Write-side type-adaptation reconciliation — 2026-08-27

- `Value`, `Set`, and structured `Where` can now carry exact semantic,
  promotion, and decay intent through `QueryModel` and `DatabaseQuery` without
  making `QueryBuilder` provider-aware.
- Gateway-instance policies implement disabled/default-explicit/schema-
  validated promotion, strict-or-fallback decay, and explicit mandatory-
  report authorization for potentially lossy rules.
- Known provider profiles resolve schema before dispatch. Cache entries are
  gateway-local, data-source/database/schema/table/column scoped,
  single-flight, preloadable, refreshable, and clearable. SQLite has a
  structured `PRAGMA table_info` fallback because its provider does not expose
  `GetSchema`.
- `OnTypeAdaptation` is synchronous, subscriber-isolated, value-free, and
  raised once per logical write adaptation. Local failures are sanitized and
  provider failures never trigger a second dispatch.
- Focused and full Data unit tests pass on both targets, including repeated
  positional parameters, subqueries, all promotion modes, strict/lossy
  behavior, hook isolation/redaction, schema single-flight/refresh/failure,
  unknown providers, SQLite metadata fallback, and no-retry guarantees.
- Compiled API baselines, package consumers, documentation, and real-provider
  evidence are reconciled below.

#### Write-side validation reconciliation — 2026-08-27

- Focused adaptation tests pass 32/32 on each target; the full Data suite passes
  169/169 on `net481` and 177/177 on `net9.0`. Both Data Release builds are
  warning-free, and both compiled API baselines match.
- The real SQL Server smoke passes on both targets. Its new adaptation check
  performs lazy schema discovery, promotes one `string` to the discovered
  `Int32` field, reports exactly one lossless `SchemaValidatedRule` hook, and
  confirms that the stored value is unchanged.
- FarmDatabase `--builder` runs exit 0 for all four target/provider pairs.
  SQLite resolves its `INTEGER` field to `Int64`; Access resolves the native
  OleDb type code to `Int32`. Both promote before dispatch and report the
  structural hook.
- Ordered decay candidates now preserve the preferred-alternative-string chain.
  Custom temporal string rules carry their .NET format, culture, and explicit
  loss classification; rejected alternatives and the selected formatter appear
  in one value-free hook report.
- Disposable package `1.1.0-local.4`, produced with `-AllowDirty`, passes the
  canonical full-solution build/test/pack gate and its external `net481` and
  `net9.0` package consumers, including the ordered formatter-chain API.
- The write-side slice is closed. The unchecked read-materialization item and
  execution step 5 remain the only work in this promoted item; they require a
  separate design/authorization decision and are not implied by this closure.

## No other promoted work

No other implementation is currently promoted. Items in
[`docs/proposals/`](docs/proposals/README.md), module findings or issues, audit
recommendations, and [`ROADMAP.md`](ROADMAP.md) intentions remain outside this
scheduler until their own formal promotion.
