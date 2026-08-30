# NekoLib.Data

**Document ID:** DATA-REFERENCE

**Schema version:** 1

**Kind:** reference

**Lifecycle:** current

**Subject:** provider-neutral query construction, gateway composition, result shapes, sessions, ownership, observation, and target-specific streaming

**Surface:** technical-reference

**Boundary:** data

**Authority role:** normative

**Mutation:** authored

**Indexing:** include

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

### Writing a custom connection factory

Implement `IDbConnectionFactory` when construction needs a provider factory,
credential refresh, tenant routing, or another application-owned policy that a
public connection-string constructor cannot express:

```csharp
using System;
using System.Data.Common;
using System.Threading.Tasks;
using NekoLib.Data.Connection;

public sealed class ProviderConnectionFactory : IDbConnectionFactory
{
    private readonly DbProviderFactory _provider;
    private readonly string _connectionString;

    public ProviderConnectionFactory(
        DbProviderFactory provider,
        string connectionString)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _connectionString = connectionString
            ?? throw new ArgumentNullException(nameof(connectionString));
    }

    public Task<DbConnection> Create()
    {
        var connection = _provider.CreateConnection()
            ?? throw new InvalidOperationException("The provider returned no connection.");
        connection.ConnectionString = _connectionString;
        return Task.FromResult(connection);
    }

    public void Dispose()
    {
        // Dispose only resources owned by the factory itself.
    }
}
```

Every `Create()` call must return a **new, closed** connection. The gateway opens
and disposes each returned connection, except while a `DbSession` owns it. The
factory must not retain or later dispose those returned instances. `Create()` has
no cancellation token; keep it bounded and leave network opening to the gateway.
Choose `ContextOwned` when the context should dispose the factory, or `External`
when the composition root owns its lifetime.

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

### Provider composition and dialects

The shipped translators are deliberately small SQL-shaping components, not
complete providers:

| Provider family | Consumer composition | Built-in translation and binding |
|---|---|---|
| SQL Server | Supply a factory for the chosen `DbConnection` implementation, such as an application-owned `Microsoft.Data.SqlClient` factory, and configure credentials, encryption, pooling, retry, and server policy outside Data. | `SqlServerQueryTranslator` renders `TOP (n)` after `SELECT` or `SELECT DISTINCT`; ordinary binding is named. |
| SQLite | Supply the SQLite provider package and connection factory. File location, creation, pragmas, concurrency mode, and migration policy remain application concerns. | `SqliteQueryTranslator` appends `LIMIT n`; ordinary binding is named; structured schema discovery can use `PRAGMA table_info` because `Microsoft.Data.Sqlite` does not implement `GetSchema`. |
| Access/OleDb | Supply an OleDb connection factory and the correctly bitness-matched ACE/Jet installation and connection string. | `AccessQueryTranslator` renders `TOP n`, rewrites the supported `COUNT(DISTINCT ...)` shape through an aliased distinct subquery, and automatic binding is positional by placeholder occurrence. |

A translator does not prove that a provider engine, package version, operating
system, native driver, or deployment configuration is supported. A consumer may
pair a custom factory and translator for another ADO.NET provider, but owns its
compatibility tests, runtime prerequisites, dialect coverage, and failure policy.

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
[structured QueryBuilder migration guide](migrations/querybuilder-structured-api.md)
for replacements and the compatibility window.

OleDb binds by occurrence order, not parameter name. Automatic binding selects
the positional binder for OleDb and named binding elsewhere. Do not reorder
generated placeholders independently of their parameters.

### QueryBuilder statement lifecycle

`QueryBuilder` is a sealed, mutable statement object. `Select`,
`SelectDistinct`, `InsertInto`, `Update`, and `DeleteFrom` start a new statement
and clear the previous table, fragments, values, parameters, row limit, command
timeout, and all-row opt-ins. Configure and build one statement at a time; do
not mutate one builder concurrently.

