# Data Module Audit - Pass 1

Date: 2026-05-29
Scope: `src/Data/NekoLib.Data`
Constraint: source code was read only. This file is the only intended audit artifact.

## Pass 1 Status

This pass focused on code comprehension plus first-order async, performance,
resource lifetime, and data leakage risks. It did not attempt fixes.

The module was found at `src/Data/NekoLib.Data`.

Generated outputs under `bin/` and `obj/` exist in the module tree and were
ignored for source analysis.

Verification run:

- `dotnet msbuild src\Data\NekoLib.Data\NekoLib.Data.csproj -getProperty:DefineConstants -p:TargetFramework=net481`
  returned `TRACE;NEKOLIB;NETFRAMEWORK;DEBUG`.
- `dotnet msbuild src\Data\NekoLib.Data\NekoLib.Data.csproj -getProperty:DefineConstants -p:TargetFramework=net9.0`
  returned `TRACE;NEKOLIB;NET_9;DEBUG`.
- `dotnet build src\Data\NekoLib.Data\NekoLib.Data.csproj --no-restore`
  succeeded for `net481` and `net9.0`, with 47 warnings and 0 errors.

## Intended Shape Of The Module

The project targets `net481;net9.0` in `NekoLib.Data.csproj:4`, with
`NET_9` added for `net9.0` and `NETFRAMEWORK` added for `net481`
(`NekoLib.Data.csproj:20-25`).

The core intent appears to be a lightweight provider-neutral SQL data gateway:

- `QueryBuilder` builds provider-neutral SQL plus parameters.
- `IDbQueryTranslator` turns the neutral model into provider-specific SQL.
- `SqlServerQueryTranslator` and `AccessQueryTranslator` mainly adapt `TOP`.
- `QueryExecutionContext` owns the connection factory, translator, options,
  and SQL lifecycle events.
- `DatabaseGateway` executes raw SQL and `QueryBuilder` SQL through partial
  class files split into raw, DTO, dynamic, and universal paths.
- Raw mode returns `Dictionary<string, RecordItem>`.
- DTO mode maps rows into public writable properties by reflection.
- Dynamic mode returns `DynamicRow`, backed either by `ExpandoObject` or by
  runtime-emitted IL types.
- `net9.0` gets the async-streaming paths behind `#if NET6_0_OR_GREATER`.
- `net481` has task-returning async methods, but depends heavily on ADO.NET
  providers supporting real async; otherwise it falls back to sync APIs.

## Confirmed Findings

### 1. `net481` Access/OleDb parameter ordering guard is not active

`ApplyParameters` only enables OleDb positional handling under `#if NET481`
(`Gateway/DatabaseGateway.Core.cs:193-231`). The project defines
`NETFRAMEWORK` for `net481`, not `NET481`, as verified by MSBuild. That means
`isAccess` becomes false for the `net481` target and OleDb commands receive
named parameters in dictionary enumeration order.

Impact: Access/OleDb ignores parameter names and binds positionally. Queries
can execute with values assigned to the wrong placeholders, especially after
dictionary merges or builder reuse. This is correctness-critical and can turn
filters into unintended data exposure.

### 2. Opening a connection can leak the newly created connection on failure

`OpenConnectionAsync` creates a connection, then attempts `OpenAsync`, falling
back to sync `Open` only for `NotSupportedException`
(`Gateway/DatabaseGateway.Core.cs:46-60`). If `OpenAsync` throws any other
exception, or if fallback `Open` throws, the newly created connection is not
disposed.

Impact: repeated login failures, bad connection strings, provider errors, or
cancelled opens can accumulate undisposed connection objects. This is a real
resource-lifetime issue because the caller never receives the connection.

### 3. Async APIs silently fall back to blocking sync database calls

`OpenConnectionAsync`, `ExecuteReaderSafeAsync`, `ExecuteNonQuerySafeAsync`,
and `ReadSafeAsync` catch `NotSupportedException` and call sync ADO.NET APIs
(`Gateway/DatabaseGateway.Core.cs:51-58`, `154-190`). In those fallback paths,
the cancellation token is no longer meaningful during the blocking provider
operation.

Impact: on `net481`, and on any provider with weak async support, methods that
look async can block thread-pool or UI threads and ignore cancellation while
opening, executing, or reading. This is a performance and responsiveness risk.

### 4. QueryBuilder accepts raw SQL identifiers and clauses without a trust boundary

