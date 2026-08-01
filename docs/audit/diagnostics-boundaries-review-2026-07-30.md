# Diagnostics Sector Boundary and Naming Review

**Kind:** audit

**Lifecycle:** current

**Status:** in progress; core boundary decisions promoted to the roadmap

**Review date:** 2026-07-30

**Decision promotion date:** 2026-08-01

**Reference ref:** `master` / `HEAD`

**Reference commit:** `1727a1cac3f66666b2df02bc618ad6ab45807a49`

**Product-code coverage:** reviewed source matches the reference commit

**Open-work authority:** [`TODO.md`](../../TODO.md)

## Baseline and working-tree state

This review covers the committed product source at `HEAD` plus the current
working-tree documentation that governs the review. The working tree was dirty
before this review began:

- `AGENTS.md`, `README.md`, and `TODO.md` had pre-existing documentation
  changes.
- Devices source and tests had pre-existing tracked and untracked changes.
- `.agents/` and `.claude/` contained pre-existing untracked local material.
- There were no pre-existing changes under `src/Diagnostics`, `src/Core`,
  `src/DebugUtils`, `tests/NekoLib.Diagnostics.Tests`, or
  `tests/NekoLib.DebugUtils.Tests`.

This review does not treat the unrelated working-tree changes as reviewed
product behavior. The only changes authorized by the review itself are this
artifact and a roadmap item that tracks completion of the review.

## Scope

### Included

- `src/Diagnostics/**`
- `src/Core/NekoLib.Core/Diagnostics/**`
- `src/Core/NekoLib.Core/Observability/**`
- `src/DebugUtils/NekoLib.DebugUtils/**`
- Direct product consumers in Navigation and Watchdog
- Relevant unit tests, package-consumer probes, project references, and local
  packaging scripts
- Naming, responsibility boundaries, cross-platform placement, and the
  interaction between logging, telemetry, crash reporting, and DebugUtils

### Excluded

- Product-code fixes or renames
- Broad feature-module instrumentation
- A complete Linux crash-dump design
- Ignored runtime scenarios, which are not clean-clone evidence
- Unrelated Devices changes already present in the working tree

## Review constraints and accepted decisions

The following are inputs to the review rather than findings awaiting promotion:

1. Feature modules should depend on stable contracts in `NekoLib.Core`, not on
   the concrete Logger, DebugUtils, or crash-reporting packages.
2. `NekoLib.Diagnostics.Windows` is intentionally platform-specific and should
   remain the isolated home for Windows-only behavior. A future Linux adapter
   must not dilute that boundary.
3. NekoLib should remain appropriate for small and medium applications while
   leaving a clear path to larger deployments. Enterprise infrastructure,
   generic hosts, or extra adapter assemblies require a demonstrated use case.
4. DebugUtils remains opt-in, in-process, and more intrusive than ordinary
   logging. It must not become a second severity-based logger.
5. `ObservabilityContext` is not an accepted public name. The word
   "observability" may describe an industry concept, but it is too broad for
   the current `ILogger` plus `ITelemetrySink` container.
6. The broad observability freeze remains active. This review may describe the
   DebugUtils/crash-reporting bridge but does not authorize its implementation.

## Current project topology

| Project | Targets | Product references | Current responsibility |
|---|---|---|---|
| `NekoLib.Core` | `net481;net9.0` | none | Shared logging, telemetry, diagnostics-context, and DebugUtils contracts plus null implementations |
| `NekoLib.Logger` | `net481;net9.0` | Core | Severity filtering, synchronous sink fan-out, a debugger sink, an in-memory telemetry sink, and the concrete `Diagnostics` context |
| `NekoLib.Diagnostics` | `net481;net9.0` | none | Process-wide exception hooks and best-effort crash-bundle creation |
| `NekoLib.Diagnostics.Windows` | `net481;net9.0-windows` | Diagnostics | WinForms exception forwarding, WER suppression, and `dbghelp` minidumps |
| `NekoLib.DebugUtils` | `net481;net9.0` | Core | Process-wide optional operation buffer, state providers, commands, and runtime diagnostics |

