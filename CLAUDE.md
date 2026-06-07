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
dotnet build src/Navigation/NekoLib.Navigation/NekoLib.Navigation.csproj
```

Build for a specific target framework:
```bash
dotnet build src/Data/NekoLib.Data/NekoLib.Data.csproj -f net481
dotnet build src/Data/NekoLib.Data/NekoLib.Data.csproj -f net9.0
```

Run all unit tests:
```bash
dotnet test NekoLib.sln
```

Run tests for a single project:
```bash
dotnet test tests/NekoLib.Navigation.Tests/Unit/NekoLib.Navigation.Tests.Unit.csproj
dotnet test tests/NekoLib.Data.Tests/Unit/NekoLib.Data.Tests.Unit.csproj
```

Run a single test by name (xUnit filter syntax):
```bash
dotnet test tests/NekoLib.Navigation.Tests/Unit/ --filter "FullyQualifiedName~TestClassName.MethodName"
```

Query defined constants for a project/TFM (useful for verifying conditional compilation):
```bash
dotnet msbuild src/Data/NekoLib.Data/NekoLib.Data.csproj -getProperty:DefineConstants -p:TargetFramework=net481
dotnet msbuild src/Data/NekoLib.Data/NekoLib.Data.csproj -getProperty:DefineConstants -p:TargetFramework=net9.0
```

**Runtime tests** (`runtime_tests/`) are WinForms `.exe` apps — run them by launching directly, not via `dotnet test`.

There is no CI/CD pipeline — builds are manual via `dotnet` CLI or Visual Studio 2022.

## Module Map

| Module | Path | Targets | Purpose |
|---|---|---|---|
| `NekoLib.Core` | `src/Core/NekoLib.Core/` | net481, net9.0 | Foundation: logging/telemetry contracts (`ILogger`, `ILogSink`, `ITelemetrySink`, `IDiagnosticsContext`, `LogEntry`, `LogLevel`, `TelemetryEvent`), null objects, and the `IDebugUtils` observability contract. Zero dependencies. |
| `NekoLib.Logger` | `src/Diagnostics/NekoLib.Logger/` | net481, net9.0 | Concrete logging: `Logger`, `Diagnostics` context, `DebugLogSink`, `MemoryTelemetrySink`. References `NekoLib.Core`. |
| `NekoLib.DebugUtils` | `src/DebugUtils/NekoLib.DebugUtils/` | net481, net9.0 | Concrete `IDebugUtils` (`DebugUtilsRuntime`): opt-in observability hub with operation ring buffer, pull-based state providers, and invokable commands. References only `NekoLib.Core`. Opt-in / debug builds only. |
| `NekoLib` | `src/Hosting/NekoLib/` | net481, net9.0 | Bootstrap facade: `Neko.CreateBuilder()`, `NekoHost`, version/ID constants |
| `NekoLib.Diagnostics` | `src/Diagnostics/NekoLib.Diagnostics/` | net481, net9.0 | Cross-platform crash orchestration: `CrashHandler` (AppDomain/TaskScheduler hooks, crash bundle + crash.txt + tails), pluggable `CrashDumpWriter`. OS-specific dump/WER/WinForms hooks live in `NekoLib.Diagnostics.Windows`. |
| `NekoLib.Diagnostics.Windows` | `src/Diagnostics/NekoLib.Diagnostics.Windows/` | net481, net9.0-windows | OS-specific crash facilities: `WindowsCrash.UseMiniDump()` (dbghelp.dll), `WindowsCrash.HookWinForms()` (Application.ThreadException), `CrashSuppressor` (kernel32 WER). References `NekoLib.Diagnostics`. |
| `NekoLib.Navigation` | `src/Navigation/NekoLib.Navigation/` | net481, net9.0 | Page lifecycle runtime (see below) |
| `NekoLib.Navigation.WinForms` | `src/Navigation/NekoLib.Navigation.WinForms/` | net481, net9.0-windows | WinForms platform adapter |
| `NekoLib.Navigation.Wpf` | `src/Navigation/NekoLib.Navigation.Wpf/` | net481, net9.0-windows | WPF platform adapter |
| `NekoLib.Data` | `src/Data/NekoLib.Data/` | net481, net9.0 | Provider-neutral SQL gateway with `QueryBuilder` |
| `NekoLib.Mvvm` | `src/Mvvm/NekoLib.Mvvm/` | net481, net9.0 | `ViewModelBase`, `RelayCommand` |
| `NekoLib.Devices` | `src/Devices/NekoLib.Devices/` | net481, net9.0 | Serial/hardware protocol abstraction |
| `NekoLib.Pipes` | `src/Pipes/NekoLib.Pipes/` | net481, net9.0 | Named pipe IPC (server, client, events) |
| `NekoLib.Watchdog` | `src/Watchdog/NekoLib.Watchdog/` | net481, net9.0-windows | Runtime health monitoring (Win32 hotkeys/PInvoke) |

## Architecture & Layering Rules

Each module follows a strict three-layer pattern:

1. **Contracts/** — pure interfaces and data-only types. No logic, no platform assumptions.
2. **Runtime/** — concrete implementations of those contracts.
3. **Adapters/** — platform-specific glue (WinForms/WPF timer, event dispatch, UI blocking). Only present in Navigation platform projects.

Dependencies flow downward only: `Adapters` → `Runtime` → `Contracts`. Feature modules reference contracts, never runtime classes from sibling modules.

Specific cross-module rules:
- **Feature modules** may only reference `NekoLib.Core` and optionally `NekoLib.Diagnostics.Abstractions`.
- **Only `NekoLib` (the entrypoint/hosting)** may reference the concrete `NekoLib.Diagnostics` runtime.

## Compile-Time Conventions

All projects share these constants:

| Symbol | When active |
|--------|-------------|
| `NEKOLIB` | always |
| `NETFRAMEWORK` | net481 only |
| `NET_9` | net9.0 / net9.0-windows |
| `WINFORMS` | any WinForms-enabled TFM |
| `WINFORMS_NETFRAMEWORK` | WinForms + net481 |
| `WINFORMS_NET_9` | WinForms + net9.0-windows |

Use `#if NET6_0_OR_GREATER` for streaming and modern async APIs (not `#if NET_9`), since the Data module uses that guard for `IAsyncEnumerable` streaming paths.

