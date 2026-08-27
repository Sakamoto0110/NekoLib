# Data Type Adaptation and QueryBuilder API Review — 2026-08-26

**Document ID:** DATA-TYPE-ADAPTATION-QUERYBUILDER-API-REVIEW-20260826

**Schema version:** 1

**Kind:** audit

**Lifecycle:** historical

**Subject:** accepted NekoLib.Data type-adaptation policies and QueryBuilder public API normalization

**Surface:** audit

**Boundary:** data

**Authority role:** evidence

**Mutation:** snapshot

**Indexing:** include

**Status:** design accepted; implementation and validation complete

**Reference date:** 2026-08-26

**Reference commit:** `fc10319a439edc4943a1226fc66d0cf4ee2d2e2a`

**Last reconciliation:** 2026-08-27

**Current state:** `DATA-ADAPT-QB-001` complete; QueryBuilder, write adaptation, provider validation, and DTO temporal materialization reconciliations appear below

## Outcome

The owner accepted one coherent Data design with two related but independent
parts:

1. policy-controlled type adaptation at the gateway boundary; and
2. one canonical structured fluent convention for `QueryBuilder` values and
   predicates.

This record closes the owner-intent ambiguity around the NekoMarketplace
`P-001` proposal and accepts the `NOTE-012`/`NOTE-013` QueryBuilder correction
as design input. It does not claim that any new API, policy, schema cache,
provider profile, hook, compatibility attribute, test, package, or runtime
behavior exists at the reference commit.

The accepted work is promoted to Phase G3 of the live roadmap. Implementation
requires a separate authorization and must update current source, tests,
compiled API baselines, technical documentation, migration guidance, and the
changelog together.

## Baseline and evidence boundary

The review covers the `NekoLib.Data` source, tests, compiled-API policy, current
Data reference, and NekoMarketplace external-consumer intake as they existed in
the working tree at the reference commit. The worktree was clean before this
documentation change; `master` was two commits ahead of `origin/master` and not
behind it.

Current source confirms that:

- `QueryBuilder.Where(string, params object[])` accepts a trusted SQL condition
  template while `WhereIn`, `WhereNotIn`, `WhereBetween`, and `WhereLike` expose
  column/value-oriented shapes;
- `InsertInto` and `Update` accept dictionaries at statement creation time;
- `Join` accepts a trusted `ON` expression;
- logical QueryBuilder parameters are currently transported as
  `Dictionary<string, object?>`;
- gateway binders, not `QueryBuilder`, own named and positional physical
  parameter creation; and
- no promotion, decay, schema-discovery, provider-profile, or type-adaptation
  notification policy described below is implemented.

The source and compiled assemblies remain authoritative for current behavior.
The NekoMarketplace intake is consumer evidence; this document records the
accepted direction; [`TODO.md`](../../TODO.md) is the live implementation owner.

## Accepted terminology and boundaries

### Promotion

**Promotion** is an authorized conversion from a consumer-supplied value to the
requested semantic type before database dispatch. Examples include converting
the string `"54"` to an `Int32`, or a declared textual representation to a
temporal type.

Promotion is principally an input/write concern. An exact-type value is not a
promotion.

### Provider adaptation and decay

**Provider adaptation** selects a provider-compatible binding representation
for a semantic value. **Decay** is the fallback from the preferred
representation to another compatible representation when the preferred one is
unavailable. The term does not mean expiration, duration, scheduling, or cache
eviction.

Ordinary read materialization between provider and CLR representations is often
unavoidable. Lossy decay is not. Compatibility and loss classification are
independent decisions.

### Initial temporal scope

The first implementation covers `DateTime` and `DateTimeOffset` on both read
and write paths. A format such as `dd/MM` was an illustration of one formatter,
not the canonical storage contract. `DateOnly` and `TimeOnly` are excluded from
the first shared public surface because NekoLib.Data also targets `net481`.

The architecture may support registered numeric, Boolean, `Guid`, enum, and
additional temporal adapters without making every possible conversion an
automatic framework behavior.

## Accepted promotion policy

