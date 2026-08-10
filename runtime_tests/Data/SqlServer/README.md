# Data / SQL Server (E4-SQL)

**Kind:** guide

**Lifecycle:** current

**Owner:** `NekoLib.Data`

**OS / target:** Windows, `net481` and `net9.0`, **x64 only**

**Prerequisites:** a container engine, the adopted `nekolib-sqlserver` container,
and the SA password in `NEKOLIB_SQLSERVER_PASSWORD`. See below.

**Last verification:** 2026-08-08 — **automated runtime.** `--smoke` and the
recovery rehearsal both passed with exit code 0 against SQL Server 16.0.4265.3
Developer Edition in the pinned container. The rehearsal ran 82 minutes on
`net9.0`, inside the suite's specified 60–90 minute window, with all seven fault
handlers passing and no unexpected failures. Smoke passed on both target
frameworks. **The soak is blocked on two scenario defects** recorded below.

## Purpose

Drive `NekoLib.Data` against a real database server, over a real network
transport, for the behaviour a local file database cannot produce: connection
pooling, mid-flight cancellation, transport loss, and the process-wide lifetime
of IL-emitted dynamic result types.

Three different things are involved, and the scenario keeps them apart
everywhere it reports:

| Layer | What it is | Where it lives |
|---|---|---|
| ADO.NET abstractions | `DbConnection`, `DbCommand`, `DbDataReader` | all `NekoLib.Data` ever sees |
| `Microsoft.Data.SqlClient` | the concrete provider | **only** this scenario project |
| SQL Server | the engine | a container this machine happens to have |

Passing establishes evidence for the exact combination the run recorded. It does
not add a provider dependency to `NekoLib.Data` and it is not a general
"NekoLib supports SQL Server" claim.

The library needs no change to reach SQL Server: the scenario supplies an
`IDbConnectionFactory` that returns `SqlConnection`, and the existing
`SqlServerQueryTranslator` renders the dialect. The provider package is
referenced by this project and by nothing under `src/`, which is the topology
the suite requires and is visible in the two `.csproj` files.

## Why the existing evidence was not enough

[`Data / FarmDatabase`](../FarmDatabase/README.md) covers SQLite and Access
thoroughly, and it closes what a file database can close. Three things it
explicitly could not:

- **Mid-flight cancellation.** A local file database answers in under a
  millisecond, so only refusal of an already-cancelled token could be proven.
- **Pooling.** SQLite and ACE have nothing that behaves like a server-side
  connection pool.
- **Dynamic-result lifetime.** The dynamic path had only ever run with a single
  row shape, so the process-wide IL schema cap was never approached.

Those three are what this scenario exists for. Everything else it covers is
there so the three are not measured in isolation.

## Prerequisites in detail

### The adopted container

The container is **owned by the repository owner and adopted by the scenario**.
It is described in [`container/container.json`](container/container.json), which
is a description and not a recipe — nothing in this scenario creates one.

| Setting | Value |
|---|---|
| Name | `nekolib-sqlserver` |
| Image | `mcr.microsoft.com/mssql/server:2022-CU26-ubuntu-22.04` |
| Digest | `sha256:ba4c8329f48fb8f02e1416be6a930ebfd71268caee78aa985f3af4315e457c89` |
| Published port | `1433` |

The scenario may **start, stop, restart, pause and unpause** it, for documented
setup and fault steps only. It records the running or stopped state it found and
restores that state during cleanup. It never removes or recreates the container,
its volume, its network, or its credentials.

### The password

Read from `NEKOLIB_SQLSERVER_PASSWORD` at process startup, in the process, user
and machine scopes in that order — the fallback exists because `setx` writes to
the user scope and a shell that was already open never sees it.

It is never written to source, to documentation, to a command line, to a log, to
a result file, or to a generated connection string that anything prints. There
is no command-line option to supply it, deliberately: a command line is visible
to every process on the machine.

