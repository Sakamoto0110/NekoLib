# AGENTS.md

Working agreements for Codex in this repository.

**Documentation lives in [`README.md`](README.md)** — what NekoLib is, the module
map, targets, and the dependency graph. This file does not repeat it; it carries
the handoff state and the rules that are easy to get wrong.

| Need | Go to |
|---|---|
| What the framework is, module map, compatibility | [`README.md`](README.md) |
| Navigation internals — lifecycle, guards, adapters, APIs | [`src/Navigation/NekoLib.Navigation/README.md`](src/Navigation/NekoLib.Navigation/README.md) |
| Roadmap, phase plan, observability freeze | [`TODO.md`](TODO.md) |
| Per-module audits and open items | [`docs/audit/`](docs/audit/), [`src/Data/NekoLib.Data/DataAudit.md`](src/Data/NekoLib.Data/DataAudit.md) |

---

# ⚠ HANDOFF — state as of 2026-07-26

A point-in-time snapshot written at handover. If it contradicts the code, the
code wins — and please correct it here.

## Verified at handoff

`dotnet test NekoLib.sln`, Windows, 2026-07-26:

- **478/478 passing**, 0 failures, across `net481` and `net9.0`/`net9.0-windows`.
- **665 build warnings, all pre-existing nullable (CS86xx)** — counted across
  both TFMs, so roughly half that per target. Concentrated in
  `NekoLib.Navigation` (304), `NekoLib.Watchdog` (140),
  `NekoLib.Navigation.WinForms` (80). **Do not add new ones.**

## Local commits are NOT pushed

`master` was level with `origin/master` at `d352fa8`; everything since sits
locally only. **Run `git log --oneline origin/master..HEAD` first.** If you are
working from a fresh clone of the remote you do not have any of it — you have a
pre-2026-07-26 repo without the extended observability hooks, without the freeze
record, and without the documentation restructure.

## Where the work was heading

The observability module is **frozen**, deliberately, not abandoned. When it is
unfrozen, the recommended order is:

1. **Consumer bridge** — dump the ring buffer + `CaptureState()` into the
   `CrashHandler` crash bundle. This is what turns the module from "a buffer
   nobody reads" into a post-mortem tool, and it is the highest-value step.
2. **One real command case** — `RegisterCommand`/`TryInvokeCommand` is a third of
   the interface and has never been exercised. Validate it once before
   replicating the pattern across five modules.
3. **B4 per module** — start with `Data` (the `QueryExecutionContext` events are
   already the seam) and `Pipes` (`IPipeMetrics` is already the extension point).

Do not extend observability without an explicit decision to unfreeze.

## What was just completed

- **Phase A (A1–A8).** Created `NekoLib.Core` (contracts, zero deps),
  `NekoLib.Logger` (concrete logging), and `NekoLib.Diagnostics.Windows`
  (dbghelp/kernel32 PInvoke split out of `NekoLib.Diagnostics`). Unblocked
  `net9.0` on the modules that do not need Windows. Inverted the `CrashHandler`
  dump writer to avoid a Diagnostics ↔ Diagnostics.Windows cycle.
- **B1/B2/B3** — `IDebugUtils` in Core, `DebugUtilsRuntime` in
  `NekoLib.DebugUtils`, and the Navigation observer pilot.
- **Extended Navigation telemetry (2026-07-26)** — the observer now has two
  fidelity levels; see the Diagnostics section of the Navigation README.
- **Devices audit closed** (`d352fa8`) — remaining `SerialCommTransport` items.
- **Documentation restructure (2026-07-26)** — `README.md` became the central
  doc, the Navigation README became the full technical reference, and three
  obsolete files were deleted (see below).

## What is deliberately incomplete (the freeze)

Recorded in full in the freeze section of [`TODO.md`](TODO.md). Summary:

1. **B4 was never done.** Only Navigation emits. `Data`, `Pipes`, `Watchdog`,
   `Devices` and `Diagnostics` do not reference `IDebugUtils` at all. **Trap:**
   the `IntegrationDemo_481` runtime app shows `Data/*` and `Pipes/*` operations
   in the ring buffer — that is *the app* calling `Record` by hand, not the
   libraries emitting. Swap the app and the instrumentation is gone.