`From`, `Join`, `Where`, `WhereIn`, `WhereBetween`, `GroupBy`, and `OrderBy`
concatenate caller-provided table names, column names, join expressions, and
conditions directly into SQL (`Query/QueryBuilder.cs:124-227`,
`260-267`, `337-379`, `411-434`). Values can be parameterized, but identifiers
and clauses are raw SQL.

Impact: the builder is only safe if these inputs are trusted compile-time
fragments. Passing user-controlled table names, column names, sort expressions,
or conditions is SQL injection. The current API shape does not document or
enforce that boundary.

### 5. Subquery parameter names collide and overwrite parent parameters

`WhereExists` and `WhereNotExists` build the subquery and copy its parameters
directly into the parent dictionary by key (`Query/QueryBuilder.cs:230-251`).
Both parent and subquery start parameter names at `@p1`.

Impact: a subquery can overwrite a parent parameter with the same name. The
generated SQL can then run with a different value from the one attached to the
visible parent condition. This can produce wrong results or expose rows outside
the intended filter.

### 6. INSERT/UPDATE query building mutates the builder every time `Build()` runs

`Build()` delegates to `BuildInsert` and `BuildUpdate`
(`Query/QueryBuilder.cs:312-334`). Those methods allocate new parameter names
and append to `_parameters` each time (`Query/QueryBuilder.cs:385-425`).

Impact: `Build()` is not idempotent for INSERT/UPDATE. Reusing a builder,
logging before execution, translating more than once, or using a subquery-like
flow can accumulate stale unused parameters and generate different SQL over
time. With OleDb positional binding, extra or reordered parameters are
especially dangerous.

### 7. Some public methods swallow database failures and return sentinel values

`GetRaw(QueryBuilder)` catches all exceptions, raises an error event, and then
returns `result`, which can be null despite the `Task<List<...>>` signature
(`Gateway/DatabaseGateway.raw_dto.cs:168-185`). `Insert(QueryBuilder)` and
`Update(QueryBuilder)` catch all exceptions and return `-1`
(`Gateway/DatabaseGateway.raw_dto.cs:209-247`). Callback exceptions in
`ReadRaw` and `ReadDynamic` are also swallowed after raising events
(`Gateway/DatabaseGateway.raw_dto.cs:116-120`,
`Gateway/DatabaseGateway.Dynamic.cs:334-341`).

Impact: callers can observe success-shaped task completion after failed
database work or failed row processing. This hides operational failures,
encourages partial processing, and can make transaction logic unsafe.

### 8. Telemetry events can leak raw SQL and full query results

`QueryExecutionContext` exposes events carrying raw SQL text and optional
result objects (`Query/QueryExecutionContext.cs:27-33`, `61-89`). The shared
`WithCommandAsync` raises `OnSuccess` with the full result returned by the work
delegate (`Gateway/DatabaseGateway.Core.cs:81-90`). `GetRaw(string, ...)`
returns the full row list from that delegate (`Gateway/DatabaseGateway.raw_dto.cs:45-80`),
so event subscribers can receive the entire raw result set.

Impact: diagnostics subscribers become a data exfiltration path. Even though
parameters are not separately included, raw SQL may contain literals, and raw
result objects can contain sensitive rows. The prototype mentions
`ConnectionStringMasked`, but the implemented event path has no masking policy.

### 9. QueryBuilder wrapper methods duplicate dispatch/success events

The QueryBuilder overloads raise dispatch and success around calls that also
run through `WithCommandAsync`, which raises dispatch and success again
(`Gateway/DatabaseGateway.raw_dto.cs:175-183`, `188-205`, `209-247`;
`Gateway/DatabaseGateway.Core.cs:85-90`).

Impact: telemetry consumers can double-count commands and timings. Error and
success sequences become hard to reason about, especially because some wrapper
methods swallow exceptions.

### 10. Streaming error events do not cover enumeration-time failures

The public streaming methods return `IAsyncEnumerable` inside a `try/catch`,
but the database open/read/map work happens later during enumeration
(`Gateway/DatabaseGateway.Dynamic.cs:361-419`,
`Gateway/DatabaseGateway.raw_dto.cs:384-455`,
`Gateway/DatabaseGateway.Universal.cs:167-277`). The public `try/catch` only
covers creation of the enumerable, not the deferred execution.

Impact: `OnError` is not raised for most streaming failures. Consumers get
exceptions from enumeration but diagnostics miss the error path.

### 11. Streaming is implemented only for `net9.0`, but public streaming interfaces are not conditional

