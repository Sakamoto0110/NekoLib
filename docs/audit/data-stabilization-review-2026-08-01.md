# NekoLib.Data Stabilization Review — 2026-08-01

**Kind:** audit

**Lifecycle:** historical

**Subject:** code-first review of query construction, execution, mapping,
streaming, provider boundaries, sessions, transactions, and validation gaps

**Reference date:** 2026-08-01

**Reference commit:** `628442a58cdf2e2374cc7e48fa10d394d3fc3b87`

**Last reconciliation:** 2026-08-01

**Current state:** accepted implementation work is authoritative in
[`TODO.md`](../../TODO.md) Phase E1; real-provider validation is authoritative
in Phase E4

**Reviewed baseline:** the `NekoLib.Data` product and test files at `HEAD`. The
working tree already contained an unrelated modification to `TODO.md`; that file
was not changed during the initial review. The later promotion turn deliberately
updated only the Data roadmap sections and reconciled this evidence file.

**Authority:** this file preserves evidence, proposed alternatives, provider
research, and the historical reconciliation. `TODO.md` is the sole authoritative
active-work list. Product code was not changed by the review or promotion turns.

This review began as the temporary root artifact `DATA_TODO.md`. Once its
confirmed findings received accepted directions, the executable work was
promoted to the live roadmap and this evidence was preserved as a historical
snapshot. Later implementation outcomes belong in a dated reconciliation;
rejected alternatives and obsolete findings remain historical rather than
returning to the live roadmap.

## Promotion reconciliation — 2026-08-01

| Review findings | Authoritative disposition |
|---|---|
| DATA-001, DATA-008, DATA-009, DATA-010, DATA-020 | Promoted to `TODO.md` E1.1 with fail-closed and fail-fast query-builder directions. |
| DATA-002, DATA-011 | Promoted to E1.2 with synchronous ordered subscriber isolation and exactly one stream terminal outcome. |
| DATA-004, DATA-005, DATA-006 | Promoted to E1.3 with strict mapping, no invalid dynamic fallback, and one typed mapping pipeline. |
| DATA-003, DATA-007, DATA-015 | Promoted to E1.4 as current positional-binding, fallback-policy, DML, and transaction stabilization. |
| DATA-013 | Portable parameter metadata and command timeout promoted to E1.4; a provider-native hook is conditional on a concrete E4 provider gap. |
| DATA-014 | Factory ownership and session state/affinity promoted to E1.4; data-source-style and cancellable create/open expansion is conditional on E4 evidence. |
| DATA-012 | Promoted to E1.5 with Expando as the default and bounded IL behavior pending measurement. |
| DATA-018, DATA-019 | Promoted to E1.6 for fake-provider contract tests and source hygiene; real providers are owned by E4. |
| DATA-016 | Deferred to F1 because namespace and public-surface cleanup require a breaking-change policy. |
| DATA-017 | Existing lossy compatibility contract accepted; no new lossless model was promoted. |
| MongoDB and other non-relational stores | Not promoted as NekoLib modules; remain application-owned native integrations during Phase E. |

The detailed sections below are the evidence and rationale for those decisions,
not an additional implementation checklist.

---

## Review outcome

`NekoLib.Data` has a small, understandable ADO.NET foundation, but it is not yet
safe to describe as provider-neutral or production-confident. The most important
current risks are correctness risks, not missing database brands:

- an empty `WhereIn`/`WhereNotIn` silently removes the predicate and can turn an
  `UPDATE` into a full-table operation;
- an event subscriber can change a successful database operation into an
  apparent failure, mask the original provider exception, or prevent dispatch;
- OleDb parameters are ordered by generated parameter number rather than by
  placeholder occurrence in the SQL text;
- typed mapping silently suppresses conversion and property-assignment failures;
- the advertised universal fallback passes or casts `DynamicRow` as unrelated
  DTO types and therefore fails at runtime;
- nominally equivalent typed APIs do not share one mapping pipeline and can
  return different values for the same row.

Provider expansion should begin only after the fail-open predicate, observer,
parameter-binding, and mapping contracts have accepted directions. Otherwise,
every additional provider multiplies ambiguous behavior and test combinations.

---

## Evidence and validation completed

The review covered all tracked files under `src/Data/NekoLib.Data`, all current
Data unit tests, the Data project files, and the historical
[`data-first-pass.md`](data-first-pass.md) after the current code had
been inspected. The historical audit was used only as a reconciliation list.

Current project facts:

- `NekoLib.Data` targets `net481` and `net9.0` with nullable reference types
  enabled and implicit usings disabled;
- it has no project references, including no reference to `NekoLib.Core`;
- its only package dependency is `Microsoft.Bcl.AsyncInterfaces` on `net481`;
- its current translators adapt only row-limit syntax (`TOP` or `LIMIT`); they
  are not complete SQL dialect or provider adapters;
- the only automated Data tests cover `QueryBuilder` and the three translators;
  no current test executes `DatabaseGateway`, mapping, sessions, streaming,
  events, or a real database provider;
