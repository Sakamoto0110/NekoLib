# Data Module

**Kind:** guide

**Lifecycle:** historical

**Subject:** previously machine-local Claude Data guidance retained as
migration input

**Reference date:** not recorded

**Reference commit:** not recorded

**Current state:** pending the planned current-state audit; not authoritative

> **Documentation migration notice (2026-08-22):** This previously local file
> is now versioned as migration input and has not yet completed its planned
> current-state audit. Reverify every technical claim against current source,
> project files, tests, `TODO.md`, and the authoritative Data documentation
> before relying on it.

Guidance for `NekoLib.Data` under `src/Data/`. Solution-wide rules (layering, compile-time constants, build commands) live in the root `CLAUDE.md`.

**Module conventions:** Nullable **enabled**, ImplicitUsings **disabled** — match these, don't flip them. The code carries pre-existing nullable warnings (CS86xx); don't introduce new ones.

Use `#if NET6_0_OR_GREATER` for streaming and modern async APIs (not `#if NET_9`) — the `IAsyncEnumerable` streaming paths use that guard. `PlatformGuards.cs` provides runtime version checks for cases conditional compilation can't cover.

## Gateway structure

`IDatabaseGateway` is the composition of `IDmlGateway` + `IDqlGateway` + `IDqlStreamingGateway` + `ITclGateway`.

`DatabaseGateway` is split across partial classes by concern:

| File | Responsibility |
|------|----------------|
| `DatabaseGateway.Core.cs` | Core execution pipeline (`WithCommandAsync`), connection handling, error raising |
| `DatabaseGateway.raw_dto.cs` | Reflection-based typed DTO query paths |
| `DatabaseGateway.Dynamic.cs` | IL-emitted dynamic type generation for `DynamicRow` |
| `DatabaseGateway.Universal.cs` | Type-agnostic `Get<T>` with DTO→Dynamic fallback |
| `DatabaseGateway.Helpers.cs` | Column schema extraction and helper utilities |
| `DatabaseGateway.Interface.cs` | Interface compliance surface |

## Mental Model

- `IDbConnectionFactory.Create()` returns a **new closed connection** every call.
- `DatabaseGateway` is stateless except for its `QueryExecutionContext` (owns factory, translator, events).
- `QueryExecutionContext` must be disposed by the caller; `DatabaseGateway` itself is not `IDisposable`.
- Raw mode converts all values to invariant-culture strings via `RecordItem`; null vs. empty string is lost.
- Dynamic mode defaults to `ExpandoObject`; IL-emitted types are behind an options flag and emit non-unloadable types.
- Streaming (`IAsyncEnumerable`) is the only low-memory pull path and is net9-only.

## Known Issues

The Data module has a detailed audit at `src/Data/NekoLib.Data/DataAudit.md`. Active issues:

- **OleDb/Access parameter ordering** (finding #1): The `#if NET481` guard in `DatabaseGateway.Core.cs` uses the wrong symbol — the project defines `NETFRAMEWORK`, not `NET481`. OleDb positional binding is therefore disabled on net481, which is correctness-critical.
- **`QueryBuilder.Build()` is not idempotent** for INSERT/UPDATE (finding #6): calling `Build()` more than once accumulates parameters. Do not reuse a builder after building a DML query.
- **Subquery parameter collision** (finding #5): `WhereExists`/`WhereNotExists` copies subquery parameters into the parent by key; both start naming from `@p1`, so collisions silently overwrite parent parameters.
- **Async silently falls back to sync** (finding #3): on net481 (and providers with weak async support), all async ADO.NET calls catch `NotSupportedException` and fall through to blocking sync. Cancellation tokens are then ignored.
- **Streaming is net9-only** despite `IDqlStreamingGateway` being compiled on net481 (finding #11, mitigated with `[Obsolete(error: true)]` on net481).
- **Mapping failures are silent** (finding #16): DTO and dynamic mapping swallow property-set/conversion exceptions; returned objects may contain default values.
- **`QueryBuilder`-based `Insert`/`Update` do not accept a `DbSession`** (finding #13, partial): session-aware reads/streaming exist, but DML via `QueryBuilder` still bypasses the transaction.
- **Telemetry events expose raw SQL and full result sets** (finding #8): subscribers receive unmasked SQL and full row data. Slow or throwing subscribers directly slow database calls.

## Tests

Run the unit tests:
```bash
dotnet test tests/NekoLib.Data.Tests/Unit/NekoLib.Data.Tests.Unit.csproj
```

**Data tests use real database fixtures** in `tests/NekoLib.Data.Tests/Shared/`: `Pods.db` (SQLite) and `PodsDB/` (Access). Do not mock the database layer in these tests.
