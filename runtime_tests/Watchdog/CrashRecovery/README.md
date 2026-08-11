# Watchdog deployed-Host crash and recovery (E3-WDOG)

**Kind:** guide

**Lifecycle:** current

**Owner:** `NekoLib.Watchdog` and its `NekoLib.Watchdog.Host` deployment package

**OS / target:** Windows, `net481` and `net9.0-windows`

**Prerequisites:** a Windows user allowed to start and terminate the scenario's
own processes. Package-backed runs also require an already-created immutable
`NekoLib.Watchdog.Host` package and a consumer output built from that exact
package.

**Last verification:** 2026-08-11 - **passing short source-layout development
probes on both targets.** Each final run passed 20/20 checks, exercised all six
smoke faults and seven healthy generations, exited 0 in about 123 seconds, and
completed endpoint/process cleanup. Seven isolated contract checks pass per
target. These runs are below the 15-minute smoke minimum and are not package
evidence; full smoke, rehearsal, package-backed proof and soak remain open.

## Purpose

Exercise Watchdog across the same deployment and IPC boundaries an application
uses: a controller starts a separate scenario application, the application calls
`WatchdogBootstrap.EnsureStarted`, and bootstrap locates the separately deployed
`NekoLib.Watchdog.Host` sidecar. Later application generations are started by
the Host rather than by the controller.

This complements `Supervisor481`. That scenario hosts `WatchdogRuntime` in its
own WinForms process; E3-WDOG is the unattended deployed-sidecar scenario.

## Process and authority model

| Process | Responsibility |
|---|---|
| Controller | Owns the single `RunArtifacts`, schedule, checks, exact PID/path registry, cleanup, and final exit code |
| Scenario application child | Calls the public bootstrap API, exposes a scenario-owned health/shutdown pipe, persists generation and armed-crash records, and terminates itself at prewritten child-owned fault offsets |
| Deployed Watchdog Host | Owns attach, supervision, child restart, public Watchdog RPC, log forwarding, crash-bundle finalization, manifests, checksums, log tails, and retention |

The child processes are workload, not harness workers. The shared harness did
not gain multi-process support. No product module gained a `TestControl`, fault
injection, reflection hook, or privileged action surface.

The controller writes `schedule.json`, then durably writes `child-plan.tsv`
with the schedule hash, campaign ID, QPC origin/frequency, event ID, kind,
offset, and repetition count **before it starts the first application process**.
The child writes an `armed` record containing campaign ID, event ID, kind,
generation, PID, and monotonic timestamp before each planned terminal.

## Deployment layouts and claim boundary

The `--layout` value is recorded in `environment.json` and `result.json`:

| Layout | Application root | Valid claim |
|---|---|---|
| `source` | Staged after the controller build under `bin/<configuration>/<tfm>/ScenarioApplication/` | Development/build validation of the deployed path and bootstrap shape only; **not package evidence** |
| `disposable-package` | External consumer output built from an immutable local package | Package behavior for the exact package version and SHA-256 recorded by the run |
| `published-consumer` | External application output built from a published package | Published-consumer behavior for the exact package version and SHA-256 recorded by the run |

The two package-backed layouts require all of `--application-root`,
`--package-file`, and `--package-version`. The controller verifies the child and
`NekoLib.Watchdog.Host/NekoLib.Watchdog.Host.exe` exist and hashes the supplied
package before launch. It also records the PE machine and file version of both
executables, verifies the package ID/version from its nuspec, and requires the
deployed Host bytes to match one exact payload entry inside that package.

This delivery did **not** run `eng\pack-local.ps1`, create a package version, or
claim package behavior. Creating a new immutable package remains a separately
authorized action.

## Implemented modes and faults

All modes return the shared automated exit codes and use the deterministic
schedule generator:

- `--smoke` uses the specified 15-minute default and one low-density pass over
  ordinary exit, unhandled crash, Host restart, paused clean shutdown, and
  repeated bootstrap.
- `--recovery-rehearsal` uses the specified 60-minute default, two ordinary
  exits, two unhandled crashes, one twelve-terminal fast-crash loop, Host
  restart, paused clean shutdown, and repeated bootstrap.
- `--soak <duration>` repeats the recovery set across four cycles. Four hours is
  the required Phase E gate; a shorter run is marked below the specified window.
  Sixteen hours remains optional extended confidence.

The fast loop uses twelve armed terminals deliberately. Its first terminal may
come from a generation older than Watchdog's current three-second fast-crash
threshold; twelve terminals still guarantee two full groups of five fast
restarted generations. The controller therefore requires two observed cooling
gaps of at least nine seconds around the current ten-second behavior.

