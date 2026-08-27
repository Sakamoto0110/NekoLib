# NekoLib.Data

**Kind:** reference

**Lifecycle:** current

**Subject:** provider-neutral query construction, gateway composition, result
shapes, sessions, ownership, observation, and target-specific streaming

`NekoLib.Data` is an instance-scoped ADO.NET gateway for applications that need
one provider-neutral query and lifecycle contract across `net481` and `net9.0`.
It owns no global registry, connection string, credential, provider package, or
retry policy. The consumer composes those boundaries explicitly.

## Composition

The supported entry point is `NekoLib.Data.Gateway.DatabaseGateway`, normally
held through `IDatabaseGateway`:

```csharp
using NekoLib.Data.Connection;
using NekoLib.Data.Gateway;
using NekoLib.Data.Query;

var factory = new DbConnectionAbstractFactory<MyDbConnection>(connectionString);
using var context = new QueryExecutionContext(
    factory,
    new SqliteQueryTranslator());

IDatabaseGateway database = new DatabaseGateway(context);
```

`MyDbConnection` represents a consumer-supplied `DbConnection` type with a
public connection-string constructor. Applications with a provider-specific
creation policy should implement `IDbConnectionFactory` instead.

`QueryExecutionContext` owns:

- the connection factory when ownership is `ContextOwned`;
- the provider translator and gateway options;
- query lifecycle event subscriptions and bounded observer-failure retention;
- the affinity identity used to prevent a session from crossing contexts.

`DatabaseGateway` uses but does not dispose the context. Dispose the context
after every gateway and session that uses it. With
`DbConnectionFactoryOwnership.External`, the caller also retains responsibility
for disposing the factory.

## Public capability surface

`IDatabaseGateway` composes small capability interfaces:

| Capability | Contract |
|---|---|
| `IRawQueryGateway` | lossy `RecordItem` rows, buffered or synchronous callback processing, plus parameterized existence checks |
| `IDtoQueryGateway` | direct reader-to-DTO mapping with strict or explicit lenient conversion |
| `IDynamicQueryGateway` | `DynamicRow` results backed by Expando or bounded Reflection.Emit types |
| `IDqlStreamingGateway` | asynchronous raw, DTO, and dynamic streams on `net9.0` only |
| `IDmlGateway` | raw SQL and builder non-query execution with optional session participation |
| `ITclGateway` | context-affine session creation |

The concrete gateway exposes the same capability methods publicly. Consumers
may use either the composite interface, a narrower capability interface, or the
concrete instance for database operations. Schema-cache control and the
instance-scoped type-adaptation hook are concrete-gateway controls.

The former universal family is not supported. Select an explicit result shape:
`GetDto`/`ReadDto`, `GetDynamic`/`ReadDynamic`, `GetRaw`/`ReadRaw`, or the
matching `Stream*` method.

## Target matrix

| Contract | `net481` | `net9.0` |
|---|---|---|
| Buffered raw, DTO, and dynamic reads | yes | yes |
| Callback raw, DTO, and dynamic reads | yes | yes |
| `DbSession` and transactions | yes | yes |
| `IAsyncEnumerable<T>` streaming | absent | yes |
| DTO trimming metadata | unavailable | declared on public reflection entry points |

Streaming is not represented by an obsolete placeholder on `net481`; the
interface is absent from that assembly. Use callback reads there when rows must
be processed without buffering the whole result.

## Queries and translators

`QueryBuilder` produces a provider-neutral `QueryModel`. The translator owned
by the context converts it to a provider-specific `DatabaseQuery`. Built-in
translators cover SQL Server, SQLite, and Access/OleDb.

The canonical fluent convention is `InsertInto(table).Value(column, value)`,
`Update(table).Set(column, value)`, structured
`Where(column, QueryOperator, value)`, and `JoinOn(...)`. Use `WhereTrusted(...)`
or `JoinTrusted(...)` only when a structured call cannot represent the required
SQL. The former dictionary-based `InsertInto`/`Update`, condition-template
`Where`, and raw-expression `Join` overloads remain warning-only compatibility
shims until a future major release; current code should use the canonical APIs.

