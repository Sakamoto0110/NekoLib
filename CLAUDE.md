# CLAUDE.md

**Kind:** guide

**Lifecycle:** historical

**Subject:** previously machine-local Claude repository guidance retained as
migration input

**Reference date:** not recorded

**Reference commit:** not recorded

**Current state:** pending the planned current-state audit; not authoritative

> **Documentation migration notice (2026-08-22):** This previously local file
> is now versioned as migration input and has not yet completed its planned
> current-state audit. Until that audit is accepted, source and project files
> define implementation truth, while `AGENTS.md`, `README.md`, `TODO.md`, and
> the documentation authority index define current repository guidance. Do not
> treat conflicting module maps, workflow statements, inventories, freezes, or
> architecture descriptions below as authoritative.

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

**`src/Tools/BundlerTool/`** is a standalone dev utility, not part of `NekoLib.sln`. Build it directly:
```bash
dotnet build src/Tools/BundlerTool/BundlerTool.csproj
```

There is no CI/CD pipeline — builds are manual via `dotnet` CLI or Visual Studio 2022.

## Module Map

| Module | Path | Targets | Purpose |
|---|---|---|---|
| `NekoLib.Core` | `src/Core/NekoLib.Core/` | net481, net9.0 | Foundation: logging/telemetry contracts (`ILogger`, `ILogSink`, `ITelemetrySink`, `IDiagnosticsContext`, `LogEntry`, `LogLevel`, `TelemetryEvent`), null objects, and the `IDebugUtils` observability contract. Zero dependencies. |
| `NekoLib.Logger` | `src/Diagnostics/NekoLib.Logger/` | net481, net9.0 | Concrete logging: `Logger`, `Diagnostics` context, `DebugLogSink`, `MemoryTelemetrySink`. References `NekoLib.Core`. |
| `NekoLib.DebugUtils` | `src/DebugUtils/NekoLib.DebugUtils/` | net481, net9.0 | Concrete `IDebugUtils` (`DebugUtilsRuntime`): opt-in observability hub with operation ring buffer, pull-based state providers, and invokable commands. References only `NekoLib.Core`. Opt-in / debug builds only. **❄ FROZEN 2026-07-26** — see the freeze section in `TODO.md`. |
| `NekoLib` | `src/Hosting/NekoLib/` | net481, net9.0 | Bootstrap facade: `Neko.CreateBuilder()`, `NekoHost`, version/ID constants |
| `NekoLib.Diagnostics` | `src/Diagnostics/NekoLib.Diagnostics/` | net481, net9.0 | Cross-platform crash orchestration: `CrashHandler` (AppDomain/TaskScheduler hooks, crash bundle + crash.txt + tails), pluggable `CrashDumpWriter`. OS-specific dump/WER/WinForms hooks live in `NekoLib.Diagnostics.Windows`. |
| `NekoLib.Diagnostics.Windows` | `src/Diagnostics/NekoLib.Diagnostics.Windows/` | net481, net9.0-windows | OS-specific crash facilities: `WindowsCrash.UseMiniDump()` (dbghelp.dll), `WindowsCrash.HookWinForms()` (Application.ThreadException), `CrashSuppressor` (kernel32 WER). References `NekoLib.Diagnostics`. |
| `NekoLib.Navigation` | `src/Navigation/NekoLib.Navigation/` | net481, net9.0 | Page lifecycle runtime (see `src/Navigation/CLAUDE.md`) |
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
- **Feature modules** may only reference `NekoLib.Core` (which carries the logging/telemetry/diagnostics contracts — there is no separate abstractions project). Documented exception: `NekoLib.Watchdog` also references `NekoLib.Pipes` for its IPC channel.
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

**Nullable is enabled in every module.** The codebase still carries pre-existing nullable warnings (CS86xx); don't introduce new ones.

**Implicit usings are disabled everywhere except `NekoLib.Pipes` and `NekoLib.Devices`.** In all other projects, `using` directives must be explicit. When editing a project, match its existing settings rather than flipping them.

## Navigation Module

The Navigation module is the most complex. Detailed guidance — canonical lifecycle order, FROZEN components, key contracts, overlay primitives, bootstrap, observability bridge, and tests — lives in `src/Navigation/CLAUDE.md` (loaded automatically when working under `src/Navigation/`). Read `src/Navigation/NekoLib.Navigation/README.md` before modifying the module.

## Data Module

Detailed guidance — gateway structure, mental model, known issues from `DataAudit.md`, conditional-compilation notes, and test fixtures — lives in `src/Data/CLAUDE.md` (loaded automatically when working under `src/Data/`).

## Other Modules

**Observability** (`NekoLib.Core.Observability` + `NekoLib.DebugUtils`) — **❄ FROZEN 2026-07-26.** Opt-in, zero-cost-when-disabled debug sink: modules push through the `IDebugUtils` contract in Core, whoever hosts `DebugUtilsRuntime` pulls via `GetOperations`/`CaptureState`/`TryInvokeCommand`. Only **Navigation** has hooks (`DebugUtilsNavigationObserver`, wired by `PageNavBootstrap.UseDebugUtils`); Data/Pipes/Watchdog/Devices/Diagnostics have none, and the command channel has no producer or consumer anywhere. Don't extend without an explicit decision — the full list of what's deliberately incomplete is in the freeze section of `TODO.md`.

**Diagnostics** (`NekoLib.Diagnostics`): `ILogger`/`Logger` with standard levels (Trace → Fatal); pluggable `ILogSink`/`ITelemetrySink` with built-in Console, Memory, Debug, and Null variants; `CrashHandler`/`DumpWriter` for unhandled exceptions; `IDiagnosticsContext` injected into feature modules (Navigation uses this).

**Pipes** (`NekoLib.Pipes`): `PipeServer`/`PipeClient` framed message transport; `PipeEventHub` pub/sub over pipes (`IAsyncDisposable` on net9.0 only). Uses `Newtonsoft.Json` on net481 and `System.Text.Json` on net9.0.

**Watchdog** (`NekoLib.Watchdog`): Process monitoring library with companion host executable (`NekoLib.Watchdog.Host`). Depends on `NekoLib.Pipes` (IPC channel) and `NekoLib.Core` (logging/telemetry contracts). The `Supervisor_481` runtime test exercises this end-to-end.

**MVVM** (`NekoLib.Mvvm`): Intentionally minimal. `ViewModelBase` with `INotifyPropertyChanged` and `SetProperty` helper; `RelayCommand`/`RelayCommand<T>` with safe `T` coercion. Works with both WinForms data binding and WPF binding.

**Devices** (`NekoLib.Devices`): Serial port/hardware abstraction targeting `net481;net9.0` (not `-windows`). On net9.0, `System.IO.Ports` comes from NuGet; on net481 it is built-in. No unit test project exists for this module.
