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
concrete instance; operation availability does not depend on that choice.

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

The builder parameterizes values. Table names, column names, join expressions,
ordering, grouping, and raw condition templates remain trusted SQL fragments;
the module does not quote or validate caller-controlled identifiers. Empty
`IN` and `NOT IN`, statement reuse, subquery parameter isolation, and
unconstrained updates follow the fail-closed rules enforced by the builder.

OleDb binds by occurrence order, not parameter name. Automatic binding selects
the positional binder for OleDb and named binding elsewhere. Do not reorder
generated placeholders independently of their parameters.

## Reads and mapping

Buffered `Get*` methods return all rows. Callback `Read*` methods invoke a
synchronous callback while the reader and connection are open; keep callbacks
short and do not perform slow per-row I/O inside a transaction.

DTO mapping binds public writable properties case-insensitively and uses the
invariant conversion matrix. `DataMappingFailureMode.Strict` throws a
`DataMappingException` containing column/property/type evidence without the
source value. `Lenient` leaves the affected property unchanged and continues.

`RecordItem` deliberately stores invariant text and cannot preserve database
nulls, binary values, provider-specific precision, or every original type.
`DataMapper` is a compatibility bridge from that lossy representation to a
DTO; direct `GetDto`/`ReadDto` mapping retains more source fidelity.

Dynamic reads default to the AOT-safe Expando-backed `DynamicRow`. Reflection
Emit is explicit through `DynamicMode.IL`, process-wide, bounded by the first
configured schema limit, and not unloadable. `GetDynamicIlMetrics()` exposes a
passive snapshot of that cache; it does not reset or reconfigure it.

## Commands and sessions

String `Insert`, `Update`, and `Delete` methods all execute one ADO.NET
non-query and return the provider's affected-row count; the method name does not
parse or validate the SQL verb. Builder overloads accept statements produced by
`InsertInto` or `Update`.

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
redacted by default. A stream raises exactly one terminal outcome after cleanup:
`Completed`, `Failed`, `Cancelled`, or `DisposedBeforeCompletion`. Observer
failures are retained in a bounded snapshot and never replace the authoritative
database outcome.

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
surface is checked separately for both targets:

```powershell
.\eng\verify-public-api.ps1 -PackageId NekoLib.Data
dotnet test tests/NekoLib.Data.Tests/Unit/NekoLib.Data.Tests.Unit.csproj
```

The SQLite/Access and SQL Server runtime scenarios are independent provider
evidence under `runtime_tests/Data/`; they are not part of `dotnet test`.