Implementation streaming regions are behind `#if NET6_0_OR_GREATER`
(`Gateway/DatabaseGateway.Dynamic.cs:358-422`,
`Gateway/DatabaseGateway.raw_dto.cs:377-459`,
`Gateway/DatabaseGateway.Universal.cs:165-297`). The public
`IDqlStreamingGateway` interface is compiled unconditionally and exposes
`IAsyncEnumerable` methods to `net481` consumers
(`Gateway/IDqlStreamingGateway.cs:7-30`).

Impact: the module advertises a streaming contract for `net481` even though
`DatabaseGateway` has no corresponding streaming methods there. This is
probably intentional for `net9.0` advanced features, but the public contract
does not reflect that split.

### 12. Gateway interfaces do not match the actual `DatabaseGateway` implementation

`Gateway/IDatabaseGateway.cs` composes DQL, streaming, DML, and TCL interfaces
(`Gateway/IDatabaseGateway.cs:1-10`). Those interfaces include string-based
DTO/dynamic/universal reads, string streaming, `Delete`, and session-aware DML
(`Gateway/IDqlGateway.cs:8-35`, `Gateway/IDmlGateway.cs:8-27`,
`Gateway/IDqlStreamingGateway.cs:7-30`). The actual `DatabaseGateway` class
does not declare that it implements this interface
(`Gateway/DatabaseGateway.Core.cs:27`) and does not provide several of the
declared members.

Impact: the interface layer appears aspirational or stale. It is not a reliable
abstraction for consumers, and it obscures what API surface is supported on
`net481` versus `net9.0`.

### 13. Transaction session support exists but is mostly disconnected

`DbSession` supports nested transaction depth and rollback on dispose
(`DbSession.cs:23-70`). `DatabaseGateway.Core` has private `WithCommandAsync`
overloads that accept a `DbSession` (`Gateway/DatabaseGateway.Core.cs:102-151`).
The public DML methods do not accept a session, and the interface methods that
do accept sessions are not implemented by `DatabaseGateway`
(`Gateway/IDmlGateway.cs:8-27`).

Impact: the intended transaction path cannot be used through the current public
gateway methods. Callers can open a session, but normal Insert/Update paths
will create separate connections and operate outside that transaction.

### 14. `DbSession.Dispose` can fail before disposing the connection

`Dispose` calls `Rollback()` when a transaction is active, then disposes the
connection (`DbSession.cs:65-70`). If `Rollback()` or `Transaction.Dispose()`
throws, `Connection.Dispose()` is skipped.

Impact: a rollback failure during cleanup can leak the connection. Disposal
also has no disposed guard, so later `Commit`, `Rollback`, or `BeginTransaction`
can operate on disposed state.

### 15. DTO mapping has avoidable per-row reflection and column scans

`GetDto` and `ReadDto` fetch public properties and call `reader.HasColumn` for
each property on every row (`Gateway/DatabaseGateway.raw_dto.cs:286-314`,
`325-372`). `HasColumn` linearly scans all reader fields
(`Gateway/DatabaseGateway.Helpers.cs:17-30`). `DataMapper` also scans the row
dictionary for each property with `FirstOrDefault`
(`Mapping/DataMapper.cs:32-46`, `67-92`).

Impact: DTO mapping is roughly rows * properties * columns in the hot path.
Large result sets will spend significant time in reflection and repeated
string comparisons. This matters more for `net481` where other paths may also
fall back to sync I/O.

### 16. Mapping failures and type conversion failures are silently suppressed

`GetDto`, `ReadDto`, `DataMapper.Map`, and `FillDynamicObject` all swallow
conversion or property-set exceptions (`Gateway/DatabaseGateway.raw_dto.cs:306-313`,
`357-364`, `Mapping/DataMapper.cs:42-46`, `86-91`,
`Gateway/DatabaseGateway.Dynamic.cs:200-215`).

Impact: returned DTOs can contain default values with no signal to the caller.
For data access code, silent truncation or failed conversion is a correctness
and auditability problem.

### 17. Raw row materialization loses type/null information

Raw mode converts every column value to invariant-culture string values
(`Gateway/DatabaseGateway.raw_dto.cs:61-69`, `103-113`). Streaming raw mode
converts `DBNull` to empty string (`Gateway/DatabaseGateway.raw_dto.cs:432-441`).
`RecordItem` then parses back from strings (`RecordItem.cs:38-106`).

Impact: null versus empty string is lost, binary values become stringified,
and provider-specific values can lose precision or formatting. This is
probably a deliberate "raw display" design, but it should not be treated as a
lossless data model.

### 18. Duplicate result column names are not handled safely

