---
name: nekolib-data
description: Implement, diagnose, review, document, or test NekoLib.Data, including QueryBuilder, DatabaseGateway, QueryExecutionContext, DbSession, SQL translators, typed and dynamic mapping, streaming, database providers, and transactions. Use for work under src/Data or tests/NekoLib.Data.Tests, and for application code whose behavior depends on NekoLib data APIs.
---

# Work on NekoLib Data

Preserve provider-neutral behavior across `net481` and `net9.0` while verifying
provider-specific assumptions explicitly.

## Establish current truth

1. Read `../../../AGENTS.md`.
2. Inspect `../../../src/Data/NekoLib.Data/NekoLib.Data.csproj`.
3. Read `../../../docs/modules/Data/REFERENCE.md` for the current documented
   contract, then verify affected claims against source and tests.
4. Read the affected implementation and its existing tests before proposing or
   making a change.
5. Consult `../../../docs/modules/Data/audits/initial-audit.md` only for historical
   leads. Reverify every finding against current source and tests before
   treating it as open.

## Classify the change

Identify every affected seam:

- query construction and parameter ownership
- provider translation and SQL dialect behavior
- command execution and connection lifetime
- transaction and `DbSession` ownership
- raw, DTO, dynamic, or streaming result mapping
- cancellation, error propagation, and query lifecycle events

Trace the complete path from public API to command execution. Do not fix only a
wrapper when the shared core owns the behavior.

## Preserve Data invariants

- Preserve both `net481` and `net9.0`.
- Use the constants declared by the project: `NETFRAMEWORK` and `NET_9`. Do not
  invent or copy constants from another module.
- Preserve nullable and implicit-using settings from each affected project.
- Keep NekoLib.Data free of project references unless the request explicitly
  justifies a dependency and the repository graph is deliberately changed.
- Treat OleDb parameter binding as positional. Preserve SQL placeholder order
  and parameter insertion order instead of relying on names.
- Keep subquery parameters isolated from parent-query parameters.
- Keep repeated `Build()` calls idempotent for INSERT and UPDATE paths.
- Distinguish parameterized values from raw identifiers and SQL clauses; never
  describe raw caller-controlled fragments as injection-safe.
- Dispose connections, commands, readers, and sessions on every success,
  cancellation, and failure path according to ownership.
- Preserve error propagation and avoid duplicate dispatch, success, or error
  events.
- Avoid `record` for types shared with `net481`.
- Read the current Inspection instrumentation freeze in `../../../ROADMAP.md`
  and any promoted Data work in `../../../TODO.md` before adding Core
  dependencies or module instrumentation.

## Add or update tests

- Mirror the source area under `../../../tests/NekoLib.Data.Tests/Unit/`.
- Name tests `MethodName_Condition_ExpectedResult`.
- The tracked `../../../tests/NekoLib.Data.Tests/Shared/` fixtures are legacy
  assets and current tests do not reference them by name. Verify their schema
  and deliberately wire them before citing real-database coverage. Do not infer
  executed coverage from their presence.
- Add regression coverage for parameter order, builder reuse, translation, and
  resource lifetime whenever those paths change.
- Cover both target frameworks when conditional compilation or provider
  behavior is involved.

## Verify proportionally

Start with the narrowest relevant test, then expand according to impact:

```powershell
dotnet test tests/NekoLib.Data.Tests/Unit/NekoLib.Data.Tests.Unit.csproj
dotnet build src/Data/NekoLib.Data/NekoLib.Data.csproj -f net481
dotnet build src/Data/NekoLib.Data/NekoLib.Data.csproj -f net9.0
dotnet test NekoLib.sln
```

Use MSBuild property inspection when conditional constants matter:

```powershell
dotnet msbuild src/Data/NekoLib.Data/NekoLib.Data.csproj -getProperty:DefineConstants -p:TargetFramework=net481
```

Report exactly which providers, fixtures, target frameworks, and commands were
verified. Do not generalize a query-builder-only test into database execution
coverage.