- `tests/NekoLib.Data.Tests/Shared/Pods.db` and `PodsDB` are not referenced by
  current source or tests and are not evidence of executed database coverage.

Commands executed successfully on Windows:

```powershell
dotnet build src\Data\NekoLib.Data\NekoLib.Data.csproj --no-restore
dotnet test tests\NekoLib.Data.Tests\Unit\NekoLib.Data.Tests.Unit.csproj --no-restore
dotnet msbuild src\Data\NekoLib.Data\NekoLib.Data.csproj -getProperty:DefineConstants -p:TargetFramework=net481
dotnet msbuild src\Data\NekoLib.Data\NekoLib.Data.csproj -getProperty:DefineConstants -p:TargetFramework=net9.0
```

Results:

- both library targets built with zero warnings and zero errors;
- 23/23 Data tests passed on `net481` and 23/23 passed on `net9.0`;
- the constants were `NEKOLIB;NETFRAMEWORK` for `net481` and
  `NEKOLIB;NET_9` for `net9.0`, in addition to configuration constants.

Read-only executable probes against the built `net481` assembly produced:

```text
EMPTY_IN_DML_SQL=UPDATE Accounts SET Status = @p1
REUSED_SELECT_SQL=SELECT DISTINCT Id, Name FROM Customers
SUBQUERY_TOP=SELECT * FROM Customers WHERE EXISTS (SELECT Id FROM Orders)
IMPLICIT_SELECT=SELECT * FROM Accounts
SQL=SELECT * FROM T WHERE B = @p2 AND A = @p1
BOUND_ORDER=Parameter1=11,Parameter2=22
OBSERVER_EXCEPTION=escaped:...observer failure
```

No real Access, SQL Server, SQLite, PostgreSQL, MySQL, Oracle, Firebird, Db2,
ODBC, or MongoDB server was used. Provider behavior beyond the in-memory OleDb
command probe remains a validation gap.

---

## Classification used below

- **Confirmed:** directly present in current code and, where practical,
  reproduced by a build-time or executable probe.
- **Contract gap:** current behavior is observable, but the intended public
  contract has not been decided.
- **Expansion blocker:** not necessarily a current regression, but must be
  resolved before claiming broad provider support.
- **Proposed direction:** a review recommendation, not an accepted design.

Priorities describe risk if the affected API is used; they do not authorize an
implementation order by themselves.

---

## P0 — fail-open query construction

### DATA-001 — Empty collection predicates are silently omitted

**Status:** confirmed. **Priority:** P0 for data-changing statements.

**Evidence:** `WhereIn` and `WhereNotIn` return without adding a condition when
the supplied collection is empty
(`src/Data/NekoLib.Data/Query/QueryBuilder.cs:174-214`). An executable probe
produced `UPDATE Accounts SET Status = @p1` from an update followed by an empty
`WhereIn("Id", values)`.

**Impact:** an application that expects an empty set to match no rows can update
every row. `WhereNotIn` has the same fail-open implementation even though its
mathematical empty-set result is different. The current API also silently ignores
a null collection or empty column name, hiding caller mistakes.

**Proposed direction:** represent predicates structurally and define explicit
constant-predicate semantics. At minimum, empty `IN` must become false rather
than disappear. Independently add a DML safeguard that rejects an `UPDATE`
without a predicate unless the caller uses an explicit, conspicuous
`AllowAllRows()`-style opt-in. Decide whether empty `NOT IN` becomes true or is
rejected; do not let it implicitly remove the safety boundary.

**Acceptance evidence required:** dual-target regression tests for empty, null,
single, and multiple values in both predicates; an update-specific regression;
and an explicit all-rows update test.

---

## P1 — operation and data correctness

### DATA-002 — Event subscribers can alter database outcomes

**Status:** confirmed. **Priority:** P1.

**Evidence:** `QueryExecutionContext` invokes `OnSqlDispatch`, `OnSuccess`, and
`OnError` directly without subscriber isolation
(`src/Data/NekoLib.Data/Query/QueryExecutionContext.cs:28-38`). The gateway calls
`RaiseSuccess` inside the same `try` that executes the command, then catches the
subscriber exception and calls `RaiseError`
(`src/Data/NekoLib.Data/Gateway/DatabaseGateway.Core.cs:86-107` and
`:117-159`). A probe confirmed that a throwing `OnSuccess` handler escapes.

**Impact:** a committed insert or update can be reported as failed, encouraging a
duplicate retry. A throwing error subscriber can mask the original provider
exception. A slow subscriber also extends command latency, and a dispatch
subscriber can prevent the command from running.

**Proposed direction:** define these events as best-effort observations that
cannot affect the operation result. Invoke subscribers individually, isolate
their exceptions, and expose observer failures through a separate non-recursive
diagnostic callback or returned snapshot. Keep the current SQL/result redaction
defaults. Do not add a Core or Inspection dependency while the broader rollout
remains frozen.

**Acceptance evidence required:** tests with multiple subscribers where one
throws during generated, dispatch, success, and error notifications; tests
proving the original provider result/exception remains authoritative; and an
explicit decision about synchronous versus queued notification delivery.