```csharp
var insert = new QueryBuilder()
    .InsertInto("Inventory")
    .Value("Sku", sku)
    .Value("Quantity", quantity);

var update = new QueryBuilder()
    .Update("Inventory")
    .Set("Quantity", quantity)
    .Where("Id", QueryOperator.Equal, id);
```

The builder parameterizes values. Table names, column names, projections,
ordering, grouping, and explicitly trusted fragments remain trusted SQL; the
module does not quote or validate caller-controlled identifiers. Empty `IN` and
`NOT IN`, statement reuse, subquery parameter isolation, and unconstrained
updates and deletes follow the fail-closed rules enforced by the builder.
`AllowAllRowsUpdate()` and `AllowAllRowsDelete()` both default to disabled,
apply only to the current statement, and are cleared on builder reuse. See the
[structured QueryBuilder migration guide](../../../docs/migrations/querybuilder-structured-api.md)
for replacements and the compatibility window.

OleDb binds by occurrence order, not parameter name. Automatic binding selects
the positional binder for OleDb and named binding elsewhere. Do not reorder
generated placeholders independently of their parameters.

## Write-side type adaptation

Builder parameters carry provider-neutral logical identity, table/column
provenance, semantic target, and optional exact adaptation rules. The builder
does not open a connection, discover schema, choose a provider representation,
or raise adaptation events. The gateway resolves each logical value once before
the binder creates named or positional provider parameters.

Promotion converts consumer input to its authorized semantic type. The default
`TypePromotionPolicy.ExplicitOnly` requires a rule on the individual value:

```csharp
var insert = new QueryBuilder()
    .InsertInto("Inventory")
    .Value("Quantity", "54", parameter =>
        parameter.AllowPromotion(TypePromotions.StringToInt32));

await database.Insert(insert, cancellationToken);
```

`Disabled` rejects even a field-explicit promotion. `SchemaValidated` may also
select a registered lossless rule when structured table/column metadata proves
the target type and a known provider profile confirms the binding. It never
uses unrestricted `Convert.ChangeType`, current-culture guessing, raw SQL
parsing, or a failed database call as authorization. Invalid text and overflow
fail locally through `TypeAdaptationException`; the command is not dispatched.

Provider adaptation first retains the exact semantic representation. A decay
rule is considered only when the known provider profile rejects that preferred
representation. `TypeDecayPolicy.Strict` stops there.
`AllowFallback` permits ordered registered candidates. Configure the primary
candidate with `AllowDecay(...)` and append later alternatives with
`AllowDecayFallback(...)`; every candidate converts from the original semantic
value. The gateway records rejected representations and emits one event for the
candidate finally selected.

Potentially lossy adaptation remains rejected unless the logical value
supplies the exact rule and `TypeLossPolicy.AllowExplicitAndReport` is enabled.
For example, converting `DateTimeOffset` to UTC `DateTime` discards the original
offset and therefore requires both opt-ins. The last candidate may be a string
formatter created with `CreateDateTimeToString(...)` or
`CreateDateTimeOffsetToString(...)`. There is no canonical presentation format:
the rule carries the exact .NET format, culture, and loss classification.
Custom formatters default to `PotentiallyLossy`; the built-in round-trip `"O"`
rules and the shipped `Guid` string rule are classified lossless.

Schema discovery is gateway-local and keyed by provider, data source/database,
schema, table, and column. `Lazy` is the default and performs one thread-safe,
single-flight load on first schema-dependent structured use. It will not begin
discovery after a session transaction has started. `Preload` requires the
consumer to load the selected columns first; `Disabled` performs no automatic
lookup. Migrations can refresh selected columns or clear the cache:

```csharp
await gateway.PreloadSchemaAsync(
    "dbo.Inventory",
    new[] { "Quantity", "OccurredAt" },
    cancellationToken);

await gateway.RefreshSchemaAsync(
    "dbo.Inventory",
    new[] { "Quantity" },
    cancellationToken);

gateway.ClearSchemaCache();
```

SQL Server and Access/OleDb use their ADO.NET schema collections. Because
`Microsoft.Data.Sqlite` does not implement `DbConnection.GetSchema`, the SQLite
profile falls back to `PRAGMA table_info` for the already-structured table
identity. Unknown providers may execute exact or field-explicit operations but
cannot authorize automatic schema-based promotion.

`OnTypeAdaptation` reports each completed logical write promotion/decay and
each DTO-property temporal materialization once, even when an OleDb placeholder
occurs multiple times physically. Events contain structural type, provider,
strategy, loss, reason, provenance, attempt, formatter/culture, property or
parameter identity, and correlation evidence; they never contain
input/converted values, SQL, parameter collections, connection strings,
credentials, or raw inner errors. Subscribers are synchronous and isolated and
cannot authorize an adaptation. Configure options and rule collections before
concurrent gateway use.

Raw-SQL dictionaries and `DbParameterSpec` remain explicit binding escape
hatches and do not trigger schema inference. The gateway never dispatches one
representation and retries a mutation with another. See the
[type-adaptation migration guide](../../../docs/migrations/data-type-adaptation.md)
for policy selection and examples.

## Reads and mapping

Buffered `Get*` methods return all rows. Callback `Read*` methods invoke a
synchronous callback while the reader and connection are open; keep callbacks
short and do not perform slow per-row I/O inside a transaction.

DTO mapping binds public writable properties case-insensitively and uses the
invariant conversion matrix. `DataMappingFailureMode.Strict` throws a
`DataMappingException` containing column/property/type evidence without the
source value. `Lenient` leaves the affected property unchanged and continues.

Temporal conversions are explicit policy surfaces. Lossless built-in rules
cover round-trip `"O"` text and UTC `DateTime` to `DateTimeOffset`; each actual
conversion raises a value-free `Read`/`Materialization` adaptation event.
Potentially lossy conversions require both a rule scoped to the DTO property
and `TypeLossPolicy.AllowExplicitAndReport`:

```csharp
var options = new DatabaseGatewayOptions
{
    TypeLossPolicy = TypeLossPolicy.AllowExplicitAndReport
};

options.ReadTypeAdaptationRules.Add(
    ReadTypeAdaptationRule.For<EventRow>(
        nameof(EventRow.OccurredAt),
        TypeMaterializations.DateTimeOffsetToUtcDateTime));
```

That rule preserves the instant but discards the original offset, so the event
is classified `PotentiallyLossy`. Custom textual formats use
`CreateStringToDateTime(...)`, `CreateStringToDateTimeOffset(...)`,
`CreateDateTimeToString(...)`, or `CreateDateTimeOffsetToString(...)`; their
default classification is also `PotentiallyLossy`. Missing authorization and
missing temporal rules fail through a value-free `TypeAdaptationException`
available as `DataMappingException.AdaptationFailure`.
`DataMappingFailureMode.Lenient` does not bypass either policy failure.

`RecordItem` deliberately stores invariant text and cannot preserve database
nulls, binary values, provider-specific precision, or every original type.
`DataMapper` is a compatibility bridge from that lossy representation to a
DTO; it does not have a gateway/provider hook and retains its legacy textual
conversion behavior. Direct `GetDto`, `ReadDto`, and `StreamDto` mapping retain
provider values and enforce the gateway read-adaptation policy.

Dynamic reads default to the AOT-safe Expando-backed `DynamicRow`. Reflection
Emit is explicit through `DynamicMode.IL`, process-wide, bounded by the first
configured schema limit, and not unloadable. `GetDynamicIlMetrics()` exposes a
passive snapshot of that cache; it does not reset or reconfigure it.

