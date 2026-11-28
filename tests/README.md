# Verification Taxonomy

**Kind:** reference

**Lifecycle:** current

**Subject:** automated test and package-probe organization

`tests/` contains automated verification. Directory names describe the primary
suite organization; they do not override what a test actually exercises.

## Classification axes

Classify a verification independently on all four axes:

| Axis | Values | Meaning |
|---|---|---|
| Execution | automated, manual | Whether one command can determine pass/fail without human interaction |
| Scope | unit, integration, functional, package probe | The boundary exercised, from one component through deployed/package behavior |
| Prerequisites | in-process, filesystem, Windows, IPC/network, process, database, hardware, package feed | External resources required to run it truthfully |
| Entry point | `dotnet test`, packaging script, explicit executable | How the verification is started |

A test under a `Unit/` project may still be integration-scoped when it opens a
real loopback socket, named pipe, filesystem tree, or child process. This is not
automatically a reason to move it: split a suite when prerequisites, runtime,
isolation, or its canonical command materially differ.

## Versioned suites

| Suite | Primary scope | Additional real boundaries | Targets |
|---|---|---|---|
| `NekoLib.Core.Tests/Unit` | unit | none | net481, net9.0 |
| `NekoLib.Data.Tests/Unit` | unit | current tests use fakes/translators; tracked `Shared/` fixtures are not referenced by name | net481, net9.0 |
| `NekoLib.Devices.Tests/Unit` | unit + integration | loopback TCP and in-process named pipes; no real serial port | net481, net9.0 |
| `NekoLib.Diagnostics.Tests/Unit` | unit + integration | temporary filesystem crash bundles | net481, net9.0-windows |
| `NekoLib.Inspection.Tests/Unit` | unit | process-global slot and concurrency, isolated in-process | net481, net9.0 |
| `NekoLib.Http.Tests/Unit` | unit | controlled `HttpMessageHandler`; no public internet | net481, net9.0 |
| `NekoLib.Logging.Tests/Unit` | unit + integration | temporary filesystem rolling logs | net481, net9.0 |
| `NekoLib.Mvvm.Tests/Unit` | unit | Windows-targeted command surface on net9 | net481, net9.0-windows |
| `NekoLib.Navigation.Tests/Unit` | unit | in-memory platform fakes plus process-global facade tests | net481, net9.0-windows |
| `NekoLib.Pipes.Tests/Unit` | unit + integration | real named-pipe IPC in-process | net481, net9.0-windows |
| `NekoLib.Telemetry.Tests/Unit` | unit | none | net481, net9.0 |
| `NekoLib.Watchdog.Tests/Unit` | unit + integration/functional | temporary filesystem, named-pipe RPC, and controlled child processes | net481, net9.0-windows |
| `NekoLib.PackageConsumers/` | package probe | fresh local packages, restore graph, compiler rejection, WinForms/WPF build targets, direct-only Host deployment, and packaged Host protocol startup | net481, net9.0, net9.0-windows per probe |

All `NekoLib.*.Tests/Unit` projects above belong to `NekoLib.sln`.
`NekoLib.PackageConsumers/` deliberately does not: a normal source build must
not depend on packages that have not been produced yet. The canonical packaging
workflow restores, builds, and runs the applicable consumers after it validates
package contents. Host probes use session-owned working directories and stop
the package-backed sidecar before returning.

Watchdog's controlled-process tests resolve the Windows system `cmd.exe` and
copy it into an isolated temporary workspace to obtain a unique target path.
That OS binary is a declared Windows prerequisite; it is not stored under
`tools/` or treated as repository-owned payload.

## Canonical commands

Whole solution:

```powershell
dotnet build NekoLib.sln -c Release
dotnet test NekoLib.sln -c Release -m:1 --no-build
.\eng\verify-public-api.ps1
```

The public API command is an automated assembly-contract check, not a test
suite. It builds the library family and fails when a package/target surface
differs from its accepted snapshot.

One test project and one target framework:

```powershell
dotnet test tests/NekoLib.Pipes.Tests/Unit/NekoLib.Pipes.Tests.Unit.csproj
dotnet test tests/NekoLib.Data.Tests/Unit/NekoLib.Data.Tests.Unit.csproj -f net481
```

One test or class:

```powershell
dotnet test tests/NekoLib.Navigation.Tests/Unit/NekoLib.Navigation.Tests.Unit.csproj --filter "FullyQualifiedName~NavigationRuntimeTests.MethodName"
```

Package-consumer probes use the packaging entry point and a new immutable
version:

```powershell
$packageVersion = Read-Host "Enter a new immutable package version"
.\eng\pack-local.ps1 -PackageVersion $packageVersion
```

Use `-AllowDirty` only with a disposable, previously unused version. The script
builds and tests the solution, publishes the Watchdog Host payloads, packs the
family, validates package structure, and restores/builds/runs the consumer
probes.

## 2.0 regression coverage

The unreleased major includes focused regressions for each changed contract:

| Contract | Regression coverage | Entry point |
|---|---|---|
| Four retired QueryBuilder overloads | `LegacyOverloads_AreAbsentFromPublicSurface`, migrated INSERT/UPDATE parameter values, dictionary/null migration and every supported structured/trusted join type | Data focused suite on both targets |
| Consumer source migration | `DataLegacyOverloads` builds with the packaged Data reference, then rejects each removed call independently on both targets with the expected compiler diagnostic | `eng/test-local-packages.ps1`, for family 2.0 or later |
| Shared Inspection timeout | `CaptureSnapshot_SharedBudgetExpires_SkipsLaterProviders` checks every skipped callback and a fresh capture after release; the zero-budget regression checks that callbacks are never invoked | Inspection focused suite on both targets |
| Public Navigation diagnostics | `ContextProperties_BeforeStartAndAfterShutdown_ReportPublicBootstrapGuidance` checks all four mounted properties; duplicate bootstrap checks the public shutdown/start guidance | Navigation focused suite on both targets |
| Minidump opt-in and captured options | Default external-crash reports contain text without a dump; an explicitly supplied writer is captured at construction; the native writer with `None` produces no file | Diagnostics focused suite on both targets |

The full package flow combines these checks with the four native desktop
consumers and the existing recovery/bootstrap matrix. API manifests verify
compiled signatures separately. The
[release policy](../docs/public-api-release-policy.md) owns the clean-source
qualification and publication boundary.

## Manual scenarios

Manual or interactive evidence does not belong under `tests/`. Shared runnable
scenarios use [`runtime_tests/`](../runtime_tests/README.md) and an explicit
executable launch; machine-only experiments use `.local/runtime-tests/`.
Neither is invoked by `dotnet test`.
