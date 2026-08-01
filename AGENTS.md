# AGENTS.md

Working agreements for Codex in this repository.

**Documentation lives in [`README.md`](README.md)** — what NekoLib is, the module
map, targets, and the dependency graph. This file does not repeat it; it carries
the handoff state and the rules that are easy to get wrong.

| Need | Go to |
|---|---|
| What the framework is, module map, compatibility | [`README.md`](README.md) |
| Navigation internals — lifecycle, guards, adapters, APIs | [`src/Navigation/NekoLib.Navigation/README.md`](src/Navigation/NekoLib.Navigation/README.md) |
| Live roadmap and the current Inspection freeze | [`TODO.md`](TODO.md) |
| Verification taxonomy and canonical commands | [`tests/README.md`](tests/README.md) |
| Shared manual runtime scenarios | [`runtime_tests/README.md`](runtime_tests/README.md) |
| Documentation authority and lifecycle | [`docs/README.md`](docs/README.md) |
| Historical audit records (reverify findings against code/tests) | [`docs/audit/`](docs/audit/) |
| Completed roadmap history | [`docs/history/`](docs/history/) |

---

## Durable product and architecture context

- NekoLib targets small and medium PDV/DM applications: unattended,
  touch-first, single-window shells that may remain on `net481` or run on
  `net9.0`.
- Navigation is the main product surface. Its public static
  `NavigationService` facade is intentional for this application class, not a
  legacy shim to remove. `PageNavBootstrap.Start()` mounts it; always
  `await NavigationService.Shutdown()` before mounting a fresh context.
- `NekoLib.Core` is the required contract foundation for Navigation, Logging,
  Telemetry, Inspection, Diagnostics, and Watchdog. The other feature families
  are optional according to their actual project references; "optional" never
  means "has no dependencies".
- The source and the `*.csproj` files are authoritative. `TODO.md` and the audit
  files preserve decision/history context and may describe findings that were
  fixed later.

---

# Current architecture state — Phase D, 2026-08-01

- Capability vocabulary is now `Logging`, `Telemetry`, `Inspection`,
  `Diagnostics`, and `Diagnostics.Windows`. `NekoLib.Logger`,
  `NekoLib.DebugUtils`, `IDiagnosticsContext`, and `ObservabilityContext` are
  not compatibility surfaces; Phase D intentionally used a clean break.
- Logging is synchronous and ordered, with bounded recent snapshots and a
  rolling file sink. Telemetry stores bounded completed operations in memory;
  it does not persist raw operations in v1.
- Inspection retains the opt-in singleton-capable model. Module-facing
  `IInspectionRecorder` is separate from read-only
  `IInspectionSnapshotSource`; Diagnostics cannot invoke actions.
- Diagnostics owns incident evidence. Optional Logging, Telemetry, and
  Inspection sources are supplied through Core contracts, are collected with
  budgets and redaction, and may yield a partial bundle.
- Navigation is the first telemetry producer. `NavigationTimingContext` allows
  the application to report authentication completion; `page_ready` means the
  successful synchronous Navigation lifecycle, not first paint.
- Data and Devices were evaluated but not instrumented. They retain no Core
  project reference. The broad B4 Inspection rollout remains frozen.
- CRASH-01, CRASH-02, and WIN-01 remain review-only in
  `docs/audit/diagnostics-boundaries-review-2026-07-30.md`.

# ⚠ HISTORICAL HANDOFF — state as of 2026-07-27

A point-in-time snapshot written at handover. If it contradicts the code, the
code wins — and please correct it here.

**Validation reference commit:** not recorded; the previously uncommitted
product work described below later landed in `1727a1c`.

## Verified at handoff

`dotnet test NekoLib.sln`, Windows, 2026-07-27:

- **836/836 passing**, 0 failures and 0 skipped, across `net481` and
  `net9.0`/`net9.0-windows`.
- **553 build warnings, all pre-existing identities and predominantly nullable
  (CS86xx)** — counted by a full `-t:Rebuild` across both TFMs. A clean detached
  `HEAD` rebuild produced the former 665-warning baseline; the 2026-07-27 work
  removed 112 occurrences, and normalized warning comparison confirmed zero new
  identities. **Do not add new ones.**

