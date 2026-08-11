# Confidence / Long-running campaign (E3-ORCH)

**Kind:** guide

**Lifecycle:** current

**Owner:** Phase E confidence stabilization

**OS / target:** Windows, Windows PowerShell 5.1 or PowerShell 7

**Prerequisites:** the `dotnet` CLI, plus whatever each selected scenario needs.
The orchestrator reports a scenario's prerequisites; it never installs them.

**Last verification:** 2026-08-10 — **automated runtime.** An `E3-OBS` smoke
campaign exercised artifact layout v2 and exited 0 with the worker result
reconciled and indexed. The earlier two-worker acceptance campaign also exited
0; see the verification record.

## Purpose

Run several Phase E scenarios as one campaign: generate and persist a seeded
fault schedule, launch the selected executables, watch them, collect their exit
codes, and write one aggregate result.

It is deliberately thin. It contains **no business assertions** — what a fault
means, and whether a run passed, are decisions belonging to the scenarios. The
orchestrator only places faults in time and reports what the workers decided.

## What it will and will not touch

Ownership is the rule the whole script is built around.

- It starts only local processes, and records each one — id, process name, and
  start time — the moment it starts, before starting the next.
- It stops only processes it recorded, and re-verifies name and start time
  before forcing anything, because a PID on its own is not an identity.
- It **never** starts or stops a container, service, or endpoint. Resources a
  scenario adopts, such as the SQL Server container, are listed as `adopts` in
  the configuration, recorded as adopted in `owned.json`, and restored by the
  scenario that adopted them.
- It never deletes a database, a fixture, or another campaign's artifacts.

## Configuration

[`campaign.json`](campaign.json) lists the scenarios;
[`campaign.schema.json`](campaign.schema.json) is the versioned shape `run.ps1`
accepts, and a `schemaVersion` it does not recognise is refused rather than
guessed at.

The checked-in configuration is schema v2. The script still accepts schema v1;
entries that omit `artifactLayoutVersion` retain the old directory convention.
This is configuration compatibility, not an artifact migration.

Scenarios are configuration rather than knowledge baked into the script, which
is what lets an unfinished scenario sit disabled without blocking work on the
others. Each entry supplies its own executable, its arguments per mode, the
fault kinds it can dispatch, the prerequisites to report, and anything it
adopts. Arguments support the tokens `{seed}`, `{artifacts}`, `{campaignId}`,
`{workerId}`, `{schedule}`, `{duration}` and `{durationSeconds}`.

Six entries are registered today. The three E3-NAV entries are disabled by
default because their native hosts require an interactive Windows desktop,
which campaign preflight cannot prove. Their standalone qualifying smokes pass:

| Id | What it is |
|---|---|
| `E4-SQL` | `NekoLib.Data` against the adopted SQL Server container; owns seven fault kinds |
| `E3-OBS` | Logging, Telemetry and passive Inspection; owns seven scenario-local fault kinds |
| `Data-FarmDatabase-SQLite` | the existing FarmDatabase simulation, headless; owns no faults and accepts no schedule |
| `E3-NAV-winforms-net481` | Opt-in WinForms `net481` Navigation worker; owns 14 scenario-local fault kinds |
| `E3-NAV-winforms-net9.0` | Opt-in WinForms `net9.0-windows` Navigation worker; owns 14 scenario-local fault kinds |
| `E3-NAV-wpf-net9.0` | Opt-in WPF `net9.0-windows` Navigation worker; owns 14 scenario-local fault kinds |

## Usage

```powershell
.\runtime_tests\Confidence\LongRunning\run.ps1 -Mode smoke
```

| Option | Meaning |
|---|---|
| `-Mode smoke\|recovery\|soak` | which mode each scenario is asked for |
| `-Duration 90m` | campaign window; defaults are 20m, 60m and 4h |
| `-Seed 20260808` | seeds the fault schedule |
| `-Scenarios E4-SQL,...` | explicit list; defaults to every enabled scenario |
| `-Build` | builds each selected project first, explicitly |
| `-PreflightOnly` | checks and stops |
| `-PrintScheduleOnly` | prints the schedule and stops, touching nothing |
| `-FailWorker <id>` | launches one worker with an invalid argument, on purpose |
| `-StopStale` | ends processes an earlier unfinished campaign recorded |

Soak defaults to the required 4-hour gate. `-Duration 16h` remains supported as
an optional extended-confidence run for slow leak or drift detection; it never
blocks Phase E closure after the 4-hour gate passes.

