# Observability / Logging, Telemetry and Inspection (E3-OBS)

**Kind:** guide

**Lifecycle:** current

**Owner:** `NekoLib.Logging`, `NekoLib.Telemetry`, `NekoLib.Inspection`

**OS / target:** Windows, `net481` and `net9.0`

**Prerequisites:** none beyond the .NET SDK. No container, no service, no
hardware, no credential. The scenario writes only inside its own run directory.

**Last verification:** see [Verification record](#verification-record).

## Purpose

Drives the three opt-in observability capabilities the way an application drives
them — through their public APIs, with no test hook anywhere in `src/` — and
asserts what each one does under sustained load, deliberate failure, and
recovery.

The three share one process for the practical reason that an application
composes them together, but they are never one claim. Each has its own phase,
its own checks and its own section in `result.json`, and one collapsing entirely
does not stop the other two from running and being reported.

## What a passing run does *not* establish

These are written into every `environment.json` and `result.json` as well,
because the easiest mistake this scenario could cause is being read as proof
that the modules are instrumented.

- **No module emits because of this scenario.** Only Navigation has Inspection
  hooks. `Data`, `Pipes`, `Watchdog`, `Devices` and `Diagnostics` have none.
  Everything recorded here was pushed by the scenario itself.
- **No action is registered or invoked.** The Inspection action channel and the
  module-instrumentation rollout are frozen and explicitly out of scope. A check
  exists solely to assert that this scenario's action count stays zero, so a
  future run cannot quietly start proving otherwise.
- **Every injected failure is scenario-owned.** The failing sinks, the blocking
  sink, the file lock and the misbehaving state providers all live in this
  project. No fault-injection or `TestControl` API was added to any product
  module.
- **Rotation and retention are proved at a deliberately small maximum file
  size.** That establishes the mechanics. It says nothing about retention
  capacity at a production file size, and the two are reported separately.
- **Ordering claims are about delivery order**, not timestamp order. See
  [What the first runs measured](#what-the-first-runs-measured).
- **Telemetry v1 keeps completed operations in bounded memory** and does not
  persist them, so nothing here is evidence about durability across a restart.

## Build

```powershell
dotnet build runtime_tests\Observability\LongRunningRecovery\NekoLib.Observability.RuntimeTests.LongRunningRecovery\NekoLib.Observability.RuntimeTests.LongRunningRecovery.csproj
```

Both targets build with no warnings. The project is outside `NekoLib.sln` and is
never invoked through `dotnet test`.

## Launch

The executable is the scenario. Every mode returns its result as an exit code;
nothing requires a person to read the output to decide pass or fail.

```powershell
.\runtime_tests\Observability\LongRunningRecovery\NekoLib.Observability.RuntimeTests.LongRunningRecovery\bin\Debug\net9.0\NekoLib.Observability.RuntimeTests.LongRunningRecovery.exe --smoke
```

Replace `net9.0` with `net481` for the other target. The two are separate
claims and write to separate run directories.

### Options

| Option | Meaning |
|---|---|
| `--smoke` | every workload class, then the same classes repeated under steady traffic for the smoke window |
| `--recovery-rehearsal` | every enabled failure and recovery transition at least once |
| `--soak <duration>` | sustained run with the full fault schedule, for example `4h` |
| `--smoke-duration <d>` | smoke window, default `15m` (the suite specifies 15–30m) |
| `--rehearsal-duration <d>` | rehearsal window, default `60m`. **Ask for about `70m`** — see below |
| `--seed <integer>` | seeds the fault schedule and the sink failure schedule |
| `--artifacts <absolute-dir>` | run directory root |
| `--fault-schedule <file>` | use a schedule generated elsewhere, as `E3-ORCH` does |
| `--print-schedule` | print the schedule for this seed and exit, touching nothing |
| `--log-rate <entries/s>` | expected sustained write rate; `0` (default) is unthrottled |
| `--keep-work` | leave the working directory for inspection |
| `--no-global-inspection` | skip the checks that install the process-wide Inspection slot |

A run shorter than the window its mode specifies is flagged in its own
`summary.md` and carries `belowSpecifiedWindow: true` in `result.json`, so a
debugging run can never be cited as evidence by accident.

**The requested window is not the elapsed time**, and the flag judges the
elapsed one. The schedule reserves a quiet window at each end, so the last fault
lands well before the nominal deadline and the cool-down finishes shortly after
it: `--rehearsal-duration 60m` actually elapses about 53 minutes, which is
*below* the suite's 60-minute lower bound. Ask for about `70m` to land inside
60–90. The smoke has no such gap, because its cycle loop runs right up to the
deadline.

## What each mode covers

### `--smoke`

Runs every workload class once, then repeats all three capability matrices under
concurrent steady traffic until the window elapses, sampling at every cycle.

The matrices themselves finish in seconds on this hardware. Stopping there would
prove the assertions and nothing about behaviour over time, which is exactly what
the suite's 15-to-30-minute window is for: drift shows up in the second part, and
the run ends with a `resources` check that asserts it did not happen.

### `--recovery-rehearsal`

Capability matrices, then the seeded fault schedule, then the matrices again so
the run ends by proving ordinary work still holds. Seven fault kinds, each
dispatched at its planned monotonic offset from the campaign start:

| Kind | Expected terminal and recovery |
|---|---|
| `log-sink-throws` | every entry offered to the failing sink also reaches the healthy one; disarming restores ordinary logging |
| `log-file-locked` | writes fail inside the sink and are swallowed by the `Logger`; other sinks are unaffected; the file is complete and writable once the lock is released |
| `log-flush-blocked` | `Flush(timeout)` returns `false` inside its bound rather than hanging; writing continues; `Flush` returns `true` once released |
| `telemetry-sink-throws` | the pipeline keeps recording, bounded retention is unaffected, the next operation records normally |
| `inspection-provider-throws` | the failing slot carries a thrown marker; healthy providers and operations stay in the same snapshot |
| `inspection-provider-times-out` | the slow slot carries a timed-out marker and the capture returns inside its budget |
| `inspection-global-teardown` | disposal restores the process-wide slot to the null recorder; recording through it is inert; a fresh `EnableGlobal` succeeds |

`--no-global-inspection` drops the last kind. That changes the schedule and
therefore its hash, which is correct: a run covering six of seven transitions is
a different plan and must not claim the same determinism evidence.

### `--soak <duration>`

The fault schedule and the capability cycles run together, serialised through the
scenario's exclusivity gate, with steady traffic underneath that holds nothing
and is free to fail and be counted.

## The fault schedule

Generated before any work starts, persisted to `schedule.json` before the first
operation, and never changed during the run. Faults carry monotonic offsets from
campaign start rather than wall-clock times, so a run that starts late still
executes the same relative plan.

The generator is the shared harness's; the vocabulary — what a kind targets and
what recovery from it looks like — is this scenario's. The generator version
`e3obs-schedule-1` is covered by the hash, so changing it invalidates this
scenario's recorded determinism evidence and nobody else's.

```powershell
.\...\NekoLib.Observability.RuntimeTests.LongRunningRecovery.exe --recovery-rehearsal --print-schedule
```

Seed `20260808` produces `fnv1a64:af14ff69cf61b022` on **both** target
frameworks. That equality is the point: `System.Random` and `GetHashCode` differ
between .NET Framework and modern .NET, so the schedule uses a written-out
SplitMix64 and FNV-1a instead.

## Artifacts

One run directory under `artifacts/validation/phase-e/`:

```text
e3obs-<mode>-<tfm>-s<seed>-<timestamp>/
  environment.json     commit and dirty flag, host, runtime, module versions, claim boundaries
  schedule.json        the plan, written before the first operation
  events.jsonl         one JSON object per line, so a truncated file still parses
  summary.json         identical to result.json
  summary.md           the readable table, including per-phase totals
  E3-OBS/
    stdout.log
    stderr.log
    samples.csv
    checks.ndjson      present only when bounded check retention is enabled
    result.json
```

That is standalone layout v1. Under `E3-ORCH`, the orchestrator supplies the
campaign and worker identities and the same files land in layout v2:

```text
<campaign-id>/
  schedule.json  owned.json  summary.json  summary.md
  workers/
    E3-OBS-net9.0/
      process.stdout.log  process.stderr.log
      environment.json  schedule.json  events.jsonl  summary.json  summary.md
      E3-OBS/
        stdout.log  stderr.log  samples.csv  checks.ndjson  result.json
```

`environment.json`, `result.json` and the readable summary state the artifact
layout version and worker id. Runs without both `--campaign-id` and
`--worker-id` keep v1; existing v1 artifacts are not moved. `checks.ndjson` is
created lazily only when bounded retention streams full check detail.

`samples.csv` carries the columns every scenario shares plus this scenario's
own: `log_entries_written`, `log_files_rolled`, `log_recent_retained`,
`telemetry_completed`, `telemetry_retained`, `inspection_recorded`,
`inspection_retained`, `inspection_providers`.

## Cleanup and side effects

The scenario creates one working directory, `<run>/E3-OBS/work/`, and writes
every log file it produces inside it. Nothing outside the run directory is
touched: no service is started or stopped, no machine configuration is changed,
no environment variable is read.

**Removing that directory is an assertion, not housekeeping.** On Windows the
delete fails outright if any sink is still holding a file, so it is how "no
unexpected process-held file handle" is proved rather than asserted. A failure
becomes a cleanup problem and exit code 6.

Cleanup also verifies that the process-wide Inspection slot is back to the null
recorder, and that the logger disposed its sinks exactly once.

Ctrl+C is taken rather than allowed to kill the process: cleanup runs, a partial
summary is written, and the run exits 8.

## What the first runs measured

### `LogEntry.TimestampUtc` is not a delivery-order key under concurrent writers

`Logger.Log` stamps the entry **before** taking its dispatch lock, so two
threads can be stamped in one order and delivered in the other. The documented
contract is the delivery order — inline, in sink registration order, under one
lock — and that is what `concurrent-writer-ordering` asserts, by comparing a
rolling order fingerprint between two sinks.

The number of timestamp inversions is recorded as a note on every run rather
than asserted, because it is an observation about the current implementation and
not a failed claim. An application that sorts entries by timestamp and expects
that to equal delivery order is relying on something Logging does not promise.

### An abandoned telemetry scope is invisible

`ITelemetryOperation` is not `IDisposable` and there is no finaliser, so an
operation started and never completed is simply never recorded — no sink write,
no error, nothing in the snapshot, including after a forced collection. The
`abandoned-scope` check asserts exactly that and says in its notes that this is
the current behaviour rather than a guarantee the API states. An application that
needs abandonment detected has to arrange it itself.

### The process-wide Inspection slot is push-only by design

`InspectionProvider.Current` is typed `IInspectionRecorder`. Reading needs the
separate `IInspectionSnapshotSource`, so a module holding the slot cannot read
the buffer back through it. That is the documented module/consumer split working
as intended, and the check now records it as a note.

### `CaptureState()` applies no budget

`CaptureSnapshot(max, timeout)` returns `<snapshot timed out>` for a provider
that exceeds the budget; `CaptureState()` waits for it. Both isolate a throwing
provider identically. The distinction is recorded because a consumer reaching
for the convenience read on a UI thread would be choosing an unbounded wait.

### The resource baseline, and what the drift check actually asserts

The suite forbids inventing a memory, latency or throughput threshold before
measurements establish a baseline. These are the first measurements, taken over
a 15-minute smoke on each target:

| | `net9.0` | `net481` |
|---|---|---|
| Cycles | 164 | 152 |
| Operations | 1 851 430 | 1 575 277 |
| Threads | 14 → 18 (+4) | 15 → 27 (+12) |
| Handles | 284 → 314 (+30) | 302 → 389 (+87) |
| Private bytes | 9.2 → 32.5 MiB | 16.7 → 27.7 MiB |
| Managed heap | 2.1 → 5.2 MiB | 3.8 → 6.3 MiB |
| Periodic samples with a higher heap than the previous | 82 of 163 | 80 of 151 |

Each cycle builds and disposes about a dozen loggers with their sinks, several
telemetry pipelines and several inspection runtimes, so a per-cycle leak of even
one thread or one handle would show as growth in the hundreds. It does not.

`net481` drifts more than `net9.0` on both counts. That is recorded rather than
explained: the two runtimes schedule and finalize differently, and this scenario
has no evidence about which difference is responsible.

What the check asserts is therefore deliberately modest, and split by kind:

- **threads and handles** are asserted against a bounded allowance, because they
  are discrete resources with clear ownership;
- **memory** is asserted only on *shape* — that the managed heap is not
  monotonic across the periodic samples. Both targets fall at roughly half their
  samples, which is a working collector rather than an accumulating structure.
  No megabyte figure is asserted, because these runs are what a later threshold
  would have to be derived from.

### Two scenario defects the sustained smoke found

Neither is a product finding; both are recorded because the handoff predicted
this class of problem and it cost real time.

1. **Exact-count assertions against a shared instance.**
   `sustained-ordered-writes` counted entries on the workspace logger, which the
   soak's background traffic also writes to. It expected 4000 and saw 4130. The
   check now owns its own logger; assertions live on instances the check owns,
   and the shared logger carries traffic and faults. The fault checks that must
   act on the shared logger were restated as traffic-independent invariants —
   "every entry offered to the failing sink also reached the healthy one" rather
   than an exact delta.
2. **Fixed scratch file names.** Every matrix runs once per cycle, so
   `repeated-create-use-dispose` was reading its own previous cycle's file and
   expected 1000 lines while seeing 2000. Scratch paths are now unique per
   invocation.

Both were found by the *smoke*, not the soak, because the sustained smoke runs
the same cycle loop. That is why the soak later completed on its first attempt
where `E4-SQL`'s needed three: the two defects above, plus moving the periodic
sample inside the exclusivity gate, were already fixed by the time a fault and
an assertion first overlapped. A sustained smoke is much cheaper than a soak and
finds the same class of problem — it is worth running one before any long
campaign.

### Three reporting defects this scenario exposed, and their fixes

None of these changed a verdict; all three made a run *describe itself* wrongly,
which is worse in an unattended suite than a visible failure.

1. **Interrupted checks were reported as failures.** In the harness, not here:
   `CheckRunner` recorded every `OperationCanceledException` as a failed check,
   so the first Ctrl+C test summarised a merely-stopped run as "145 passed,
   5 failed". `CheckRunner` now takes the run's token and records those as
   skipped. `E4-SQL` had the same flaw. Recorded in the harness README.
2. **The below-window flag measured the wrong thing.** It compared the
   *requested* duration, so a rehearsal that asked for 60 minutes and elapsed
   52.9 declared itself compliant with a 60–90 minute specification. It now
   judges elapsed time, and the option table says to request about 70 minutes.
3. **A note that was simply untrue.** The drift check reported "the run was
   shorter than the specified window" whenever it found no periodic samples —
   which is every rehearsal, because that mode runs no cycle loop. It now says
   so instead of blaming the duration.

## Procedure and expected result

1. Build both targets. Expected: success, no warnings.
2. Run `--print-schedule` on each target. Expected: the same normalized hash.
3. Run `--smoke` on `net9.0`. Expected: exit code 0, zero failed checks, zero
   unexpected failures, and a cleanup line reporting the working directory
   removed.
4. Run `--smoke` on `net481`. Expected: the same.
5. Run `--recovery-rehearsal` on either target. Expected: exit 0 with all seven
   fault kinds reporting `ok`.
6. Interrupt a `--soak` with Ctrl+C. Expected: exit code 8, a written
   `summary.md` marked interrupted, and the working directory removed.

## Verification record

Record only runs that were actually performed. Every row below except the first
is **automated runtime** — a versioned executable asserted the outcomes and
returned an exit code. The first row is **build-only** and proves source
compatibility, nothing more.

There is no interactive or automated-UI evidence here, and there should not be:
this scenario has no user interface, and all of its claims are about library
behaviour under load and failure.

| Date | Target | Mode | Result |
|---|---|---|---|
| 2026-08-09 | both | build | Both targets build with no warnings. |
| 2026-08-09 | both | `--print-schedule` | Seed `20260808` produces `fnv1a64:af14ff69cf61b022` on `net481` and `net9.0`. The equality across targets is the determinism claim. |
| 2026-08-09 | `net9.0` | `--smoke` (15m) | **Exit 0.** 4951 checks, 0 failed, 0 skipped, 905s. 164 cycles under concurrent steady traffic; 1 851 430 operations with **zero unexpected failures**. Threads 14 → 18, handles 284 → 314 across the whole run. Managed heap 2.1 → 5.2 MiB and rose at 82 of 163 periodic samples, so it is not monotonic. All three bounded structures ended exactly at capacity. 1819 files removed at cleanup, so no sink outlived its handle. |
| 2026-08-09 | `net481` | `--smoke` (15m) | **Exit 0.** 4591 checks, 0 failed, 0 skipped, 903s. 152 cycles; 1 575 277 operations with **zero unexpected failures**. Threads 15 → 27, handles 302 → 389 — a larger drift than `net9.0` showed, and still flat against 152 cycles that each build and dispose a dozen loggers. Managed heap 3.8 → 6.3 MiB, rising at 80 of 151 periodic samples. 1687 files removed at cleanup. The process ran **x64**, so its memory samples are comparable with the other target's. |
| 2026-08-09 | `net9.0` | `--recovery-rehearsal --rehearsal-duration 70m` | **Exit 0.** 68 checks, 0 failed, 0 skipped, **62.3 minutes elapsed — inside the specified 60–90 window**, `belowSpecifiedWindow: false`. **All seven fault kinds proven**, each with its documented terminal, a successful post-recovery probe, and provider/registration counts back to baseline. 8071 operations, 23 expected failures, **zero unexpected failures**. Threads 14 → 18, handles 282 → 306. Schedule `fnv1a64:962fea82f7477901`. |
| 2026-08-10 | `net481` | `--recovery-rehearsal --rehearsal-duration 70m` | **Exit 0.** 68 checks, 0 failed, 0 skipped, **62.3 minutes elapsed**, all seven fault kinds proven. This completes the matrix: both targets have now passed both smoke and rehearsal, with nothing skipped on either. It also produced the **same schedule hash and byte-identical counters** as the `net9.0` rehearsal — 8071 operations, 8046 successes, 23 expected failures, 2 cancellations — so the rehearsal is deterministic in its workload, not only in its plan. Threads 15 → 21, handles 300 → 354, both above `net9.0`'s figures and consistent with what the `net481` smoke measured. |
| 2026-08-09 | `net9.0` | `--recovery-rehearsal` (default 60m) | **Exit 0**, 68 checks, 0 failed, all seven faults — but only **52.9 minutes elapsed**, below the suite's lower bound. Superseded by the 70m run above and kept here because it is why the window flag is now judged on elapsed time rather than on the requested duration. |
| 2026-08-09 | `net9.0` | `--soak 15m` | **Exit 0 — the first soak to run to natural completion.** 3848 checks, 0 failed, 0 skipped, 903s, 128 cycles, 1 324 827 operations, **zero unexpected failures**. **All seven fault kinds fired while the capability cycles were running** and all seven passed, which is the claim the earlier interrupted soaks could not make. Threads 14 → 19, handles 283 → 311 — in line with the smoke despite the added fault traffic. Managed heap 2.1 → 4.5 MiB, rising at 67 of 127 periodic samples. 1413 files removed at cleanup. Schedule `fnv1a64:173b5243f59382ef`. |
| 2026-08-09 | `net9.0` | Ctrl+C on `--soak` | **Exit 8.** A real `CTRL_BREAK` to the process reached the handler; 144 checks passed, 6 skipped as interrupted, 0 failed; the workspace disposed, the process-wide slot was restored, and the working directory was removed. |
| 2026-08-10 | `net9.0` | `E3-ORCH` campaign, `recovery`, `-Duration 70m` | **Aggregate exit 0, worker exit 0.** All eight campaign phases ran, and the worker passed 68 checks with 0 failed across all four phases. **This is the run that proves `--fault-schedule` end to end**: the worker's recorded `scheduleHash` is the orchestrator's own `fnv1a64:57d4189e5a941ecf`, and the seven faults fired in the orchestrator's order, which differs from the order the scenario generates for itself. Elapsed 59.1 minutes, so `belowSpecifiedWindow: true` — correctly, and this run is therefore orchestration evidence, not rehearsal evidence. |
| 2026-08-09 | `net9.0` | `E3-ORCH` schedule parsing | The orchestrator generates a 7-fault schedule for this scenario from `campaign.json`, and the scenario loads all 7 events back through `--fault-schedule` with matching offsets and kinds. Superseded by the campaign above, which dispatches from it rather than only parsing it. |
| 2026-08-11 | `net9.0` | `--soak 3m` bounded-retention probe | **Exit 0.** 908 checks, 0 failed/skipped, 181.5s. `checks.ndjson` contained 908 valid JSON lines with exact phase totals matching `result.json`; 38 distinct checks were retained, `detailTruncated` was true, the detail log was complete, and no write failed. All seven faults passed across 330,928 operations with zero unexpected failures. Cleanup removed 335 files, restored the null Inspection recorder, and left no process. The short duration remains historical fact; this row closes retention wiring, not a soak-duration claim. |

### Status: outcome-first gate complete

Both targets have smoke and recovery evidence, every workload and fault kind is
executed, the soak overlap and Ctrl+C cleanup paths pass, orchestrated schedule
dispatch is proven, and the bounded-retention wiring has executed end to end.
No known E3-OBS evidence gap remains.

A 25-minute heap comparison or four-/sixteen-hour soak remains optional if a
future resource observation warrants targeted duration evidence. Do not combine
such a measurement with SQL Server on this host: the orchestrator record shows
that concurrent load can turn environment pressure into false failures.

The bounded-retention change remains important historical context. Before it,
`CheckRunner` would have retained roughly 61,500 detailed results during a
four-hour run and could have made the harness look like a library leak. It now
keeps exact bounded category counts, streams full detail to `checks.ndjson`, and
returns `EvidenceIncomplete` if that log cannot be completed. Fourteen isolated
harness assertions pass on both targets, and the three-minute scenario probe
above confirms the real wiring and cleanup.

### Artifact-layout deviation resolved

The first orchestrated recovery campaign found that v1 gave the orchestrator
and worker separate `E3-OBS` directories and hid the real result below a
worker-generated campaign id. That historical run remains at its original path.
Layout v2 now produces one unambiguous worker subtree:

```text
campaign-recovery-…/
  schedule.json  summary.json  summary.md  owned.json
  workers/
    E3-OBS-net9.0/
      process.stdout.log  process.stderr.log
      environment.json  schedule.json  summary.json  summary.md
      E3-OBS/
        result.json  samples.csv  stdout.log  stderr.log
```

An automated 12-second orchestrated regression exited 0 with 91 checks, no
failures, matching aggregate/worker schedule hash
`fnv1a64:683eb00b749a22bb`, and the result indexed at
`workers/E3-OBS-net9.0/E3-OBS/result.json`. A direct standalone regression also
exited 0 and retained v1. Neither shortened run replaces the already recorded
15-minute smoke or rehearsal evidence.