## Local commits are NOT pushed

At the start of the 2026-07-27 task, `master` was level with `origin/master` and
the worktree was clean. The Watchdog attach/bootstrap and process-wide
DebugUtils changes from that task are uncommitted at handoff. **Run
`git log --oneline origin/master..HEAD` and `git status --short` first.**

## Where the work was heading

The minimal process-wide observability foundation was explicitly unfrozen and
completed on 2026-07-27. Broad module instrumentation remains **frozen**,
deliberately, not abandoned. When the next layer is unfrozen, the recommended
order is:

1. **Consumer bridge** — dump the ring buffer + `CaptureState()` into the
   `CrashHandler` crash bundle. This is what turns the module from "a buffer
   nobody reads" into a post-mortem tool, and it is the highest-value step.
2. **One real command case** — `RegisterCommand`/`TryInvokeCommand` has direct
   infrastructure tests but no feature-module registration. Validate one
   operational case before replicating the pattern across five modules.
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
  `NekoLib.DebugUtils`, and correlated Navigation observability.
- **Navigation lifecycle/trace correction (2026-07-27)** — an explicit limited
  unfreeze added pre-dispatch request tracing, linked guard attempts, monotonic
  stage timing, page/cache/background/surface/idle/runtime mirrors, and corrected
  history, lifecycle, teardown and concurrent-shutdown behavior. The stability-
  sensitive components are frozen again; see the Navigation README.
- **Devices audit closed** (`d352fa8`) — remaining `SerialCommTransport` items.
- **Documentation restructure (2026-07-26)** — `README.md` became the central
  doc, the Navigation README became the full technical reference, and three
  obsolete files were deleted (see below).
- **Local NuGet distribution (2026-07-26)** — the 13 library modules now pack
  independently under one coordinated version. `NekoLib.Watchdog.Host` is a
  tools/build deployment package with isolated net481, win-x86, and win-x64
  payloads. PackageReference-only WinForms/WPF consumers verify the graph.
- **Watchdog self-bootstrap/attach (2026-07-27)** — an application can start the
  deployed Host, hand off its current PID, verify a bounded PID/token handshake,
  and let the existing restart path supervise later child instances.
- **Process-wide DebugUtils foundation (2026-07-27)** — Core owns the NO-OP
  default slot, `DebugUtilsRuntime.EnableGlobal()` owns deterministic singleton
  activation/teardown, Navigation can opt into the slot, and DebugUtils now has
  direct dual-target tests.

## What is deliberately incomplete (the freeze)

Recorded in full in the freeze section of [`TODO.md`](TODO.md). Summary:

1. **B4 was never done.** Only Navigation emits. `Data`, `Pipes`, `Watchdog`,
   `Devices` and `Diagnostics` do not reference `IDebugUtils` at all. **Trap:**
   the `IntegrationDemo_481` runtime app shows `Data/*` and `Pipes/*` operations
   in the ring buffer — that is *the app* calling `Record` by hand, not the
   libraries emitting. Swap the app and the instrumentation is gone.
2. **No feature module uses the command channel.**
   `RegisterCommand`/`TryInvokeCommand` now have direct infrastructure tests, but
   there is still no real module registration.
3. **No reusable consumer surface.** No viewer, no `ILogSink`/file bridge,
   nothing in the crash bundle. Every app wires its own.
4. **Navigation exposes no DebugUtils commands.** That is deliberate until an
   async/cancellation/timeout/UI-marshalling command contract exists. Page
   presence events and transient-blank suppression now have headless regressions.

## Open items elsewhere

The audit files are dated snapshots, not a live issue tracker. The status below
was reconciled against the current source and tests on 2026-07-26; reverify it
before making a change.