`DatabaseGatewayOptions` will expose a gateway-instance-scoped
`TypePromotionPolicy` with these values:

| Value | Contract |
|---|---|
| `Disabled` | Blocks every promotion, including a rule declared explicitly for a field or parameter. Exact-type values and provider adaptation remain available. |
| `ExplicitOnly` | Default. Allows promotion only when the logical field or parameter carries an exact registered rule. Schema metadata may validate the destination but does not authorize the conversion. |
| `SchemaValidated` | Opt-in. Also permits registered, proven-lossless promotion when schema metadata confirms the target and the provider profile confirms the binding. |

The policy applies only to promotion. It does not disable exact binding,
provider adaptation, decay, or read materialization.

An explicit promotion rule must identify at least:

- source semantic type;
- target semantic type;
- parser, formatter, or converter identity;
- culture or format strategy where relevant; and
- loss classification.

A Boolean `AllowPromotion` flag without a specific rule is insufficient. The
gateway must not use unrestricted `Convert.ChangeType` or culture-dependent
guessing as an authorization mechanism.

For example, an explicitly authorized `"54"` to `Int32` promotion parses and
range-checks locally, then binds the promoted value once. An invalid input such
as `"fifty-four"` fails before database dispatch with sanitized evidence.

## Accepted loss and fallback rules

The semantic rules are fixed even though the final public name of the loss
policy type remains an implementation-review detail:

- exact compatible representation is preferred;
- a registered lossless fallback may run only when the applicable promotion or
  decay policy permits it;
- a potentially lossy fallback is disabled by default;
- potentially lossy adaptation requires an explicit rule on the logical field
  or parameter and mandatory reporting through the adaptation hook;
- a strict gateway mode must be able to reject at the first incompatible
  preferred representation instead of following the fallback chain; and
- no lossy path may be silent, inferred solely from a provider exception, or
  globally enabled by the notification hook.

The implementation review must choose concise public names for the strict and
loss-authorization settings without weakening these rules. This naming decision
does not reopen the accepted safety behavior.

## Failure and dispatch semantics

Adaptation is resolved before the command is dispatched. The gateway may move
to the next registered fallback only for a recognized local incompatibility and
only when policy permits that fallback.

The database operation executes once. The gateway must not send one
representation, catch a provider/database error, and retry the same operation
with another representation. That pattern is unsafe for mutations and makes
the executed command ambiguous.

Final adaptation failures expose sanitized structural evidence such as source
and target types, provider identity, strategy identifier, loss class, and a
stable reason code. They must not expose input values, converted values, SQL,
parameters, connection strings, credentials, or unfiltered provider exception
messages. Inner exceptions must not be attached blindly when they may echo
sensitive values.

## Accepted adaptation hook

The gateway will expose an instance-scoped observational hook conceptually named
`OnTypeAdaptation`. It reports what the gateway did; it never grants permission
to do it.

The hook is synchronous, lightweight, and isolated. A hook failure must not
replace the database or adaptation outcome. One event is raised per logical
adaptation, not per physical provider-parameter occurrence.

The event may contain:

- direction (`read` or `write`);
- kind (`promotion` or `decay`);
- source and target semantic types;
- provider identity and selected binding representation;
- logical table/column/parameter identity where known;
- strategy and formatter identifiers;
- loss classification;
- stable reason code;
- bounded attempt summary; and
- correlation identifier.

It must not contain original or converted values, SQL text, parameter
collections, connection strings, credentials, raw inner-exception text, or
hashes of sensitive values.

## Provider profiles and schema discovery

A generic mapping such as `Int32 -> DbType.Int32` is not sufficient proof that
a provider accepts the representation. The implementation separates:

1. the consumer value;
2. the requested semantic target; and
3. the provider-specific binding representation.

Known and tested providers may have validated profiles backed by their actual
provider type vocabulary or enum. Unknown providers receive no automatic
binding guesses; the consumer must register an explicit strategy.

The accepted `SchemaDiscoveryMode` values are:

| Value | Contract |
|---|---|
| `Disabled` | Performs no automatic schema discovery. Exact and explicitly described operations may continue. |
| `Lazy` | Default. Loads the required provider/type or column metadata on first structured use before a transaction begins. |
| `Preload` | Explicitly loads the selected metadata before normal command use. |

Provider `DataTypes` metadata supplies the available provider vocabulary;
column schema supplies the concrete field target. Provider/type catalogs are
cached per gateway/provider, and column catalogs per database/schema/table.
Lazy discovery must be thread-safe and single-flight. Caches live with the
gateway and have an explicit refresh path for migrations.

The framework does not parse arbitrary raw SQL to infer tables or columns. A
raw-SQL caller supplies any required metadata explicitly.

If schema discovery is unavailable, exact and explicitly typed operations that
do not require schema-dependent promotion may continue. A schema-dependent
promotion fails locally without guessing and without database dispatch.

`Lazy` discovery is not authorization. Under the default `ExplicitOnly`
promotion policy, schema validates declared intent. Only the opt-in
`SchemaValidated` mode can use schema as one of the prerequisites for automatic
lossless promotion.

## Accepted QueryBuilder public API direction

`QueryBuilder` will have one canonical structured fluent convention. The public
surface will not permanently maintain both dictionary/template-first and
field/value-first forms as equal recommended APIs.

The accepted target shape is:

```csharp
builder
    .InsertInto("Inventory")
    .Value("Quantity", "54", parameter =>
        parameter.AllowPromotion(TypePromotions.StringToInt32));

builder
    .Update("Inventory")
    .Set("Quantity", 54)
    .Where("Id", QueryOperator.Equal, id);
```

The exact type names in the example are target API vocabulary, not a claim that
they exist at the reference commit.

The structured surface will provide:

- `InsertInto(table).Value(column, value)`;
- `Update(table).Set(column, value)`;
- structured `Where(column, operator, value)` predicates;
- parameter-configuration overloads for explicit promotion, logical type, and
  other neutral metadata; and
- a structured join path whose exact final spelling is selected during the API
  implementation review.

`WhereIn`, `WhereNotIn`, `WhereBetween`, and `WhereLike` already express
structured field/value intent and remain valid candidates for compatible
configuration overloads. This decision does not deprecate them.

Raw SQL remains a separate `DatabaseGateway` escape hatch. If a trusted SQL
fragment must remain inside `QueryBuilder`, it must use an explicitly named
trusted-fragment API rather than making an ordinary string indistinguishable
from a field name. The implementation review must finalize that spelling before
the old condition-template overload is marked obsolete.

## Separation of responsibilities

`QueryBuilder` records neutral logical parameter descriptors and provenance:
logical identity, table/column when known, value, semantic target, and an
explicit adaptation rule. It does not open schema, inspect provider enums,
create `DbCommand` or `DbParameter` objects, select binding mode, or raise the
adaptation hook.

`QueryModel` and `DatabaseQuery` transport the neutral descriptors. The gateway
resolves promotion and provider adaptation. The binder remains under the
gateway and creates physical provider parameters. `DbParameterSpec` remains the
raw-SQL and one-off explicit-binding surface.

This boundary keeps fluent call consistency without turning `QueryBuilder` into
a provider-aware gateway.

## Named and positional parameter contract

Named or positional binding is not exposed as a difference in the fluent API.
The builder creates logical parameter identities.

- A named binder creates the provider parameters required by the final SQL and
  resolves them by logical name.
- A positional binder uses final SQL occurrence order as the sole binding
  authority, rewrites placeholders to `?`, and creates one physical parameter
  for each occurrence.
- Repeated occurrences of one logical parameter may therefore produce multiple
  physical positional parameters.
- Call order, dictionary enumeration order, logical name, and rule-registration
  order are not positional binding authority.
- Promotion and adaptation execute once per logical parameter. The adaptation
  hook reports once even when a positional binder materializes that value more
  than once.
- Subquery rewriting must preserve the final-SQL ordering invariant already
  covered by QueryBuilder regressions.

Raw-SQL named/positional inconsistencies are a separate gateway review and must
not be hidden inside the QueryBuilder API change.

