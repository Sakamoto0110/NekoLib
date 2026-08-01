# NekoLib

A family of independent, dual-target C# libraries for desktop and embedded
applications — built for **PDV/DM-class software**: kiosk and point-of-sale
shells that run unattended for days, on hardware that ranges from current
machines to boxes still pinned to .NET Framework.

Every module targets **.NET Framework 4.8.1** and **.NET 9.0** side by side, and
every module is usable on its own. There is no framework to buy into: reference
the one you need and ignore the rest.

## Who this is for

The design assumptions come from unattended retail/dispensing terminals:

- **The app never closes.** Page lifecycle is deterministic and leaks are treated
  as bugs, not as something the next restart will clean up.
- **Nobody is watching it.** An idle timeout signs the session out and returns to
  a known screen; crashes produce a bundle you can read after the fact.
- **The hardware is mixed.** net481 and net9.0 are peers — neither is a
  second-class target, and nothing is allowed to compile on only one of them by
  accident.
- **Touch-first, single-window.** Navigation swaps pages inside one host panel
  rather than opening windows.

If you are building a normal multi-window desktop app most of this still works,
but the tradeoffs were not made with you in mind.

## Navigation is the core

`NekoLib.Navigation` is the module everything else orbits. It is a page
lifecycle runtime for a single-window shell: page registration, a deterministic
navigation order, history, guards, session, idle behavior, and four overlay
primitives — with WinForms and WPF adapters keeping application pages
framework-native.

```csharp
PageNavBootstrap
    .Use<WinFormsPlatformAdapter>(mainPanel)
    .RegisterPagesFromAssembly(typeof(IdlePage).Assembly)
    .ConfigurePages(cfg =>
    {
        cfg.Page<IdlePage>().AsIdle().StrongSingleton();
        cfg.Page<AdminPage>().StrongSingleton();
    })
    .UseIdleTimeout(10_000)
    .Start();
```

`Start()` mounts the static facade, so view-models navigate directly:

```csharp
await NavigationService.SwitchPage<DashboardPage>();
await NavigationService.GoBackAsync();
NavigationService.Session.SignIn(roles: new[] { "admin" });
```

Guards are declared on the page and evaluated before navigation:

```csharp
[RequireRole("admin")]
public sealed class AdminPage : PageView { }
```

**→ Full technical reference:
[`src/Navigation/NekoLib.Navigation/README.md`](src/Navigation/NekoLib.Navigation/README.md)**
— lifecycle order, guards, reuse policies, load modes, overlays, platform
adapters, logging, telemetry, Inspection, and the stability-sensitive components.

## Optional modules

Pick what you need. Navigation requires `NekoLib.Core`; the other modules are
optional unless one of their documented dependents brings them transitively.

| Module | What it gives you |
|---|---|
| `NekoLib.Core` | Small contracts for independent Logging, Telemetry, and Inspection capabilities, plus their null implementations. Zero dependencies. |
| `NekoLib.Logging` | Synchronous ordered severity logging, bounded recent entries, debugger output, and bounded rolling-file persistence. |
| `NekoLib.Telemetry` | Bounded in-process operation timings with correlation IDs, checkpoints, outcomes, dimensions, and read-only snapshots. |
| `NekoLib.Diagnostics` | Incident orchestration: records a fatal event, requests a bounded log flush, captures supplied recent evidence, and writes a partial crash bundle. Dump writing remains pluggable. |
| `NekoLib.Diagnostics.Windows` | The Windows half of the above: minidumps via dbghelp, WER suppression, and the WinForms `ThreadException` hook. |
| `NekoLib.Data` | Provider-neutral SQL gateway with a fluent `QueryBuilder`, typed and dynamic reads, streaming, and transactions. |
| `NekoLib.Mvvm` | `ViewModelBase` and `RelayCommand`/`RelayCommand<T>`. Deliberately tiny; works with WinForms and WPF binding alike. |
| `NekoLib.Pipes` | Named-pipe IPC: request/response RPC plus pub/sub events over framed JSON. |
| `NekoLib.Watchdog` | Process supervision — application-side Host bootstrap/attach, restart on crash, crash bundling, an RPC control channel, and a companion host executable. |
| `NekoLib.Devices` | Hardware protocol abstraction over serial ports, TCP streams, named pipes, and test doubles. |
| `NekoLib.Inspection` | Opt-in in-process runtime inspection: a bounded operation buffer, pull-based state providers, runtime diagnostics, and constrained actions. No-op when disabled. Broad module instrumentation remains frozen — see [`TODO.md`](TODO.md). |

