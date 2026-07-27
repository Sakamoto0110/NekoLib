# AGENTS.md

Guidance for Codex working in this repository.

**NekoLib** is a family of independent, dual-target C# libraries for desktop and
embedded applications, targeting both **.NET Framework 4.8.1** and **.NET 9.0**.
Modules are designed to be used independently — there is no required coupling
between them. Repository: https://github.com/Sakamoto0110/NekoLib

---

# ⚠ HANDOFF — state as of 2026-07-26

This section is a point-in-time snapshot written when the work was handed over.
Everything below the `---` after it is durable repo guidance. If this snapshot
contradicts the code, the code wins — and please correct it here.

## Verified at handoff

Run on 2026-07-26, Windows, `dotnet test NekoLib.sln`:

- **478/478 passing**, 0 failures, across `net481` and `net9.0`/`net9.0-windows`.
- **665 build warnings, all pre-existing nullable (CS86xx)** — the count is
  across both TFMs, so roughly half that per target. Concentrated in
  `NekoLib.Navigation` (304), `NekoLib.Watchdog` (140),
  `NekoLib.Navigation.WinForms` (80). **Do not add new ones.**
- HEAD at handoff: `da86afc`.

## Three local commits are NOT pushed

`master` was level with `origin/master` at `d352fa8`. These three sit on top,
committed locally only:

| Commit | What |
|---|---|
| `e58d7c2` | `docs(navigation)`: corrected the canonical lifecycle order in the Navigation README (it still described `OnEnterAsync`/`OnExitAsync`, which no longer exist) |
| `3cb5ad6` | `feat(navigation)`: two-tier `DebugUtilsNavigationObserver` + 7 new tests |
| `da86afc` | `docs`: recorded the extended hooks and froze the observability module |

If you are working from a fresh clone of the remote, **you do not have these** —
you have a repo in the pre-2026-07-26 state, without the extended observability
hooks and without the freeze record. Check `git log --oneline -4` first.

## Where the work was heading (last intentions)

The observability module is **frozen**, deliberately, not abandoned. When it is
unfrozen, the recommended order is:

1. **Consumer bridge** — dump the ring buffer + `CaptureState()` into the
   `CrashHandler` crash bundle. This is what turns the module from "a buffer
   nobody reads" into a post-mortem tool, and it is the highest-value step.
2. **One real command case** — `RegisterCommand`/`TryInvokeCommand` is a third
   of the interface and has never been exercised. Validate it once before
   replicating the pattern across five modules.
3. **B4 per module** — start with `Data` (the `QueryExecutionContext` events are
   already the seam) and `Pipes` (`IPipeMetrics` is already the extension point).

Do not extend observability without an explicit decision to unfreeze.

## What was just completed

- **Phase A complete (A1–A8).** Created `NekoLib.Core` (logging/telemetry/
  observability contracts, zero deps), `NekoLib.Logger` (concrete logging),
  `NekoLib.Diagnostics.Windows` (dbghelp/kernel32 PInvoke split out of
  `NekoLib.Diagnostics`). Unblocked `net9.0` on modules that do not need Windows.
  Inverted the `CrashHandler` dump writer to avoid a Diagnostics ↔
  Diagnostics.Windows cycle.
- **B1/B2/B3** — `IDebugUtils` contract in Core, `DebugUtilsRuntime` in
  `NekoLib.DebugUtils`, and the Navigation observer pilot.
- **Extended Navigation telemetry (2026-07-26)** — the observer now has two
  fidelity levels; see `src/Navigation/NekoLib.Navigation/Diagnostics/DebugUtilsNavigationObserver.cs`
  and the `TODO.md` B3 section.
- **Devices audit closed** (`d352fa8`) — `SerialCommTransport` remaining items.

## What is deliberately incomplete (the freeze)

Recorded in full in the freeze section of `TODO.md`. Summary:

1. **B4 was never done.** Only Navigation emits. `Data`, `Pipes`, `Watchdog`,
   `Devices` and `Diagnostics` do not reference `IDebugUtils` at all. **Trap:**
   the `IntegrationDemo_481` runtime app shows `Data/*` and `Pipes/*` operations
   in the ring buffer — that is *the app* calling `Record` by hand, not the
   libraries emitting. Swap the app and the instrumentation is gone.
