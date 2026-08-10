# Confidence / Long-running campaign (E3-ORCH)

**Kind:** guide

**Lifecycle:** current

**Owner:** Phase E confidence stabilization

**OS / target:** Windows, Windows PowerShell 5.1 or PowerShell 7

**Prerequisites:** the `dotnet` CLI, plus whatever each selected scenario needs.
The orchestrator reports a scenario's prerequisites; it never installs them.

**Last verification:** 2026-08-08 — **automated runtime.** A two-worker smoke
campaign exited 0, and all three of the suite's acceptance criteria were
exercised. See the verification record.

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

Scenarios are configuration rather than knowledge baked into the script, which
is what lets an unfinished scenario sit disabled without blocking work on the
others. Each entry supplies its own executable, its arguments per mode, the
fault kinds it can dispatch, the prerequisites to report, and anything it
adopts. Arguments support the tokens `{seed}`, `{artifacts}`, `{schedule}`,
`{duration}` and `{durationSeconds}`.

Two workers are registered today, because two is what exists:

| Id | What it is |
|---|---|
| `E4-SQL` | `NekoLib.Data` against the adopted SQL Server container; owns seven fault kinds |
| `Data-FarmDatabase-SQLite` | the existing FarmDatabase simulation, headless; owns no faults and accepts no schedule |

## Usage

```powershell
.\runtime_tests\Confidence\LongRunning\run.ps1 -Mode smoke
```

| Option | Meaning |
|---|---|
| `-Mode smoke\|recovery\|soak` | which mode each scenario is asked for |
| `-Duration 90m` | campaign window; defaults are 20m, 60m and 16h |
| `-Seed 20260808` | seeds the fault schedule |
| `-Scenarios E4-SQL,...` | explicit list; defaults to every enabled scenario |
| `-Build` | builds each selected project first, explicitly |
| `-PreflightOnly` | checks and stops |
| `-PrintScheduleOnly` | prints the schedule and stops, touching nothing |
| `-FailWorker <id>` | launches one worker with an invalid argument, on purpose |
| `-StopStale` | ends processes an earlier unfinished campaign recorded |

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
  <scenario-id>/
    stdout.log
    stderr.log
```

Each worker also writes its own run directory beneath the campaign directory,
in its own layout. The orchestrator does not rewrite or summarise a worker's
evidence; `summary.json` indexes it.

`owned.json` without a `summary.json` is what marks a campaign as unfinished,
and it is how the next run finds stale processes.

## Procedure and expected result

1. `-PrintScheduleOnly` twice with the same seed prints the same
   `normalized-hash`; a different seed prints a different one.
2. `-PreflightOnly` reports the engine, disk, each executable, each
   prerequisite, and anything stale, without starting a worker.
3. `-Mode smoke` runs both workers to completion and exits 0.
4. `-FailWorker E4-SQL` exits 4 while the other worker still completes.
5. Killing the orchestrator mid-campaign leaves `schedule.json` and
   `owned.json` behind; the next run reports the orphan and ends it only with
   `-StopStale`.

## Cleanup and side effects

The orchestrator creates one directory per campaign and starts the configured
worker processes. It removes nothing.

Each worker's own side effects are its own and are documented in its scenario:
`E4-SQL` creates and drops one SQL Server database and restores the adopted
container to the state it found; `Data-FarmDatabase-SQLite` recreates its SQLite
fixture under `%LOCALAPPDATA%\NekoLib\FarmDatabase\`.

## Known limits

- **Graceful stop means waiting.** These are console scenarios and there is no
  reliable way to deliver Ctrl+C to another console group, so a worker that
  outlives the deadline is given a bounded grace period and then forced. Workers
  are expected to end themselves; the force path is a backstop, not the plan.
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
| 2026-08-08 | **Load finding, not a defect.** The first concurrent campaign ran with about 670 MB of free physical memory and SQL Server logins began exceeding 15 seconds in the post-login phase. `Microsoft.Data.SqlClient` then blocks further attempts to that pool for several seconds and rethrows the cached exception, so **one slow login was reported by seven consecutive checks with identical timing text**. The scenario's non-measured connections now allow 60 seconds and it no longer mints a separate pool per probe. This is machine capacity, and it is the reason a 16-hour campaign should not share this host with other heavy work. |

| 2026-08-10 | **First recovery campaign, aggregate exit 0.** A single-worker `recovery` campaign with `-Duration 70m` drove `E3-OBS` for 59 minutes through all eight phases. It is also the first proof that a worker actually **dispatches from the orchestrator's schedule** rather than merely parsing it: the worker recorded the campaign's own hash `fnv1a64:57d4189e5a941ecf` and fired its seven faults in the orchestrator's order, which differs from the order that scenario generates for itself. `E4-SQL` was left out on purpose — see the load finding above. |

**A recovery campaign has now run; the soak campaign has not.** The 16-hour
campaign remains open, and the suite is explicit that it does not start merely
because this script works. The recovery campaign that ran had a single worker,
so multi-worker aggregation still rests on the smoke campaign above.

### An artifact-layout deviation this exposed

The suite specifies one run directory as `<campaign-id>/<scenario-id>/…`, but a
worker builds its own campaign id from whatever `--artifacts` root it is handed,
so its real output lands at
`<campaign-id>/<worker-campaign-id>/<scenario-id>/result.json` while the
orchestrator separately writes `<campaign-id>/<scenario-id>/stdout.log`. Two
directories end up sharing a scenario's name with different contents, and a
reader following the suite's layout will not find `result.json`. This affects
every worker equally and is recorded rather than changed, because moving it
would relocate `E4-SQL`'s recorded artifact paths as well.