Ordinary logging does not require Diagnostics or Inspection:

```csharp
var fileSink = new RollingFileLogSink(new RollingFileLogSinkOptions
{
    FilePath = Path.Combine(AppContext.BaseDirectory, "logs", "app.log"),
    MaximumFileBytes = 2 * 1024 * 1024,
    RetainedFileCount = 4
});
using var logger = new Logger(LogLevel.Info, fileSink);
logger.Info("Application started", category: "Startup");
```

Telemetry and Inspection are separate opt-in capabilities. Navigation accepts
each Core contract independently:

```csharp
var telemetry = new TelemetryPipeline();
using var inspection = InspectionRuntime.EnableGlobal();

PageNavBootstrap
    .Use<WinFormsPlatformAdapter>(mainPanel)
    .UseLogging(logger)
    .UseTelemetry(telemetry)
    .UseInspection()
    // page registration/configuration
    .Start();
```

`InspectionProvider.Current` lives in Core and defaults to
`NullInspection.Instance`. Only one global runtime may be active. Disposing it
restores the NO-OP and clears operations, state providers, and actions. The
overload `UseInspection(IInspectionRecorder)` accepts an explicit non-global
recorder. Diagnostics sees only `IInspectionSnapshotSource`, so it cannot invoke
registered actions.

Navigation telemetry creates one correlated `Navigation/page_switch` operation.
It owns the page-switch start and synchronous lifecycle-completed `page_ready`
boundaries; an application guard may report the intermediate authentication
checkpoint through `NavigationArgs.WithTiming(...)`. This yields total,
time-to-authenticated, and post-auth-to-ready measurements without claiming pure
HTTP or first-paint timings.

At the composition root, the same independent implementations can be supplied
to incident capture without creating feature-module dependencies:

```csharp
var crashes = new CrashHandler(new CrashHandlerOptions
{
    CrashRootDirectory = Path.Combine(AppContext.BaseDirectory, "crash"),
    Logger = logger,
    TelemetrySnapshotSource = telemetry,
    InspectionSnapshotSource = inspection,
    EvidenceCollectionTimeout = TimeSpan.FromMilliseconds(250)
});
crashes.Install();
```

## Compatibility

| | |
|---|---|
| Targets | `net481` and `net9.0` (`net9.0-windows` for the UI and Win32 modules) |
| Language | C# `latest`; **no `record`** in types shared across targets — net481 lacks `IsExternalInit` |
| Nullable | Configured per module; preserve the existing setting documented in `AGENTS.md` |
| Tooling | Visual Studio 2022 or the `dotnet` CLI. No CI/CD — builds are manual |
| Platform | `net481` and every `-windows` target build on Windows only |

```bash
dotnet build NekoLib.sln
dotnet test NekoLib.sln
```

## Local NuGet packages

Package production is opt-in: the 13 library projects and the Watchdog sidecar
are packages; tests, runtime scenarios, `BundlerTool`, and the constants-only
`src/Hosting/NekoLib` project are not.

Use the packaging entry point instead of packing individual projects:

```powershell
.\eng\pack-local.ps1 -PackageVersion 1.0.0-local.3
```

The command requires a clean Git worktree, builds and tests the solution,
publishes the Watchdog Host payloads, packs the whole family, validates package
structure and cross-TFM compatibility, restores clean PackageReference-only
consumers, and finally copies the verified artifacts to
`artifacts/local-feed/`. Main packages and `.snupkg` symbol packages are
retained. Package versions are immutable: after publishing `local.3`, use
`local.4` for changed bits.

Use `-AllowDirty` only for a disposable validation version; a package produced
from uncommitted sources cannot carry exact Git/Source Link provenance.

Register the generated folder as a source on a consumer machine:

```powershell
dotnet nuget add source C:\path\to\NekoLib\artifacts\local-feed --name NekoLibLocal
dotnet add package NekoLib.Navigation.WinForms --version 1.0.0-local.3
```

The same verified `.nupkg` files can be pushed to an authenticated private
NuGet v3 feed; no package or consumer project changes are required.