Exit codes match the scenarios' own contract: `0` success, `2` usage, `3`
prerequisite, `4` a worker failed, `5` a worker outlived the deadline, `6`
reconciliation, `7` unexpected.

## The schedule

Generated from the seed before anything starts and written before the first
process launches, so a machine that dies mid-campaign still leaves a document
saying what should have happened.

It carries a schema version, the campaign id and seed, the requested duration,
monotonic offsets, quiet windows at both ends, a minimum recovery interval, a
bounded fault count, and a stable hash of its normalized form. Faults from all
scenarios share one timeline, each in its own slice, so two scenarios never
receive simultaneous destructive faults.

**Smoke plans no faults at all.** The suite defines smoke as every workload
class without destructive fault density, so its schedule is empty — and still
written, so a campaign directory always records what was planned.

Determinism is checkable without running anything:

```powershell
.\runtime_tests\Confidence\LongRunning\run.ps1 -Mode recovery -Duration 90m -Seed 20260808 -PrintScheduleOnly
```

`Get-Random` and `GetHashCode` are both avoided in
[`lib/Deterministic.ps1`](lib/Deterministic.ps1). The first wraps a .NET
`Random` whose algorithm differs between Windows PowerShell and PowerShell 7,
and the second is randomised per process; either would make the same seed
produce different schedules on different hosts. SplitMix64 and FNV-1a are
written out instead, over `BigInteger` with an explicit 64-bit mask, because
PowerShell promotes an overflowing unsigned multiply to `double` rather than
wrapping.

## Artifacts

```text
artifacts/validation/phase-e/<campaign-id>/
  schedule.json     written before the first worker starts
  owned.json        every process this campaign started, updated as each starts
  summary.json      aggregate result, worker exit codes, resource samples
  summary.md        the same, plus the orchestrator's own log
  workers/
    <worker-id>/
      process.stdout.log
      process.stderr.log
      environment.json
      schedule.json
      events.jsonl
      summary.json
      summary.md
      <scenario-id>/
        stdout.log
        stderr.log
        samples.csv
        checks.ndjson    only when check retention is bounded, i.e. soak
        result.json
```

This is artifact layout v2. The worker owns everything below its `worker-id`;
the orchestrator owns the campaign-level files and redirects the process streams
to the separately named `process.*.log` files. It requires a v2 worker result at
the indexed path during reconciliation. Scenario configuration without
`artifactLayoutVersion: 2` retains the v1 directory convention, so old config
files and historical artifacts remain valid and are never migrated.

`owned.json` without a `summary.json` is what marks a campaign as unfinished,
and it is how the next run finds stale processes.

## Procedure and expected result

1. `-PrintScheduleOnly` twice with the same seed prints the same
   `normalized-hash`; a different seed prints a different one.
2. `-PreflightOnly` reports the engine, disk, each executable, each
   prerequisite, and anything stale, without starting a worker.
3. `-Mode smoke` runs every selected worker to completion and exits 0.
4. `-FailWorker E4-SQL` exits 4 while the other worker still completes.
5. Killing the orchestrator mid-campaign leaves `schedule.json` and
   `owned.json` behind; the next run reports the orphan and ends it only with
   `-StopStale`.

## Cleanup and side effects

The orchestrator creates one directory per campaign and starts the configured
worker processes. It removes nothing.