`ExtractSchema` appends every schema column name to `Columns`, but only stores
the first type per name in `ColumnTypes` (`Gateway/DatabaseGateway.Helpers.cs:53-70`).
Raw dictionaries then overwrite duplicate keys, and IL dynamic type generation
attempts to define repeated property/field names from the column list
(`Gateway/DatabaseGateway.Dynamic.cs:120-159`).

Impact: joins that return duplicate column names can lose data in raw mode or
fail in IL dynamic mode. This is common in real SQL unless aliases are enforced.

### 19. Universal generic read does not use its translator type and fallback is not meaningful

`Get<TTranslator, T>` declares `TTranslator : IDbQueryTranslator, new()` but
does not instantiate or use it; it delegates to methods that use `ctx.Translator`
(`Gateway/DatabaseGateway.Universal.cs:30-61`). The generic constraint
`where T : new()` means the DTO path is always selected first, and the dynamic
fallback casts `DynamicRow` to `T`, which only works for `T` equal to
`DynamicRow` or `object`.

Impact: the method signature promises per-call translator selection and
universal fallback, but the implementation does neither reliably.

### 20. Access `TOP DISTINCT` translation appears wrong

`AccessQueryTranslator` transforms `SELECT DISTINCT ...` plus top into
`SELECT TOP n DISTINCT ...` (`Query/Translators.cs:47-75`). Access SQL normally
uses `SELECT DISTINCT TOP n ...`, matching the SQL Server branch's ordering
pattern more closely.

Impact: distinct top queries for Access may produce invalid SQL. This needs a
provider-backed test in the next pass.

### 21. `ClearEventsOnContextDispose` option is ignored

`DatabaseGatewayOptions.ClearEventsOnContextDispose` exists
(`DatabaseGatewayOptions.cs:51-54`), but `QueryExecutionContext.Dispose`
always clears all events regardless of the option
(`Query/QueryExecutionContext.cs:35-47`).

Impact: low risk for leaks because clearing events is usually desirable, but
the option currently misleads callers and tests.

## Build Warnings Worth Carrying Forward

The build succeeded for both targets, but warnings point to real nullable and
API-shape issues:

- `Gateway/DatabaseGateway.raw_dto.cs:176` and `:185`: `GetRaw(QueryBuilder)`
  can return null despite a non-nullable list return.
- `Query/QueryBuilder.cs:237`, `:250`, `:286`, `:301`, `:421`: nullable
  values are enumerated through `KeyValuePair<string, object>`.
- `Gateway/DatabaseGateway.Universal.cs:118`: `ilType` is assigned but unused.
- `Gateway/DatabaseGateway.Dynamic.cs` has several net9-only nullable warnings
  around dynamic type and conversion paths.

## Module Mental Model For Next Pass

Important invariants and assumptions discovered:

- `IDbConnectionFactory.Create()` is documented to return a new closed
  connection every time (`Connection/Factories.cs:13-23`).
- `DatabaseGateway` is effectively stateless except for its
  `QueryExecutionContext`.
- `QueryExecutionContext` owns the factory and translator and must be disposed
  by someone, but `DatabaseGateway` itself is not disposable.
- `QueryBuilder` is mutable, accumulates parameters, and is not thread-safe.
- `QueryBuilder.Build()` is safe to call repeatedly for SELECT, but not for
  INSERT/UPDATE.
- Raw and DTO non-streaming reads materialize full result sets into memory.
- Callback reads reduce list allocation, but callbacks are synchronous Actions
  and exceptions are swallowed in some paths.
- Streaming is the only pull-based low-memory read path, and it is net9-only.
- Dynamic `Expando` is the default. IL dynamic mode exists but emits non-
  unloadable runtime types and is guarded by options/AOT checks.
- Telemetry events are synchronous event invocations on the query execution
  path. Slow subscribers will slow database calls, and throwing subscribers can
  affect execution because event invocation is not isolated.

## Suggested Next Pass

Next pass should verify behavior with focused tests or small harnesses, still
inside the Data module:

- Prove the OleDb parameter-order issue under `net481` with an Access/OleDb
  command or a fake command that records parameter order.
- Prove `QueryBuilder.Build()` mutation for INSERT/UPDATE.
- Prove `WhereExists` parameter collision.
- Prove streaming errors skip `OnError`.
- Prove `GetRaw(QueryBuilder)` returns null on failure.
- Check whether `AccessQueryTranslator` output is accepted by the intended
  Access provider.
- Decide whether public interfaces are intended contracts or old prototypes.
- Decide whether raw SQL/result telemetry should be masked, disabled by
  default, or documented as sensitive.