## Compatibility and deprecation decision

NekoLib `1.0.0` is already the stable baseline. The old QueryBuilder overloads
will therefore remain temporarily as compatibility shims. They will delegate to
the same logical model as the new fluent API and must not retain a second
behavioral implementation.

Each replaced overload is marked with a warning-only attribute after its
replacement exists:

```csharp
[Obsolete(
    "This overload is retained for compatibility and will be removed in the next major version. Use <replacement> instead.",
    error: false)]
```

The replacement name in the actual message must be concrete. `error: false` is
mandatory: existing consumers receive a compiler warning, not a compiler error.
The deprecated overloads remain available for at least one released minor
version and may be removed only in the next major version (`2.0.0` or later).
Changing the marker to `error: true` is removal-equivalent and is not accepted
by this decision.

The mandatory first deprecation set is:

| Current overload | Replacement direction |
|---|---|
| `InsertInto(string, Dictionary<string, object?>)` | `InsertInto(string).Value(...)` |
| `Update(string, Dictionary<string, object?>)` | `Update(string).Set(...)` |
| `Where(string, params object[])` | structured `Where(...)`, or the finalized explicitly trusted fragment API when structured predicates are insufficient |
| `Join(string, string, string)` | the finalized structured join API |

`Select`, `From`, `GroupBy`, `OrderBy`, `WhereIn`, `WhereNotIn`,
`WhereBetween`, and `WhereLike` are not made obsolete by this record. The
implementation review may identify another genuinely replaced overload, but it
must record the replacement and migration before adding an attribute.

The new APIs and compatibility shims must land atomically. Documentation must
recommend only the new convention once it is implemented.

## Rejected alternatives

The accepted design rejects:

- maintaining two equivalent public QueryBuilder conventions indefinitely;
- placing the provider binder, schema cache, provider enum discovery, or
  adaptation hook inside `QueryBuilder`;
- static/global adaptation authorization or notification state;
- treating schema discovery as promotion permission under `ExplicitOnly`;
- unrestricted conversion guessing;
- silently lossy fallback;
- including input values or database text in adaptation diagnostics;
- probing the database with one representation and retrying after dispatch;
- parsing arbitrary raw SQL to guess table/column metadata; and
- making old stable overloads fail compilation through `Obsolete(error: true)`.

## Implementation gate and required sequence

Implementation is not authorized by this record. When separately authorized,
the work should proceed in reviewable slices:

1. finalize public names and compiled API deltas, including the loss-policy,
   trusted-fragment, and structured-join spellings;
2. introduce neutral logical parameter descriptors without changing provider
   binding behavior;
3. add the structured `Value`, `Set`, and predicate APIs plus compatibility
   delegation;
4. add warning-only obsolete markers with concrete replacement messages;
5. add promotion rules, gateway policy, redacted failures, and the observational
   hook;
6. add provider profiles and lazy schema discovery;
7. validate every target and provider-binding path; and
8. update compiled API baselines, current Data reference, changelog, migration
   guide, package evidence, and the live roadmap closure.

No source implementation, public API baseline, module reference, migration
guide, or changelog is changed during this design-only promotion.

## Required validation

The implementation cannot close without evidence for:

- `net481` and `net9.0` builds and focused tests;
- named and OleDb positional binders;
- repeated logical parameters and final SQL occurrence order;
- subquery parameter rewriting and build idempotence;
- `Disabled`, default `ExplicitOnly`, and opt-in `SchemaValidated` promotion;
- exact input, successful explicit promotion, rejected input, overflow, and
  culture/format behavior;
- no database dispatch after a local adaptation failure;
- no retry after database dispatch;
- strict fallback rejection and explicit lossy authorization;
- hook redaction, isolation, and one-event-per-logical-adaptation behavior;
- lazy schema single-flight, cache refresh, and schema-unavailable behavior;
- known-provider profiles and fail-closed unknown-provider behavior;
- compiled public API baseline diffs and warning-only obsolete markers;
- migration and changelog accuracy;
- package-only consumer compilation using both the new and deprecated shapes;
  and
