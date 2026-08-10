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

## Planned Phase E suite

[`PHASE_E_SCENARIO_SUITE.md`](PHASE_E_SCENARIO_SUITE.md) is the implementation
brief for the remaining long-running, recovery, and SQL Server scenarios. It is
not runtime evidence and none of its planned paths are active until their source
builds, their assertions return process exit codes, and their operational
README records truthful prerequisites, procedures, cleanup, and verification.

The suite deliberately keeps roadmap state in `TODO.md`; its own role is to
specify scenario boundaries, deterministic fault scheduling, artifact contracts,
per-module workloads, and acceptance criteria for the implementer.

## Active shared scenarios

| Scenario | Scope | Status | Instructions |
|---|---|---|---|
| Confidence / Long-running campaign (E3-ORCH) | Deterministic campaign orchestration: seeded fault schedule persisted before launch, explicit scenario selection, strict process ownership, aggregate exit code, and stale-campaign reconciliation | **Automated runtime, 2026-08-08.** A two-worker smoke campaign exited 0, and all three of the suite's acceptance criteria were exercised: reproducible schedule hash, a deliberately failed worker failing the campaign without disturbing the other, and an orchestrator killed mid-campaign leaving a prewritten schedule whose orphan the next run identified. Only smoke has run; the rehearsal and 16-hour campaign remain open | [`Confidence/LongRunning/README.md`](Confidence/LongRunning/README.md) |
| Data / SQL Server (E4-SQL) | `NekoLib.Data` against a real server engine in an adopted local container: pooling and ownership, mid-flight cancellation, transport loss and recovery, transactions, read shapes and mapping, streaming cleanup, and `DynamicMode.IL` schema-cap lifetime | **Automated runtime, 2026-08-08.** `--smoke` exited 0 on both target frameworks against SQL Server 16.0.4265.3 in the pinned container — 28 checks on `net9.0`, 27 plus a correctly skipped streaming check on `net481` — with cleanup reconciled and the container restored. The recovery rehearsal then passed inside the specified window: 82 minutes on `net9.0`, exit 0, 31 checks, all seven fault handlers proven, zero unexpected failures. This is the first real-server provider evidence in the repository. The 16-hour soak is **blocked on two scenario defects** the 15-minute soak exposed, both recorded in the scenario | [`Data/SqlServer/README.md`](Data/SqlServer/README.md) |
| Data / FarmDatabase | Same application code against SQLite and Access: query dialects and binding, transactions and rollback, read shapes, cancellation, streaming, deterministic long-running simulation, and application-owned Logging/Telemetry measurements. Also exercises Mvvm binding and attribute-only Navigation registration | The `--builder` probe passed on both target frameworks and both providers on 2026-08-08 from scenario `4186e48` plus Data fix `865d90f`; broader headless evidence and the interactive passes are recorded in the scenario | [`Data/FarmDatabase/README.md`](Data/FarmDatabase/README.md) |
| Devices / com0com serial parity | Real virtual-COM configuration, timeout/cancellation, read modes, reopen, PCB-A text and PCB-B binary protocol readiness | Interactive pass on `net481` and `net9.0` on 2026-08-01 from working trees based on NekoLib `628442a` and emulator `9c9528f`; middleware behavior also confirmed visually by the user; repeat after immutable commits | [`Devices/Com0Com/README.md`](Devices/Com0Com/README.md) |
| Observability / Logging, Telemetry, Inspection (E3-OBS) | The three opt-in capabilities composed as an application composes them, each with independent assertions and its own result section: ordering and bounded snapshots, sink-failure isolation, rotation and retention, bounded flush, telemetry retention and correlation, Inspection provider budgets, and process-wide slot lifecycle | **Automated runtime, 2026-08-09.** `--smoke` exited 0 on both target frameworks over the specified 15-minute window — 4951 checks on `net9.0` and 4591 on `net481`, **zero failed and zero skipped on either**, across 164 and 152 cycles and roughly 1.8M and 1.6M operations with no unexpected failure. Threads and handles stayed flat and every bounded structure ended at capacity. The recovery rehearsal then passed inside the specified window: 62.3 minutes on `net9.0`, exit 0, 68 checks, **all seven fault kinds proven**, zero unexpected failures. Ctrl+C on a soak exits 8 with cleanup reconciled. It needs no container, service or hardware, and every failure it injects is scenario-owned. **The soak path is proven**: `--soak 15m` exited 0 with 3848 checks and 128 cycles, and all seven fault kinds fired while the assertion cycles were running. The 16-hour campaign is now a question of duration rather than correctness | [`Observability/LongRunningRecovery/README.md`](Observability/LongRunningRecovery/README.md) |
| Navigation / WinForms smoke | WinForms page switching, idle, guards, overlays, interaction blocker, and shutdown on both target families | All performable steps were driven on `net481` by 2026-08-04 at `7e26b87`; step 6 is not performable as written, and the toast and popover steps also passed on `net9.0-windows` | [`Navigation/WinFormsSmoke/README.md`](Navigation/WinFormsSmoke/README.md) |
| Navigation / WPF smoke | WPF page switching, idle, guards, overlays, reset, and shutdown | Steps 1, 2, 3, 5, 6, and 7 passed interactively on `net9.0-windows` by 2026-08-04 at `7e26b87`; step 4 is not performable because the scenario has no guarded page | [`Navigation/WpfSmoke/README.md`](Navigation/WpfSmoke/README.md) |
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