`PlatformGuards.cs` (in `NekoLib.Data`) provides runtime version checks for cases conditional compilation can't cover.

**Implicit usings are disabled** across the entire solution. All `using` directives must be explicit.

**Nullable annotations and implicit usings vary by module** — when editing a project, match its existing settings rather than flipping them:

| Module | Nullable | ImplicitUsings |
|--------|----------|----------------|
| Navigation | disabled | disabled |
| Data | **enabled** | disabled |
| Diagnostics | enabled | — |
| Pipes | enabled | enabled |
| Mvvm | disabled | disabled |
| Devices | disabled | **enabled** |

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

**Key contracts:**
- `IPageView` — minimal page contract (Name, NativeView handle, IsDisposed)
- `IPageLifecycle` — optional lifecycle hooks (OnNavigatedToAsync, OnNavigatedFromAsync, OnEnterAsync, OnExitAsync)
- `IPageHost` — owns attach/detach lifecycle
- `IPlatformAdapter` — abstracts WinForms vs WPF concerns (timer, event dispatch, interaction blocking)
- `IGuard` / `GuardContext` / `GuardResult` — async access control evaluated before navigation
- Guards compose via `AndGuard`, `OrGuard`

**Typical bootstrap:**
```csharp
PageNavBootstrap
    .Use<WinFormsPlatformAdapter>(this)
    .RegisterPagesFromAssembly(typeof(IdlePage).Assembly)
    .ConfigurePages(cfg =>
    {
        cfg.Page<IdlePage>().AsIdle().StrongSingleton();
        cfg.Page<AdminPage>().StrongSingleton();
    })
    .UseIdleTimeout(10_000)
    .Start();
```

`Start()` auto-mounts the resulting `NavigationContext` onto the static
`NavigationService` facade — view-models can call `NavigationService.SwitchPage<T>()`
right after. `UseContext` is `internal` and not part of the public surface;
shut down with `NavigationService.Shutdown()` to release subscribers and the
adapter before a fresh `Start()`.

`InternalsVisibleTo("NekoLib.Navigation.Tests.Unit")` is set in the Navigation project.