- real-provider evidence covering Access plus at least one named-parameter
  provider.

This review executed no build, test, compiled API, package, provider, runtime,
or release validation. Its evidence is design and current-source review only.

## Decision record

On 2026-08-26 the owner accepted the policy separation, default
`ExplicitOnly` promotion, lazy schema discovery, provider-aware binding,
sanitized adaptation reporting, structured QueryBuilder direction, neutral
logical/physical parameter split, and warning-only compatibility window
recorded above.

The unresolved items are limited to final public spelling where this record
explicitly says so. They do not authorize weaker conversion, loss, diagnostic,
dispatch, positional-ordering, or compatibility semantics.

## Implementation reconciliation — QueryBuilder slice, 2026-08-26

The owner subsequently authorized a QueryBuilder-only implementation slice.
The working tree at the same reference commit now selects the public names
`QueryOperator`, `QueryJoinType`, `WhereTrusted(...)`, `JoinOn(...)`, and
`JoinTrusted(...)` and adds the canonical `Value(...)`, `Set(...)`, and
structured `Where(...)` surface.

The former dictionary-based `InsertInto` and `Update`, condition-template
`Where`, and raw-expression `Join` overloads remain callable. Each now delegates
to the canonical implementation and carries a concrete warning-only
`ObsoleteAttribute` replacement message. No compatibility warning is an error.

Validation for this bounded slice established:

- clean `net481` and `net9.0` builds for `NekoLib.Data`;
- passing focused structured-query, compatibility, subquery, idempotence, named
  binding, and final-SQL positional-order regressions;
- a passing full solution test run with zero test failures;
- expected compiled API baseline additions for both Data target frameworks;
- successful builds of the repository-owned SQL Server and FarmDatabase Data
  runtime scenario projects after migration to the canonical surface;
- current Data reference, migration, changelog, and documentation-index
  registration; and
- a passing disposable package smoke run for `1.1.0-local.1` across `net481`
  and `net9.0`, compiling both canonical and deprecated calls.

The package was produced from the intentionally dirty implementation tree with
`-AllowDirty`; it is local evidence, not clean release provenance. No real
database provider was executed. Neutral adaptation descriptors, promotion,
provider representation decay, loss policy, sanitized failures, adaptation
hooks, provider profiles, schema discovery, and their provider/runtime evidence
remain unimplemented and separately gated in `TODO.md`.

## Implementation reconciliation — write-side adaptation slice, 2026-08-27

The owner expanded the next authorized slice to activate promotion, decay,
schema discovery, and hooks rather than stopping at neutral descriptors. The
working tree at the same reference commit now carries logical parameter
provenance through QueryBuilder, QueryModel, DatabaseQuery, gateway adaptation,
and final named or positional binding.

The implemented write-side contract selects `TypePromotionPolicy`,
`TypeDecayPolicy`, `TypeLossPolicy`, `SchemaDiscoveryMode`, explicit
`TypePromotionRule`/`TypeDecayRule` objects, sanitized
`TypeAdaptationException`, and the instance-scoped `OnTypeAdaptation` hook.
Adaptation finishes before one database dispatch. Potentially lossy paths need
both a field rule and `AllowExplicitAndReport`; strict decay stops at the first
incompatible preferred representation.

Schema metadata is cached per gateway and provider/data-source/database/schema/
table/column identity with lazy single-flight, explicit preload, selected
refresh, and complete clear operations. SQL Server and Access use ADO.NET
schema collections. The SQLite profile falls back to structured
`PRAGMA table_info` because `Microsoft.Data.Sqlite` does not implement
`DbConnection.GetSchema`. Raw SQL is not parsed or schema-promoted.

Dual-target unit evidence covers disabled/default-explicit/schema-validated
promotion, invalid input and overflow, strict and lossy decay, hook redaction
and isolation, no retry, named and repeated positional binding, subquery
descriptor rewriting, schema concurrency/cache/refresh/failure, unknown
providers, transaction timing, preload mode, and the SQLite metadata fallback.