2. **The command channel is dead.** `RegisterCommand`/`TryInvokeCommand` have
   zero registrations, invocations or tests in the whole repo.
3. **No reusable consumer surface.** No viewer, no `ILogSink`/file bridge,
   nothing in the crash bundle. Every app wires its own.
4. **`NekoLib.DebugUtils` has no test project.** Ring-buffer eviction is covered
   only indirectly via the Navigation observer; `ClearOperations`, `CommandKeys`,
   the command channel and concurrency are untested.
5. **`NoPageAttached`/`NoPageVisible` are wired but untested** — firing them
   deterministically needs a real host, not the test fakes.

## Open items elsewhere

Pre-existing, not part of the observability work. Each audit has the detail; do
not re-derive them.

| Module | Open | Where |
|---|---|---|
| Watchdog | M5 (update mechanism — genuinely unimplemented), M8 (pipe-name hash collision), M9 (ring-buffer silent drop), L4, L5, L6. M7 + L7 deferred by decision | `docs/audit/watchdog-first-pass.md` |
| Pipes | per-subscriber bounded event queue + drop policy, pipe ACL/security, graceful drain on `Dispose`. All H/M/L findings closed | `docs/audit/pipes-first-pass.md` |
| Devices | `ReadLine` timeout semantics, `SerialConfig` validation, `ThrowIfDisposed()`, `RawText` ASCII decision — *some may be closed by `d352fa8`, verify* | `docs/audit/devices-first-pass.md` |
| Data | next pass is verification-by-test: OleDb parameter order, `QueryBuilder.Build()` mutation on INSERT/UPDATE, `WhereExists` collision, telemetry masking decision | `src/Data/NekoLib.Data/DataAudit.md` |
| Navigation | NEW-12, NEW-13 (cosmetic API smells); one manual probe in §2.8 | `docs/audit/navigation-audit.md` |
| Navigation | `PageDescriptor.AllowAnonymous` is stored but **never consulted by the runtime** — guards on the descriptor always run | — |

## Deleted on 2026-07-26 — do not resurrect

Removed because every type they described was verified absent from the repo:

- **`MIGRATION_NOTES.md`** — documented a `NekoLib.Diagnostics.Abstractions`
  project (it became `NekoLib.Core`), plus `DiagnosticsRuntime`,
  `IDiagnosticContext`, `Neko.CreateBuilder()`, `NekoHost` and
  `IDiagnosticsBuilder`. None exist. Its one still-true item, the Pipes
  `connection_closed` breaking change, survives in
  `docs/audit/pipes-first-pass.md`.
- **`src/Navigation/NekoLib.Navigation/TODO.md`** — dated 2026-02-25, truncated
  mid-code-block, broken encoding. Referred to `PageTimeoutController`,
  `WinFormsPageTimeoutAdapter`, `IPageTimeoutService` and
  `IPageTimeoutServiceFactory`; that file was their only mention anywhere.
  Timeout handling became the idle system.
- **`codex_readme.md`** — its content was the good Navigation documentation and
  has been promoted into `src/Navigation/NekoLib.Navigation/README.md`.

## What you will NOT see in a fresh clone

`.gitignore`d on purpose. If you are told these exist and cannot find them, this
is why — **do not recreate them**:

- **All of `runtime_tests/`** — six WinForms/WPF scenario apps, none of which are
  in `NekoLib.sln` either. `IntegrationDemo_481` is the best worked example of the
  current API surface.
- **`CLAUDE.md`** at the root and under `src/Data/` and `src/Navigation/` —
  guidance files for the other assistant.

## Facts that contradict older material

- `src/Hosting/NekoLib/` is **not in `NekoLib.sln`** and holds only four
  constants files. It is **not** a bootstrap facade — there is no
  `Neko.CreateBuilder()` or `NekoHost`.
- `NekoLib.Diagnostics.Windows` is the **one `src/` project still on
  `<Nullable>disable</Nullable>`** — created in A4, after the A7 sweep had
  already listed the projects to flip, so it was missed.
- `NekoLib.Data`, `NekoLib.Devices`, `NekoLib.Mvvm`, `NekoLib.Pipes` and
  `NekoLib.Diagnostics` reference **no other project at all** — in particular
  Data and Devices do not know `NekoLib.Core`.

