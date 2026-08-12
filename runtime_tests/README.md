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

## Phase E scenario suite

[`PHASE_E_SCENARIO_SUITE.md`](PHASE_E_SCENARIO_SUITE.md) is the retained
implementation brief for the delivered long-running, recovery, and SQL Server
scenarios. It is not runtime evidence; the active inventory and each
operational README record the executable source, prerequisites, procedures,
cleanup, and verification.

The suite deliberately keeps roadmap state in `TODO.md`; its own role is to
specify scenario boundaries, deterministic fault scheduling, artifact contracts,
per-module workloads, and acceptance criteria for maintainers.

As of 2026-08-11, Phase E uses outcome-first acceptance. Nominal smoke,
rehearsal, and soak durations remain useful run modes, but closure depends on
complete workload/fault/recovery, topology, artifact, provenance, and cleanup
evidence rather than repeating every mode for every target. Historical results
keep their original duration classification.

**As of 2026-08-12 every E3 scenario has met that bar**, E3-PIPE and E3-DEV
last. What remains for all of them is optional confidence — additional target
parity, the nominal windows, four-hour soaks, interactive parity, and campaign
registration — not an open gate. The compact ten-minute sweeps that closed
E3-PIPE and E3-DEV record `belowSpecifiedWindow: true` and are not nominal
rehearsals.

## Active shared scenarios