### DATA-003 — OleDb binding does not follow SQL placeholder order

**Status:** confirmed. **Priority:** P1 for Access/OleDb.

**Evidence:** `ApplyParameters` recognizes OleDb but sorts generated names such
as `@p1` and `@p2` numerically before adding them
(`src/Data/NekoLib.Data/Gateway/DatabaseGateway.Core.cs:212-250`). OleDb binds by
position. For SQL containing `B = @p2 AND A = @p1`, the probe bound values in
`@p1`, `@p2` order, not SQL occurrence order.

**Impact:** raw SQL whose placeholder order differs from generated-name order can
write or compare the wrong values without a provider error. Repeated placeholders
also need repeated positional values after conversion to `?`; a dictionary alone
cannot represent that binding plan.

**Proposed direction:** move marker rendering and parameter binding into a small
provider adapter. For positional providers, tokenize recognized placeholders in
SQL occurrence order, render the provider marker, and create one parameter per
occurrence. Reject missing parameters, decide how unused values are handled, and
avoid parsing inside comments and string literals.

**Acceptance evidence required:** binder unit tests for reversed, repeated,
missing, unused, prefix-colliding, quoted, and commented placeholders, followed
by a real Access/OleDb runtime scenario for both queries and DML.

### DATA-004 — Mapping failures are silently suppressed

**Status:** confirmed. **Priority:** P1.

**Evidence:** direct DTO mapping catches and ignores each assignment failure
(`src/Data/NekoLib.Data/Gateway/DatabaseGateway.Helpers.cs:144-163`).
`DataMapper.Map` and `MapInto` do the same, while `ConvertValue` converts failures
to `null` or default values
(`src/Data/NekoLib.Data/Mapping/DataMapper.cs:14-150`). Dynamic IL mapping also
suppresses conversion and property-set errors
(`src/Data/NekoLib.Data/Gateway/DatabaseGateway.Dynamic.cs:189-215`).

**Impact:** a query can succeed while returning a partially initialized object.
Missing business identifiers, amounts, or flags are indistinguishable from real
database defaults. The caller receives no column, property, source type, target
type, or raw-value evidence.

**Proposed direction:** introduce an explicit mapping failure policy and a single
conversion service. New typed APIs should fail with a structured mapping
exception by default; a compatibility mode may retain lenient behavior only when
explicitly requested. The conversion matrix should deliberately cover nullability,
identity conversion, enums, `Guid`, binary values, date/time shapes, numeric
overflow, and provider-specific values.

**Acceptance evidence required:** a provider-independent fake reader matrix plus
at least SQLite and one server provider, verifying successful conversions,
nullability, overflow, unsupported conversions, and actionable exception data.

### DATA-005 — The universal typed fallback is not type-safe

**Status:** confirmed from current control flow. **Priority:** P1.

**Evidence:** `ReadUniversalDispatch` treats a target without a parameterless
constructor as dynamic, then calls the original typed delegate with a
`DynamicRow` (`src/Data/NekoLib.Data/Gateway/DatabaseGateway.Universal.cs:135-169`).
`StreamDataCore<T>` performs the equivalent cast from `DynamicRow` to `T`
(`:247-318`). This can work for `object` or `DynamicRow`, which are already
handled explicitly, but not for an unrelated DTO type.

**Impact:** the documented fallback moves an unsupported target from a clear
contract error to a late `DynamicInvoke` argument mismatch or
`InvalidCastException` after query execution has started.

**Proposed direction:** remove the implicit cross-type fallback. Keep separate,
strongly named DTO, `DynamicRow`, and raw APIs. Typed DTO methods should validate
the construction/mapping contract before opening a connection. If constructor
mapping is later desired, implement it as a real mapper feature rather than a
dynamic fallback.

**Acceptance evidence required:** tests for DTOs with and without parameterless
constructors, `object`, `DynamicRow`, wrong delegate arity/type, and failures that
occur before any provider resource is acquired.

### DATA-006 — Typed read paths do not have one semantic mapping contract

**Status:** confirmed. **Priority:** P1.

**Evidence:** DTO APIs map values directly from `DbDataReader` through
`CreateDtoFromReader`, while universal DTO paths first convert the row to
`Dictionary<string, RecordItem>` and then call `DataMapper`
(`DatabaseGateway.Helpers.cs:126-176`, `DatabaseGateway.raw_dto.cs:250-371`, and
`DatabaseGateway.Universal.cs:135-171,247-318`). `RecordItem` converts values to
invariant strings and maps `DBNull` to an empty string.

**Impact:** the same SQL row and target DTO can produce different null, binary,
date/time, enum, and numeric behavior depending on which public method was used.
Streaming and buffered calls therefore cannot be assumed equivalent.

**Proposed direction:** compile one reader-to-object binding plan per schema and
target type and reuse it for buffered, callback, and streaming APIs. Keep the
lossy `RecordItem` path separate and clearly named. Add parity tests across every
typed entry point.

---

## P2 — contract and robustness findings

### DATA-007 — Async APIs can silently execute synchronously