`Build()` creates a new provider-neutral model without consuming or mutating the
builder's pending values. Repeated builds therefore produce the same SQL and
parameter identities. INSERT and UPDATE parameters are allocated in the build
copy, and subqueries are built independently before their placeholders are
renamed into the outer statement. A builder may be reused by starting another
statement, but a `QueryModel` or translated `DatabaseQuery` is a snapshot and is
not updated by later builder calls.

Extension methods may compose the public fluent operations and return the same
builder. Subclassing and overriding its state machine are not extension seams.
Every identifier or trusted fragment introduced by a helper retains the same
caller-trust responsibility as a direct call.

### Raw SQL and explicit parameter binding

Raw gateway overloads accept provider-specific SQL and an optional dictionary
whose values are either ordinary objects or `DbParameterSpec` instances. A
plain value sets only the provider value; `null` becomes `DBNull.Value`. A
specification may also set `DbType`, `Size`, `Precision`, `Scale`, and
`Direction`, and invalid size or direction metadata fails before dispatch.

`DatabaseGatewayOptions.ParameterBindingMode` may force named or positional
binding; `Automatic` selects positional binding only for OleDb commands. Named
binding creates one physical parameter per supplied logical name. Positional
binding tokenizes `@pN` markers outside literals, quoted identifiers, and SQL
comments, rewrites them to `?`, and creates one physical parameter per SQL
occurrence. Missing and unused supplied values are rejected before execution.
The consumer must keep raw SQL placeholders and dictionary keys consistent.

`DbCommandPolicy.TimeoutSeconds` is carried by a built query. A per-query value
overrides `DatabaseGatewayOptions.DefaultCommandTimeoutSeconds`; if both are
null, the provider default remains. Timeouts must be positive. Raw string
overloads do not take a `DbCommandPolicy`, so they use the context or provider
default.

Raw SQL deliberately bypasses the translator and structured schema-adaptation
path. `DbParameterSpec` is an explicit provider-binding escape hatch, not an
automatic promotion or loss-authorization request. It cannot make an untrusted
identifier or SQL fragment safe.

### Writing a custom query translator

`IDbQueryTranslator` is a synchronous SQL-shaping seam. It receives the
provider-neutral model produced by `QueryBuilder`; it must not open a connection,
execute SQL, or own retry policy. A minimal translator preserves logical
parameter metadata and command policy when it rewrites the SQL:

```csharp
using System;
using NekoLib.Data.Query;

public sealed class FetchFirstQueryTranslator : IDbQueryTranslator
{
    public DatabaseQuery Translate(QueryModel model)
    {
        if (model == null)
            throw new ArgumentNullException(nameof(model));

        var sql = model.Sql;
        if (model.Top.HasValue)
            sql += " FETCH FIRST " + model.Top.Value + " ROWS ONLY";

        return DatabaseQuery.FromLogicalParameters(
            sql,
            model.LogicalParameters,
            model.CommandPolicy);
    }
}
```

Use `DatabaseQuery.FromLogicalParameters(...)` for builder-generated queries.
Constructing a `DatabaseQuery` only from `QueryModel.Parameters` keeps values but
loses table/column provenance and explicit promotion/decay rules. A translator
may repeat an existing placeholder, but it must preserve each logical name and
keep placeholder occurrence order aligned with the parameters. This is required
for OleDb, which binds positionally. Renaming, adding, or removing generated
parameters is outside the normal translator contract and can turn structured
metadata into unscoped compatibility values.

Provider recognition considers the connection and translator types. An
unrecognized pair may still use exact or field-explicit type rules, but it cannot
authorize automatic schema-validated promotion or provider fallback. Raw string
operations bypass the translator entirely; only `QueryBuilder` operations raise
`OnSqlGenerated` after translation and before dispatch.

