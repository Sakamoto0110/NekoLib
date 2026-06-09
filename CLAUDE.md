# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**NekoLib** is a family of independent, dual-target C# libraries for desktop and embedded applications, targeting both **.NET Framework 4.8.1** and **.NET 9.0**. Modules are designed to be used independently — there is no required coupling between them.

Repository: https://github.com/Sakamoto0110/NekoLib

## Build & Test Commands

Build the entire solution:
```bash
dotnet build NekoLib.sln
```

Build a single project (both targets):
```bash
dotnet build src/Data/NekoLib.Data/NekoLib.Data.csproj
```

Build for a specific target framework:
```bash
dotnet build src/Data/NekoLib.Data/NekoLib.Data.csproj -f net481
dotnet build src/Data/NekoLib.Data/NekoLib.Data.csproj -f net9.0
```

Run all tests:
```bash
dotnet test NekoLib.sln
```

Run tests for a single project:
```bash
dotnet test tests/NekoLib.Data.Tests/Unit/NekoLib.Data.Tests.Unit.csproj
```

Run a single test by name (xUnit filter syntax):
```bash
dotnet test tests/NekoLib.Data.Tests/Unit/ --filter "FullyQualifiedName~MyTestMethodName"
```

Query defined constants for a project/TFM (useful for verifying conditional compilation):
```bash
dotnet msbuild src/Data/NekoLib.Data/NekoLib.Data.csproj -getProperty:DefineConstants -p:TargetFramework=net481
dotnet msbuild src/Data/NekoLib.Data/NekoLib.Data.csproj -getProperty:DefineConstants -p:TargetFramework=net9.0
```

There is no CI/CD pipeline — builds are manual via `dotnet` CLI or Visual Studio 2022.

## Module Map

| Module | Path | Targets | Purpose |
|---|---|---|---|
| `NekoLib.Core` | `src/Core/NekoLib.Core/` | net481, net9.0 | Foundation: logging/telemetry contracts (`ILogger`, `ILogSink`, `ITelemetrySink`, `IDiagnosticsContext`, `LogEntry`, `LogLevel`, `TelemetryEvent`), null objects, and the `IDebugUtils` observability contract. Zero dependencies. |
| `NekoLib` | `src/Hosting/NekoLib/` | net481, net9.0 | Bootstrap facade: `Neko.CreateBuilder()`, `NekoHost`, version/ID constants |
| `NekoLib.Diagnostics` | `src/Diagnostics/NekoLib.Diagnostics/` | net481, net9.0 | Logging, telemetry sinks, `DiagnosticsRuntime` |
| `NekoLib.Navigation` | `src/Navigation/NekoLib.Navigation/` | net481, net9.0-windows | Page lifecycle runtime (see below) |
| `NekoLib.Navigation.WinForms` | `src/Navigation/NekoLib.Navigation.WinForms/` | net481, net9.0-windows | WinForms platform adapter |
| `NekoLib.Navigation.Wpf` | `src/Navigation/NekoLib.Navigation.Wpf/` | net481, net9.0-windows | WPF platform adapter |
| `NekoLib.Data` | `src/Data/NekoLib.Data/` | net481, net9.0 | Provider-neutral SQL gateway with `QueryBuilder` |
| `NekoLib.Mvvm` | `src/Mvvm/NekoLib.Mvvm/` | net481, net9.0-windows | `ViewModelBase`, `RelayCommand` |
| `NekoLib.Devices` | `src/Devices/NekoLib.Devices/` | net481, net9.0 | Serial/hardware protocol abstraction |
| `NekoLib.Pipes` | `src/Pipes/NekoLib.Pipes/` | net481, net9.0 | Named pipe IPC (server, client, events) |
| `NekoLib.Watchdog` | `src/Watchdog/NekoLib.Watchdog/` | net481, net9.0 | Runtime health monitoring |

## Architecture & Layering Rules

The layering rule (enforced by project references) is:

- **Feature modules** may only reference `NekoLib.Core` and optionally `NekoLib.Diagnostics.Abstractions`.
- **Only `NekoLib` (the entrypoint/hosting)** may reference the concrete `NekoLib.Diagnostics` runtime.

This keeps each module independently usable without pulling in the full stack.

## Compile-Time Conventions

All projects share these constants:
- `NEKOLIB` — always defined
- `NETFRAMEWORK` — defined for `net481` targets
- `NET_9` — defined for `net9.0` targets