| Scenario | Scope | Status | Instructions |
|---|---|---|---|
| Confidence / Long-running campaign (E3-ORCH) | Deterministic campaign orchestration: seeded fault schedule persisted before launch, explicit scenario selection, strict process ownership, aggregate exit code, and stale-campaign reconciliation | **Outcome-first gate complete.** A two-worker smoke exited 0; reproducible scheduling, failed-worker propagation with peer cleanup, killed-orchestrator stale ownership, and layout v2 were exercised. A single-worker recovery campaign drove `E3-OBS` through all eight phases with aggregate exit 0 and proved external schedule dispatch. Multi-worker non-smoke and four-hour campaigns remain optional load/duration confidence | [`Confidence/LongRunning/README.md`](Confidence/LongRunning/README.md) |
| Data / SQL Server (E4-SQL) | `NekoLib.Data` against a real server engine in an adopted local container: pooling and ownership, mid-flight cancellation, transport loss and recovery, transactions, read shapes and mapping, streaming cleanup, and `DynamicMode.IL` schema-cap lifetime | **Outcome-first gate complete.** Smoke/cancellation passed on both targets against SQL Server 16.0.4265.3. The 82-minute `net9.0` recovery proved all seven faults; a separate `net481` run recorded its streaming limitation; and a 15-minute soak overlapped all seven faults with sustained work, exit 0 and complete cleanup. A four-hour isolated run remains optional duration confidence | [`Data/SqlServer/README.md`](Data/SqlServer/README.md) |
| Data / FarmDatabase | Same application code against SQLite and Access: query dialects and binding, transactions and rollback, read shapes, cancellation, streaming, deterministic long-running simulation, and application-owned Logging/Telemetry measurements. Also exercises Mvvm binding and attribute-only Navigation registration | The `--builder` probe passed on both target frameworks and both providers on 2026-08-08 from scenario `4186e48` plus Data fix `865d90f`; broader headless evidence and the interactive passes are recorded in the scenario | [`Data/FarmDatabase/README.md`](Data/FarmDatabase/README.md) |
| Devices / com0com serial parity and recovery (E3-DEV) | Two mutually exclusive paths over the same pairs. **Oracle pass:** real virtual-COM configuration, timeout/cancellation, read modes, reopen, and PCB-A text and PCB-B binary parity against an independent emulator. **E3-DEV:** the scenario owns the other end of each pair and drives open/close/reopen cycles, timeouts, cancellation, endpoint switching, operation serialization, chunked and late responses, configuration and encoding parity, disposal under load, and five peer faults | **Outcome-first gate complete, 2026-08-12.** The independent oracle passed on both targets, and corrected two-minute real-COM probes passed 168/168. With NekoPcbEmulator stopped, the compact `net9.0` recovery sweep passed **33/33, recovery 5/5, exit 0** in 467.2 s: preflight proved both cross-connections, and delay, restart, disconnect, malformed frame and silence each reached their expected terminal followed by a clean request. Zero unexpected failures, empty `cleanupProblems`/`setupGaps`, and COM19, COM20, COM9 and COM10 all reopened and released. The run records `belowSpecifiedWindow: true` and is not a nominal rehearsal. This is real-com0com evidence, not physical UART, wiring or electrical behaviour, and the automated modes are not protocol parity — that claim belongs to the oracle pass. A `net481` runtime repeat, full windows, a four-hour soak and E3-ORCH registration remain optional. The two paths cannot run at once: both want `COM9`/`COM10` | [`Devices/Com0Com/README.md`](Devices/Com0Com/README.md) |
| Observability / Logging, Telemetry, Inspection (E3-OBS) | The three opt-in capabilities composed as an application composes them, each with independent assertions and its own result section: ordering and bounded snapshots, sink-failure isolation, rotation and retention, bounded flush, telemetry retention and correlation, Inspection provider budgets, and process-wide slot lifecycle | **Outcome-first gate complete.** Fifteen-minute smoke and 62.3-minute recovery passed on both targets; all seven faults passed, with byte-identical recovery counters. The 15-minute soak overlapped all faults with assertions, Ctrl+C cleanup is proven, and the later three-minute bounded-retention probe produced 908 valid detail records matching `result.json`, exit 0 and complete cleanup. Longer heap/duration runs are optional | [`Observability/LongRunningRecovery/README.md`](Observability/LongRunningRecovery/README.md) |
| Pipes / long-running and recovery (E3-PIPE) | `NekoLib.Pipes` across real process boundaries over real named pipes: payload sizes, concurrent correlation, error contracts, frame limits, malformed raw peers, endpoint release and rebinding | **Outcome-first gate complete, 2026-08-12.** Corrected two-minute probes passed 75/75 on both targets, then the compact `net9.0` recovery sweep passed **37/37, recovery 6/6, exit 0** in 530.6 s: all six scheduled faults reached their expected terminal and post-recovery probe, with zero unexpected failures, the surviving client children reporting 3,497,665 requests and zero mismatched correlation, empty `cleanupProblems`/`setupGaps`, the endpoint released and no process or pipe left. Closing it exposed three scenario-oracle defects — not `NekoLib.Pipes` defects — fixed in `698960a`. The run records `belowSpecifiedWindow: true` and is not a nominal rehearsal. Metric-stability thresholds, `MaxClients` saturation and event delivery across a restart stay uncovered; a `net481` runtime repeat, full windows, a four-hour soak and E3-ORCH registration remain optional | [`Pipes/LongRunningRecovery/README.md`](Pipes/LongRunningRecovery/README.md) |
| Watchdog / deployed-Host crash and recovery (E3-WDOG) | Independent controller, scenario application child, and separately deployed `NekoLib.Watchdog.Host`: bounded attach, replacement generations, ordinary and unhandled terminals, fast-crash cooling, paused clean shutdown, Host/bootstrap restart, forwarding, bundle integrity/retention, exact process ownership, and cleanup | **Outcome-first gate complete, 2026-08-11.** Dual-target source-layout probes passed 20/20 with all six faults and complete cleanup. The final package boundary then passed 20/20 on `net9.0-windows` from immutable `1.0.0-local.10`: exact version/SHA-256, deployed Host payload match, seven healthy generations, bundle integrity/retention, released endpoints, and exit 0. The compact run remains below the nominal smoke window; a `net481` package repeat, full mode windows, four-hour soak, and campaign registration are optional separate confidence or orchestration decisions | [`Watchdog/CrashRecovery/README.md`](Watchdog/CrashRecovery/README.md) |
| Navigation / long-running and recovery (E3-NAV) | Shared unattended workload with minimal native WinForms/WPF hosts: page lifetime and history, session guards, all load modes, surfaces, idle, repeated mount/awaited shutdown, admitted/cutoff races, 14 scenario-owned failures, passive Inspection, and resource cleanup | **Outcome-first gate complete, 2026-08-11.** All three native combinations passed qualifying 20-minute smoke with clean awaited shutdown. A clean WinForms `net481` recovery run then passed 25/25, exercised all 14 faults, exited 0 after 3681.8 seconds, and left no process/window. Dual-target contracts and the three smokes cover target/adapter parity; repeating the same recovery matrix is unnecessary. Interactive parity stays separate. The WPF private-byte trend remains an observation suitable for an optional targeted longer run, not a confirmed leak | [`Navigation/LongRunningRecovery/README.md`](Navigation/LongRunningRecovery/README.md) |
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