At minimum, test null rejection, row-limit placement, preservation of
`LogicalParameters` and `CommandPolicy`, repeated-placeholder behavior when the
dialect permits it, and both named and positional binding when applicable.

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
[type-adaptation migration guide](migrations/data-type-adaptation.md)
for policy selection and examples.

### Writing custom type-adaptation rules

`TypeValueConverter` is the executable seam behind `TypePromotionRule`,
`TypeDecayRule`, and `TypeMaterializationRule`. The delegate receives only one
non-null value: it receives no SQL, credentials, connection, schema, or ambient
context. Keep converters deterministic, use an explicit culture/format for text,
validate the declared source and target types, and classify loss honestly.
Thrown conversion or overflow failures are reported through the value-free
adaptation exception/event contract; a converter must not log the source value.

Register promotion/decay rules on the individual builder value, and register
read materialization rules in `DatabaseGatewayOptions` before concurrent use.
Custom rules do not bypass `TypePromotionPolicy`, `TypeDecayPolicy`, or
`TypeLossPolicy`.

The four rule surfaces remain distinct:

- `TypePromotionRule` converts a consumer input to its intended semantic type;
- `TypeDecayRule` selects a provider-compatible write representation when a
  known profile rejects the preferred semantic representation;
- `TypeMaterializationRule` describes one exact provider-value-to-property
  conversion; and
- `ReadTypeAdaptationRule` binds a materialization rule to a public writable DTO
  property, optionally narrowed to a column name.

`DatabaseGatewayOptions` retains its rule collections, so complete their
configuration before constructing or concurrently using gateways. A converter
is synchronous, receives no cancellation token, and runs inside the database
operation. Its exception fails that operation before write dispatch or during
row materialization; subscriber code cannot turn the failure into permission.

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

A `DynamicRow` owns the values materialized for that row and can outlive the
reader. Callback and streaming delegates, however, execute while the command,
reader, and owned connection remain live. The IL mode's generated CLR type is a
process-lifetime artifact; disposing a row, gateway, or context cannot unload
it. Use the Expando mode for AOT/trimming-sensitive deployments unless the
bounded compatibility mode is an explicit requirement.

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

`DbSession` does not serialize access to its connection or transaction. Treat
one session as a context-affine, single-operation-at-a-time scope; concurrent
commands or transaction mutations on the same session are unsupported and
provider-dependent. Independent non-session gateway calls use independent
factory-created connections and may run concurrently after the context options,
rules, and event subscriptions are fully configured.

Streaming occupies the session connection and transaction for the complete
enumeration. Consume promptly or materialize before slow downstream work.

## Cancellation, disposal, and events

Cancellation is propagated through connection open, command execution, reader
iteration, and stream enumeration where the provider supports those async
operations. A provider that rejects async operations falls back to blocking
calls only when `SynchronousFallbackMode.Enabled` is selected. Cancellation
cannot interrupt a blocking provider call after it begins.

Command timeout and cancellation are separate. The selected timeout is assigned
to `DbCommand.CommandTimeout`; the cancellation token is supplied to the
provider's asynchronous open/execute/read calls. Data does not synthesize a
retry, retry a mutation after timeout, or replace a provider exception with a
successful fallback representation.

Owned connections, commands, and readers are released on success,
cancellation, provider failure, callback failure, mapping failure, and early
stream disposal. Session-bound operations dispose commands/readers but leave
the session connection open.

Context events are synchronous, ordered, and subscriber-isolated. SQL is
redacted by default. Builder operations raise `OnSqlGenerated` after translation
and before `OnSqlDispatch`; raw string operations begin at `OnSqlDispatch`.
A stream raises exactly one `OnStreamTerminal` outcome after cleanup:
`Completed`, `Failed`, `Cancelled`, or `DisposedBeforeCompletion`. Observer
failures are retained in a bounded snapshot and never replace the authoritative
database outcome.