2. **The command channel is dead.** `RegisterCommand` / `TryInvokeCommand` have
   zero registrations, invocations or tests in the whole repo.
3. **No reusable consumer surface.** No viewer, no `ILogSink`/file bridge,
   nothing in the crash bundle. Every app wires its own.
4. **`NekoLib.DebugUtils` has no test project.** Ring-buffer eviction is covered
   only indirectly via the Navigation observer; `ClearOperations`, `CommandKeys`,
   the command channel and concurrency are untested.
5. **`NoPageAttached` / `NoPageVisible` are wired but untested** — firing them
   deterministically needs a real host, not the test fakes.

## Open items elsewhere (from the audit docs)

These are pre-existing and were not part of the observability work. Each audit
file has the detail; do not re-derive them.

| Module | Open | Where |
|---|---|---|
| Watchdog | M5 (update mechanism — genuinely unimplemented), M8 (pipe-name hash collision), M9 (ring-buffer silent drop), L4, L5, L6. M7 + L7 deferred by decision | `docs/audit/watchdog-first-pass.md` |
| Pipes | per-subscriber bounded event queue + drop policy, pipe ACL/security, graceful drain on `Dispose`. All H/M/L findings closed | `docs/audit/pipes-first-pass.md` |
| Devices | `ReadLine` timeout semantics, `SerialConfig` validation, `ThrowIfDisposed()`, `RawText` ASCII decision — *some may be closed by `d352fa8`, verify* | `docs/audit/devices-first-pass.md` |
| Data | next pass is verification-by-test: OleDb parameter order, `QueryBuilder.Build()` mutation on INSERT/UPDATE, `WhereExists` collision, telemetry masking decision | `src/Data/NekoLib.Data/DataAudit.md` |
| Navigation | NEW-12, NEW-13 (cosmetic API smells); one manual probe in §2.8 | `docs/audit/navigation-audit.md` |
| Navigation | `PageDescriptor.AllowAnonymous` is stored but **never consulted by the runtime** — guards on the descriptor always run | — |

## What you will NOT see in a fresh clone

Several things are `.gitignore`d. If you are told they exist and cannot find
them, this is why — do not recreate them:

- **All of `runtime_tests/`** — six WinForms/WPF `.exe` scenario apps, and none
  of them are in `NekoLib.sln` either. `IntegrationDemo_481` in particular is the
  best worked example of the current API surface.
- **`CLAUDE.md`** at the root and under `src/Data/` and `src/Navigation/` — the
  guidance files for the other assistant.
- **`codex_readme.md`** at the root, despite the name.

## Documents that are STALE — do not trust

- **`MIGRATION_NOTES.md`** — describes a `NekoLib.Diagnostics.Abstractions`
  project that does not exist (it became `NekoLib.Core`), plus
  `DiagnosticsRuntime`, `IDiagnosticContext`, `NullDiagnosticContext`,
  `Neko.CreateBuilder()`, `NekoBuilder`, `NekoHost`, `IDiagnosticsBuilder` —
  **none of which exist anywhere in `src/`**. The only still-true part (the Pipes
  `connection_closed` breaking change) is duplicated with more detail in
  `docs/audit/pipes-first-pass.md`. Flagged for deletion.
- **`src/Navigation/NekoLib.Navigation/TODO.md`** — dated 2026-02-25, truncated
  mid-code-block, broken encoding. Every item refers to `PageTimeoutController`,
  `WinFormsPageTimeoutAdapter`, `IPageTimeoutService`,
  `IPageTimeoutServiceFactory` — **none of which exist**; that file is their only
  mention in the repo. Timeout handling became the idle system. Flagged for
  deletion.
- **`README.md`** at the root is 9 bytes — effectively empty.

## Facts that contradict older docs

- `src/Hosting/NekoLib/` is **not in `NekoLib.sln`** and contains only four
  constants files (version/ID/compatibility/info). It is **not** a bootstrap
  facade — there is no `Neko.CreateBuilder()` or `NekoHost`.
- `NekoLib.Diagnostics.Windows` is the **one `src/` project still on
  `<Nullable>disable</Nullable>`** — it was created in A4, after the A7 sweep
  listed the projects to flip, so it was missed. Everything else under `src/` is
  `enable`. All **test** projects are `disable`.