## Commands and sessions

String `Insert`, `Update`, and `Delete` methods all execute one ADO.NET
non-query and return the provider's affected-row count; the method name does not
parse or validate the SQL verb. Builder overloads accept statements produced by
`InsertInto`, `Update`, or `DeleteFrom`.

Prefer the builder overload for ordinary deletes so values are parameterized,
the generated SQL passes through the context translator, and
`OnSqlGenerated` is raised before dispatch:

```csharp
await database.Delete(
    new QueryBuilder()
        .DeleteFrom("Orders")
        .Where("Id", QueryOperator.Equal, orderId),
    cancellationToken);
```

An unconstrained delete fails during `Build()` unless the caller opts in at the
statement site with `AllowAllRowsDelete()`. The raw string overloads remain for
provider-specific SQL and compatibility; because their SQL is supplied rather
than generated, their event lifecycle begins at `OnSqlDispatch`.

Every string command has separate non-session and session overloads. The
session parameter precedes the cancellation token:

```csharp
await database.Update(sql, parameters, cancellationToken);

using var session = await database.OpenSessionAsync(cancellationToken);
session.BeginTransaction();
try
{
    await database.Update(sql, parameters, session, cancellationToken);
    session.Commit();
}
catch
{
    session.Rollback();
    throw;
}
```

A session owns its open connection and optional transaction. It binds to the
first context that uses it and is rejected by a different context before
command creation. Commit at nested depth zero commits the provider transaction;
rollback aborts the whole current transaction. A new transaction may begin
after commit or rollback.

Streaming occupies the session connection and transaction for the complete
enumeration. Consume promptly or materialize before slow downstream work.

## Cancellation, disposal, and events

Cancellation is propagated through connection open, command execution, reader
iteration, and stream enumeration where the provider supports those async
operations. A provider that rejects async operations falls back to blocking
calls only when `SynchronousFallbackMode.Enabled` is selected. Cancellation
cannot interrupt a blocking provider call after it begins.

Owned connections, commands, and readers are released on success,
cancellation, provider failure, callback failure, mapping failure, and early
stream disposal. Session-bound operations dispose commands/readers but leave
the session connection open.

Context events are synchronous, ordered, and subscriber-isolated. SQL is
redacted by default. Builder operations raise `OnSqlGenerated` after translation
and before `OnSqlDispatch`; raw string operations begin at `OnSqlDispatch`.
A stream raises exactly one terminal outcome after cleanup: `Completed`,
`Failed`, `Cancelled`, or `DisposedBeforeCompletion`. Observer failures are
retained in a bounded snapshot and never replace the authoritative database
outcome.

## Extension boundary

Extend Data through `IDbConnectionFactory`, `IDbQueryTranslator`, the capability
interfaces, options, and event subscriptions. `DatabaseGateway`,
`QueryExecutionContext`, `QueryBuilder`, `RecordItem`, and the generic factory
are sealed and are not inheritance contracts. The module has no project
reference to Core or any other NekoLib package.

## Experimental APIs

None. The current public surface is a stable-release candidate under the
[public API and release policy](../../../docs/public-api-release-policy.md).

## Migration and verification

The F1-DATA candidate correction is documented in the
[F1-DATA migration guide](../../../docs/migrations/f1-data.md). The compiled
surface is checked separately for both targets. QueryBuilder and type-
adaptation migration guidance are maintained independently so consumers can
adopt either additive surface deliberately:

```powershell
.\eng\verify-public-api.ps1 -PackageId NekoLib.Data
dotnet test tests/NekoLib.Data.Tests/Unit/NekoLib.Data.Tests.Unit.csproj
```

The SQLite/Access and SQL Server runtime scenarios are independent provider
evidence under `runtime_tests/Data/`; they are not part of `dotnet test`.