After every recoverable fault, the controller requires a newer durable
generation, one live Host and child at their exact paths, a real child health
RPC, and exactly one occurrence of that generation's unique forwarding token in
`watchdog.log`.

## Bundle and cleanup checks

For a planned unhandled terminal, the child first writes a pending directory
with `application.json` and `event.json`, then sends the public Watchdog exception
notification and lets the exception escape `Main`. The Host must add:

- `watchdog-status.txt`;
- `watchdog.log.tail`;
- `manifest.json` with checksums enabled and the Host version/restart state;
- the documented maximum of ten retained bundles.

Final checks require unique `(eventId, generation)` identities, no duplicate
bundle finalization, every retained bundle structurally complete, and no pending
crash directory.

Cleanup requests public Host shutdown first, waits boundedly, and forces only a
PID previously adopted with its exact image path and start time. The final
summary is written only after cleanup, so a leaked exact process can promote the
result to `CleanupFailed`.

## Build

Build the controller, child, and source-staged Host for each target:

```powershell
dotnet build runtime_tests\Watchdog\CrashRecovery\NekoLib.Watchdog.RuntimeTests.CrashRecovery\NekoLib.Watchdog.RuntimeTests.CrashRecovery.csproj -f net481
dotnet build runtime_tests\Watchdog\CrashRecovery\NekoLib.Watchdog.RuntimeTests.CrashRecovery\NekoLib.Watchdog.RuntimeTests.CrashRecovery.csproj -f net9.0-windows
```

Schedule generation starts no child or Host and writes no run artifacts:

```powershell
.\runtime_tests\Watchdog\CrashRecovery\NekoLib.Watchdog.RuntimeTests.CrashRecovery\bin\Debug\net9.0-windows\NekoLib.Watchdog.RuntimeTests.CrashRecovery.exe --recovery-rehearsal --seed 20260810 --print-schedule
```

The isolated console tests start no child or Host. They cover child-plan durable
round-trip, schedule determinism and vocabulary, the Watchdog pipe-name vector,
and positive/negative package-payload provenance:

```powershell
dotnet build runtime_tests\Watchdog\CrashRecovery\NekoLib.Watchdog.RuntimeTests.CrashRecovery.Tests\NekoLib.Watchdog.RuntimeTests.CrashRecovery.Tests.csproj -f net481
.\runtime_tests\Watchdog\CrashRecovery\NekoLib.Watchdog.RuntimeTests.CrashRecovery.Tests\bin\Debug\net481\NekoLib.Watchdog.RuntimeTests.CrashRecovery.Tests.exe

dotnet build runtime_tests\Watchdog\CrashRecovery\NekoLib.Watchdog.RuntimeTests.CrashRecovery.Tests\NekoLib.Watchdog.RuntimeTests.CrashRecovery.Tests.csproj -f net9.0-windows
.\runtime_tests\Watchdog\CrashRecovery\NekoLib.Watchdog.RuntimeTests.CrashRecovery.Tests\bin\Debug\net9.0-windows\NekoLib.Watchdog.RuntimeTests.CrashRecovery.Tests.exe
```

## Execution commands

Source-layout development execution:

```powershell
.\runtime_tests\Watchdog\CrashRecovery\NekoLib.Watchdog.RuntimeTests.CrashRecovery\bin\Debug\net9.0-windows\NekoLib.Watchdog.RuntimeTests.CrashRecovery.exe --smoke --layout source
```

Package-backed execution, after an external consumer output already exists:

```powershell
.\runtime_tests\Watchdog\CrashRecovery\NekoLib.Watchdog.RuntimeTests.CrashRecovery\bin\Debug\net9.0-windows\NekoLib.Watchdog.RuntimeTests.CrashRecovery.exe --smoke --layout disposable-package --application-root C:\absolute\consumer-output --package-file C:\absolute\feed\NekoLib.Watchdog.Host.<version>.nupkg --package-version <version>
```

Start with a short, explicitly authorized source-layout probe before any full
smoke. A source run cannot substitute for the required package-backed evidence.
Do not register E3-WDOG in `campaign.json` until it has run successfully once
and its prerequisite/adoption contract has been observed.

## Artifacts

The controller writes the shared Phase E artifact contract plus:

- `environment.json`: repository state, target/runtime, exact deployment layout,
  executable paths/versions/machines, Watchdog pipe, package provenance when
  applicable, setup gaps, and claim boundaries;
- `schedule.json`: deterministic normalized fault schedule and hash;
- `E3-WDOG/work/child-plan.tsv`: durable child-owned fault plan;
- `E3-WDOG/work/state/`: generation, claim, armed, completed, and ready records;
- `E3-WDOG/work/watchdog.log` and `E3-WDOG/work/crash/`: Host output under test;
- `samples.csv`: controller resources plus generation, armed, bundle, pending,
  live-process, and supervised-process resource counts.