| Module | Current status | Historical detail |
|---|---|---|
| Watchdog | Update orchestration is explicitly `not_implemented`; the truncated SHA1 pipe identity, silent 300-entry replay-buffer eviction and relative fatal-log path remain. Application bootstrap/attach and Host argument preservation are implemented. App-log forwarding (old M7) and bring-to-front (old L7) are implemented now. | `docs/audit/watchdog-first-pass.md` |
| Pipes | Per-subscriber bounded event queue/drop policy, pipe ACL/security and graceful in-flight drain on `Dispose` remain future hardening. | `docs/audit/pipes-first-pass.md` |
| Devices | The four listed review items were all closed by `d352fa8`: nullable `ReadLine` timeout, config validation, `ThrowIfDisposed`, and documented ASCII behavior. The remaining gap is real serial I/O through a COM-port emulator/runtime scenario. | `docs/audit/devices-first-pass.md` |
| Data | The audit is materially stale: #1 (`NETFRAMEWORK` OleDb guard), #5 (subquery collision), #6 (DML build idempotence), and #21 (conditional event clearing) are fixed; #5/#6 have unit tests. Reverify every other finding before treating it as open. | `docs/audit/data-first-pass.md` |
| Navigation | NEW-12 namespace ergonomics and the last interactive prompt-close probe remain. NEW-13 `PageMetadataBuilder.Register<T>` and `AllowAnonymous` runtime enforcement are fixed. | `docs/audit/navigation-audit.md` |

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
- `DebugUtilsNavigationObserver` receives `NavigationStarted` at API entry,
  **before** UI dispatch and navigation-gate waiting. Public
  `NavigationService.Navigating` fires after descriptor lookup, with effective
  args, **before** guard evaluation. Redirects are child attempts under one
  request; one request terminal follows the full lifecycle. Subscriber exceptions
  are isolated and guards retain their 30-second timeout.
- Overlay teardown is intentionally asymmetric: Toast uses
  `DismissCurrentToast()`; Dialog, Prompt and Popover use `CloseAll()`.

---

# Working rules

## Build & test

```bash
dotnet build NekoLib.sln
dotnet test NekoLib.sln
```

Create and verify a new immutable local package version:

```powershell
.\eng\pack-local.ps1 -PackageVersion 1.0.0-local.3
```

Do not overwrite a version already present in `artifacts/local-feed`. The script
requires a clean Git worktree so package provenance matches the commit. It is
the canonical pack entry point because it publishes the RID-specific Watchdog
Host payloads before packing and runs clean package-consumer probes. Use
`-AllowDirty` only for disposable validation versions. Direct
`dotnet pack NekoLib.sln` intentionally omits the Host unless
`NekoLibWatchdogHostPayloadRoot` is supplied.

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

## Layering and project references

Do not impose a repository-wide `Contracts` / `Runtime` / `Adapters` folder
template. That three-part architecture belongs to the Navigation family:
Navigation contains contracts and runtime, while the WinForms/WPF projects are
its platform adapters. Other modules use structures suited to their own
domains.

The current cross-project graph is shallow and is the rule to preserve:

- Navigation → Core; Navigation.WinForms/Wpf → Navigation.
- Logging → Core; Telemetry → Core; Inspection → Core.
- Diagnostics → Core; Diagnostics.Windows → Diagnostics.
- Watchdog → Core + Pipes; Watchdog.Host → Watchdog.
- Data, Devices, Mvvm, Pipes and the orphan Hosting project have no
  project references.

Read the relevant `*.csproj` before adding a cross-module dependency. Do not
infer that every feature module must reference Core.

## Compile-time constants

Custom constants are **not uniform**. Inspect the target project's csproj
instead of copying a symbol from another module.

- Core, Data, Inspection, Diagnostics, Logging, Telemetry, Mvvm, Navigation and Hosting
  declare `NEKOLIB` plus their conditional `NETFRAMEWORK` / `NET_9` symbols.
- Devices declares `NETFRAMEWORK` / `NET_9`, but not `NEKOLIB`.
- Pipes uses `NET481` / `NET9`.
- Watchdog uses `NETFRAMEWORK` on net481 and `NET_9;NET9` on net9; it does not
  declare `NEKOLIB`. Watchdog.Host declares no custom symbols.
- Navigation.WinForms has its own `WINFORMS_NETFRAMEWORK` and
  `WINFORMS` / `WINFORMS_NET_9` split. Navigation.Wpf declares no custom
  symbols.
- Diagnostics.Windows has its own `WINFORMS` define in addition to its TFM
  symbols.

**Never use the `record` keyword** in types shared across targets — C# 9 `record`
needs `System.Runtime.CompilerServices.IsExternalInit`, which net481 lacks
without an explicit shim. Use ordinary classes for multi-target data types.