```powershell
$env:NEKOLIB_SQLSERVER_PASSWORD = '<the container password>'
```

### The container CLI

Docker Desktop installs per user and does not always put `docker.exe` on the
machine `PATH`. Both the scenario and the scripts look in the usual per-user and
per-machine locations before giving up; `NEKOLIB_DOCKER_CLI` overrides the
search with an absolute path.

## Preflight

```powershell
.\runtime_tests\Data\SqlServer\scripts\setup.ps1 -Start
```

Reports the engine, the container's status, the resolved image digest, the
published binding, the mounts, and whether the password variable is set — never
its value. With `-Start` it starts a stopped container and waits for the port.

A listening port is not a ready server; the scenario process runs the real
readiness probe against `DATABASEPROPERTYEX('master','Status')`.

## Build

```powershell
dotnet build runtime_tests/Data/SqlServer/NekoLib.Data.RuntimeTests.SqlServer/NekoLib.Data.RuntimeTests.SqlServer.csproj
```

## Launch

```powershell
.\runtime_tests\Data\SqlServer\NekoLib.Data.RuntimeTests.SqlServer\bin\Debug\net9.0\NekoLib.Data.RuntimeTests.SqlServer.exe --smoke --seed 20260808
.\runtime_tests\Data\SqlServer\NekoLib.Data.RuntimeTests.SqlServer\bin\Debug\net481\NekoLib.Data.RuntimeTests.SqlServer.exe --smoke --seed 20260808
```

| Option | Meaning |
|---|---|
| `--smoke` | every workload class, no destructive fault density |
| `--recovery-rehearsal` | every enabled failure and recovery transition |
| `--soak <duration>` | sustained run, for example `16h` |
| `--rehearsal-duration <d>` | rehearsal window, default `60m` |
| `--seed <integer>` | seeds the schema, the data, and the fault schedule |
| `--artifacts <absolute-dir>` | run directory root |
| `--fault-schedule <file>` | use a schedule generated elsewhere |
| `--container <name>` | adopt a differently named container |
| `--keep-database` | leave the scenario database for inspection |
| `--no-container-faults` | skip the checks that must stop the server |
| `--print-schedule` | print the schedule for this seed and exit, touching nothing |

Both targets are `x64` — `PlatformTarget` is pinned rather than left AnyCPU, so
the architecture in the evidence record is a fact about the build and not about
how the process happened to be launched.

## Watching a long run

`SoakStatus` is a small always-on-top window that says how long the run has
been going. It exists so a sixteen-hour run can be checked with a glance
instead of a terminal.

```powershell
dotnet build runtime_tests/Data/SqlServer/SoakStatus/NekoLib.Data.RuntimeTests.SqlServer.SoakStatus.csproj
.\runtime_tests\Data\SqlServer\SoakStatus\bin\Debug\net10.0-windows\SoakStatus.exe --duration 16h
```

| Option | Meaning |
|---|---|
| `--duration 16h` | adds "time left" and the expected end time |
| `--started <ISO8601 UTC>` | count from when the run actually began, not from when the window opened |
| `--pid <n>` | watch the scenario process; the window turns green and shows its exit code when it ends |
| `--title <text>` | defaults to `--soak running` |

It targets `net10.0-windows` and references neither the scenario nor
`NekoLib.Data`. That is deliberate: a status window that could disturb the run
it reports on would be worse than no window.

To have it follow the run and report its outcome, launch the scenario first and
pass its id:

```powershell
$soak = Start-Process .\...\NekoLib.Data.RuntimeTests.SqlServer.exe -ArgumentList '--soak','16h','--seed','20260808' -PassThru
Start-Process .\...\SoakStatus.exe -ArgumentList '--duration','16h','--pid',$soak.Id
```

## Procedure and expected result

Every mode is non-interactive and reports its whole result as an exit code.