Subscriber callbacks run inline and add their latency to the database call.
Configure subscriptions before concurrent use and unsubscribe application-owned
handlers when their lifetime ends. Context disposal optionally clears all event
subscriptions, disposes only a context-owned factory, and clears retained
observer failures. The retained `DbQueryObserverFailure.Exception` is the
subscriber's own exception object; Data excludes SQL and results from the
failure record but cannot sanitize text the subscriber placed in its exception.

### Security and diagnostic evidence

Only values passed through structured conditions, assignments, or parameter
dictionaries are bound as provider parameters. Table and column names,
projections, ordering, grouping, raw SQL, and `WhereTrusted`/`JoinTrusted`
fragments are executable caller-controlled SQL. Validate or select identifiers
from application-owned allowlists; never concatenate untrusted input into those
surfaces.

SQL and success results are excluded from events by default. Enabling
`EmitRawSqlInEvents` or `IncludeCommandResultInSuccessEvents` transfers the
privacy, retention, and access-control responsibility to the consumer. Type-
adaptation evidence is value-free by contract. Data does not discover, store,
rotate, redact, or authorize credentials; configure audit sinks and exception
handling outside the module without logging connection strings or parameter
values.

## Extension boundary

The implementation seams and their consumer obligations are:

| Seam | Consumer supplies or registers | Ownership and timing | Input/output and failure contract |
|---|---|---|---|
| `IDbConnectionFactory` | A factory for the chosen provider, tenant, credential-refresh, or connection-creation policy. | The context disposes it only under `ContextOwned`; returned connections transfer to gateway/session ownership. Configure factory state before concurrent calls and make `Create()` safe for the composition's intended concurrency. | Each call returns a new closed `DbConnection`; creation has no cancellation token and must not open, retain, or later dispose the returned connection. Creation failure propagates; provider configuration, retry, credentials, and pooling remain external. |
| `IDbQueryTranslator` | A synchronous translator from provider-neutral `QueryModel` to `DatabaseQuery`. | The context retains but does not dispose it. Treat it as immutable/thread-safe after composition because concurrent builder operations may call it. | Preserve logical parameter identity, provenance, occurrence order, and `CommandPolicy`; return provider SQL without opening connections or executing. Translation failure stops before dispatch. Dialect correctness and raw-SQL paths remain consumer/provider responsibilities. |
| `DbParameterSpec` and raw dictionaries | Explicit value and optional provider-neutral type/size/precision/scale/direction metadata keyed by logical placeholder name. | The caller owns the dictionary/specification and must not mutate it while an operation binds it; Data creates and disposes the physical parameters with the command. | Binding validates metadata, maps null to `DBNull`, and applies named or occurrence-ordered positional rules. Raw binding bypasses translation and schema adaptation; placeholder correctness, SQL trust, and provider-specific compatibility remain with the caller. |
| `QueryBuilder` structured APIs | Trusted identifiers plus values supplied through `Value`, `Set`, structured `Where`, collection/range predicates, subqueries, and `JoinOn`. | One mutable builder belongs to its composing call chain; start a new statement to reuse it and do not mutate it concurrently. | Values become logical parameters; identifiers remain trusted SQL. Build-time validation fails closed for invalid state, empty collections, and unconstrained DML. The gateway owns cancellation only after execution begins. |
| `QueryBuilder` trusted APIs | SQL fragments through `WhereTrusted`, `JoinTrusted`, raw projections/grouping/ordering, or compatibility overloads. | Same builder lifecycle as structured APIs. | Only recognized placeholders are parameterized; the fragment's grammar and identifiers are not validated or escaped. The consumer owns injection prevention and provider dialect correctness. |
| `TypeValueConverter` in `TypePromotionRule` | An exact input-to-semantic conversion, registered on a logical builder value. | The rule is consumer-owned immutable configuration and should be registered before concurrent use; Data invokes it synchronously and does not dispose it. | The converter receives one non-null value and returns the declared target representation. Exceptions/overflow fail locally; promotion and loss policy still gate selection; no SQL, provider object, cancellation token, or ambient service is supplied. |
| `TypeValueConverter` in `TypeDecayRule` | An exact semantic-to-provider representation candidate, normally attached to one builder value or registered as a known-profile automatic fallback. | Configure ordered candidates before concurrent use. Each candidate converts from the original semantic value. | Strict decay rejects fallback; potentially lossy decay needs the exact logical rule plus global loss opt-in. Data never dispatches one candidate and retries the mutation with another. |
| `TypeValueConverter` in `TypeMaterializationRule` and `ReadTypeAdaptationRule` | A provider-value conversion and a DTO-property/optional-column binding. | Register automatic lossless rules or explicit property rules in options before concurrent use; mapping owns no consumer resource. | The target must match a public writable non-indexed property. Missing rules, conversion errors, and unauthorized loss remain failures even in lenient mapping; evidence excludes the source value. |
| Provider-specific factory/translator pair | A compatible `DbConnection` factory, translator, provider package, and explicit options for the selected engine. | The application owns package version, credentials, deployment, retries, and provider configuration; Data owns only connections/commands/readers it creates or a session retains. | Recognition enables known provider profiles and schema rules. An unknown pair may still use exact or field-explicit rules, but it cannot authorize automatic schema-based promotion or provider fallback. |