**Status:** confirmed. **Priority:** P2, elevated for UI and unattended hosts.

**Evidence:** connection open, command execution, and reader advancement catch
`NotSupportedException` and call synchronous ADO.NET methods
(`src/Data/NekoLib.Data/Gateway/DatabaseGateway.Core.cs:49-80,173-210`).

**Impact:** an API named `Async` can block the calling thread for an unbounded
provider call. Cancellation cannot interrupt that synchronous portion, and the
behavior varies silently by provider.

**Proposed direction:** make provider async capability and synchronous fallback
an explicit policy. Prefer native async when supported; otherwise reject the
operation or require an opt-in appropriate to the application. Check cancellation
before a permitted fallback and document that in-flight cancellation is not
guaranteed. Do not use `Task.Run` as a false cancellation guarantee.

### DATA-008 — QueryBuilder is reusable but does not reset query-mode state

**Status:** confirmed and reproduced. **Priority:** P2.

**Evidence:** query type defaults to the first enum value (`Select`), select
columns accumulate, and select/count/distinct flags are not reset when another
mode method is called (`src/Data/NekoLib.Data/Query/QueryBuilder.cs:14-48,78-123`).
A reused builder produced `SELECT DISTINCT Id, Name FROM Customers`; calling only
`From("Accounts")` on a new builder produced `SELECT * FROM Accounts` even though
the `Build` error says a query mode must be selected.

**Impact:** reuse carries columns, predicates, joins, ordering, `Top`, and mode
flags into later statements. The API neither enforces single use nor defines
reset semantics.

**Proposed direction:** choose one contract: make the builder single-use and
reject mutation after `Build`, or implement explicit immutable query states.
Introduce an undefined initial query kind and validate incompatible transitions.
Avoid a partial reset that leaves other lists or flags behind.

### DATA-009 — Template placeholder arity is not validated

**Status:** confirmed. **Priority:** P2.

**Evidence:** `Where` replaces `@p1`, `@p2`, and so on only for supplied values,
then adds a parameter for every value regardless of whether its placeholder was
present (`src/Data/NekoLib.Data/Query/QueryBuilder.cs:150-171`). Missing values
leave tokens unresolved; extra values create unused parameters.

**Impact:** failures occur later and differ by provider. Regex replacement also
does not establish a safe grammar for string literals, comments, or raw clauses.

**Proposed direction:** split structured predicates from an explicitly named raw
condition template. Validate exact placeholder arity and tokenize only the
supported grammar. Preserve the existing warning that identifiers and clauses
are trusted SQL, not values made safe by parameterization.

### DATA-010 — A subquery loses its row-limit metadata

**Status:** confirmed and reproduced. **Priority:** P2.

**Evidence:** `AddSubQueryCondition` embeds `subQuery.Build().Sql` but discards
the `QueryModel.Top` value (`src/Data/NekoLib.Data/Query/QueryBuilder.cs:252-265`).
A subquery with `Top(1)` produced `EXISTS (SELECT Id FROM Orders)`.

**Impact:** the fluent API accepts a limit that is silently ignored, potentially
changing cost and behavior. A nested query cannot be translated correctly by
the outer provider because only its pre-translation SQL string is retained.

**Proposed direction:** retain nested query models/AST nodes until provider
translation. As a smaller safe interim step, reject `Top` inside a subquery with
an actionable exception rather than silently dropping it.

### DATA-011 — Early stream disposal has no terminal outcome

**Status:** confirmed from iterator control flow. **Priority:** P2.

**Evidence:** DTO, raw, dynamic, and universal streams call `RaiseSuccess` only
after full enumeration. Disposing after an early `break` runs resource cleanup
but emits neither success, failure, cancellation, nor abandonment. The dynamic
empty-schema path also reaches `yield break` before success
(`DatabaseGateway.raw_dto.cs:413-543`, `DatabaseGateway.Dynamic.cs:418-483`, and
`DatabaseGateway.Universal.cs:247-322`).

**Impact:** query lifecycle evidence is incomplete precisely for partial reads,
which are normal for streaming APIs. Consumers cannot distinguish an abandoned
stream from an operation that never started.

**Proposed direction:** define terminal outcomes such as completed, failed,
cancelled, and disposed-before-completion, and emit exactly one from `finally`.
Keep this local to Data until any broader telemetry/Inspection decision is
explicitly unfrozen.

### DATA-012 — Dynamic IL mode has divergent null and lifetime behavior

**Status:** confirmed. **Priority:** P2.

**Evidence:** emitted properties use the reader field type and skip assignment
for `DBNull`, leaving value types at their CLR default, while Expando mode stores
`null` (`src/Data/NekoLib.Data/Gateway/DatabaseGateway.Dynamic.cs:189-215,249-294`).
The runtime assembly uses `AssemblyBuilderAccess.Run`; emitted types are not
unloadable. The process-global cache can evict signatures, but eviction does not
reclaim emitted types (`:20-187`). Per-context options reconfigure that global
limit.