| Code | Meaning |
|---|---|
| 0 | every selected check passed and cleanup reconciled |
| 2 | the command line could not be understood |
| 3 | a prerequisite was missing — **an environment result, never a product finding** |
| 4 | at least one check observed a wrong outcome |
| 5 | a bounded wait expired |
| 6 | cleanup did not reconcile |
| 7 | the scenario failed outside any check |
| 8 | interrupted; bounded cleanup ran and the summary is partial |

Ctrl+C is caught rather than allowed to kill the process, so an interrupted run
still restores the container, drops its database, and writes its summary. A
second press ends it immediately.

## What each mode covers

### `--smoke`

| Phase | Checks |
|---|---|
| connection | repeated open/dispose; context-owned versus external factory ownership; session affinity refusal; use after dispose; sequential pool reuse; bounded concurrency at the pool limit |
| read | every read shape agreeing on one query; the type round-trip against a direct provider read; the builder clauses; failing callback and abandoned stream |
| transaction | parameterized insert/update/delete; commit; explicit rollback; engine-rejected statement; dispose without commit; a new transaction after commit; provider error propagation |
| cancellation | the pre-cancelled token matrix, then mid-flight cancellation of raw, typed, dynamic, callback, streaming and transaction-bound paths |
| dynamic | `DynamicMode.IL` below, at and beyond the process-wide schema cap |

Two claims deserve their own note.

**Pool reuse is measured, not assumed.** Every read shape can be satisfied
without a pool; what proves reuse is asking the server which session served each
call. Sequential calls must all report one `@@SPID`; thirty-two concurrent calls
against a pool limited to eight must report at most eight, and a burst
immediately afterwards must still complete, which is what a leaked checked-out
connection would prevent.

**Mid-flight cancellation uses a server-visible start signal.** A wall-clock
sleep before cancelling would prove only that time passed. The scenario marks
its batch with a comment, polls `sys.dm_exec_requests` until the server reports
that exact batch as executing, and only then cancels. Raw paths are held open
with `WAITFOR DELAY`; the builder-driven path, which has no clause for that, is
held open by a row lock taken from a separate control connection — the way a
stalled query actually looks.

The scenario accepts `OperationCanceledException` **or** the provider's own
cancellation shape (`SqlException` with number 0) as a cancellation terminal, and
records which one arrived. It does not accept a generic provider failure.

### `--recovery-rehearsal`

Warms up, then dispatches a seeded fault schedule at monotonic offsets from
campaign start, then cools down with the read and cancellation matrices.

| Fault | Expected recovery |
|---|---|
| `connect-while-server-down` | the open attempt fails cleanly; ordinary work succeeds once the server is back |
| `transport-loss-during-command` | the in-flight command fails; a fresh command succeeds after recovery |
| `transport-loss-during-transaction` | nothing commits; disposal does not throw; the digest is unchanged |
| `transport-loss-during-stream` | exactly one failed or cancelled stream terminal |
| `stale-pooled-connection` | the scenario's own bounded retry reaches a working connection |
| `container-restart` | the database is reachable again with the same contents |
| `schema-recreation` | the same seed rebuilds the same database |

Retry is the scenario's loop, never the library's. `NekoLib.Data` has no retry
policy and this run does not ask for one; inventing automatic retry in the
gateway would be a product decision that provider evidence alone cannot make.

The provider's exception type, error number, class and state are recorded for
every failure. That is what makes a transport loss distinguishable from a login
refusal when this file is read months from now.

### `--soak <duration>`

Crosses the dynamic boundary once — emitted types are process-wide, so repeating
it measures nothing — then cycles the read and transaction matrices while the
fault schedule runs to its offsets, sampling throughout.

## The fault schedule

Generated before any work starts, persisted before the first operation, and
immutable for the run. It carries a schema version, the campaign id and seed,
the requested duration, monotonic offsets, quiet windows at both ends, a minimum
recovery interval between faults, a bounded fault count, and a stable hash of
its normalized form.