Use `#if NET6_0_OR_GREATER` for streaming and modern async APIs (not `#if NET_9`), since the Data module uses that guard for `IAsyncEnumerable` streaming paths.

**Implicit usings are disabled** across the entire solution. All `using` directives are explicit.

**Nullable annotations** are inconsistent across modules: enabled in `NekoLib.Data` and `NekoLib.Diagnostics`; disabled in `NekoLib.Navigation` and `NekoLib.Mvvm`.

## Navigation Module

The Navigation module is the most complex. Read `src/Navigation/NekoLib.Navigation/README.md` before modifying it.

**Canonical lifecycle order (DO NOT CHANGE):**
```
Resolve target page instance
→ Navigating(from, toType, args)
→ Reset timeout
→ FROM: OnNavigatedFromAsync() → OnExitAsync()
→ Detach + Cleanup (cache-policy driven)
→ Attach + BringToFront + Visible=true
→ TO: OnNavigatedToAsync(args)
→ Load strategy: ShowImmediately | LoadBeforeShow | LoadInBackground
→ OnEnterAsync(args)
→ CurrentChanged + History.Record
→ Navigated(from, to, args)
```

`NavigationContext` is the **only component allowed** to invoke lifecycle methods.

**FROZEN components** — do not modify without strong justification; extensions must live outside `Core/`:
- `NavigationContext.cs`
- `PageRegistry.cs`
- `PageFactory.cs`
- `PageLifecycleCleanupService.cs`
- `PlatformRegistry.cs`

**Typical bootstrap:**
```csharp
var ctx = PageNavBootstrap
    .Use(this, new WinFormsPlatformAdapter())
    .RegisterPagesFromAssembly(typeof(MainPage).Assembly)
    .ConfigurePages(cfg =>
    {
        cfg.Page<IdlePage>().AsIdle().StrongSingleton();
        cfg.Page<AdminPage>().AsModal().StrongSingleton();
    })
    .Timeout(10)
    .Start();
```

`InternalsVisibleTo("NekoLib.Navigation.Tests.Unit")` is set in the Navigation project.

## Data Module — Known Issues

The Data module has a detailed audit at `src/Data/NekoLib.Data/DataAudit.md`. Open issues to be aware of:

- **OleDb/Access parameter ordering** (finding #1): The `#if NET481` guard in `DatabaseGateway.Core.cs` uses the wrong symbol — the project defines `NETFRAMEWORK`, not `NET481`. OleDb positional binding is therefore disabled on net481, which is correctness-critical.
- **`QueryBuilder.Build()` is not idempotent** for INSERT/UPDATE (finding #6): calling `Build()` more than once accumulates parameters. Do not reuse a builder after building a DML query.
- **Subquery parameter collision** (finding #5): `WhereExists`/`WhereNotExists` copies subquery parameters into the parent by key; both start naming from `@p1`, so collisions silently overwrite parent parameters.
- **Async silently falls back to sync** (finding #3): on net481 (and providers with weak async support), all async ADO.NET calls catch `NotSupportedException` and fall through to blocking sync. Cancellation tokens are then ignored.
- **Streaming is net9-only** despite `IDqlStreamingGateway` being compiled on net481 (finding #11, mitigated with `[Obsolete(error: true)]` on net481).
- **Mapping failures are silent** (finding #16): DTO and dynamic mapping swallow property-set/conversion exceptions; returned objects may contain default values.
- **`QueryBuilder`-based `Insert`/`Update` do not accept a `DbSession`** (finding #13, partial): session-aware reads/streaming exist, but DML via `QueryBuilder` still bypasses the transaction.
- **Telemetry events expose raw SQL and full result sets** (finding #8): subscribers receive unmasked SQL and full row data. Slow or throwing subscribers directly slow database calls.

## Data Module — Mental Model

- `IDbConnectionFactory.Create()` returns a **new closed connection** every call.
- `DatabaseGateway` is stateless except for its `QueryExecutionContext` (owns factory, translator, events).
- `QueryExecutionContext` must be disposed by the caller; `DatabaseGateway` itself is not `IDisposable`.
- Raw mode converts all values to invariant-culture strings via `RecordItem`; null vs. empty string is lost.
- Dynamic mode defaults to `ExpandoObject`; IL-emitted types are behind an options flag and emit non-unloadable types.
- Streaming (`IAsyncEnumerable`) is the only low-memory pull path and is net9-only.
