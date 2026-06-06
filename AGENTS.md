# AGENTS.md

This file provides guidance to Codex (Codex.ai/code) when working with code in this repository.

## Build & Test Commands

```powershell
# Build entire solution
dotnet build NekoLib.sln

# Build a single project
dotnet build src/Navigation/NekoLib.Navigation/NekoLib.Navigation.csproj

# Run all unit tests
dotnet test NekoLib.sln

# Run a single test project
dotnet test tests/NekoLib.Navigation.Tests/Unit/NekoLib.Navigation.Tests.Unit.csproj
dotnet test tests/NekoLib.Data.Tests/Unit/NekoLib.Data.Tests.Unit.csproj
dotnet test tests/NekoLib.Mvvm.Tests/Unit/NekoLib.Mvvm.Tests.Unit.csproj

# Run a single test by name
dotnet test tests/NekoLib.Navigation.Tests/Unit/ --filter "FullyQualifiedName~TestClassName.MethodName"
```

Runtime (scenario/integration) tests live in `runtime_tests/` and are WinForms apps, not xUnit — run them by launching directly, not via `dotnet test`.

## Repository Layout

```
src/
  Hosting/NekoLib/          — versioning constants (v1.0.0)
  Navigation/
    NekoLib.Navigation/     — core navigation framework (framework-agnostic)
    NekoLib.Navigation.WinForms/  — WinForms platform adapter
    NekoLib.Navigation.Wpf/       — WPF platform adapter
  Data/NekoLib.Data/        — database gateway & query builder
  Diagnostics/NekoLib.Diagnostics/  — logging, telemetry, crash handling
  Pipes/NekoLib.Pipes/      — named pipe IPC
  Watchdog/
    NekoLib.Watchdog/       — process monitoring library
    NekoLib.Watchdog.Host/  — standalone watchdog executable
  Mvvm/NekoLib.Mvvm/        — ViewModelBase, RelayCommand, RelayCommand<T>
  Devices/NekoLib.Devices/  — serial port / hardware abstraction
  Tools/BundlerTool/        — code bundling tool (net481, WinExe)
tests/                      — xUnit unit tests (mirrors src/ module layout)
runtime_tests/
  WinForms_481/             — general WinForms scenario app
  LoginFlow_481/            — Navigation + MVVM login flow scenario
  Supervisor_481/           — Watchdog runtime scenario app
```

## Multi-Targeting Conventions

All library projects dual-target `net481` (full .NET Framework 4.8.1) and `net9.0` or `net9.0-windows`. Use these conditional compilation symbols in `#if` blocks:

| Symbol | When active |
|--------|-------------|
| `NETFRAMEWORK` | net481 only |
| `NET_9` | net9.0 / net9.0-windows only |
| `WINFORMS` | any WinForms-enabled TFM |
| `WINFORMS_NETFRAMEWORK` | WinForms + net481 |
| `WINFORMS_NET_9` | WinForms + net9.0-windows |

`PlatformGuards.cs` (in `NekoLib.Data`) provides runtime version checks for cases conditional compilation can't cover.

## Architecture

### Layering Rules

Each module follows a strict three-layer pattern:

1. **Contracts/** — interfaces and data-only types (`IPageView`, `ILogger`, `IDatabaseGateway`). No implementation dependencies.
2. **Runtime/** — concrete implementations of those contracts.
3. **Adapters/** — platform-specific glue (WinForms/WPF timers, event dispatch, UI blocking) — only in Navigation platform projects.

Dependencies flow downward only: `Adapters` → `Runtime` → `Contracts`. Feature modules reference contracts, never runtime classes from sibling modules.

### Navigation (`NekoLib.Navigation`)

Page/component navigation framework for desktop apps:

- `IPageView` — minimal page contract (Name, NativeView handle, IsDisposed)
- `IPageHost` — owns the attach/detach lifecycle of pages
- `IPlatformAdapter` — abstracts WinForms vs WPF concerns (timer, event dispatch, interaction blocking)
- `IGuard<T>` / `GuardContext` / `GuardResult` — async access control evaluated before navigation
- `PageNavBootstrap` — entry point for wiring up navigation in a host application
- Guards compose: `AndGuard`, `OrGuard`, `RequireAuthenticatedGuard`
- Diagnostics integration via `NavigationDiagnostics` / `NavigationLogged` events (hooks into `NekoLib.Diagnostics`)

### Data (`NekoLib.Data`)

Database access layer with a gateway pattern:

- `IDatabaseGateway` = `IDmlGateway` + `IDqlGateway` + `IDqlStreamingGateway` + `ITclGateway`
- `DatabaseQuery` — builder for ad-hoc parameterized queries
- `QueryBuilder` — fluent SQL construction
- `DataMapper` — maps `IDataReader` rows to typed objects
- `DynamicRow` — untyped result bag for schema-unknown queries

`DatabaseGateway` is split across partial classes by concern:

| File | Responsibility |
|------|---------------|
| `DatabaseGateway.Core.cs` | Core execution pipeline (`WithCommandAsync`), connection handling, error raising |
| `DatabaseGateway.raw_dto.cs` | Reflection-based typed DTO query paths |
| `DatabaseGateway.Dynamic.cs` | IL-emitted dynamic type generation for `DynamicRow` |
| `DatabaseGateway.Universal.cs` | Type-agnostic `Get<T>` with DTO→Dynamic fallback |
| `DatabaseGateway.Helpers.cs` | Column schema extraction and helper utilities |
| `DatabaseGateway.Interface.cs` | Interface compliance surface |

**OleDb/Access caveat:** parameter binding on net481 with OleDb is position-dependent, not name-dependent. `QueryBuilder` guards against this but be aware when writing raw queries or subqueries — parameter name collisions between a subquery and its parent can silently overwrite bindings.

Nullable is **enabled** in this project — stricter than the rest of the solution.

Data unit tests use real database fixtures in `tests/NekoLib.Data.Tests/Shared/`: `Pods.db` (SQLite) and `PodsDB/` (Access). Do not mock the database layer in these tests.

### Diagnostics (`NekoLib.Diagnostics`)

- `ILogger` / `Logger` — standard levels (Trace → Fatal)
- `ILogSink` / `ITelemetrySink` — pluggable output targets
- Built-in sinks: `ConsoleLogSink`, `MemoryTelemetrySink`, `DebugLogSink`, `NullTelemetrySink`
- `CrashHandler` / `CrashSupressor` / `DumpWriter` — unhandled exception handling and dump writing
- `IDiagnosticsContext` — scoped context injected into feature modules (Navigation uses this)

### Pipes (`NekoLib.Pipes`)

Named-pipe IPC:

- `PipeServer` / `PipeClient` — framed message transport
- `PipeEventHub` — pub/sub over pipes; implements `IAsyncDisposable` on net9.0 only
- `PipeMessage` / `PipeFraming` / `PipeError` — protocol types
- `IPipeMetrics` / `SimplePipeMetrics` — optional telemetry injected into server and hub
- Uses `Newtonsoft.Json` on net481 only; uses `System.Text.Json` on net9.0

### Watchdog (`NekoLib.Watchdog`)

Process monitoring library with a companion host executable (`NekoLib.Watchdog.Host`). Depends on both `NekoLib.Pipes` (IPC channel to monitored processes) and `NekoLib.Diagnostics` (logging). The `Supervisor_481` runtime test exercises this module end-to-end.

### MVVM (`NekoLib.Mvvm`)

Intentionally minimal — no framework dependency:

- `ViewModelBase` — `INotifyPropertyChanged` base with `SetProperty` helper
- `RelayCommand` / `RelayCommand<T>` — `ICommand` wrappers with safe `T` coercion

Works with both WinForms data binding and WPF binding.

### Devices (`NekoLib.Devices`)

Serial port / hardware abstraction. Targets `net481;net9.0` (not `-windows`). On net9.0, `System.IO.Ports` is pulled from NuGet; on net481 it is a built-in. No unit test project exists for this module.

## Nullable & ImplicitUsings per Module

| Module | Nullable | ImplicitUsings |
|--------|----------|---------------|
| Navigation | disabled | disabled |
| Navigation.WinForms | — | — |
| Data | **enabled** | disabled |
| Diagnostics | enabled | — |
| Pipes | enabled | enabled |
| Mvvm | disabled | disabled |
| Devices | disabled | **enabled** |
| Watchdog | — | — |

When editing a project, match its existing nullable setting — do not flip it.

## Test Layout Conventions

- Unit tests mirror the source module: `tests/NekoLib.{Module}.Tests/Unit/`
- Test class names match the class under test; facts/theories use `MethodName_Condition_ExpectedResult` or similar descriptive naming
- `runtime_tests/` is for runnable WinForms apps that exercise full scenarios — they belong there, not in `tests/`
- `Devices` has no unit test project