The project files establish the intended shallow graph:

```text
Feature modules ───────────────> NekoLib.Core contracts
NekoLib.Logger ────────────────> NekoLib.Core
NekoLib.DebugUtils ────────────> NekoLib.Core
NekoLib.Diagnostics.Windows ───> NekoLib.Diagnostics
NekoLib.Diagnostics ───────────> no NekoLib project
```

Evidence:

- [`NekoLib.Logger.csproj`](../../src/Diagnostics/NekoLib.Logger/NekoLib.Logger.csproj#L38)
- [`NekoLib.DebugUtils.csproj`](../../src/DebugUtils/NekoLib.DebugUtils/NekoLib.DebugUtils.csproj#L32)
- [`NekoLib.Diagnostics.csproj`](../../src/Diagnostics/NekoLib.Diagnostics/NekoLib.Diagnostics.csproj)
- [`NekoLib.Diagnostics.Windows.csproj`](../../src/Diagnostics/NekoLib.Diagnostics.Windows/NekoLib.Diagnostics.Windows.csproj#L43)
- [`NekoLib.Navigation.csproj`](../../src/Navigation/NekoLib.Navigation/NekoLib.Navigation.csproj#L55)
- [`NekoLib.Watchdog.csproj`](../../src/Watchdog/NekoLib.Watchdog/NekoLib.Watchdog.csproj#L32)

## Current behavior

### Core contracts

`NekoLib.Core.Diagnostics` owns `ILogger`, `ILogSink`, `LogEntry`,
`LogLevel`, `ITelemetrySink`, `TelemetryEvent`, and `IDiagnosticsContext`.
`IDiagnosticsContext` is only a two-property container for a logger and a
telemetry sink. Null objects allow consumers to avoid null checks.

`NekoLib.Core.Observability` separately owns `IDebugUtils`,
`NullDebugUtils`, and the process-wide `DebugUtilsProvider`.

Evidence:

- [`IDiagnosticsContext.cs`](../../src/Core/NekoLib.Core/Diagnostics/IDiagnosticsContext.cs#L3)
- [`ILogger.cs`](../../src/Core/NekoLib.Core/Diagnostics/ILogger.cs#L5)
- [`ITelemetrySink.cs`](../../src/Core/NekoLib.Core/Diagnostics/ITelemetrySink.cs#L3)
- [`IDebugUtils.cs`](../../src/Core/NekoLib.Core/Observability/IDebugUtils.cs#L14)
- [`DebugUtilsProvider.cs`](../../src/Core/NekoLib.Core/Observability/DebugUtilsProvider.cs#L10)

### Logger

`NekoLib.Logger.Logger` applies a minimum severity, creates one `LogEntry`
with a UTC timestamp, forwards it synchronously to every configured sink, and
isolates each sink failure. With no sink, an accepted log entry is discarded.

The package includes:

- `DebugLogSink`, which writes to `System.Diagnostics.Debug`;
- `MemoryTelemetrySink`, which stores telemetry events for tests/debugging;
- `Diagnostics`, which combines an `ILogger` and an `ITelemetrySink` and
  substitutes Core null objects.

Evidence:

- [`Logger.cs`](../../src/Diagnostics/NekoLib.Logger/Logger.cs#L11)
- [`DebugLogSink.cs`](../../src/Diagnostics/NekoLib.Logger/Sinks/DebugLogSink.cs#L9)
- [`MemoryTelemetrySink.cs`](../../src/Diagnostics/NekoLib.Logger/Sinks/MemoryTelemetrySink.cs#L10)
- [`Diagnostics.cs`](../../src/Diagnostics/NekoLib.Logger/Diagnostics.cs#L5)

### Cross-platform crash reporting

`CrashHandler.Install()` registers the instance in a process-wide registry and
installs `AppDomain.UnhandledException` and
`TaskScheduler.UnobservedTaskException` hooks once. An unobserved task
exception is marked observed after dispatch.

Each handler prevents reentrant processing, raises isolated subscriber events,
writes `crash.txt`, optionally invokes a dump delegate, copies configured file
tails, and optionally notifies an external Watchdog integration. Crash-path
failures are intentionally swallowed.

Evidence:

- [`CrashHandler.cs`](../../src/Diagnostics/NekoLib.Diagnostics/CrashHandler.cs#L74)
- [`CrashHandler.cs`](../../src/Diagnostics/NekoLib.Diagnostics/CrashHandler.cs#L100)
- [`CrashHandler.cs`](../../src/Diagnostics/NekoLib.Diagnostics/CrashHandler.cs#L135)
- [`CrashHandler.cs`](../../src/Diagnostics/NekoLib.Diagnostics/CrashHandler.cs#L159)
- [`CrashHandler.cs`](../../src/Diagnostics/NekoLib.Diagnostics/CrashHandler.cs#L211)

### Windows crash adapter

`WindowsCrash.UseMiniDump()` installs the internal `MiniDumpWriter` delegate.
`WindowsCrash.HookWinForms()` forwards `Application.ThreadException` to the
cross-platform handler. `CrashSuppressor.Enable()` sets process-wide Windows
error modes.

Evidence:

- [`WindowsCrash.cs`](../../src/Diagnostics/NekoLib.Diagnostics.Windows/WindowsCrash.cs#L15)
- [`DumpWritter.cs`](../../src/Diagnostics/NekoLib.Diagnostics.Windows/DumpWritter.cs#L8)
- [`CrashSupressor.cs`](../../src/Diagnostics/NekoLib.Diagnostics.Windows/CrashSupressor.cs#L10)

### DebugUtils

`IDebugUtils` is the feature-module-facing push/register contract. The concrete
runtime owns the pull/consume surface:

- bounded sequenced operation snapshots;
- state-provider snapshots;
- command lookup and invocation;
- scalar runtime diagnostics;
- one optional process-wide installation.

It has no severity levels and no persistence. Calls occur inline, so modules
must keep emitted payload construction and state providers bounded.

Evidence:

- [`IDebugUtils.cs`](../../src/Core/NekoLib.Core/Observability/IDebugUtils.cs#L14)
- [`DebugUtilsRuntime.cs`](../../src/DebugUtils/NekoLib.DebugUtils/DebugUtilsRuntime.cs#L26)
- [`DebugUtilsRuntime.cs`](../../src/DebugUtils/NekoLib.DebugUtils/DebugUtilsRuntime.cs#L103)
- [`DebugUtilsRuntime.cs`](../../src/DebugUtils/NekoLib.DebugUtils/DebugUtilsRuntime.cs#L193)

## Consumer interaction with Core

### Navigation

Navigation references only Core. `UseDiagnostics(IDiagnosticsContext)` accepts
the Core contract; `DiagnosticsNavigationSink` emits an informational log and a
telemetry event for navigation, plus a warning for guard denial. It does not
reference `NekoLib.Logger`.

`UseDebugUtils(IDebugUtils)` also accepts the Core contract. Navigation tests
reference the concrete DebugUtils package to verify the integration, but the
Navigation product project does not.

Evidence:

- [`PageNavBootstrap.cs`](../../src/Navigation/NekoLib.Navigation/Bootstrap/PageNavBootstrap.cs#L101)
- [`DiagnosticsNavigationSink.cs`](../../src/Navigation/NekoLib.Navigation/Diagnostics/DiagnosticsNavigationSink.cs#L13)
- [`NekoLib.Navigation.csproj`](../../src/Navigation/NekoLib.Navigation/NekoLib.Navigation.csproj#L55)

### Watchdog

Watchdog references Core and Pipes, not Logger or Diagnostics. It consumes
`ILogSink`, `ITelemetrySink`, and `LogEntry` from Core and provides its own
`WatchdogPipeLogSink`.

The Watchdog test project references `NekoLib.Diagnostics` to verify
composition between `CrashHandler.ExternalNotifier` and Watchdog. This is test
and integration-root coupling, not a Watchdog product dependency.

Evidence:

- [`WatchdogOptions.cs`](../../src/Watchdog/NekoLib.Watchdog/WatchdogOptions.cs#L48)
- [`WatchdogPipeLogSink.cs`](../../src/Watchdog/NekoLib.Watchdog/WatchdogPipeLogSink.cs#L20)
- [`NekoLib.Watchdog.csproj`](../../src/Watchdog/NekoLib.Watchdog/NekoLib.Watchdog.csproj#L32)
- [`NekoLib.Watchdog.Tests.Unit.csproj`](../../tests/NekoLib.Watchdog.Tests/Unit/NekoLib.Watchdog.Tests.Unit.csproj#L28)

### Boundary result

The requested dependency rule is already true for the inspected product
projects:

- feature modules depend on Core contracts;
- concrete implementations are selected by the application/composition root;
- the Windows adapter is the intentional implementation-level exception;
- test projects may reference concrete implementations for integration
  verification.

This graph is a positive finding and should be preserved through any rename.

## Confirmed findings

The findings remain the evidence snapshot. Accepted implementation work is
owned by [TODO Phase D](../../TODO.md#phase-d--logging-telemetry-diagnostics-and-inspection-boundaries),
not duplicated here. Findings without an accepted direction remain review-only.

### DGN-01 — HIGH — "Diagnostics" has multiple incompatible public meanings

**Classification:** naming and module boundary

**Status:** confirmed; direction accepted and promoted to TODO Phase D

Observed meanings include:

- `NekoLib.Core.Diagnostics`: logging and telemetry contracts;
- `NekoLib.Logger.Diagnostics`: a logger-plus-telemetry container;
- `NekoLib.Diagnostics`: crash reporting;
- `NekoLib.Navigation.Diagnostics`: Navigation-local trace infrastructure;
- "runtime diagnostics" inside DebugUtils.

The user cannot infer whether "Diagnostics" means ordinary logging, telemetry,
runtime tracing, or post-mortem crash collection. The ambiguity is visible in
the package and type names, not only in documentation.

**Decision outcome:** accepted capability vocabulary is `Logging`, `Telemetry`,
`Inspection`, `Diagnostics`, and `Diagnostics.Windows`. The roadmap owns the
rename and migration work.

### CORE-01 — HIGH — `TelemetryEvent` is not a valid `LogEntry` specialization

**Classification:** Core model correctness and boundary

**Status:** confirmed; correction accepted and promoted to TODO Phase D

`TelemetryEvent` inherits `LogEntry`, redeclares `TimestampUtc` with `new`, and
does not initialize the base `LogEntry` state. A consumer that sees the value
as `LogEntry` observes the base timestamp and other base fields at their
defaults rather than the telemetry values.

This inheritance also states that telemetry is a kind of log entry, while the
public API exposes independent `ILogSink` and `ITelemetrySink` channels.

Evidence:

- [`TelemetryEvent.cs`](../../src/Core/NekoLib.Core/Diagnostics/TelemetryEvent.cs#L6)
- [`LogEntry.cs`](../../src/Core/NekoLib.Core/Diagnostics/LogEntry.cs#L9)

**Decision outcome:** telemetry is independent from logging and must not inherit
from `LogEntry`.

### BND-01 — HIGH — `NekoLib.Logger` owns more than logging

**Classification:** package boundary

**Status:** confirmed; direction accepted and promoted to TODO Phase D

The package contains the Logger implementation, a telemetry implementation,
and the concrete implementation of `IDiagnosticsContext`. Its public name
communicates one capability while its surface implements three.

This does not currently force feature projects to reference the package, but it
makes the composition API and package selection difficult to explain.

Evidence:

- [`NekoLib.Logger.csproj`](../../src/Diagnostics/NekoLib.Logger/NekoLib.Logger.csproj)
- [`Diagnostics.cs`](../../src/Diagnostics/NekoLib.Logger/Diagnostics.cs#L5)
- [`MemoryTelemetrySink.cs`](../../src/Diagnostics/NekoLib.Logger/Sinks/MemoryTelemetrySink.cs#L10)

**Decision outcome:** the target design does not use `IDiagnosticsContext` as a
logger-plus-telemetry container. Consumers receive the smallest independent
Core contracts they require.

### CRASH-01 — HIGH — the cross-platform crash API exposes Windows minidump policy

**Classification:** portability boundary

**Status:** confirmed; current Windows behavior is intentional

`NekoLib.Diagnostics` targets plain `net9.0`, but its public
`CrashDumpLevel` contains Windows minidump concepts and defaults to
`MiniDumpNormal`. The cross-platform bundle writer also chooses the fixed
filename `crash.dmp`.

The current split successfully removes P/Invoke from the cross-platform
assembly, but the policy vocabulary and artifact naming still assume the
Windows implementation. This is a constraint for a future Linux adapter.

Evidence:

- [`CrashDumpLevel.cs`](../../src/Diagnostics/NekoLib.Diagnostics/CrashDumpLevel.cs#L7)
- [`CrashHandler.cs`](../../src/Diagnostics/NekoLib.Diagnostics/CrashHandler.cs#L19)
- [`CrashHandler.cs`](../../src/Diagnostics/NekoLib.Diagnostics/CrashHandler.cs#L216)

**Recommended decision:** keep the Windows project in place and evaluate a
platform-neutral crash-artifact writer contract before adding Linux support.
Do not move Windows implementation code back into the base project.

### CRASH-02 — MEDIUM — the generic crash core contains Watchdog-specific policy

**Classification:** integration boundary

**Status:** confirmed

`CrashHandlerOptions.ExternalNotifier` appears generic, but it is invoked only
when `NotifyWatchdog` is true and `NEKO_UNDER_WATCHDOG` exists. This makes the
base crash module aware of one supervisor and prevents the same extension point
from behaving generically without Watchdog state.

Evidence:

- [`CrashHandler.cs`](../../src/Diagnostics/NekoLib.Diagnostics/CrashHandler.cs#L40)
- [`CrashHandler.cs`](../../src/Diagnostics/NekoLib.Diagnostics/CrashHandler.cs#L174)
- [`CrashHandler.cs`](../../src/Diagnostics/NekoLib.Diagnostics/CrashHandler.cs#L237)

**Recommended decision:** keep supervisor policy in Watchdog or the application
composition root and leave the crash core with a generic, explicitly configured
notification mechanism.

### LOG-01 — MEDIUM — the concrete logging package has no durable general-purpose sink

**Classification:** capability gap

**Status:** confirmed; durable logging accepted and promoted to TODO Phase D

The only built-in `ILogSink` in `NekoLib.Logger` writes to
`System.Diagnostics.Debug`. Calling `Info()` with no supplied sink drops the
entry after filtering. Watchdog has its own file logging behavior, but the
Logger package does not expose a reusable file sink.

Evidence:

- [`Logger.cs`](../../src/Diagnostics/NekoLib.Logger/Logger.cs#L11)
- [`DebugLogSink.cs`](../../src/Diagnostics/NekoLib.Logger/Sinks/DebugLogSink.cs#L9)

**Decision outcome:** the logging pipeline will include a reusable bounded file
sink. Exact rotation, concurrency, durability, and retention policy remain
design work inside the promoted roadmap item.

### CORE-02 — MEDIUM — `LogEntry` exposes data the Logger cannot populate consistently

**Classification:** Core API coherence

**Status:** confirmed; correction accepted and promoted to TODO Phase D

`LogEntry` has a category, but `ILogger` and the concrete Logger expose no
category-aware operation. `LogEntry.ToString()` is declared `new virtual`
instead of overriding `object.ToString()`, so behavior differs by static view
of the object.

Evidence:

- [`LogEntry.cs`](../../src/Core/NekoLib.Core/Diagnostics/LogEntry.cs#L9)
- [`ILogger.cs`](../../src/Core/NekoLib.Core/Diagnostics/ILogger.cs#L5)
- [`Logger.cs`](../../src/Diagnostics/NekoLib.Logger/Logger.cs#L17)

**Decision outcome:** align `LogEntry`, `ILogger`, and formatting semantics as
part of the logging pipeline work.

### TEST-01 — MEDIUM — Diagnostics and Logger boundaries lack direct regression coverage

**Classification:** verification gap

**Status:** confirmed; coverage work promoted to TODO Phase D

The shared Diagnostics test project contains only two tests per TFM:

- one crash-subscriber isolation test;
- one null diagnostics-context smoke test.

It references Core, Logger, and Diagnostics in one assembly. There are no direct
tests for Logger severity filtering, sink fan-out, sink failure isolation,
telemetry snapshots, Core model semantics, dump delegates, file tails, handler
lifecycle, or Windows integration. The test project does not reference
`NekoLib.Diagnostics.Windows`.

Evidence:

- [`NekoLib.Diagnostics.Tests.Unit.csproj`](../../tests/NekoLib.Diagnostics.Tests/Unit/NekoLib.Diagnostics.Tests.Unit.csproj#L24)
- [`CrashHandlerTests.cs`](../../tests/NekoLib.Diagnostics.Tests/Unit/CrashHandlerTests.cs#L8)
- [`DiagnosticsNullTests.cs`](../../tests/NekoLib.Diagnostics.Tests/Unit/DiagnosticsNullTests.cs#L7)

**Decision outcome:** tests will mirror the accepted target boundaries and gate
public renames and package migration.

### WIN-01 — LOW — Windows source filenames and hook lifecycle need cleanup/verification

**Classification:** naming and verification

**Status:** confirmed

The tracked filenames `CrashSupressor.cs` and `DumpWritter.cs` are misspelled,
although the type names are correct. `HookWinForms()` installs an additive
anonymous handler with no idempotency guard or removal handle; its documentation
therefore relies on callers obeying "call once."

Evidence:

- [`CrashSupressor.cs`](../../src/Diagnostics/NekoLib.Diagnostics.Windows/CrashSupressor.cs)
- [`DumpWritter.cs`](../../src/Diagnostics/NekoLib.Diagnostics.Windows/DumpWritter.cs)
- [`WindowsCrash.cs`](../../src/Diagnostics/NekoLib.Diagnostics.Windows/WindowsCrash.cs#L34)

**Recommended decision:** retain the Windows project boundary, correct physical
naming during the eventual refactor, and specify/test whether the hook is
one-shot process policy or a reversible installation.

### DBG-01 — KNOWN/FROZEN — no reusable DebugUtils-to-crash-reporting consumer bridge exists

**Classification:** known integration gap under the observability freeze

**Status:** confirmed; target bridge promoted but implementation remains frozen

DebugUtils exposes pull APIs only on `DebugUtilsRuntime`; `IDebugUtils` remains
the feature-module push/register contract. `CrashHandler` can receive manually
formatted `ExtraLines`, but it has no reusable bounded/redacted contributor for
operations, state, or runtime diagnostics.

A direct `NekoLib.Diagnostics -> NekoLib.DebugUtils` dependency would invert the
desired implementation boundary. Making feature modules implement a
Diagnostics-owned crash interface would also force them to reference the crash
package.

**Decision outcome:** Diagnostics may consume a bounded read-only Inspection
snapshot through composition, but it cannot invoke actions or reference the
concrete Inspection package. The implementation remains subject to the explicit
observability unfreeze recorded in the roadmap.

Evidence:

- [`IDebugUtils.cs`](../../src/Core/NekoLib.Core/Observability/IDebugUtils.cs#L14)
- [`DebugUtilsRuntime.cs`](../../src/DebugUtils/NekoLib.DebugUtils/DebugUtilsRuntime.cs#L193)
- [`CrashHandler.cs`](../../src/Diagnostics/NekoLib.Diagnostics/CrashHandler.cs#L31)
- [`TODO.md`](../../TODO.md#L278)

## Positive findings to preserve

1. Core already provides the dependency-inversion boundary requested for
   logging, telemetry, and DebugUtils consumers.
2. Navigation and Watchdog product assemblies do not reference Logger or
   Diagnostics implementations.
3. The Windows P/Invoke and WinForms hook are isolated from the plain `net9.0`
   crash project.
4. Logger isolates sink exceptions so logging cannot break feature behavior.
5. DebugUtils separates the module-facing write API from the concrete
   consumer-facing read API and has materially stronger direct tests than the
   Diagnostics sector.

## Risks and hypotheses not accepted as work

- Renaming PackageIds may require compatibility packages or a clean breaking
  release; the external consumer population has not been established.
- A Linux crash adapter will probably need different artifact names and policy,
  but no Linux implementation requirements have been collected.
- Splitting additional internal seams into adapter packages could create more
  operational cost than value. Any boundary beyond the accepted target
  capabilities still requires an independent deployment or dependency need.

## Decision register

### Accepted

- Feature modules consume Core contracts rather than concrete diagnostics
  packages.
- Windows-only crash behavior remains isolated in the `.Windows` project.
- The target capability vocabulary is `Logging`, `Telemetry`, `Inspection`,
  `Diagnostics`, and `Diagnostics.Windows`.
- `NekoLib.DebugUtils` is renamed to `NekoLib.Inspection`; Inspection remains
  opt-in, in-process, more intrusive than logging, and distinct from
  severity-based logging.
- Logging owns severity-based records, sink dispatch, recent-entry access, flush
  behavior, and reusable bounded disk persistence.
- Telemetry is an independent operation-timing channel, not a subtype of a log
  entry. Its first use is correlated Navigation timing with monotonic elapsed
  values and optional checkpoints; the model remains reusable by other modules.
- For the initial Navigation scenario, the accepted measures are total page
  switch time, time to authentication completion, and post-authentication time
  to page readiness. They must not be labeled pure authentication or page-load
  duration unless those exact boundaries are measured.
- Authentication and application API work remain application concerns;
  Navigation accepts correlation/checkpoint input without gaining knowledge of
  authentication or catalog APIs.
- Diagnostics owns incident and crash evidence composition. It consumes logging
  and optional telemetry or read-only Inspection snapshots through abstractions
  supplied by the composition root, not concrete project references.
- The target design removes the logger-plus-telemetry `IDiagnosticsContext`
  container in favor of the smallest independent contracts.
- `ObservabilityContext` is not the target public name.
- Review findings are not implementation tasks until explicitly accepted.

### Pending

- Compatibility strategy for public type and PackageId renames.
- Exact logging queue, rotation, retention, flush, and failure policies.
- Whether telemetry v1 persists raw operation timings or only exposes bounded
  in-memory snapshots.
- The exact read-side abstraction or composition adapter used for Diagnostics
  to consume Inspection snapshots.
- Whether Navigation-local `Diagnostics` namespaces are renamed after the
  capability split; no namespace-only cleanup has been accepted yet.
- The platform-neutral dump/artifact extension shape.
- The generic crash-bundle contribution seam.
- The final placement of Watchdog-specific notification policy.

### Rejected for this review

- Silently implementing findings while the review is active.
- Moving Windows P/Invoke back into the cross-platform project.
- Making feature modules reference Logger, DebugUtils, or crash-reporting
  implementations.
- Adding an enterprise-style host, service registry, or integration package
  hierarchy without a concrete consumer requirement.

## Validation performed

All commands ran on Windows against the current working tree with reviewed
product source equal to the reference commit.

```text
dotnet build src/Diagnostics/NekoLib.Diagnostics.Windows/NekoLib.Diagnostics.Windows.csproj
  --no-restore
  net481 and net9.0-windows: succeeded, 0 warnings, 0 errors

dotnet sln NekoLib.sln list
  confirmed Core, Logger, Diagnostics, Diagnostics.Windows, DebugUtils,
  and their reviewed test projects are solution members

dotnet test tests/NekoLib.Diagnostics.Tests/Unit/NekoLib.Diagnostics.Tests.Unit.csproj --no-restore
  net481: 2 passed
  net9.0-windows: 2 passed

dotnet test tests/NekoLib.DebugUtils.Tests/Unit/NekoLib.DebugUtils.Tests.Unit.csproj --no-restore
  net481: 16 passed
  net9.0: 16 passed

dotnet test tests/NekoLib.Watchdog.Tests/Unit/NekoLib.Watchdog.Tests.Unit.csproj
  --no-restore
  --filter "FullyQualifiedName~WatchdogCrashBundleTests|FullyQualifiedName~WatchdogLogForwardingTests"
  net481: 7 passed
  net9.0-windows: 7 passed

dotnet test tests/NekoLib.Navigation.Tests/Unit/NekoLib.Navigation.Tests.Unit.csproj
  --no-restore
  --filter "FullyQualifiedName~DebugUtils|FullyQualifiedName~Diagnostics"
  net481: 39 passed
  net9.0-windows: 39 passed
```

## Residual validation gaps

- The full solution was not rebuilt or tested as part of this initial review
  pass.
- No package was produced because the working tree is dirty and no package
  implementation changed.
- `NekoLib.Diagnostics.Windows` has no direct automated test project.
- No minidump or WER behavior was exercised.
- No Linux runtime validation was possible or attempted.
- The current Diagnostics tests do not verify most of the behavior described
  above; several findings rely on direct source and project-file evidence.

## Promotion status

The accepted work for DGN-01, CORE-01, BND-01, LOG-01, CORE-02, TEST-01, and the
frozen target direction of DBG-01 is authoritative in
[TODO Phase D](../../TODO.md#phase-d--logging-telemetry-diagnostics-and-inspection-boundaries).
This review retains only the evidence and decision rationale.

CRASH-01, CRASH-02, and WIN-01 remain review-only until their directions are
accepted. The review therefore remains in progress.

## Reconciliation

On 2026-08-01, the accepted logging, telemetry, Diagnostics, and Inspection
boundary decisions were promoted to TODO Phase D. No product code changed in
that promotion. When the remaining review decisions are complete, mark this
artifact historical and append later implementation outcomes here without
rewriting the original evidence snapshot.

### Phase D implementation outcome — 2026-08-01

Commit `1ff2594b43d8646f5ed93c1f1a47a042af10ec35` completed the accepted Phase D
scope as a clean breaking migration:

- Core now exposes independent Logging, Telemetry, and Inspection contracts.
  The combined `IDiagnosticsContext` and the old Observability contracts were
  removed.
- `NekoLib.Logging`, `NekoLib.Telemetry`, and `NekoLib.Inspection` own their
  respective implementations. Logging v1 is synchronous and ordered with a
  bounded rolling-file sink; Telemetry v1 retains bounded completed operations
  in memory and does not persist them.
- Diagnostics now consumes bounded Logging, Telemetry, and read-only Inspection
  evidence through Core abstractions. It cannot invoke Inspection actions.
- Navigation accepts the three capabilities independently and produces the
  accepted correlated page-switch timings without changing its canonical
  lifecycle order. Data and Devices were evaluated but were not instrumented or
  given Core references.

Validation ran on Windows against the Phase D working tree, which still
contained the unrelated pre-existing Devices changes recorded in the baseline:

```text
dotnet test NekoLib.sln --no-restore -m:1 -v:minimal
  all solution test projects passed across both supported target families
  898 test executions, 0 failures, 0 skipped

.\eng\pack-local.ps1 -PackageVersion 1.0.0-local.5 -AllowDirty
  Release build and solution tests passed
  Watchdog Host payload validation passed
  WinForms and WPF package-consumer probes passed
  15 immutable disposable validation packages published to artifacts/local-feed
```

Direct rebuilds of Core, Logging, Telemetry, Inspection, and Diagnostics
completed with zero warnings. The full solution retained its pre-existing
nullable-warning identities; no Phase D warning identity was introduced.

This reconciliation resolves the original pending rename compatibility,
logging persistence, telemetry persistence, and read-side composition choices.
CRASH-01, CRASH-02, and WIN-01 were not promoted or implemented and remain the
only review decisions keeping this artifact in progress.