## What a passing run does not establish

- update orchestration, remote administration, Linux behavior, cross-user or
  elevated-user security, and power-loss durability are outside scope;
- the scenario's health/shutdown pipe is same-user workload control, not a
  security boundary or product API;
- source layout cannot prove package deployment;
- virtual unhandled exceptions and controlled process termination do not prove
  physical machine failure or operating-system crash behavior.

## Verification record

- 2026-08-10 / working tree based on `4f8980b`: build-only on Windows. Explicit
  builds of the controller/child/Host graph passed for `net481` and
  `net9.0-windows`. Repeated `--print-schedule` checks were used only for static
  schedule determinism: smoke `fnv1a64:67480520c857ba99`, recovery rehearsal
  `fnv1a64:677fcab193b16fbf`, and 4-hour soak
  `fnv1a64:c6982c14a3add117` for seed `20260810`, byte-identical across targets;
  seed `99` differed as expected. The six isolated contract checks passed on
  each target. These checks and previews started no child or Host. No smoke,
  recovery rehearsal, soak, bundle finalization, process cleanup, or
  package-backed mode has executed. This is readiness evidence, not Watchdog
  runtime evidence.
- 2026-08-11 / dirty working tree at `fff6307`: two identical source-layout
  development probes ran on `net9.0-windows` with
  `--smoke --smoke-duration 2m --layout source --seed 20260810`. Both persisted
  the same schedule before the first application (`fnv1a64:7d3f49941df33843`),
  exercised all six scheduled faults and reached seven healthy generations.
  Each finalized with exit 4, 19 passed checks, one failed check, no skipped
  checks, and about 123 seconds elapsed. The artifacts are
  `artifacts/validation/phase-e/e3wdog-smoke-net9.0-s20260810-20260811T153528212Z`
  and
  `artifacts/validation/phase-e/e3wdog-smoke-net9.0-s20260810-20260811T155129488Z`.
  Bundle integrity, retention, generation accounting, exact live-pair checks,
  endpoint release and process cleanup passed in both runs; no scenario process
  remained.
- Both probes reproduced the same first-generation-only product defect. The
  initially attached application exited normally and was replaced, but public
  status reported a null `lastExitCode`; the next ordinary terminal from a
  Host-started generation reported 0. `WatchdogRuntime.AttachInitialProcess()`
  acquires the initial application through `Process.GetProcessById()`. After the
  process exits, the monitor catches the failed `ExitCode` read and stores null.
  The existing attach regression asserts restart and identity but not the
  attached process's exit code. This is not a schedule or recovery-wait race:
  the replacement generation was already durable, healthy and acknowledged in
  both runs before status was read.
- A narrow product fix was authorized after those two attempts. The attach path
  now materializes the process handle while the initial application is alive,
  so the same `Process` retains exit-code observability after termination. A
  focused regression discards the launcher's handle, exits the attached process
  with code 17, and asserts public status after restart. It failed with
  `lastExitCode: null` before the fix and passes on `net481` and
  `net9.0-windows` after it. The complete Watchdog test suite passes 84/84 on
  each target. No public API, pipe protocol, bootstrap behavior or restart
  policy changed.
- The first `net481` probe after the product fix exited 7 during repeat
  bootstrap because immediate ownership adoption observed a transient
  unavailable `Process.MainModule.FileName`. Exact adoption now retries the same
  PID's image path and start time for a bounded five seconds; it never adopts an
  incomplete identity and cleanup still never selects by broad process name.
  The next probe completed all faults but exited 4 when final retention parsed
  child-owned integer strings through the harness numeric reader. E3-WDOG now
  parses only that persisted string contract with invariant checked `Int64`,
  covered by a seventh isolated contract check. The shared harness is unchanged.
- Final source-layout probes on the complete corrected source passed on both
  targets. `net481` artifact
  `artifacts/validation/phase-e/e3wdog-smoke-net481-s20260810-20260811T164300191Z`
  and `net9.0-windows` artifact
  `artifacts/validation/phase-e/e3wdog-smoke-net9.0-s20260810-20260811T164537338Z`
  each record smoke hash `fnv1a64:7d3f49941df33843`, 20 passed checks, zero
  failed/skipped, all six faults, seven healthy generations, both ordinary exit
  codes as 0, valid bundle integrity/retention, zero cleanup problems, released
  Watchdog and child-health endpoints, and exit 0 in about 123 seconds.
- Full smoke/rehearsal windows, package-backed evidence and the four-hour soak
  remain unexecuted. The scenario remains out of `campaign.json`, and these
  deliberately short source-layout passes are neither smoke-gate nor package
  evidence. No package was created.