Project references become NuGet dependencies, so an application normally
references only its top-level modules. For example,
`NekoLib.Navigation.WinForms` brings Navigation and Core transitively.

`NekoLib.Watchdog.Host` is a deployment package rather than a compile-time
library. Reference it directly from the executable project. On build and
publish it copies an isolated sidecar to:

```text
<application output>/NekoLib.Watchdog.Host/NekoLib.Watchdog.Host.exe
```

That subdirectory is owned by the package and replaced on each build/publish so
obsolete files from an older Host payload cannot survive an upgrade.

The package carries an AnyCPU `net481` payload plus framework-dependent
`win-x86` and `win-x64` .NET 9 payloads. Selection follows
`NekoLibWatchdogHostRid`, `RuntimeIdentifier`, then `PlatformTarget`, defaulting
to `win-x64`. Set `NekoLibWatchdogHostDeploy=false` to disable deployment. A
.NET 9 Host still requires the corresponding x86 or x64 .NET 9 Runtime on the
target machine.

Call the application-side bootstrap near the beginning of `Main`, passing the
original arguments:

```csharp
static void Main(string[] args)
{
    WatchdogBootstrap.EnsureStarted(args);
    // normal application startup
}
```

The first call starts the deployed sidecar, attaches it to the current PID, and
waits for a bounded PID/token handshake over the target-specific pipe. The first
application process keeps its existing parent; after it exits, the Host uses the
normal restart path and becomes the parent of subsequent instances. Restarted
instances receive `NEKO_UNDER_WATCHDOG=1`, so the bootstrap returns immediately
instead of recursively starting another Host. If a Watchdog already answers on
the same target pipe and confirms the current PID, no second Host is started. A
conflicting supervised PID fails clearly instead of being mistaken for a valid
handoff. Lock wait, preflight, pipe I/O and readiness confirmation share the one
bounded timeout supplied to `EnsureStarted`.

The package-consumer probes live under `tests/NekoLib.PackageConsumers/` and
cover single- and multi-target WinForms plus WPF without any `ProjectReference`.
They are not part of `NekoLib.sln`, because a normal source build must not
require packages to have been produced first.

## Module map

| Module | Path | Targets | References |
|---|---|---|---|
| `NekoLib.Core` | `src/Core/NekoLib.Core/` | net481, net9.0 | — |
| `NekoLib.Logging` | `src/Logging/NekoLib.Logging/` | net481, net9.0 | Core |
| `NekoLib.Telemetry` | `src/Telemetry/NekoLib.Telemetry/` | net481, net9.0 | Core |
| `NekoLib.Inspection` | `src/Inspection/NekoLib.Inspection/` | net481, net9.0 | Core |
| `NekoLib.Diagnostics` | `src/Diagnostics/NekoLib.Diagnostics/` | net481, net9.0 | Core |
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
| `NekoLib` | `src/Hosting/NekoLib/` | net481, net9.0 | — (version/ID constants; not in the solution) |

Inside Navigation, dependencies flow one way:
`Adapters` → `Runtime` → `Contracts`. Across packages, dependencies follow the
`References` column above: platform adapters depend on Navigation,
Diagnostics.Windows depends on Diagnostics; Logging, Telemetry, Inspection, and
Diagnostics depend only on Core; Watchdog depends on Core and Pipes. The graph
has no cycles.

`src/Tools/BundlerTool/` is a standalone dev utility and is not part of
`NekoLib.sln`.

## Where things are

| | |
|---|---|
| Navigation technical reference | [`src/Navigation/NekoLib.Navigation/README.md`](src/Navigation/NekoLib.Navigation/README.md) |
| Roadmap, phase plan, and the Inspection instrumentation freeze | [`TODO.md`](TODO.md) |
| Per-module audits and their open items | [`docs/audit/`](docs/audit/) |
| Data module audit | [`src/Data/NekoLib.Data/DataAudit.md`](src/Data/NekoLib.Data/DataAudit.md) |
| Working agreements for coding agents | [`AGENTS.md`](AGENTS.md) |

Unit tests live in `tests/NekoLib.{Module}.Tests/Unit/`. `runtime_tests/` holds
runnable WinForms/WPF scenario apps — launch them directly, never via
`dotnet test`.

## License

See [`LICENSE.txt`](LICENSE.txt).