**Impact:** changing dynamic mode changes row semantics. The cache bounds known
signatures, not lifetime emitted types; global limit changes can evict and later
re-emit schemas. This matters in long-running kiosk processes with schema churn.

**Proposed direction:** keep Expando as the production default. Decide whether IL
mode provides measured value. If retained, make null semantics equivalent, use a
one-way process cap without re-emission after eviction, expose measurements, and
document ownership as process-global rather than per context.

### DATA-013 — Parameters and commands lack provider metadata and policy hooks

**Status:** expansion blocker. **Priority:** P2.

**Evidence:** queries carry only `Dictionary<string, object?>`; the binder sets
only `ParameterName` and `Value`. Commands are always `CommandType.Text`, and
`DatabaseGatewayOptions` has no command timeout, parameter metadata, or provider
configuration hook (`DatabaseGateway.Core.cs:86-168,212-250`,
`Query/DatabaseQuery.cs:12-28`, and `DatabaseGatewayOptions.cs:28-69`).

**Impact:** callers cannot express `DbType`, size, precision, scale, direction,
provider type, or a provider-native data type name. Type inference is not enough
for all SQLite, PostgreSQL, SQL Server, Oracle, or output-parameter cases. There
is also no library-level command timeout policy.

**Proposed direction:** add a provider-neutral `DbParameterSpec` for portable
metadata and a provider adapter hook for native metadata. Let the adapter render
markers, bind parameters, and configure commands. Keep provider packages out of
the core Data project. Add an explicit default command timeout with per-command
override and a clear `null`/provider-default meaning.

### DATA-014 — Factory and session ownership is narrower than modern providers

**Status:** contract gap and expansion blocker. **Priority:** P2.

**Evidence:** `IDbConnectionFactory.Create()` is cancellation-free and requires
a new closed connection; the generic implementation uses a string constructor
(`src/Data/NekoLib.Data/Connection/Factories.cs:12-50`).
`QueryExecutionContext.Dispose` always disposes the factory
(`QueryExecutionContext.cs:45-59`). `DbSession` publicly accepts any
`DbConnection` without null, state, or ownership validation (`DbSession.cs:11-23`).

**Impact:** sharing a pooled data-source/factory across contexts can cause
premature disposal. Modern providers may prefer a long-lived data source that
opens connections asynchronously. An externally constructed closed session can
fail much later with an unclear error.

**Proposed direction:** specify ownership explicitly. Support a factory/data
source that can open with cancellation and declare whether the context owns it.
Validate session connection state and affinity before command creation. Preserve
the current simple generic factory as a compatibility adapter, not the universal
provider model.

### DATA-015 — Transaction and DML surfaces are asymmetric

**Status:** confirmed contract gap. **Priority:** P2.

**Evidence:** string DML methods on `IDmlGateway` accept an optional `DbSession`,
but their implementations are explicit interface methods
(`Gateway/IDmlGateway.cs:8-27` and `DatabaseGateway.Interface.cs:21-46`). The
concrete QueryBuilder `Insert` and `Update` methods do not offer session overloads
(`DatabaseGateway.raw_dto.cs:201-223`), and delete is not a symmetric concrete
surface. `DbSession` uses synchronous begin/commit/rollback/dispose only and
permanently records rollback state (`DbSession.cs:25-128`).

**Impact:** discoverability and transaction participation depend on whether the
caller holds the interface or concrete type. A caller can accidentally execute
builder DML outside the intended transaction. Modern provider async transaction
capabilities are unavailable.

**Proposed direction:** define one intentional DML/session surface before adding
providers. Give equivalent operations equivalent session overloads, validate
connection affinity, and decide whether a session is a one-transaction unit of
work or may start a new transaction after commit/rollback. Add async transaction
members only with a dual-target compatibility design.

### DATA-016 — Public API organization is internally named and uneven

**Status:** confirmed contract gap. **Priority:** P2.

**Evidence:** the public `DatabaseGateway` type is in
`NekoLib.Data.Internal.Gateway`, while its interfaces live in
`NekoLib.Data.Gateway`. Buffered, callback, streaming, raw, DTO, dynamic, and
universal methods have different concrete/interface visibility and session
coverage across the gateway partials.

**Impact:** the main public type looks internal, IntelliSense does not present one
coherent capability surface, and future providers would amplify overload count
and compatibility burden.

**Proposed direction:** inventory the public API and choose the supported entry
points before a namespace or overload cleanup. Prefer small capability interfaces
and explicit method families. Treat any namespace move or removal as a deliberate
breaking change; do not add compatibility wrappers without a release policy.

### DATA-017 — RecordItem is intentionally lossy but exposed as “raw” data

**Status:** confirmed contract gap. **Priority:** P2.

**Evidence:** `ReadRecordRow` converts `DBNull` to an empty string and every other
value through invariant `Convert.ToString`; `RecordItem` stores only the type name
and string value and returns sentinel defaults on failed conversion
(`DatabaseGateway.Helpers.cs:126-141` and `RecordItem.cs:8-171`).

**Impact:** null versus empty, binary values, exact provider types, and some
precision/format distinctions cannot be reconstructed. The behavior is documented
in the type, so changing it in place would be a compatibility break.