Options configure policy and events provide synchronous observation.
`IDatabaseGateway`, `IDqlGateway`, `IDmlGateway`, and the narrower interfaces are
capability views for consumers; they are not plug-in contracts used by
`DatabaseGateway` internally.

`DatabaseGateway`, `QueryExecutionContext`, `QueryBuilder`, `RecordItem`, and
the generic factory are sealed and are not inheritance contracts. The module has
no project reference to Core or any other NekoLib package.

## Experimental APIs

None. The current public surface is part of the stable contract under the
[public API and release policy](../../public-api-release-policy.md).

## Runtime and provider evidence boundary

Two versioned scenarios live under `runtime_tests/Data` and execute separately
from `dotnet test`:

- [FarmDatabase](../../../runtime_tests/Data/FarmDatabase/README.md) is a Windows
  x64 WinForms scenario for `net481` and `net9.0-windows`. SQLite needs only its
  restored packages; Access requires a matching x64 ACE OleDb installation. Its
  deterministic `--builder` matrix proves structured translation, named versus
  positional binding, schema discovery, and adaptation across the two file
  providers. The interactive workflow additionally covers application
  composition and transactions, but only the target/provider/steps named in its
  result table are evidence.
- [SQL Server](../../../runtime_tests/Data/SqlServer/README.md) is a Windows x64
  command-line scenario for `net481` and `net9.0`. It requires a container
  engine, the adopted pinned `nekolib-sqlserver` container, and the password
  environment variable described by its procedure. It proves the recorded
  provider/engine combination, pooling, read-shape parity, cancellation,
  transport loss, recovery, dynamic schema limits, and adaptation. Its
  specified-window recovery proof is `net9.0`; `net481` cannot execute the
  intentionally absent streaming fault.

Those records are exact historical evidence, not an assertion that every
provider version or deployment is supported. Neither scenario was executed as
part of the 2026-08-30 module-population record. The tracked legacy `Pods.db`
and `PodsDB` fixtures are not database coverage because no current test or
versioned scenario wires them.

## Migration and verification

The F1-DATA candidate correction is documented in the
[F1-DATA migration guide](migrations/f1.md). The compiled
surface is checked separately for both targets. QueryBuilder and type-
adaptation migration guidance are maintained independently so consumers can
adopt either additive surface deliberately:

```powershell
.\eng\verify-public-api.ps1 -PackageId NekoLib.Data
dotnet test tests/NekoLib.Data.Tests/Unit/NekoLib.Data.Tests.Unit.csproj
```

The SQLite/Access and SQL Server runtime scenarios are independent provider
evidence under `runtime_tests/Data/`; they are not part of `dotnet test`.