---

# Working rules

## Build & test

```bash
dotnet build NekoLib.sln
dotnet test NekoLib.sln
```

Single project, single TFM, single test:

```bash
dotnet build src/Navigation/NekoLib.Navigation/NekoLib.Navigation.csproj
dotnet build src/Data/NekoLib.Data/NekoLib.Data.csproj -f net481
dotnet test tests/NekoLib.Navigation.Tests/Unit/ --filter "FullyQualifiedName~TestClassName.MethodName"
```

Verify conditional-compilation constants for a TFM:

```bash
dotnet msbuild src/Data/NekoLib.Data/NekoLib.Data.csproj -getProperty:DefineConstants -p:TargetFramework=net481
```

**No CI/CD** — builds are manual. `net481` and every `-windows` target build on
Windows only, so a Linux/container environment cannot validate this repo; say so
rather than reporting a partial build as green.

`src/Tools/BundlerTool/` is not in the solution; build it directly.

## Layering — enforced by project references

`Adapters` → `Runtime` → `Contracts`, downward only. Feature modules reference
contracts, never runtime classes from sibling modules.

- Feature modules may reference only `NekoLib.Core`. The one documented
  exception is `NekoLib.Watchdog`, which also references `NekoLib.Pipes`.
- Only the entrypoint/hosting project may reference the concrete
  `NekoLib.Diagnostics` runtime. In practice nothing does today except
  `NekoLib.Diagnostics.Windows`.

Each module is `Contracts/` (pure interfaces and data-only types),
`Runtime/` (implementations), and `Adapters/` (platform glue, Navigation
platform projects only).

## Compile-time constants

| Symbol | When active |
|--------|-------------|
| `NEKOLIB` | always |
| `NETFRAMEWORK` | net481 only |
| `NET_9` | net9.0 / net9.0-windows |
| `WINFORMS` | any WinForms-enabled TFM |
| `WINFORMS_NETFRAMEWORK` | WinForms + net481 |
| `WINFORMS_NET_9` | WinForms + net9.0-windows |

**Never use the `record` keyword** in types shared across targets — C# 9 `record`
needs `System.Runtime.CompilerServices.IsExternalInit`, which net481 lacks
without an explicit shim. Use ordinary classes for multi-target data types.

## Nullable & ImplicitUsings — read from the csproj files, 2026-07-26

**Match a project's existing settings; never flip them.**

| Project | Nullable | ImplicitUsings |
|---|---|---|
| Core, Logger, DebugUtils, Diagnostics, Navigation (+WinForms, +Wpf), Data, Mvvm, Devices, Pipes, Watchdog (+Host), NekoLib | `enable` | disabled, **except** Pipes (`enable`) and Devices (`true`) |
| **Diagnostics.Windows** | **`disable`** | disabled |
| All `*.Tests.Unit` projects | `disable` | disabled |

## Tests

- Unit tests mirror the source module: `tests/NekoLib.{Module}.Tests/Unit/`.
- Names follow `MethodName_Condition_ExpectedResult`.
- **Data tests use real database fixtures** in `tests/NekoLib.Data.Tests/Shared/`
  — `Pods.db` (SQLite) and `PodsDB/` (Access). **Do not mock the database layer.**
- **Navigation tests that mount the static facade** touch process-wide state:
  they must carry `[Collection("NavigationServiceFacade")]` and
  `await NavigationService.Shutdown()` in a `finally`.
  `DebugUtilsNavigationObserverFacadeTests` is the reference.
- `runtime_tests/` holds runnable `.exe` scenario apps — launch them directly,
  never via `dotnet test`.

## Module gotchas

- **Navigation** — the canonical lifecycle order is marked DO NOT CHANGE, and
  `NavigationContext`, `NavigationRuntime`, `PageRegistry` and `PageFactory` are
  FROZEN; extensions live outside `Core/`. **Read the Navigation README before
  modifying the module.**
- **Data** — on net481 with OleDb, parameter binding is position-dependent, not
  name-dependent. Parameter name collisions between a subquery and its parent
  can silently overwrite bindings.
- **Diagnostics** — since A4 the `Application.ThreadException` hook is **no
  longer automatic**; a WinForms app must call `WindowsCrash.HookWinForms()` at
  startup.