Each worker's own side effects are its own and are documented in its scenario:
`E4-SQL` creates and drops one SQL Server database and restores the adopted
container to the state it found; `E3-OBS` writes only beneath its worker
directory; `Data-FarmDatabase-SQLite` recreates its SQLite fixture under
`%LOCALAPPDATA%\NekoLib\FarmDatabase\`. Each E3-NAV worker owns only its native
window, scenario state, and worker artifact directory, and awaits Navigation
shutdown before it exits.

## Known limits

- **Graceful stop means waiting.** These are console scenarios and there is no
  reliable way to deliver Ctrl+C to another console group, so a worker that
  outlives the deadline is given a bounded grace period and then forced. Workers
  are expected to end themselves; the force path is a backstop, not the plan.
- **Native Navigation workers are opt-in.** Preflight cannot prove that the
  current Windows session has an interactive desktop. Select those workers only
  from an attended desktop, keep their three IDs separate, and treat the native
  windows as part of the run rather than as UI automation.
- **Concurrency is a real load.** The first two-worker campaign on this machine
  saturated it — see the verification record.
- **No readiness handshake.** Warm-up checks that a worker did not exit
  immediately; it does not wait for a scenario-defined ready signal, because no
  scenario exposes one yet.

## Verification record

| Date | Result |
|---|---|
| 2026-08-08 | **Schedule determinism.** `-Mode recovery -Duration 90m -Seed 20260808 -PrintScheduleOnly` printed `fnv1a64:cfc084039abb71b8` on two consecutive runs; seed 99 printed a different hash; smoke planned 0 faults. The 90-minute plan placed 7 faults between +844s and +4626s with 300-second quiet windows and no gap under the 45-second minimum. |
| 2026-08-08 | **Two-worker smoke campaign, aggregate exit 0.** `E4-SQL` and `Data-FarmDatabase-SQLite` both exited 0. Two orchestrator defects were found and fixed by earlier attempts at this same run: `Start-Process -PassThru` returns a process whose `ExitCode` is null after exit unless its `Handle` is touched first, so **every worker was initially reported as failed**, including one that had passed; and two campaigns starting inside the same second produced the same campaign id and the second silently reused the first's directory. |
| 2026-08-08 | **Failed worker, aggregate exit 4.** `-FailWorker E4-SQL` launched that worker with an invalid argument; it exited 2 immediately, `Data-FarmDatabase-SQLite` still ran to completion and exited 0, and the campaign failed as a whole. |
| 2026-08-08 | **Orchestrator killed mid-campaign.** A FarmDatabase-only campaign was killed after it had recorded ownership. `schedule.json` survived and no `summary.json` was written. The next run identified the orphan by PID, process name and start time, reported it without touching it, and ended it only when `-StopStale` was passed. |
| 2026-08-08 | **Load finding, not a defect.** The first concurrent campaign ran with about 670 MB of free physical memory and SQL Server logins began exceeding 15 seconds in the post-login phase. `Microsoft.Data.SqlClient` then blocks further attempts to that pool for several seconds and rethrows the cached exception, so **one slow login was reported by seven consecutive checks with identical timing text**. The scenario's non-measured connections now allow 60 seconds and it no longer mints a separate pool per probe. This is machine capacity, and it is the reason a multi-hour campaign should not share this host with other heavy work. |
| 2026-08-10 | **First recovery campaign, aggregate exit 0.** A single-worker `recovery` campaign with `-Duration 70m` drove `E3-OBS` for 59 minutes through all eight phases. It is also the first proof that a worker actually **dispatches from the orchestrator's schedule** rather than merely parsing it: the worker recorded the campaign's own hash `fnv1a64:57d4189e5a941ecf` and fired its seven faults in the orchestrator's order, which differs from the order that scenario generates for itself. `E4-SQL` was left out on purpose — see the load finding above. |
| 2026-08-10 | **Artifact layout v2, aggregate exit 0.** `-Mode smoke -Duration 12s -Scenarios E3-OBS` wrote the worker result at `workers/E3-OBS-net9.0/E3-OBS/result.json`, indexed that exact path from the aggregate summary, and reconciled with no problems. Worker and aggregate both recorded `fnv1a64:683eb00b749a22bb`; 91 checks passed and none failed. A direct 8-second standalone run also exited 0 with 61 checks and retained layout v1 without a `workerId`. These shortened runs are contract regression evidence, not smoke-duration evidence. |

**A recovery campaign has now run; the soak campaign has not.** The 4-hour
campaign remains open, and the suite is explicit that it does not start merely
because this script works. The recovery campaign that ran had a single worker,
so multi-worker aggregation still rests on the smoke campaign above.
After a passing 4-hour campaign, a 16-hour repetition is optional additional
confidence rather than a completion gate.

### Artifact-layout deviation resolved

The first recovery campaign exposed a v1 nesting ambiguity: process capture and
scenario evidence used separate directories with the same scenario name, while
the real `result.json` sat beneath a worker-generated campaign id. Layout v2
resolves it with explicit `--campaign-id` and `--worker-id` arguments and the
`workers/<worker-id>/<scenario-id>/result.json` contract above. The orchestrator
now fails reconciliation if a configured v2 worker does not write that result.

The change is additive. A scenario launched without both orchestration arguments
still generates its own campaign id and writes the standalone v1 layout. Prior
artifact directories keep their original paths and remain historical evidence.