**Proposed direction:** retain `RecordItem` as an explicitly lossy display/export
model. Add a separately named lossless row/cell representation with `object?`,
ordinal, provider field type, and null state when a real use case requires it.
Do not silently redefine the existing “raw” contract.

### DATA-018 — Provider confidence is currently translator-only

**Status:** confirmed validation gap. **Priority:** P2.

**Evidence:** only 23 QueryBuilder/translator tests exist per target. Translators
only rewrite row limits (`src/Data/NekoLib.Data/Query/Translators.cs:7-98`). No
test exercises a real provider, gateway lifetime, session, event, mapper, or
stream. The tracked SQLite-looking fixture is unused.

**Impact:** passing tests establish SQL-string behavior, not provider correctness,
resource lifetime, cancellation, transaction behavior, or dual-target package
compatibility.

**Proposed direction:** first add provider-independent contract tests with fake
ADO.NET objects, then a deliberate real-provider matrix. SQLite is the practical
local baseline; Access/OleDb is necessary for the existing positional branch; one
server provider should exercise pooling, cancellation, and network failures.
Record real provider/package/server versions and distinguish automated, local,
and machine-dependent results.

### DATA-019 — Tracked Data source contains obsolete and non-English material

**Status:** confirmed hygiene finding. **Priority:** P3.

**Evidence:** `src/Data/NekoLib.Data/Connection/DbSession.cs` is a fully commented
obsolete duplicate. Public XML documentation and comments throughout Data remain
in Portuguese, contrary to the repository rule that versioned code and
documentation are written in English.

**Impact:** dead alternatives create false authority, and mixed-language public
documentation makes generated API docs inconsistent. This is not a runtime defect.

**Proposed direction:** delete the commented duplicate after confirming there is
no historical dependency, translate public documentation while touching the
affected API, and keep cleanup separate from behavioral changes so reviews remain
clear.

### DATA-020 — Raw identifiers and clauses remain a caller trust boundary

**Status:** confirmed and already documented in principle. **Priority:** P2 when
untrusted input is possible.

**Evidence:** table names, selected columns, joins, conditions, grouping, and
ordering are concatenated into SQL. Only supplied values are parameterized
(`src/Data/NekoLib.Data/Query/QueryBuilder.cs:78-287,355-435`).

**Impact:** parameterization does not make identifiers or arbitrary clauses safe.
Passing user-controlled strings to these positions is SQL injection.

**Proposed direction:** keep raw SQL capability but name the boundary explicitly.
Provide validated/quoted identifier helpers per provider for common structured
operations and reserve conspicuous `Raw*` members for trusted fragments. Do not
claim that a single cross-dialect quoting rule is portable.

---

## Provider-expansion architecture

### Relational providers

The current ADO.NET shape can support more relational databases, but a
“translator” cannot remain only a `TOP`/`LIMIT` rewrite. A small provider adapter
should own only the differences that the current generic layer cannot express:

- parameter marker rendering and binding order;
- portable and native parameter metadata;
- identifier quoting and row-limit/pagination rendering;
- connection/data-source creation and async capability policy;
- command configuration, timeout defaults, generated-key/returning behavior,
  and provider-specific exception classification where a real use case exists.

The Data core should continue to reference only ADO.NET contracts. Provider
packages belong in consumer applications, test fixtures, or optional adapters
that are created only after a concrete need is accepted.

### Deliberate provider matrix

The following list is a planning matrix, not a support claim. Every row requires
package restore/build probes for both NekoLib targets and real execution before it
can be marked supported.

| Provider | Typical NekoLib use | Proposed priority | Required work before a support claim |
|---|---|---:|---|
| Microsoft SQL Server / Azure SQL via `Microsoft.Data.SqlClient` | Common central database for Windows business applications | High | Replace legacy assumptions, validate both TFMs, parameter metadata, pooling, cancellation, transactions, encryption defaults, and network failure behavior. Microsoft's current provider supports .NET Framework 4.6.2+ and modern .NET; pin and probe the selected version. |
| SQLite via `Microsoft.Data.Sqlite` or a deliberately selected alternative | Local/offline kiosk database and deterministic integration baseline | High | Choose native deployment strategy, validate parameter types and concurrency, wire a real temporary database, and stop treating the unused fixture as coverage. |
| PostgreSQL via Npgsql | Common open-source server database | High | Support `NpgsqlDataSource`, positional `$1` markers, explicit PostgreSQL types, cancellation, pooling, and dual-target package selection. |
| MySQL / MariaDB via Oracle Connector/NET or MySqlConnector | Frequently deployed business/server database | High | Choose one driver deliberately, validate MariaDB compatibility separately, and test parameter, transaction, async, TLS, and both-TFM behavior. |
| Microsoft Access via OleDb | Existing legacy desktop/PDV database | High for compatibility | Fix positional binding, test the installed provider architecture/RID, and run real `.mdb`/`.accdb` query and transaction scenarios. |
| Firebird via `FirebirdSql.Data.FirebirdClient` | Embedded/legacy commercial and POS deployments | Medium | Verify current package targets, embedded versus server modes, parameter syntax, transactions, native deployment, and both TFMs. |
| Oracle Database via ODP.NET | Enterprise deployments when demanded | Demand-driven | Select the correct managed Framework/Core packages, validate target and native requirements, parameter directions/types, transactions, and Oracle-specific returning semantics. |
| IBM Db2 / Informix via IBM Data Server Provider | Enterprise/IBM environments | Demand-driven | Treat Framework and Core packages/platforms separately; verify licensing, deployment, architecture, package targets, and provider-native behavior. |
| ODBC via `System.Data.Odbc` | Compatibility escape hatch for otherwise unsupported databases | Low, compatibility-only | Use a positional binder, document driver-manager/native dependencies, test each actual ODBC driver, and avoid calling one generic ODBC pass a database support claim. |