- `NekoLib.Data`, `NekoLib.Devices`, `NekoLib.Mvvm`, `NekoLib.Pipes` and
  `NekoLib.Diagnostics` reference **no other project at all** — in particular
  Data and Devices do not know `NekoLib.Core`.

---

# Durable repo guidance

## Build & Test Commands

```bash
dotnet build NekoLib.sln
dotnet test NekoLib.sln
```

Single project, or a single target framework:

```bash
dotnet build src/Navigation/NekoLib.Navigation/NekoLib.Navigation.csproj
dotnet build src/Data/NekoLib.Data/NekoLib.Data.csproj -f net481
```

Single test project, or a single test:

```bash
dotnet test tests/NekoLib.Navigation.Tests/Unit/NekoLib.Navigation.Tests.Unit.csproj
dotnet test tests/NekoLib.Navigation.Tests/Unit/ --filter "FullyQualifiedName~TestClassName.MethodName"
```

Verify conditional-compilation constants for a given TFM:

```bash
dotnet msbuild src/Data/NekoLib.Data/NekoLib.Data.csproj -getProperty:DefineConstants -p:TargetFramework=net481
```

There is **no CI/CD pipeline** — builds are manual via `dotnet` CLI or Visual
Studio 2022. `net481` and the `-windows` targets only build on Windows.

`src/Tools/BundlerTool/` is a standalone dev utility, not part of `NekoLib.sln`;
build it directly.

## Module Map

| Module | Path | Targets | References |
|---|---|---|---|
| `NekoLib.Core` | `src/Core/NekoLib.Core/` | net481, net9.0 | — |
| `NekoLib.Logger` | `src/Diagnostics/NekoLib.Logger/` | net481, net9.0 | Core |
| `NekoLib.DebugUtils` | `src/DebugUtils/NekoLib.DebugUtils/` | net481, net9.0 | Core |
| `NekoLib.Diagnostics` | `src/Diagnostics/NekoLib.Diagnostics/` | net481, net9.0 | — |
| `NekoLib.Diagnostics.Windows` | `src/Diagnostics/NekoLib.Diagnostics.Windows/` | net481, net9.0-windows | Diagnostics |
| `NekoLib.Navigation` | `src/Navigation/NekoLib.Navigation/` | net481, net9.0 | Core |
| `NekoLib.Navigation.WinForms` | `src/Navigation/NekoLib.Navigation.WinForms/` | net481, net9.0-windows | Navigation |
| `NekoLib.Navigation.Wpf` | `src/Navigation/NekoLib.Navigation.Wpf/` | net481, net9.0-windows | Navigation |
| `NekoLib.Data` | `src/Data/NekoLib.Data/` | net481, net9.0 | — |
| `NekoLib.Mvvm` | `src/Mvvm/NekoLib.Mvvm/` | net481, net9.0 | — |
| `NekoLib.Devices` | `src/Devices/NekoLib.Devices/` | net481, net9.0 | — |
| `NekoLib.Pipes` | `src/Pipes/NekoLib.Pipes/` | net481, net9.0 | — |
| `NekoLib.Watchdog` | `src/Watchdog/NekoLib.Watchdog/` | net481, net9.0-windows | Core, Pipes |
| `NekoLib.Watchdog.Host` | `src/Watchdog/NekoLib.Watchdog.Host/` | net481, net9.0-windows | Watchdog |
| `NekoLib` (hosting) | `src/Hosting/NekoLib/` | net481, net9.0 | — (**not in the sln**) |

**Core** carries `ILogger`, `ILogSink`, `ITelemetrySink`, `IDiagnosticsContext`,
`LogEntry`, `LogLevel`, `TelemetryEvent`, the null objects, and the `IDebugUtils`
observability contract. There is no separate abstractions project.

`NekoLib.Pipes` uses `Newtonsoft.Json` on net481 and `System.Text.Json` on
net9.0. `NekoLib.Devices` pulls `System.IO.Ports` from NuGet on net9.0 (built-in
on net481).

## Architecture & Layering Rules

Each module follows a strict three-layer pattern:

1. **Contracts/** — pure interfaces and data-only types. No logic, no platform
   assumptions.
2. **Runtime/** — concrete implementations of those contracts.
3. **Adapters/** — platform-specific glue. Only present in Navigation platform
   projects.

Dependencies flow downward only: `Adapters` → `Runtime` → `Contracts`. Feature
modules reference contracts, never runtime classes from sibling modules.

- Feature modules may only reference `NekoLib.Core`. Documented exception:
  `NekoLib.Watchdog` also references `NekoLib.Pipes` for its IPC channel.
- Only the entrypoint/hosting project may reference the concrete
  `NekoLib.Diagnostics` runtime. In practice nothing does today except
  `NekoLib.Diagnostics.Windows`.

## Compile-Time Conventions

| Symbol | When active |
|--------|-------------|
| `NEKOLIB` | always |
| `NETFRAMEWORK` | net481 only |
| `NET_9` | net9.0 / net9.0-windows |
| `WINFORMS` | any WinForms-enabled TFM |
| `WINFORMS_NETFRAMEWORK` | WinForms + net481 |
| `WINFORMS_NET_9` | WinForms + net9.0-windows |

**Do not use the `record` keyword** in types shared across targets — C# 9
`record` needs `System.Runtime.CompilerServices.IsExternalInit`, which net481
lacks without an explicit shim. Use ordinary classes for data types in
multi-target projects.

## Nullable & ImplicitUsings — verified 2026-07-26

Read from the csproj files, not from memory. **Match a project's existing
settings; do not flip them.**

| Project | Nullable | ImplicitUsings |
|---|---|---|
| Core, Logger, DebugUtils, Diagnostics, Navigation (+WinForms, +Wpf), Data, Mvvm, Devices, Pipes, Watchdog (+Host), NekoLib | `enable` | disabled, **except** Pipes (`enable`) and Devices (`true`) |
| **Diagnostics.Windows** | **`disable`** | disabled |
| All `*.Tests.Unit` projects | `disable` | disabled |

The codebase carries ~330 pre-existing nullable warnings per target. Don't
introduce new ones.

## Module Notes

**Navigation** is the most complex module. Its canonical lifecycle order is
marked DO NOT CHANGE, and `NavigationContext`, `NavigationRuntime`,
`PageRegistry` and `PageFactory` are FROZEN — extensions live outside `Core/`.
**Read `src/Navigation/NekoLib.Navigation/README.md` before modifying it.**

**Data** — provider-neutral SQL gateway. `DatabaseGateway` is split across
partial classes by concern. **OleDb/Access caveat:** parameter binding on net481
with OleDb is position-dependent, not name-dependent; parameter name collisions
between a subquery and its parent can silently overwrite bindings. Data unit
tests use real database fixtures in `tests/NekoLib.Data.Tests/Shared/` —
`Pods.db` (SQLite) and `PodsDB/` (Access). **Do not mock the database layer.**

**Diagnostics** — `CrashHandler` (AppDomain/TaskScheduler hooks, crash bundle +
crash.txt + tails) with a pluggable `CrashDumpWriter`. OS-specific facilities
(`WindowsCrash.UseMiniDump()`, `WindowsCrash.HookWinForms()`, `CrashSuppressor`)
live in `NekoLib.Diagnostics.Windows`. Note the behavior change from A4: the
`Application.ThreadException` hook is **no longer automatic** — a WinForms app
must call `WindowsCrash.HookWinForms()` at startup.

**Mvvm** — intentionally minimal. `ViewModelBase` + `RelayCommand`/
`RelayCommand<T>` with safe `T` coercion. Works with both WinForms and WPF
binding.

## Test Layout Conventions

- Unit tests mirror the source module: `tests/NekoLib.{Module}.Tests/Unit/`.
- Test names follow `MethodName_Condition_ExpectedResult`.
- Navigation tests use fakes in `tests/NekoLib.Navigation.Tests/Unit/Fakes/`.
  Tests that mount the static `NavigationService` facade touch process-wide
  state: they must carry `[Collection("NavigationServiceFacade")]` and
  `await NavigationService.Shutdown()` in a `finally`.
  `DebugUtilsNavigationObserverFacadeTests` is the reference.
- `runtime_tests/` holds runnable WinForms/WPF scenario apps — launch them
  directly, never via `dotnet test`. (Currently gitignored; see the handoff.)