## Nullable & ImplicitUsings — read from the csproj files, 2026-08-01

**Match a project's existing settings; never flip them.**

| Project | Nullable | ImplicitUsings |
|---|---|---|
| Core, Logging, Telemetry, Inspection, Diagnostics, Navigation (+WinForms, +Wpf), Data, Mvvm, Devices, Pipes, Watchdog (+Host), NekoLib | `enable` | disabled, **except** Pipes (`enable`) and Devices (`true`) |
| **Diagnostics.Windows** | **`disable`** | disabled |
| All `*.Tests.Unit` projects | `disable` | disabled |

## Tests

- `tests/README.md` owns the verification taxonomy. A test inside a `Unit/`
  project may still be integration-scoped when it uses real filesystem, IPC,
  network, or process boundaries.
- Unit tests mirror the source module: `tests/NekoLib.{Module}.Tests/Unit/`.
- Names follow `MethodName_Condition_ExpectedResult`.
- `tests/NekoLib.Data.Tests/Shared/Pods.db` and `PodsDB` are tracked legacy
  fixtures even though the directory also matches `.gitignore`. Current
  versioned tests do not reference either fixture by name. Do not cite them as
  executed database coverage until a test deliberately wires and verifies them.
- **Navigation tests that mount the static facade** touch process-wide state:
  they must carry `[Collection("NavigationServiceFacade")]` and
  `await NavigationService.Shutdown()` in a `finally`.
  `InspectionNavigationObserverFacadeTests` is the reference.
- `runtime_tests/` holds versioned runnable scenario apps with procedures in
  `runtime_tests/README.md`. Build and launch them explicitly, never through
  `dotnet test`. Machine-only experiments belong under ignored `.local/`.

## Reviews and audit artifacts

- Treat a review, audit or assessment as read-only with respect to product code
  unless the request explicitly includes fixes. Creating a requested review
  artifact is allowed; silently implementing its recommendations is not.
- State the exact baseline before reporting findings: commit/ref, scope, and
  whether the review covers `HEAD`, the working tree, or both. If the tree is
  dirty, distinguish pre-existing changes from the reviewed change set.
- Verify findings against current source, project files and executable tests.
  Historical documents provide leads and decision context, not current facts.
- Separate observed facts, risks/hypotheses, accepted decisions and rejected
  alternatives. Prioritize actionable findings by impact and cite tight
  file/line evidence; if no findings remain, say so and identify residual
  validation gaps.
- When a durable module or architecture review is requested, write it in
  English under `docs/audit/` with its date, reference commit, scope, evidence
  and validation. The review is a snapshot, never the live issue tracker.
- Use this promotion flow:
  **review/audit → accepted decisions → `TODO.md` → implementation → current
  technical documentation**. During investigation, `TODO.md` may track the
  review itself, but must not list speculative fixes as confirmed work.
- Promote a finding to `TODO.md` only after the code confirms it, a direction is
  chosen and the work is actually intended. Keep rejected alternatives and
  their rationale in the review; keep open work authoritative in one roadmap.
- Once a review is complete, mark it historical and preserve its original
  findings. Record later outcomes in a short reconciliation section or index
  instead of rewriting the snapshot as though it knew later fixes.

## Module gotchas

- **Navigation** — the canonical lifecycle order is marked DO NOT CHANGE.
  `NavigationContext`, `NavigationRuntime`, `PageRegistry` and `PageFactory` are
  stability-sensitive and frozen again after the explicit 2026-07-27
  lifecycle/trace correction. **Read the Navigation README before modifying the
  module.** Await `NavigationService.Shutdown()`. Navigation and reset operations
  use the navigation gate; Dialog/Prompt/Popover deliberately marshal to UI
  without taking that gate.
- **Data** — on net481 with OleDb, parameter binding is position-dependent, not
  name-dependent. `ApplyParameters` now activates the OleDb path with
  `NETFRAMEWORK`, and `QueryBuilder` now isolates subquery parameters and builds
  INSERT/UPDATE idempotently; keep the existing regression tests when changing
  those paths.
- **Diagnostics** — since A4 the `Application.ThreadException` hook is **no
  longer automatic**; a WinForms app must call `WindowsCrash.HookWinForms()` at
  startup.