Two runs of the same seed must produce the same hash, **including across
`net481` and `net9.0`**. That is why the generator carries its own SplitMix64
rather than using `System.Random`, whose algorithm differs between the two
runtimes, and why the hash is a written-out FNV-1a rather than a BCL hash whose
value is randomized per process.

`--fault-schedule` accepts a schedule generated elsewhere, so `E3-ORCH` can own
generation for a whole campaign; only events naming this scenario are kept.

`--print-schedule` generates and prints the schedule without touching the
server, the container, or the password. That is how the determinism claim is
checked, and it means the check needs no database:

```powershell
.\...\net481\NekoLib.Data.RuntimeTests.SqlServer.exe --recovery-rehearsal --seed 20260808 --print-schedule
.\...\net9.0\NekoLib.Data.RuntimeTests.SqlServer.exe --recovery-rehearsal --seed 20260808 --print-schedule
```

## Artifacts

One run directory under `artifacts/validation/phase-e/`:

```text
<campaign-id>/
  environment.json     commit and dirty state, Windows, architecture, runtime,
                       provider and library versions, container facts, server
                       version, and the setup gaps found
  schedule.json        the persisted fault schedule and its hash
  events.jsonl         container actions and fault dispatches, one per line
  summary.json
  summary.md
  E4-SQL/
    stdout.log
    stderr.log
    samples.csv        private bytes, managed heap, threads, handles,
                       connections created, operation totals, server sessions
    result.json        every check with its claim, outcome, timing and notes
```

That is standalone artifact layout v1. When `E3-ORCH` supplies both
`--campaign-id` and `--worker-id`, E4-SQL uses layout v2 instead:

```text
<campaign-id>/
  schedule.json  owned.json  summary.json  summary.md
  workers/
    E4-SQL-net9.0/
      process.stdout.log  process.stderr.log
      environment.json  schedule.json  events.jsonl  summary.json  summary.md
      E4-SQL/
        stdout.log  stderr.log  samples.csv  result.json
```

The orchestrator indexes that exact result path and treats its absence as a
reconciliation failure. Runs without both orchestration arguments keep v1, so
recorded E4-SQL evidence is neither moved nor reinterpreted.

`environment.json` records `passwordRecorded: false` as a standing assertion
about what the file contains.

## Cleanup and side effects

Each run creates one database named `NekoLibE4Sql_<campaign-id>` and drops it at
the end, including after an interrupt or a failure. Cleanup runs on its own
token and its own three-minute budget, because cleanup that stopped because the
run was cancelled would leave exactly the mess it exists to prevent.

If a fault left the container stopped and the run found it running, cleanup
starts it again so the database can be dropped, then restores the state the run
found.

Databases from **other** runs are listed and never dropped. To remove them:

```powershell
.\runtime_tests\Data\SqlServer\scripts\cleanup.ps1 -DropDatabases
```

That script refuses any name that does not carry the scenario prefix. It can
also `-Stop` the container. It never removes or recreates it.

Nothing is written inside the repository except the artifact directory. No
ports are opened, no services installed, no registry writes.

## Setup gaps on this machine

Recorded by the preflight on 2026-08-08 and reported by every run:

- **The database port is published on every host interface, not loopback
  only.** Docker's `HostIp` for `1433/tcp` is empty. The suite requires
  loopback-only publication; connecting through `127.0.0.1` does not establish
  it. Correcting this means recreating a user-owned container, which is outside
  the scenario's authority, so it is reported rather than fixed.
- **The container carries no mount.** Its data therefore lives in its own
  writable layer, which **survives `docker stop`, `docker start` and
  `docker restart`** and is lost only if the container is removed. "Ephemeral"
  here means "no named volume", not "wiped on restart", and the restart check
  asserts survival rather than assuming a wipe.

## What the first runs measured

Three things the smoke runs established that no fake ADO.NET object could have.

### Cancellation reports two different terminals, depending on what held the command

Every mid-flight path was cancelled after the server confirmed it was executing,
and the terminal was not the same for all of them:

| Held open by | Terminal |
|---|---|
| `WAITFOR DELAY` | `SqlException` number 0 — *"A severe error occurred on the current command … Operation cancelled by user."* |
| waiting on a row lock | `TaskCanceledException` |

Both are cancellation shapes and both are accepted, but a caller writing
`catch (OperationCanceledException)` would catch only the second. That is a real
consequence for application code and it is why the check classifies the outcome
instead of asserting one exception type.

The lifecycle counters differ too: the `WAITFOR` paths raise the error event
(`dispatched=1 success=0 error=1`), while the lock-wait path surfaces as a
cancellation before the gateway's error event runs (`dispatched=1 success=0
error=0`). No path ever raised a success terminal.

### An `XLOCK` hint does not stall a reader; a real write does

Holding a command open for cancellation needed a way to make an ordinary
`QueryBuilder` query wait, and the obvious one does not work. Measured against
this server, three ways:

| Statement in the blocking transaction | `sys.dm_tran_locks` | Reader under READ COMMITTED |
|---|---|---|
| `SELECT … WITH (XLOCK, ROWLOCK, HOLDLOCK)` | `X` on the row's key | **not blocked**, returned in 2ms |
| `UPDATE … SET V = V + 1` | `X` on the row's key | blocked until the transaction ended |
| `UPDATE … SET V = V` | `X` on the row's key | blocked until the transaction ended |

The lock is genuinely there in all three cases. Locking a row without changing
it simply leaves the reader nothing to wait for. Note the third row in
particular: a non-updating update still blocks, so "SQL Server optimises the
no-op away" is *not* the explanation — that was this scenario's first, wrong
theory, and measuring is what discarded it. `BlockingLock` therefore performs a
real modification inside a transaction that is always rolled back.

### Pooling behaves as the gateway assumes

Twenty-four sequential gateway calls were served by exactly one server session.
Thirty-two concurrent calls against a pool capped at eight used exactly eight,
and a following burst of sixteen completed, so nothing stayed checked out. The
gateway opens a connection per call and returns it; the pool is what makes that
cheap, and this is the first evidence in the repository that it does.

## What this does not prove

- Nothing about certificate validation. The connection encrypts, but with
  `TrustServerCertificate` enabled against the container's self-signed
  certificate.
- Nothing about a remote server, a non-loopback network, an authenticated domain
  login, or a non-SA principal.
- Nothing about SQL Server versions, editions, or images other than the one
  whose digest the run recorded.
- Nothing about the FarmDatabase UI, which has never executed against SQL
  Server. The interactive FarmDatabase procedure remains SQLite and Access
  evidence only.
- Translator construction, fake ADO.NET contract tests, and commands executed by
  a server are three separate claims. Only the third is what this scenario
  produces.

## Verification record

| Date | Target | Result |
|---|---|---|
| 2026-08-08 | `net481` and `net9.0` | **Build only, zero warnings.** The command-line contract was exercised: no mode returns 2 with usage, and a missing `NEKOLIB_SQLSERVER_PASSWORD` returns 3 with a message naming the variable. `container.json` is copied next to both executables. |
| 2026-08-08 | preflight | **`setup.ps1` run against the real machine.** It found engine 29.6.2, container `nekolib-sqlserver` in status `exited`, the pinned image and its digest `sha256:ba4c…e457c89`, `HostIp` empty, no mounts, and `NEKOLIB_SQLSERVER_PASSWORD` unset. Both setup gaps above come from that run. |
| 2026-08-08 | `net9.0` | **Smoke, exit 0.** 28 checks, 0 failed, in 7s against SQL Server 16.0.4265.3 Developer Edition (RTM), image digest `sha256:ba4c…e457c89`, `Microsoft.Data.SqlClient 6.1.6`, x64. 18 read shapes agreed on 6 rows with an identical summed quantity of 924; 24 sequential calls used one server session and 32 concurrent against a pool of 8 used exactly 8; every mid-flight path was cancelled after the server confirmed execution; the IL cap emitted 12 types, rejected the 13th shape, and fell back to Expando for a context that allowed it. Cleanup dropped the database and returned the container to `exited`. Counters: 192 operations, 174 successes, 6 expected failures, **0 unexpected failures**, 12 cancellations. |
| 2026-08-08 | `net481` | **Smoke, exit 0.** 27 checks passed, 1 correctly skipped — `mid-flight-streaming`, because `IDqlStreamingGateway` carries `[Obsolete(error: true)]` below net6 and is not part of `IDatabaseGateway` at all. 15 read shapes instead of 18, the difference being streaming, and the same summed quantity of 924 as `net9.0`. Same digest `w4 p24 m60 pq4776 pv9541.77 mq1124` on both targets. Counters: 186 operations, 169 successes, 6 expected failures, **0 unexpected failures**, 11 cancellations. |
| 2026-08-08 | `net9.0` | **Recovery rehearsal, exit 0, but below the specified window.** 31 checks, 0 failed, in 600s with `--rehearsal-duration 10m`; the suite specifies 60 to 90 minutes, so `result.json` carries `belowSpecifiedWindow: true` and **this run is not rehearsal evidence**. It does establish that all seven fault handlers work. Each fault's provider error was recorded: `10054` for transport loss during a command and during a stream, `1225` for a connection attempt while the server was down, `10053` for a commit over a dead transport. The interrupted stream reported exactly one `Failed` terminal. The container restart left the database present and its digest unchanged. Schema recreation from the same seed reproduced the same digest. The stale-pool probe recovered on its first attempt — the pool had already discarded the dead handles rather than serving one. Not yet run on `net481`. |
| 2026-08-08 | `net9.0` | **Recovery rehearsal inside the specified window, exit 0.** 31 checks, 0 failed, 0 skipped, in 4924s (82 minutes) with `--rehearsal-duration 90m`. All seven fault handlers passed: `transport-loss-during-command` 21.5s, `connect-while-server-down` 73.1s, `transport-loss-during-stream` 22.4s, `schema-recreation` 0.1s, `container-restart` 17.6s, `stale-pooled-connection` 20.9s, `transport-loss-during-transaction` 20.5s. 4871 operations, 4848 successes, 10 expected failures, **0 unexpected failures**, 13 cancellations. The database was dropped and the container returned to `exited`. **This is the run that satisfies the suite's rehearsal requirement**, and it must be `net9.0`: `net481` skips the streaming fault entirely. |
| 2026-08-08 | `net481` | **Recovery rehearsal, exit 0, below the window.** 29 checks passed, 0 failed, 2 skipped, in 538s with `--rehearsal-duration 10m`. The two skips are `mid-flight-streaming` and `transport-loss-during-stream`, absent below net6 by design, so this target covers six of the seven faults and can never cover the seventh. The six produced the same provider error numbers as `net9.0` — `1225`, `10054`, `10053` — which is the result one wants from a library that promises both targets. 540 operations, 0 unexpected failures. Below the specified window, so it is a second data point rather than rehearsal evidence. |
| 2026-08-08 | both TFMs | **Shared harness extracted; behaviour unchanged.** The scenario-agnostic half moved to `runtime_tests/Shared/NekoLib.RuntimeTests.Harness/`. Both targets build clean, smoke exits 0 with the same data digest, and the recorded schedule hash `fnv1a64:49a3ab65b5f249e9` is byte-identical on both — which is what proves the move did not disturb determinism. **The extraction changed no check count:** smoke reports 29 rather than the 28 recorded below because of the `state-baseline` check added while fixing the soak earlier the same day, which is a separate change. |
| 2026-08-08 | `net9.0` | **Soak, exit 0, third attempt.** `--soak 15m` ran 911s with **85439 checks passed, 0 failed, 0 skipped** and 335283 operations — 313920 successes, 21363 expected failures, **0 unexpected failures**. All seven faults executed concurrently with the workload cycles and every one recovered. The database was dropped and the container returned to `exited`. The soak path is proven; the 16-hour campaign is now a calendar decision rather than a risk. |
| 2026-08-08 | `net9.0` | **Soak, exit 4, second attempt.** 88423 checks passed, **12 failed — all of them `provider-error-propagation`**, on counters: two dispatches where one was expected, one success where none was. Steady-state traffic between faults was sharing the workspace whose lifecycle counters that check zeroes and asserts on. 348584 operations, 0 unexpected failures. The crash was gone; the accounting was not. |
| 2026-08-08 | `net9.0` | **Soak, exit 7, first attempt — a scenario defect, not a product one.** `--soak 15m` died outside every check with `SqlException 10054` from `ScenarioSchema.DigestAsync`, called on the first line of `TransactionMatrix.RunAsync` while a scheduled fault had the container stopped. See the open defects below. Before dying it had run 8486 checks with 3 failures and 36396 operations in 126s, so the workload itself holds; the coordination does not. Cleanup then could not drop its database because the server was down, leaving one behind. |
| 2026-08-08 | both TFMs | **Schedule determinism, verified.** `--recovery-rehearsal --seed 20260808 --print-schedule` produced `fnv1a64:49a3ab65b5f249e9` on two consecutive `net481` runs and on `net9.0`, and seed `99` produced a different hash. The generated plan holds 7 faults with 288-second quiet windows at both ends. This is the one acceptance criterion that needs no server, and it is the reason the generator carries its own PRNG and hash rather than the BCL's. |

### Three defects the soak found, and what they cost

All three were scenario defects — none touched `NekoLib.Data` — and none of
them could have been found by smoke or by the rehearsal, because in both of
those the matrices run before and after the fault window and never during it.
Only the soak overlaps assertions with faults.

They were found by running `--soak 15m` three times before committing a night
to sixteen hours. The first run would have died inside the first twenty
minutes.

**1. Assertion matrices ran concurrently with container-stopping faults**
(exit 7). A fault stopped the server while a matrix was mid-flight, and
`TransactionMatrix.RunAsync` took its opening digest *outside* any check, so
the transport error escaped the whole assertion mechanism and killed the
process. Fixed with `PhaseContext.ExclusiveAsync`: a fault holds it while it
executes, a workload cycle holds it while a matrix runs. The stray digest now
lives inside a `state-baseline` check as well, so a future escape becomes a red
check rather than a dead process.

**2. Cleanup gave up on dropping the database when the server was down.** It
restarted the container only if the run had *found* it running — a condition
with nothing to do with "there is a database to drop". A run that found it
stopped, and whose last fault left it stopped, leaked a database. It now starts
the container whenever it has cleanup to do, and `RestoreInitialState` still
puts the container back afterwards.

**3. Background traffic shared the workspace the matrices assert on** (exit 4,
twelve failures, all in `provider-error-propagation`). Steady-state traffic
between faults is deliberately outside the semaphore, since it should be free
to run and fail — but it was running on `context.Workspace`, whose lifecycle
counters several checks zero and then make claims about. The check expected one
dispatch and no successes and saw two and one. Steady traffic now has its own
gateway; it touches nobody's counters.

The third is the one worth remembering: the first fix was half a fix, and the
run that proved it was half a fix is the only reason the sixteen-hour campaign
will not fail on a counter.

### What is still open

- **The 16-hour soak.** Never started. No longer blocked: `--soak 15m` now
  passes with exit 0, so the path is proven and the remaining question is
  calendar rather than risk. The host must not share the run with other heavy
  work — see the load finding in the orchestrator's record.
- **The FarmDatabase interactive pass** at campaign start and end, which the
  suite asks for alongside this scenario and which stays SQLite/Access evidence.

Until those close, `TODO.md`'s `E4-SQL` item stays open.