**Navigation tests** use fakes in `tests/NekoLib.Navigation.Tests/Unit/Fakes/`: `RuntimeTestFixture` wires a full in-memory runtime with `FakePlatformAdapter`, `FakePageHost`, and `StubPageViews`. Test naming follows `MethodName_Condition_ExpectedResult`.

## Data Module

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

**Data tests use real database fixtures** in `tests/NekoLib.Data.Tests/Shared/`: `Pods.db` (SQLite) and `PodsDB/` (Access). Do not mock the database layer in these tests.

### Mental Model

- `IDbConnectionFactory.Create()` returns a **new closed connection** every call.
- `DatabaseGateway` is stateless except for its `QueryExecutionContext` (owns factory, translator, events).
- `QueryExecutionContext` must be disposed by the caller; `DatabaseGateway` itself is not `IDisposable`.
- Raw mode converts all values to invariant-culture strings via `RecordItem`; null vs. empty string is lost.
- Dynamic mode defaults to `ExpandoObject`; IL-emitted types are behind an options flag and emit non-unloadable types.
- Streaming (`IAsyncEnumerable`) is the only low-memory pull path and is net9-only.

### Known Issues

The Data module has a detailed audit at `src/Data/NekoLib.Data/DataAudit.md`. Active issues:

- **OleDb/Access parameter ordering** (finding #1): The `#if NET481` guard in `DatabaseGateway.Core.cs` uses the wrong symbol — the project defines `NETFRAMEWORK`, not `NET481`. OleDb positional binding is therefore disabled on net481, which is correctness-critical.
- **`QueryBuilder.Build()` is not idempotent** for INSERT/UPDATE (finding #6): calling `Build()` more than once accumulates parameters. Do not reuse a builder after building a DML query.
- **Subquery parameter collision** (finding #5): `WhereExists`/`WhereNotExists` copies subquery parameters into the parent by key; both start naming from `@p1`, so collisions silently overwrite parent parameters.
- **Async silently falls back to sync** (finding #3): on net481 (and providers with weak async support), all async ADO.NET calls catch `NotSupportedException` and fall through to blocking sync. Cancellation tokens are then ignored.
- **Streaming is net9-only** despite `IDqlStreamingGateway` being compiled on net481 (finding #11, mitigated with `[Obsolete(error: true)]` on net481).
- **Mapping failures are silent** (finding #16): DTO and dynamic mapping swallow property-set/conversion exceptions; returned objects may contain default values.
- **`QueryBuilder`-based `Insert`/`Update` do not accept a `DbSession`** (finding #13, partial): session-aware reads/streaming exist, but DML via `QueryBuilder` still bypasses the transaction.
- **Telemetry events expose raw SQL and full result sets** (finding #8): subscribers receive unmasked SQL and full row data. Slow or throwing subscribers directly slow database calls.

## Other Modules

**Diagnostics** (`NekoLib.Diagnostics`): `ILogger`/`Logger` with standard levels (Trace → Fatal); pluggable `ILogSink`/`ITelemetrySink` with built-in Console, Memory, Debug, and Null variants; `CrashHandler`/`DumpWriter` for unhandled exceptions; `IDiagnosticsContext` injected into feature modules (Navigation uses this).

**Pipes** (`NekoLib.Pipes`): `PipeServer`/`PipeClient` framed message transport; `PipeEventHub` pub/sub over pipes (`IAsyncDisposable` on net9.0 only). Uses `Newtonsoft.Json` on net481 and `System.Text.Json` on net9.0.

**Watchdog** (`NekoLib.Watchdog`): Process monitoring library with companion host executable (`NekoLib.Watchdog.Host`). Depends on both `NekoLib.Pipes` (IPC channel) and `NekoLib.Diagnostics` (logging). The `Supervisor_481` runtime test exercises this end-to-end.

**MVVM** (`NekoLib.Mvvm`): Intentionally minimal. `ViewModelBase` with `INotifyPropertyChanged` and `SetProperty` helper; `RelayCommand`/`RelayCommand<T>` with safe `T` coercion. Works with both WinForms data binding and WPF binding.

**Devices** (`NekoLib.Devices`): Serial port/hardware abstraction targeting `net481;net9.0` (not `-windows`). On net9.0, `System.IO.Ports` comes from NuGet; on net481 it is built-in. No unit test project exists for this module.
