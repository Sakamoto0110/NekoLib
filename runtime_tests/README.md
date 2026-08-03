# Runtime Scenarios

**Kind:** guide

**Lifecycle:** current

**Subject:** shared manual and interactive runtime verification

`runtime_tests/` contains versioned executable scenarios whose behavior needs
human observation or interaction. They are shared evidence only when their
source, prerequisites, procedure, and verification status are present here.
They stay outside `NekoLib.sln` and are never invoked through `dotnet test`.

## Operational contract

Every active scenario must document:

- purpose and owning module/capability;
- OS, target framework, and external prerequisites;
- exact build command and executable to launch;
- manual steps and expected results;
- cleanup and side effects;
- last verified date and commit, distinguishing build-only from interactive
  verification.

Organize scenarios first by module or capability. UI technology and target
framework belong in the scenario name or metadata, not as the only top-level
identity.

## Active shared scenarios

| Scenario | Scope | Status | Instructions |
|---|---|---|---|
| Devices / com0com serial parity | Real virtual-COM configuration, timeout/cancellation, read modes, reopen, PCB-A text and PCB-B binary protocol readiness | Interactive pass on `net481` and `net9.0` on 2026-08-01 from working trees based on NekoLib `628442a` and emulator `9c9528f`; middleware behavior also confirmed visually by the user; repeat after immutable commits | [`Devices/Com0Com/README.md`](Devices/Com0Com/README.md) |
| Navigation / WinForms smoke | WinForms page switching, idle, guards, overlays, interaction blocker, and shutdown on both target families | Builds on `net481` and `net9.0-windows` on 2026-08-03 against baseline `ae17810`; interactive procedure never performed | [`Navigation/WinFormsSmoke/README.md`](Navigation/WinFormsSmoke/README.md) |
| Navigation / WPF smoke | WPF page switching, idle, guards, overlays, reset, and shutdown | Build succeeded on 2026-08-01 at scenario-source commit `32fc67e` and again on 2026-08-03 at `ae17810`; interactive procedure not rerun | [`Navigation/WpfSmoke/README.md`](Navigation/WpfSmoke/README.md) |
| Watchdog / Supervisor481 | In-process Watchdog supervision driven through real Pipes RPC/events | Build succeeded on 2026-08-01 at scenario-source commit `32fc67e`; interactive procedure not rerun | [`Watchdog/Supervisor481/README.md`](Watchdog/Supervisor481/README.md) |

Build success proves source compatibility only. It does not prove the manual
expected results until a person performs the documented steps and updates the
scenario's verification record with a commit.

## Legacy local inventory

The following pre-Phase-C directories were moved without deletion to
`.local/runtime-tests/legacy/`. That tree is ignored and is not repository
coverage:

| Former directory | Classification | Evidence for the decision |
|---|---|---|
| `DiagnosticsPipesWatchdog` | retain as local legacy reference; rebuild only after the remaining Diagnostics crash/Windows decisions | Fails on both targets because the pre-Phase-D `NekoLib.Diagnostics.Contracts` and `.Sinks` namespaces no longer exist |
| `IntegrationDemo_481` | local rebuild candidate | Fails against the clean Phase D break: old Logger/DebugUtils projects, `IDiagnosticsContext`, and `IDebugUtils` |
| `LoginFlow_481` | local archive; behavior is covered by current Navigation tests/reference | Fails on removed `SetHome`, `NavigationService.UseContext`, and `GoHomeAsync` APIs |
| `WinForms_481` | local legacy manual reference | Fails because it still composes the removed combined Diagnostics/Logger surface |

These classifications are based on `dotnet build` runs on Windows on
2026-08-01 against repository baseline `c473966`. Local files may be kept for
comparison, but shared documentation must not cite them as active evidence.

## Machine-only experiments

Put experiments under `.local/runtime-tests/`. They may be incomplete,
machine-specific, or depend on private assets. Because `.local/` is ignored,
they must not be prerequisites for a documented repository result.

## New scenario template

Copy [`SCENARIO_TEMPLATE.md`](SCENARIO_TEMPLATE.md) into a module/capability
directory. Do not mark the scenario active until its source builds and every
required field has a truthful value.