Official planning references, checked on 2026-08-01:

- [Microsoft.Data.SqlClient introduction and target support](https://learn.microsoft.com/en-us/sql/connect/ado-net/introduction-microsoft-data-sqlclient-namespace?view=sql-server-ver17)
- [Microsoft.Data.Sqlite overview](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/)
- [Npgsql basic usage, data sources, placeholders, and parameter types](https://www.npgsql.org/doc/basic-usage.html)
- [MySQL Connector/NET version requirements](https://dev.mysql.com/doc/connector-net/en/connector-net-versions.html)
- [Firebird ADO.NET provider](https://www.firebirdsql.org/en/net-provider/)
- [Oracle ODP.NET overview](https://docs.oracle.com/en/database/oracle/oracle-database/26/odpnt/intro002.html)
- [IBM Data Server Provider for .NET](https://www.ibm.com/docs/en/db2/11.1.0?topic=adonet-data-server-provider-net)
- [System.Data.Odbc API](https://learn.microsoft.com/en-us/dotnet/api/system.data.odbc)

Package versions and compatibility tables are mutable. Recheck them at the time
of an implementation decision rather than copying today's version into a durable
contract.

---

## MongoDB integration assessment

### Architecture decision

MongoDB should not be implemented as another `IDbQueryTranslator`. The official
driver uses `MongoClient`, `IMongoDatabase`, `IMongoCollection<T>`, filters,
updates, BSON/POCO serializers, and `IClientSession`; these are not
`DbConnection`, `DbCommand`, SQL, or positional/named SQL parameters.

Forcing MongoDB through `QueryBuilder` would either discard native capabilities
or create a misleading lowest-common-denominator API. It would also make SQL
terms such as table, join, group clause, and `TOP` pretend to be portable when
their semantics are not.

**Proposed direction:** keep the relational core unchanged and choose one of two
small options after Phase E:

1. **Application-owned driver integration (preferred until reuse is proven).**
   Applications reference `MongoDB.Driver`, create and reuse `MongoClient`, and
   expose native collections directly. NekoLib adds nothing.
2. **Optional sibling adapter, only after a repeated use case exists.** A small
   `NekoLib.Data.MongoDB` project may own client/database configuration and
   session lifetime while deliberately exposing native driver filters, updates,
   collections, and sessions. It must not reference or emulate the SQL
   `QueryBuilder`.

If the optional adapter is accepted, its minimum shape should remain small:

- accept or create one reusable `IMongoClient` with explicit ownership;
- provide a database context that returns `IMongoCollection<T>` rather than a
  repository framework;
- wrap `IClientSessionHandle` only where ownership or transaction cleanup adds
  value, preserving native transaction options and cancellation;
- let the MongoDB driver own BSON/POCO serialization rather than routing through
  `RecordItem` or the relational `DataMapper`;
- do not add Microsoft DI, a generic host, a provider registry, or a universal
  cross-database query language.

### Future MongoDB admission criteria

These are decision criteria, not active roadmap tasks. Reassess them only after
a later explicit use-case decision:

- confirm at least two concrete NekoLib consumers need shared MongoDB lifecycle
  code; otherwise keep integration application-owned;
- select an exact `MongoDB.Driver` version only after NuGet restore, build, and
  package-consumer probes pass for `net481` and `net9.0`; compatibility is
  version-sensitive and must not be inferred;
- record the supported MongoDB Server matrix and Stable API decision;
- validate reuse and ownership of `MongoClient`; do not create one client per
  operation or session;
- validate CRUD, cancellation, selection/connect timeouts, offline recovery,
  authentication/TLS, serialization failure, duplicate key errors, and
  server/network interruption;
- validate sessions and transactions against a topology that actually supports
  them, and preserve the rule that a session belongs to its creating client;
- decide whether transaction retries remain entirely driver-native; do not
  introduce a second generic retry policy without evidence;
- keep MongoDB validation separate from relational provider claims.

Official planning references, checked on 2026-08-01:

- [MongoDB .NET/C# driver getting started](https://www.mongodb.com/docs/drivers/csharp/current/get-started/)
- [MongoDB client-library compatibility tables](https://www.mongodb.com/docs/drivers/compatibility/)
- [MongoDB .NET/C# sessions and transactions](https://www.mongodb.com/docs/drivers/csharp/current/crud/transactions/)
- [MongoDB driver upgrade guides](https://www.mongodb.com/docs/drivers/csharp/current/reference/upgrade/)

### Other non-relational technologies

Redis, LiteDB, Elasticsearch/OpenSearch, and specialized time-series/vector
databases are frequently useful, but they are not interchangeable database
providers for this SQL gateway. Treat Redis as a cache/key-value capability,
search engines as search/index capabilities, and embedded document stores as
their own native integrations. Add one only for a proven application use case;
do not grow a generic provider registry in anticipation.

---

## Promotion decision history

The following decisions produced the authoritative E1 work in `TODO.md`. This
section records rationale only and does not own implementation status.

### D0 — Stop fail-open behavior

- Accepted constant-predicate semantics for empty `IN` and `NOT IN`.
- Accepted an explicit full-table DML safeguard.
- Promoted DATA-001 and required regression tests to E1.1.

### D1 — Make operation results and mapped data authoritative

- Accepted synchronous ordered subscriber isolation for DATA-002.
- Accepted strict-by-default mapping with explicit compatibility behavior for
  DATA-004.
- Accepted removal of the invalid universal fallback in DATA-005.
- Accepted one canonical reader-to-DTO path for DATA-006.
- Promoted only those confirmed corrections to E1.2 and E1.3.

### D2 — Define the provider seam

- Accepted marker/binder responsibilities and the OleDb correction in DATA-003.
- Accepted portable parameter metadata, command timeout, and narrow provider
  hooks from DATA-013.
- Accepted explicit factory/data-source ownership and async capability policy
  from DATA-007 and DATA-014.
- Accepted non-breaking DML/session parity from DATA-015; deferred broad public
  API cleanup from DATA-016 to F1.
- Retained the no-Core-reference boundary.

### D3 — Build confidence before breadth

- Promoted fake-provider contract tests to E1.6.
- Selected SQLite and Access/OleDb as the initial real-provider evidence in E4.
- Limited the initial server-provider choice to one actual consumer-driven
  option from SQL Server, PostgreSQL, or MySQL.
- Required exact package, target, architecture, and server version evidence.

### D4 — Reassess MongoDB after Phase E

- Kept MongoDB application-owned during Phase E.
- Rejected treating MongoDB as a SQL translator or starting a sibling project
  from this review alone.
- A future accepted adapter requires a separate roadmap item and package/server
  compatibility evidence.

---

## Historical Data audit reconciliation

The table below prevents old findings from being silently promoted as current
work. Numbers refer to `docs/audit/data-first-pass.md`.

| Historical item | Current disposition on 2026-08-01 |
|---:|---|
| #1 | The `NETFRAMEWORK` OleDb branch now compiles. The distinct occurrence-order defect is current DATA-003. |
| #2 | Connection-open failure cleanup is implemented; no current leak was confirmed in that path. |
| #3 | Confirmed as DATA-007. |
| #4 | The trusted raw-fragment boundary remains and is recorded as DATA-020. |
| #5 | Subquery parameter collision was fixed and has unit coverage. |
| #6 | INSERT/UPDATE build idempotence was fixed and has unit coverage. |
| #7 | Open-session failure cleanup is implemented. |
| #8 | Event payload exposure is mitigated by secure defaults. Subscriber behavior is a different current issue, DATA-002. |
| #9 | Buffered success events now occur after reader/resource completion in the reviewed paths. |
| #10 | Stream setup/read exceptions are reported; early disposal remains DATA-011. |
| #11 | Dynamic schema cardinality is bounded by options, but IL semantic/lifetime concerns remain DATA-012. |
| #12 | The class implements `IDatabaseGateway`; public surface organization remains DATA-016. |
| #13 | Many session overloads exist, but builder DML parity remains DATA-015. |
| #14 | Session and non-session command paths are consolidated. |
| #15 | Direct DTO binding reduced reflection work, but silent and divergent mapping remains DATA-004/DATA-006. |
| #16 | Confirmed as low-priority DATA-019. |
| #17 | The lossy model is documented; the naming/contract decision remains DATA-017. |
| #18 | Duplicate dynamic columns receive unique names in current schema extraction. |
| #19 | Generic translation now uses the requested translator; the invalid fallback remains DATA-005. |
| #20 | Access translator string output is tested; real Access provider validation remains DATA-018. |
| #21 | Conditional event clearing is implemented. |

---

## Evidence-file closure state

- Complete: every P0/P1 finding has an accepted direction.
- Complete: only confirmed, intended implementation work was promoted to
  `TODO.md`.
- Complete: provider candidates remain a matrix until package and real-provider
  tests establish support.
- Complete: MongoDB remains native-driver-based and separate from SQL
  translation.
- Pending implementation: append a dated reconciliation with the implemented
  reference commit and validation outcome without rewriting this snapshot.
- Complete: this file no longer owns active work; `TODO.md` is authoritative.

This file is the detailed, commit-bound review evidence. It must not be used as
a second roadmap.
