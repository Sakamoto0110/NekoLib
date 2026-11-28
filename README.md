# NekoLib

A family of focused, dual-target C# libraries for desktop and embedded
applications — built for **PDV/DM-class software**: kiosk and point-of-sale
shells that run unattended for days, on hardware that ranges from current
machines to boxes still pinned to .NET Framework.

Every shipped module supports **.NET Framework 4.8.1** and a **.NET 9.0** target
side by side; UI and Win32 modules use `net9.0-windows`. The dependency graph is
shallow rather than dependency-free: reference the feature package you need and
NuGet brings its documented foundations transitively.

## Upgrading to 2.0

The 2.0 API removes four deprecated `QueryBuilder` overloads. Applications
and libraries that call them need to migrate those calls and rebuild. The
[2.0.0 release notes](https://github.com/Sakamoto0110/NekoLib/blob/master/docs/release-notes-2.0.0.md)
list each replacement, describe the known runtime and deployment issues, and
record the release status. For installation, choose a
[published family version](https://github.com/Sakamoto0110/NekoLib/releases).

## Who this is for

The design assumptions come from unattended retail/dispensing terminals:

- **The app never closes.** Page lifecycle is deterministic and leaks are treated
  as bugs, not as something the next restart will clean up.
- **Nobody is watching it.** Configured idle handling signs the session out and
  returns to its idle page. A popover can remain visible across that transition.
  Crash reports can preserve evidence for later inspection.
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
NavigationService.Session.SignIn(roles: new[] { "admin" });
await NavigationService.SwitchPage<AdminPage>();
```

Guards are declared on the page and evaluated before navigation:

```csharp
[RequireRole("admin")]
public sealed class AdminPage : PageView { }
```

**→ Full technical reference:
[`docs/modules/Navigation/REFERENCE.md`](docs/modules/Navigation/REFERENCE.md)**
— lifecycle order, guards, reuse policies, load modes, overlays, platform
adapters, logging, telemetry, Inspection, and the stability-sensitive components.

## Optional modules

Pick what you need. Navigation requires `NekoLib.Core`; the other modules are
optional unless one of their documented dependents brings them transitively.

| Module | What it gives you |
|---|---|
| [`NekoLib.Core`](docs/modules/Core/REFERENCE.md) | Small contracts for independent Logging, Telemetry, and Inspection capabilities, plus their null implementations. Zero dependencies. |
| [`NekoLib.Logging`](docs/modules/Logging/REFERENCE.md) | Synchronous ordered severity logging, bounded recent entries, debugger output, and bounded rolling-file persistence. |
| [`NekoLib.Telemetry`](docs/modules/Telemetry/REFERENCE.md) | Bounded in-process operation timings with correlation IDs, checkpoints, outcomes, dimensions, and read-only snapshots. |
| [`NekoLib.Diagnostics`](docs/modules/Diagnostics/REFERENCE.md) | Incident orchestration: records a fatal event, requests a bounded log flush, captures supplied recent evidence, and writes a partial crash bundle. Dump writing remains pluggable. |
| [`NekoLib.Diagnostics.Windows`](docs/modules/Diagnostics/REFERENCE.md#nekolibdiagnosticswindows) | Windows minidumps via dbghelp, WER suppression, the WinForms `ThreadException` hook, and a scoped WPF dispatcher exception hook. |
| [`NekoLib.Http`](docs/modules/Http/REFERENCE.md) | Typed, instance-scoped endpoint catalogs and bounded request execution through a consumer-owned `HttpClient`. |
| [`NekoLib.Data`](docs/modules/Data/REFERENCE.md) | Provider-neutral SQL gateway with a fluent `QueryBuilder`, typed and dynamic reads, target-specific streaming, and transactions. |
| [`NekoLib.Mvvm`](docs/modules/Mvvm/REFERENCE.md) | `ViewModelBase` and `RelayCommand`/`RelayCommand<T>`. Deliberately tiny; works with WinForms and WPF binding alike. |
| [`NekoLib.Pipes`](docs/modules/Pipes/REFERENCE.md) | Named-pipe IPC: request/response RPC plus bounded pub/sub events over framed JSON. |
| [`NekoLib.Watchdog`](docs/modules/Watchdog/REFERENCE.md) | Process supervision — opt-in same-executable auto bootstrap, optional manual sidecar attach, restart on crash, crash bundling and an RPC control channel. |
| [`NekoLib.Watchdog.Host`](docs/modules/WatchdogHost/REFERENCE.md) | Direct-reference deployment package for the versioned local Watchdog sidecar; no compile-time API. |
| [`NekoLib.Devices`](docs/modules/Devices/REFERENCE.md) | Hardware protocol abstraction over serial ports, TCP streams, named pipes, and test doubles. |
| [`NekoLib.Inspection`](docs/modules/Inspection/REFERENCE.md) | Opt-in passive in-process inspection: a bounded operation buffer, ordered pull-based state providers, budgeted snapshots, and owner diagnostics. Actions remain explicitly experimental and are not authorization. Broad module instrumentation remains frozen. |

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
restores the NO-OP and clears operations and state providers. The experimental
action registry is also cleared. The overload
`UseInspection(IInspectionRecorder)` accepts an explicit non-global recorder.
Diagnostics sees only `IInspectionSnapshotSource`, so it cannot invoke
registered actions. See the
[Inspection reference](docs/modules/Inspection/REFERENCE.md) for identity,
ordering, budget, lifecycle, and experimental-boundary contracts.

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

`ExternalNotifier` is a generic composition callback: when supplied, Diagnostics
invokes it after the artifact stage completes or fails and isolates callback
failures. The application decides whether that callback notifies Watchdog or
another local
integration; Diagnostics does not inspect Watchdog environment state. Leave it
null when no notification is required. Subscribe to `CrashBundleFailed` to learn
that incident evidence was lost, because `CrashDetected` and the notifier fire
either way. Option values are captured when the handler is constructed. WinForms
applications call `WindowsCrash.HookWinForms()` explicitly before creating any
window; repeated calls are safe and retain one process-lifetime hook. WPF
applications call `WindowsCrash.HookWpf(dispatcher)` and dispose the returned
lease on the dispatcher's owning thread.

**→ Full technical reference:
[`docs/modules/Diagnostics/REFERENCE.md`](docs/modules/Diagnostics/REFERENCE.md)**
— handler lifecycle, evidence budgets and bounds, redaction boundary, bundle
layout, and the Windows crash adapter.

## Module entry points and limits

The root module map below owns targets and project dependencies. This table is
the compact operational reference for modules that do not need a dedicated
technical manual.

| Module | Main public entry points | Important boundary | Focused validation |
|---|---|---|---|
| [Core](docs/modules/Core/REFERENCE.md) | `ILogger`, `ITelemetry`, `IInspectionRecorder`, snapshot contracts, null objects | Contracts only; no concrete pipeline or feature-module knowledge | `NekoLib.Core.Tests.Unit` |
| [Logging](docs/modules/Logging/REFERENCE.md) | `Logger`, `LoggerOptions`, `DebugLogSink`, `RollingFileLogSink` | Synchronous ordered writes; callers own sink composition, and `DisposeSinks` defaults to transferring sink disposal to the logger | `NekoLib.Logging.Tests.Unit` |
| [Telemetry](docs/modules/Telemetry/REFERENCE.md) | `TelemetryPipeline`, `TelemetryPipelineOptions` | Bounded in-memory completed operations; no persistence in v1; the caller owns one explicit terminal and sink dispatch is synchronous | `NekoLib.Telemetry.Tests.Unit` |
| [Inspection](docs/modules/Inspection/REFERENCE.md) | `InspectionRuntime`, `InspectionOptions`, `InspectionProvider` | Explicit opt-in; passive bounded evidence; at most one global runtime; actions experimental; broad module rollout frozen | `NekoLib.Inspection.Tests.Unit` |
| [Diagnostics](docs/modules/Diagnostics/REFERENCE.md) | `CrashHandler`, `CrashHandlerOptions`, `CrashDumpWriter` | Incident evidence consumer; options are captured at construction, disposal is terminal and releases the process hooks, bundles may be partial, and a failed bundle raises `CrashBundleFailed` | `NekoLib.Diagnostics.Tests.Unit` |
| [Diagnostics.Windows](docs/modules/Diagnostics/REFERENCE.md#nekolibdiagnosticswindows) | `WindowsCrash`, `CrashSuppressor` | Explicit WinForms process hook and scoped WPF dispatcher hook | build directly plus `NekoLib.Diagnostics.Tests.Unit` |
| [HTTP](docs/modules/Http/REFERENCE.md) | `HttpEndpoint`, `HttpApiCatalog`, `RelativeUriBuilder`, `HttpApiClient` | Consumer owns `HttpClient`, authentication and policy; non-success protocol evidence is preserved and response buffering is bounded | `NekoLib.Http.Tests.Unit` |
| [Data](docs/modules/Data/REFERENCE.md) | `QueryBuilder`, `DatabaseGateway`, `QueryExecutionContext`, `DbSession` | Raw identifiers/clauses remain a caller trust boundary; OleDb binding is positional | `NekoLib.Data.Tests.Unit` |
| [Mvvm](docs/modules/Mvvm/REFERENCE.md) | `ViewModelBase`, `RelayCommand`, `RelayCommand<T>` | Binding helpers only; no application host or navigation dependency; coercion needs an exact runtime type match and `Execute` does not consult `CanExecute` | `NekoLib.Mvvm.Tests.Unit` |
| [Pipes](docs/modules/Pipes/REFERENCE.md) | `PipeServer`, `PipeClient`, `PipeEventHub`, `PipeEventClient`, `IPipeMetrics` | Local cooperative-process transport, not an authorization boundary; current-user access is opt-in; event delivery is bounded/best-effort; stateful shutdown is terminal and awaitable | `NekoLib.Pipes.Tests.Unit` |
| [Watchdog](docs/modules/Watchdog/REFERENCE.md) | `WatchdogAutoBootstrap`, `WatchdogBootstrap`, `WatchdogController`, `WatchdogRuntime`, `WatchdogOptions` | Opt-in same-executable desktop startup, compatible manual sidecar and advanced runtime; configuration is captured, shutdown is terminal, evidence is bounded/best-effort, and current-user RPC/events do not protect against a hostile same-user process | `NekoLib.Watchdog.Tests.Unit` |
| [Devices](docs/modules/Devices/REFERENCE.md) | `HardwareEngine`, `ICommTransport`, serial/TCP/named-pipe transports, `ProtocolRaw` | Transport-neutral byte streams; a timed-out operation leaves an indeterminate receive state unless `CloseTransportOnNoResponse` is enabled; real COM-port behavior still needs explicit runtime validation | `NekoLib.Devices.Tests.Unit` |

Navigation and its adapters use their
[dedicated technical reference](docs/modules/Navigation/REFERENCE.md).
Pipes returns stable protocol errors to clients and reports handler exception
details only to the configured local metrics/diagnostics surface.
Run a focused suite with:

```powershell
dotnet test tests/NekoLib.<Module>.Tests/Unit/NekoLib.<Module>.Tests.Unit.csproj
```

Adapters and projects without a dedicated test assembly are built directly and
covered through their owning module's tests. The complete validation taxonomy
and package/runtime exceptions are documented in
[`tests/README.md`](tests/README.md).

## Compatibility

| | |
|---|---|
| Targets | `net481` and `net9.0` (`net9.0-windows` for the UI and Win32 modules) |
| Language | C# `latest`; **no `record`** in types shared across targets — net481 lacks `IsExternalInit` |
| Nullable | Configured per project; preserve the setting declared by the affected `*.csproj` |
| Tooling | Visual Studio 2022 or the `dotnet` CLI. Builds and validation are manual; GitHub Actions is used only for manually dispatched NuGet.org trusted publication |
| Platform | `net481` and every `-windows` target build on Windows only |

Public API stability, coordinated SemVer, deprecation, compatibility baselines,
and migration follow the current
[`public API and release policy`](docs/public-api-release-policy.md).
Consumer-visible changes are recorded in [`CHANGELOG.md`](CHANGELOG.md).

```powershell
dotnet build NekoLib.sln
dotnet test NekoLib.sln
.\eng\verify-public-api.ps1
```

## NuGet packages

Package production is opt-in: the 15 library projects and the Watchdog Host
deployment package are packaged together; tests, runtime scenarios,
`BundlerTool`, and the constants-only
`src/Hosting/NekoLib` project are not.

NekoLib releases one coordinated family of 16 main packages and 15 symbol
packages. Use the [published GitHub releases](https://github.com/Sakamoto0110/NekoLib/releases)
for the current stable version and its distribution status. Release records
preserve source provenance, immutable package hashes, validation results and
evidence boundaries; publication uses a manually dispatched trusted-publishing
workflow. Align every NekoLib reference to the same family version.

The [desktop integration contract](docs/desktop-readiness-1.2.0.md) describes
the 1.2 additions and compatibility decisions. The
[modal binding correction](docs/desktop-readiness-1.2.1.md) records the desktop
readiness patch. The [1.2.1 release record](docs/stable-release-1.2.1.md)
contains the final four-way package/recovery matrix, publication and independent
public-download evidence. The
[1.1.0 record](docs/stable-release-1.1.0.md) preserves the preceding compatible
minor; `1.0.0` remains the
[first stable family baseline](docs/stable-release-1.0.0.md).

Use the packaging entry point instead of packing individual projects:

```powershell
$packageVersion = Read-Host "Enter a new immutable package version"
.\eng\pack-local.ps1 -PackageVersion $packageVersion
```

The command requires a clean Git worktree, builds and tests the solution,
publishes the Watchdog Host payloads, packs the whole family, validates package
structure, matching XML API documentation for every managed target assembly,
and cross-TFM compatibility, restores clean PackageReference-only consumers,
and finally copies the verified artifacts to
`artifacts/local-feed/`. Main packages and `.snupkg` symbol packages are
retained. Package versions are immutable: after publishing any version, choose
a different version for changed bits.

Use `-AllowDirty` only for a disposable validation version; a package produced
from uncommitted sources cannot carry exact Git/Source Link provenance.

Install the public stable packages directly from NuGet.org:

```powershell
dotnet add package NekoLib.Navigation.WinForms
```

For unpublished validation versions, register the generated local folder as a
source on a consumer machine:

```powershell
dotnet nuget add source C:\path\to\NekoLib\artifacts\local-feed --name NekoLibLocal
$packageVersion = Read-Host "Enter the locally packed version"
dotnet add package NekoLib.Navigation.WinForms --version $packageVersion
```

The same verified package family can also be pushed under a distinct version to
an authenticated private NuGet v3 feed; no package or consumer project changes
are required.

Project references become NuGet dependencies, so an application normally
references only its top-level modules. For example,
`NekoLib.Navigation.WinForms` brings Navigation and Core transitively.

The package-consumer probes live under `tests/NekoLib.PackageConsumers/` and
cover single- and multi-target WinForms plus WPF without any `ProjectReference`.
They are not part of `NekoLib.sln`, because a normal source build must not
require packages to have been produced first.

### Automatic Watchdog startup

For a C# WinForms or WPF executable, reference `NekoLib.Watchdog` directly and
enable automatic startup in the application project. Set the MSBuild property
`NekoLibFamilyVersion` in your project or `Directory.Build.props` to the exact
family version you consume (for example, `1.4.0` for that release). For local
2.0 qualification use the exact 2.0 candidate version from your feed.

```xml
<PropertyGroup>
  <NekoLibWatchdogAutoBootstrap>true</NekoLibWatchdogAutoBootstrap>
  <NekoLibWatchdogHostingMode>SameExecutable</NekoLibWatchdogHostingMode>
</PropertyGroup>
<ItemGroup>
  <PackageReference Include="NekoLib.Watchdog" Version="$(NekoLibFamilyVersion)" />
</ItemGroup>
```

Rebuild and launch the application's `.exe`. `SameExecutable` is the default
hosting mode. Its supervisor is a second, headless process of the application
executable. The generated entry point handles supervisor invocations before
application startup and routes ordinary launches to the existing entry point.
Supervision activates at the first supported native-loop callback. Installing
the package alone leaves automatic startup disabled.

This mode requires only the managed Watchdog package and its build/analyzer
assets.
The [Watchdog integration reference](https://github.com/Sakamoto0110/NekoLib/blob/master/docs/modules/Watchdog/REFERENCE.md#automatic-application-startup)
owns compiler requirements, optional configuration, UI-hang recovery defaults,
and the global Enable/Disable/Shutdown controls.

### Optional Watchdog sidecar

Applications choosing `NekoLibWatchdogHostingMode=Sidecar` directly reference
`NekoLib.Watchdog.Host` at the same family version. Its build/publish targets
deploy the selected payload to `<application output>/NekoLib.Watchdog.Host/`.
See the [Host deployment reference](https://github.com/Sakamoto0110/NekoLib/blob/master/docs/modules/WatchdogHost/REFERENCE.md#installation-and-ownership)
for architecture selection, runtime requirements and the coordinated protocol.

The [manual sidecar route](https://github.com/Sakamoto0110/NekoLib/blob/master/docs/modules/Watchdog/REFERENCE.md#manual-sidecar-startup)
calls `WatchdogBootstrap.EnsureStarted(args)` near the beginning of `Main`,
with automatic entry routing disabled. Automatic startup uses the generated
entry point in either hosting mode.

## Module map

| Module | Path | Targets | References |
|---|---|---|---|
| `NekoLib.Core` | `src/Core/NekoLib.Core/` | net481, net9.0 | — |
| `NekoLib.Logging` | `src/Logging/NekoLib.Logging/` | net481, net9.0 | Core |
| `NekoLib.Telemetry` | `src/Telemetry/NekoLib.Telemetry/` | net481, net9.0 | Core |
| `NekoLib.Inspection` | `src/Inspection/NekoLib.Inspection/` | net481, net9.0 | Core |
| `NekoLib.Diagnostics` | `src/Diagnostics/NekoLib.Diagnostics/` | net481, net9.0 | Core |
| `NekoLib.Diagnostics.Windows` | `src/Diagnostics/NekoLib.Diagnostics.Windows/` | net481, net9.0-windows | Diagnostics |
| `NekoLib.Http` | `src/Http/NekoLib.Http/` | net481, net9.0 | — |
| `NekoLib.Navigation` | `src/Navigation/NekoLib.Navigation/` | net481, net9.0 | Core |
| `NekoLib.Navigation.WinForms` | `src/Navigation/NekoLib.Navigation.WinForms/` | net481, net9.0-windows | Navigation |
| `NekoLib.Navigation.Wpf` | `src/Navigation/NekoLib.Navigation.Wpf/` | net481, net9.0-windows | Navigation |
| `NekoLib.Data` | `src/Data/NekoLib.Data/` | net481, net9.0 | — |
| `NekoLib.Mvvm` | `src/Mvvm/NekoLib.Mvvm/` | net481, net9.0 | — |
| `NekoLib.Devices` | `src/Devices/NekoLib.Devices/` | net481, net9.0 | — |
| `NekoLib.Pipes` | `src/Pipes/NekoLib.Pipes/` | net481, net9.0 | — |
| `NekoLib.Watchdog` | `src/Watchdog/NekoLib.Watchdog/` | net481, net9.0-windows | Core, Pipes, Watchdog.Generators |
| `NekoLib.Watchdog.Generators` | `src/Watchdog/NekoLib.Watchdog.Generators/` | netstandard2.0 | — |
| `NekoLib.Watchdog.Host` | `src/Watchdog/NekoLib.Watchdog.Host/` | net481, net9.0-windows | Watchdog |
| `NekoLib` | `src/Hosting/NekoLib/` | net481, net9.0 | — |

Inside Navigation, dependencies flow one way:
`Adapters` → `Runtime` → `Contracts`. Across packages, dependencies follow the
`References` column above: platform adapters depend on Navigation,
Diagnostics.Windows depends on Diagnostics; Logging, Telemetry, Inspection, and
Diagnostics depend only on Core; HTTP, Data, Mvvm, Devices, and Pipes have no
NekoLib project dependency; Watchdog depends on Core and Pipes. The graph has no
cycles.

`src/Tools/BundlerTool/` is a standalone dev utility and is not part of
`NekoLib.sln`. Build it reproducibly through
[`eng/build-bundler.ps1`](eng/build-bundler.ps1); generated output belongs under
`artifacts/`.

## Where things are

| | |
|---|---|
| Core technical reference | [`docs/modules/Core/REFERENCE.md`](docs/modules/Core/REFERENCE.md) |
| Navigation technical reference | [`docs/modules/Navigation/REFERENCE.md`](docs/modules/Navigation/REFERENCE.md) |
| Inspection technical reference | [`docs/modules/Inspection/REFERENCE.md`](docs/modules/Inspection/REFERENCE.md) |
| Pipes technical reference | [`docs/modules/Pipes/REFERENCE.md`](docs/modules/Pipes/REFERENCE.md) |
| Watchdog technical reference | [`docs/modules/Watchdog/REFERENCE.md`](docs/modules/Watchdog/REFERENCE.md) |
| Product direction, intentions, and the Inspection instrumentation freeze | [`ROADMAP.md`](ROADMAP.md) |
| Formally promoted work and execution gates | [`TODO.md`](TODO.md) |
| Unpromoted ideas | [`docs/proposals/`](docs/proposals/README.md) |
| Documentation authority and lifecycle | [`docs/README.md`](docs/README.md) |
| Automated verification taxonomy | [`tests/README.md`](tests/README.md) |
| Shared manual runtime scenarios | [`runtime_tests/README.md`](runtime_tests/README.md) |
| Repository documentation, tools, artifacts, and local data | [`docs/repository-layout.md`](docs/repository-layout.md) |
| Development, compatibility, validation, review, and Git workflow | [`docs/development.md`](docs/development.md) |
| Historical audits and the active-review index | [`docs/audit/README.md`](docs/audit/README.md) |
| Completed roadmap history | [`docs/history/README.md`](docs/history/README.md) |

Automated suites and package probes are classified in
[`tests/README.md`](tests/README.md). Manual runtime scenarios are separate and
are versioned under [`runtime_tests/`](runtime_tests/README.md); build and launch
them explicitly, never through `dotnet test`.

## License

See [`LICENSE.txt`](LICENSE.txt).