This reconciliation did not initially claim real SQL Server, SQLite, or Access
execution, package evidence, or read-side temporal adaptation. The first two
gaps were closed by the validation reconciliation below. Read-side temporal
adaptation remains live Phase G3 work in `TODO.md`.

## Validation reconciliation — write-side adaptation, 2026-08-27

The final write-side implementation passes 32 focused adaptation tests on each
target and the full Data suite at 169 tests on `net481` and 177 on `net9.0`.
Both Release builds are warning-free, the compiled API baselines match, and the
repository documentation verifier passes.

Real-provider probes execute the schema-authorized write path rather than only
opening connections. SQL Server smoke passes on both targets with a dedicated
`String` to `Int32` lossless `SchemaValidatedRule` check. FarmDatabase passes
the same builder probe on both targets against SQLite and Access: SQLite maps
the declared `INTEGER` field to `Int64`, while Access maps the native OleDb
column type code to `Int32`. Every probe requires exactly one sanitized hook
and verifies that the stored value remains unchanged.

Disposable package `1.1.0-local.4`, built from the intentionally dirty working
tree with `-AllowDirty`, passes the canonical full-solution build, test, pack,
and external package-consumer gate for both target families. It is local
implementation evidence and not clean release provenance. This closes the
write-side slice only; DTO read materialization remains governed by
`DataMappingFailureMode` until the separately gated read-side temporal policy
is designed and authorized.

The owner then clarified that the terminal string representation is not one
canonical serialization: it is a selected formatter, with patterns such as
`yyyy/MM/dd HH:mm:ss:fff` being examples among many. The implementation now
preserves an ordered decay candidate list, permits a rejected native candidate
to fall through to a formatter-backed string rule, and reports all rejected
candidates plus the selected rule once. Formatter rules carry format, culture,
and loss classification; custom temporal formats default to potentially lossy,
while the built-in `"O"` rules remain the lossless round-trip choices.

## Implementation reconciliation — DTO temporal materialization, 2026-08-27

The owner then authorized completion of every remaining Data dependency before
commit and push. The only live item was the read-side temporal policy. Gateway
DTO reads now distinguish exact assignment, registered lossless temporal
materialization, and potentially lossy field-explicit conversion.

`GetDto`, `ReadDto`, and `StreamDto` share one policy. Round-trip `"O"` text and
UTC `DateTime` to `DateTimeOffset` are registered lossless rules and report one
sanitized `Read`/`Materialization` event whenever conversion occurs. A rule
bound by `ReadTypeAdaptationRule` to one DTO property is mandatory before a
potentially lossy conversion can run; the gateway must also select
`TypeLossPolicy.AllowExplicitAndReport`. `DataMappingFailureMode.Lenient` cannot
bypass a missing-rule or denied-loss decision.

Custom temporal parsers and formatters carry format, culture, and loss class.
They default to `PotentiallyLossy`; no global current-culture or broad temporal
parse is inferred. Failures remain value-free through
`DataMappingException.AdaptationFailure` and `TypeAdaptationException`.
The textual `RecordItem`/`DataMapper` compatibility bridge has no provider or
gateway hook and deliberately retains its legacy conversion boundary.

Focused DTO mapping tests pass 16/16 on `net9.0` and 13/13 on `net481`. The full
Data suite passes 186/186 on `net9.0` and 177/177 on `net481`; both Release
builds are warning-free and both compiled public API baselines match. This
closes `DATA-ADAPT-QB-001`; no Data implementation remains promoted in
`TODO.md`.

The real SQL Server smoke also exercises the read boundary through
`Microsoft.Data.SqlClient` 6.1.6 and SQL Server 16.0.4265.3. On both targets the
provider returned `datetime2` as `DateTime`; an explicit DTO-property rule plus
`AllowExplicitAndReport` materialized it as `DateTimeOffset` and emitted one
sanitized `Read` / `Materialization` / `ExplicitRule` / `PotentiallyLossy`
event. The complete smoke finished 31/31 on `net9.0` and 30/30 on `net481`,
where the sole streaming skip is the pre-existing target boundary. Each
temporary database was dropped and the adopted container returned to its prior
`exited` state.
