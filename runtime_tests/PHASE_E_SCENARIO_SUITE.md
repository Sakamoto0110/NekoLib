# Phase E Scenario Suite Build Specification

**Kind:** guide

**Lifecycle:** current

**Subject:** implementation specification for Phase E long-running, recovery,
and real-integration runtime scenarios

**Status:** planned; this document is not executable evidence

**Roadmap owner:** [`TODO.md`](../TODO.md), Phase E3 and E4

## Purpose

This document is the implementation brief for the runtime scenarios that close
the remaining Phase E3 and E4 confidence gaps. It tells an implementer what to
build, what each scenario must prove, and how to record evidence. It does not
track completion; `TODO.md` remains the only live roadmap.

As each scenario becomes runnable, create its directory and operational
`README.md` from [`SCENARIO_TEMPLATE.md`](SCENARIO_TEMPLATE.md), move the
scenario-specific procedure there, and add it to the active inventory in
[`runtime_tests/README.md`](README.md). Do not mark a scenario active from this
specification alone.

The planned suite is deliberately a set of small executables and scripts, not a
new test framework:

| ID | Planned path | Primary scope |
|---|---|---|
| `E3-ORCH` | `runtime_tests/Confidence/LongRunning/` | Deterministic campaign orchestration and evidence collection |
| `E3-NAV` | `runtime_tests/Navigation/LongRunningRecovery/` | Navigation lifetime, recovery, native adapter, and resource stability |
| `E3-OBS` | `runtime_tests/Observability/LongRunningRecovery/` | Logging, Telemetry, and passive Inspection behavior |
| `E3-PIPE` | `runtime_tests/Pipes/LongRunningRecovery/` | Real named-pipe load, churn, failure, and shutdown |
| `E3-WDOG` | `runtime_tests/Watchdog/CrashRecovery/` | Deployed Host supervision, crash loops, forwarding, and bundles |
| `E3-DEV` | `runtime_tests/Devices/Com0Com/` | Extend the existing independent virtual-COM scenario with soak/recovery modes |
| `E4-SQL` | `runtime_tests/Data/SqlServer/` | Real SQL Server execution, pooling, cancellation, failure, and dynamic lifetime |

## Rules for every implementation

### Architecture and scope

- Keep all scenario-only dependencies, emulators, fault controls, provider
  packages, and orchestration outside the product projects.
- Do not add a project reference to `NekoLib.Data`, `NekoLib.Devices`, or
  `NekoLib.Pipes` merely to support a scenario.
- Do not modify Navigation's stability-sensitive runtime types to make a
  scenario easier to drive.
- Do not expand the frozen Inspection action or module-instrumentation rollout.
  A scenario may compose public Logging, Telemetry, and Inspection APIs as an
  application would, but it must not claim that uninstrumented modules emit.
- Put deterministic fault injection in the scenario process, its independent
  oracle, or the campaign script. Do not add public `TestControl` or
  fault-injection APIs to a library.
- A runtime finding may justify a later product change, but the scenario task
  must first preserve the failure, command, seed, environment, and artifacts.
  Product fixes require separate authorization.

### Evidence levels

Every result must be labelled as exactly one of:

- **build-only**: projects compiled;
- **automated runtime**: a versioned executable or script asserted outcomes and
  returned an exit code;
- **interactive**: a person or UI-controlling agent performed the documented
  actions and observed the native application;
- **automated UI**: a versioned UI-driving program performed and asserted the
  interaction.

An agent clicking the UI is interactive evidence, not automated UI evidence,
unless a reusable driver is checked in and named in the verification record.
Build success, process existence, console output without assertions, and a
human reading logs are not substitutes for automated runtime outcomes.

### Common command contract

Each executable scenario must expose a non-interactive mode with these concepts
even if exact option spelling differs by module:

```text
--smoke
--recovery-rehearsal
--soak <duration>
--seed <integer>
--artifacts <absolute-directory>
--fault-schedule <absolute-file>
--campaign-id <identifier>       # orchestrated runs only
--worker-id <identifier>         # orchestrated runs only
```

Requirements:

- `--smoke` lasts approximately 15 to 30 minutes and exercises every workload
  class without destructive fault density.
- `--recovery-rehearsal` lasts approximately 60 to 90 minutes and proves every
  enabled failure and recovery transition at least once.
- the final campaign lasts 4 hours after smoke and rehearsal pass;
- an optional 16-hour extended soak may add confidence in very slow leaks or
  drift, but it is not required for scenario or Phase E completion;
- success returns exit code `0`; assertion, timeout, leak, cleanup, or
  unexpected terminal outcomes return a nonzero code;
- Ctrl+C and normal process termination perform bounded cleanup and still write
  a partial summary;
- no mode requires a person to interpret success from free-form output.

### Deterministic fault schedule

`E3-ORCH` must generate the complete fault schedule before starting workers.
The schedule is immutable during a run and contains:

- schema version;
- campaign ID and integer seed;
- requested duration;
- generation timestamp for provenance only;
- monotonic offsets from campaign start, never wall-clock timestamps for
  dispatch;
- scenario ID, fault kind, target instance, parameters, and expected recovery;
- a stable hash of the normalized schedule.

The same seed, duration, scenario set, and generator version must produce the
same schedule. Persist it before the first worker starts so an abrupt machine or
orchestrator failure cannot erase what should have happened. Record actual
start/end timestamps separately from the planned monotonic offsets.

Schedule generation must enforce quiet windows at startup and shutdown, a
minimum recovery interval between faults targeting the same resource, and a
bounded total fault count. Never schedule simultaneous destructive faults
unless a scenario explicitly owns and validates that combination.

### Measurements and artifacts

Use one aggregate campaign directory under `artifacts/validation/phase-e/`.
Layout v2 gives each process an explicit worker identity so process capture and
scenario evidence cannot collide:

```text
<campaign-id>/
  schedule.json
  summary.json
  summary.md
  owned.json
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
        result.json
```

`worker-id` identifies the process/target instance; `scenario-id` identifies
the assertions it executes. An orchestrator supplies both `--campaign-id` and
`--worker-id`, and the aggregate `summary.json` indexes every worker result.
Standalone scenarios keep the original v1 layout directly under their generated
run directory. Existing v1 artifacts are historical evidence and are never
moved or rewritten.

The environment record must include the repository commit and dirty state,
Windows version, process architecture, target framework, .NET runtime/SDK,
scenario version, dependency versions, machine CPU architecture, logical CPU
count, and installed memory. Provider, driver, container, COM, and Host versions
belong in the owning scenario's result.

At a stable cadence, record at least:

- private bytes and managed heap where available;
- process, thread, and handle counts;
- operation totals, successes, expected failures, unexpected failures, and
  cancellations;
- active/retained item counts for bounded components;
- queue/gate depth or a truthful unavailable marker;
- last progress time and cleanup state.

Take pre-workload, post-warm-up, periodic, pre-fault, post-recovery, and final
samples. Report trends and bounded counts. Do not invent a universal memory,
latency, or throughput threshold before measurements establish a baseline.

### Global pass conditions

Every scenario must assert all applicable conditions:

- its requested workload completed and made forward progress;
- every injected failure produced the expected terminal outcome;
- every expected recovery returned the scenario to useful work;
- no deadlock or progress timeout occurred;
- no semaphore, navigation gate, connection, transaction, reader, stream,
  socket, pipe, serial port, process, timer, or subscription remained owned
  after cleanup;
- bounded queues, snapshots, caches, and retained histories remained bounded;
- memory, thread, and handle samples show no unexplained monotonic growth after
  warm-up and recovery windows;
- cleanup is deterministic and repeatable;
- all child processes and endpoints left behind are identified as failures;
- result files are complete enough to reproduce the run.

## `E3-ORCH` — deterministic long-running campaign

### Deliverables

Build `runtime_tests/Confidence/LongRunning/` as a thin PowerShell
orchestrator. It must contain an operational `README.md`, `run.ps1`, a
versioned configuration schema, and deterministic schedule generation. It may
launch scenario executables but must not contain their business assertions.

The orchestrator must:

- validate prerequisites without modifying product or machine configuration;
- accept an explicit list of scenarios so incomplete scenarios do not block
  independent work;
- build or locate each selected executable explicitly, never through
  `dotnet test`;
- allocate unique local ports, pipe names, database names, COM pairs, and
  artifact directories;
- start only local resources and record every PID or container ID it owns;
- start workers, wait for readiness probes, dispatch scheduled faults, monitor
  heartbeat/progress, and collect exit codes;
- stop owned resources gracefully first, then force only exact owned targets
  after a bounded timeout;
- never stop a process, container, COM endpoint, or service it did not start or
  explicitly adopt;
- write a final aggregate pass/fail result even when one worker fails;
- leave enough evidence to distinguish worker failure, fault-controller
  failure, environment failure, and orchestrator failure.

### Campaign phases

1. **Preflight:** verify disk space, runtime prerequisites, configured external
   resources, unique endpoint allocation, and artifact writability.
2. **Baseline:** sample idle resources before workers start.
3. **Warm-up:** start every worker and require readiness plus initial progress.
4. **Workload:** run normal traffic before fault injection.
5. **Fault/recovery:** dispatch the persisted seeded schedule and require each
   owning scenario to acknowledge planned and actual outcomes.
6. **Cool-down:** stop injecting faults and require ordinary progress again.
7. **Shutdown:** request bounded graceful cleanup in dependency order.
8. **Reconciliation:** verify workers, processes, endpoints, and artifacts; then
   emit the aggregate result.

### Acceptance

The same smoke seed must generate byte-equivalent normalized schedules in two
consecutive runs. A deliberately failed worker must make the campaign fail
without preventing other workers from cleaning up. Killing the orchestrator
during a disposable rehearsal must leave a prewritten schedule and allow the
next run to identify stale owned resources without deleting unrelated ones.

## `E3-NAV` — Navigation long-running and recovery

### Deliverables

Build a native Windows scenario with a shared workload core and the smallest
necessary WinForms/WPF hosts:

- WinForms on `net481` and `net9.0-windows`;
- WPF on `net9.0-windows`;
- a non-interactive mode whose assertions do not depend on screen pixels;
- an interactive start/end procedure that reuses the expectations from the
  existing WinForms and WPF smoke scenarios.

Do not call a hidden/headless native host visual evidence. The automated mode
proves lifecycle and resource behavior; the interactive pass proves the visible
adapter behavior.

### Workload

The scenario must cover:

- thousands of page switches across transient, strong-singleton, and
  weak-singleton pages;
- forward/back navigation with state capture and restoration;
- repeated sign-in/sign-out cycles with role/permission allow, rejection, and
  redirect paths;
- repeated reset cycles followed by successful navigation;
- repeated `PageNavBootstrap.Start()` / awaited
  `NavigationService.Shutdown()` cycles;
- strong cache identity reuse and disposal on reset/shutdown;
- weak cache reuse while rooted and recreation after the scenario releases the
  last strong reference and observes collection;
- transient-page disposal after leaving;
- `KeepAttachedWhenHidden` behavior without retaining pages after teardown;
- all three load modes, including successful, failed, discarded, and late
  background results;
- redirect chains, redirect depth/cycle rejection, guard denial, guard timeout,
  and a throwing guard;
- forward/back history invariants after success, denial, redirect, and reset;
- repeated Toast, Dialog, Prompt, and Popover opening/completion/teardown,
  including overlapping modal depth and pending awaiters during reset/shutdown;
- repeated idle-timeout cycles, sign-out-to-idle behavior, user interaction
  rearming, failed/denied idle navigation, `StopIdle`, and shutdown near a tick;
- navigation requests admitted before shutdown and requests rejected after the
  shutdown cutoff;
- background loading, reset, and shutdown racing only through supported public
  calls;
- final zero attached/visible page and surface ownership after shutdown.

### Instrumentation and leak checks

Subscribe through public application surfaces and count event handlers,
request terminals, page construction/disposal, background terminals, and
surface completion. Assert exactly one terminal per request and no static
subscriber behavior after shutdown. Use weak references to scenario pages and
handler owners to verify that completed runtime instances are collectible.

Sample memory, threads, handles, page-instance counts, cache counts, history
depth, active attempts, queue/gate state, background operations, overlays, and
idle state. Capture passive Navigation Inspection snapshots only when enabled;
do not add actions or walk live UI objects from a consumer thread.

### Faults and recovery

Scenario-owned pages and guards may deterministically throw, delay, deny, or
redirect. Inject failures at registry lookup, creation, load, enter/leave
lifecycle, background load, guard, surface binding/show/cleanup, and dispatcher
availability where public adapter composition permits it. After each failure,
assert the documented rollback or blank-shell terminal and then perform a
successful navigation.

### Acceptance

All three target/platform combinations must complete smoke mode. The 4-hour
claim requires at least one native host to complete the full soak and the other
combinations to complete smoke plus recovery rehearsal. Record the exact matrix
instead of generalizing one adapter to the other. No change to the canonical
lifecycle order or frozen core is part of this scenario.

## `E3-OBS` — Logging, Telemetry, and passive Inspection

### Deliverables

Build one console scenario that composes the three opt-in capabilities as an
application. Target both `net481` and `net9.0`. Give each capability independent
assertions and result sections so a shared process does not turn them into one
claimed feature.

### Logging workload

Exercise and assert:

- sustained ordered writes at a configurable expected PDV rate;
- concurrent writers while preserving the Logger's observable ordering
  contract;
- minimum-level filtering;
- bounded recent snapshots after writing several multiples of capacity;
- snapshot ordering and `maxEntries` behavior during sustained writes;
- sink-failure isolation with a scenario-owned sink that throws on a seeded
  schedule while a healthy sink continues receiving entries;
- rolling-file rotation using a deliberately small maximum size;
- exact retained-file count and deterministic eviction of older rolled files;
- flush during ordinary activity and immediately after an injected incident,
  including bounded `ILogFlusher.Flush(timeout)` outcomes;
- disposal with `DisposeSinks` enabled and disabled;
- shutdown after sink failure and repeated create/use/dispose cycles;
- readable, complete final files with no unexpected process-held file handle.

The normal-rate phase and forced-rotation phase must be reported separately.
A tiny test file proves rotation mechanics, not production retention capacity.

### Telemetry workload

Exercise and assert:

- operation creation and completion under sustained concurrent activity;
- bounded completed-operation retention after several multiples of capacity;
- checkpoint ordering and monotonic elapsed values;
- operation ID and parent/correlation propagation across nested application
  work;
- success, expected failure, cancellation, and application-defined terminal
  measurements;
- snapshots while producers are active and after completion;
- repeated reads with `maxOperations` boundaries;
- the actual behavior of an operation scope that is abandoned without
  `Complete`, if the public API permits this, without inventing a stronger
  contract than the current implementation;
- no mutation of previously returned immutable operation/snapshot data.

### Inspection workload

Exercise and assert:

- operation retention and eviction at several multiples of configured
  capacity, using `InspectionRuntimeDiagnostics` totals and sequence bounds;
- state providers that return normally, return null, throw, and exceed the
  snapshot budget;
- provider failure and timeout isolation: healthy providers and operations
  still appear in a partial snapshot;
- repeated register/unregister cycles with provider counts returning to
  baseline;
- repeated local runtime and `EnableGlobal()` enable/dispose cycles;
- restoration of the process-wide provider slot after global disposal;
- concurrent recording and snapshot capture;
- final provider/action counts and retained ownership after disposal.

Register no actions. The scenario must assert that its own action count remains
zero; action invocation and module action rollout are explicitly out of scope.

### Acceptance

Each capability must be able to fail independently without suppressing the
other two result sections. The scenario passes only when all configured
capability checks pass, all files are closed, all registrations are disposed,
and every bounded count is within its configured capacity.

## `E3-PIPE` — Pipes long-running and recovery

### Deliverables

Build separate local server and client processes using real Windows named
pipes, plus a controller that starts both with a unique per-run pipe name.
Target both `net481` and `net9.0`. Use the current public access-policy,
queue-overflow, metrics, client, server, and event APIs; do not create a
privileged control plane.

### Workload

Exercise and assert:

- sustained request/response with small, medium, and near-limit payloads;
- multiple concurrent clients while request names and response correlation
  remain correct;
- disconnect/reconnect cycles initiated by both client and server;
- event subscriber connect/disconnect churn;
- slow subscribers under every supported bounded queue overflow policy;
- ordering for retained events and truthful dropped-event metrics;
- request timeout and cancellation without corrupting the next response;
- unknown operation and handler failure error contracts;
- over-limit inbound and outbound frames with the documented protocol error;
- malformed/truncated frames from a scenario-owned raw pipe peer;
- disposal while requests, handlers, connections, publishes, and subscribers
  are active;
- server restart on the same logical endpoint after complete disposal;
- stable request/event/error/connection metrics under sustained traffic.

### Faults and recovery

The schedule may kill only controller-owned client/server processes, close a
raw peer mid-frame, delay a handler, block one subscriber, force a timeout, and
request disposal during admitted work. Every fault must have an explicit
expected terminal, bounded recovery time, and a successful post-recovery probe.

### Resource checks

Sample server and client memory, threads, handles, active connections, queue
depth or dropped count, admitted/in-flight requests, and progress counters.
After cleanup, assert that the unique pipe can be rebound and that no owned
process remains. Do not claim ACL, remote-host, elevated-user, or adversarial
security coverage from a same-user local test.

## `E3-WDOG` — Watchdog deployed Host crash and recovery

### Deliverables

Build on the existing Supervisor scenario but validate package behavior through
the deployed sidecar layout produced by the canonical package flow. Use a
scenario child application, the deployed `NekoLib.Watchdog.Host`, and an
independent controller. The controller owns every child PID and writes the
fault acknowledgements; the product API receives no crash-test controls.

### Workload

Exercise and assert:

- bootstrap of the deployed Host and bounded attach PID/token handshake;
- supervision of the initially attached application and later restarted child
  instances;
- repeated ordinary child exits and configured restarts;
- repeated unhandled crashes at deterministic scheduled offsets;
- fast crash loops and the current bounded restart/backoff terminal behavior;
- clean application shutdown without an unwanted restart;
- Host shutdown and restart while preserving only documented arguments/state;
- application log forwarding through the real Watchdog pipe path;
- crash-bundle pending input finalization, manifest creation, optional checksum
  behavior, Watchdog log tail, retention count, and cleanup;
- Host and application version/status values recorded in the bundle;
- exactly one active supervisor/Host for one scenario child;
- no duplicate restart, duplicate forwarding subscription, or duplicate bundle
  finalization after reconnect/restart;
- attach/bootstrap repeated across independent runs.

### Crash schedule

Generate child crash events from the campaign seed and monotonic offsets.
Persist each planned crash before launch and have the child record a durable
"armed" acknowledgement before triggering it. Distinguish an intentional
scenario crash from an unexpected crash by campaign ID, event ID, and child
generation. A missing expected crash, unexpected crash, or duplicate restart is
a failure.

### Recovery and cleanup

After each recoverable crash, require the replacement child to complete a real
readiness/health probe and a log-forwarding probe. At campaign end, request
clean shutdown, wait boundedly for the child and Host, and force only recorded
owned PIDs if necessary. Assert no remaining process, pipe endpoint, pending
bundle, or half-written manifest.

The scenario must not claim update orchestration or Linux support. It must
record whether the run used source layout, disposable package layout, or a
published consumer layout; only the latter two can support a package-behavior
claim.

## `E3-DEV` — Devices virtual-COM soak and recovery

### Deliverables

Extend the existing `runtime_tests/Devices/Com0Com/` scenario rather than
creating a competing serial harness. Keep com0com as the real virtual-COM
transport boundary and keep the PCB-A/PCB-B emulator as an independent oracle
with no project reference to NekoLib.

Add smoke, recovery-rehearsal, and soak modes for both `net481` and `net9.0`.
The controller must adopt only explicitly configured COM pairs and emulator
processes that it started.

### Workload

Exercise and assert:

- repeated open, request/read, close, and reopen cycles;
- repeated finite timeout followed by a successful operation;
- cancellation while a read is pending followed by a successful operation;
- delayed complete responses and responses delivered in partial chunks;
- connection loss and reconnect using the same endpoint;
- explicit endpoint-switching attempts while open and after close, with the
  current documented allow/reject behavior;
- concurrent callers proving operation serialization;
- disposal during an active operation and idempotent final cleanup;
- no response from a timed-out/cancelled operation being consumed by the next
  operation;
- PCB-A text framing and line-reading behavior;
- PCB-B binary framing, CRC, command/response, and protocol readiness;
- configuration parity including baud, data bits, stop bits, parity, handshake,
  DTR, RTS, encoding, newline, and infinite/finite read timeout combinations;
- repeated emulator delay, silence, malformed frame, disconnect, and restart;
- port/process/handle stability over the soak.

### Acceptance and limits

Every injected delay, silence, cancellation, malformed frame, and disconnect
must produce the expected terminal and be followed by a successful clean
request. Record exact COM names, com0com version/configuration, process
architecture, target framework, emulator commit, and protocol mode.

This proves real Windows serial-port API behavior against paired virtual ports
and an independent protocol oracle. It does not prove physical UART levels,
wiring, USB adapter behavior, electrical noise, or Linux serial compatibility.

## `E4-SQL` — Data against local SQL Server

### Environment decision

Use one local SQL Server Linux container on WSL 2, hosted on the same Windows
AMD64 machine as the test process. The NekoLib scenario runs on Windows and
connects through a loopback TCP port mapped to the container.

### Current local reference setup

The repository owner reported this machine-local prerequisite as configured on
2026-08-08:

| Setting | Value |
|---|---|
| Container name | `nekolib-sqlserver` |
| Image | `mcr.microsoft.com/mssql/server:2022-CU26-ubuntu-22.04` |
| Reported host/container port | `1433:1433` (bind address not yet verified) |
| Host endpoint | `localhost,1433` |

Use these exact PowerShell commands for the pre-existing container:

```powershell
docker start nekolib-sqlserver
docker stop nekolib-sqlserver
docker restart nekolib-sqlserver
```

The repository owner confirmed all three commands in a local PowerShell session
on 2026-08-08. The Docker CLI was not available in the documenting Codex
process, so the image, resolved digest, bind address, current state, and SQL
Server readiness were not independently queried here. Before the first scenario
run, query the container without printing its environment and record those
values in the run artifacts.

The abbreviated Docker mapping `1433:1433` commonly publishes on every host
interface rather than proving the loopback-only requirement below. Connecting
through `localhost,1433` does not establish the bind restriction. The scenario
must record Docker's actual `HostIp`; if it is not loopback, report the setup gap
instead of silently claiming local-only exposure. Recreating the user-owned
container to change that binding is outside the scenario's authority.

Treat this named container as an explicitly adopted, user-owned prerequisite.
The scenario may start, stop, or restart it only for documented setup and fault
steps, and must restore its initial running/stopped state during cleanup. It
must not remove or recreate the container, its volume, network, or credentials.
Never write the SQL Server password to source, documentation, command arguments,
logs, result files, or generated connection strings; read it from the scenario's
documented environment variable at process startup.

Requirements:

- WSL 2 and a Docker- or Podman-compatible container engine;
- an official SQL Server Linux AMD64 image pinned to an exact version/tag for
  the committed scenario; never use `latest` as verification evidence;
- record the resolved image digest and `SELECT @@VERSION` output;
- publish the database port to loopback only and allocate a conflict-free local
  port;
- source credentials from environment variables or a local secret mechanism;
  never commit credentials or a populated database;
- use an ephemeral container/database by default; add a volume only for an
  explicitly named persistence/restart subcase;
- keep `Microsoft.Data.SqlClient` and every container/helper dependency only in
  the runtime scenario projects;
- target Windows x64 on both `net481` and `net9.0` and record the exact client
  package/runtime versions.

The scenario documentation must distinguish Microsoft ADO.NET abstractions,
the `Microsoft.Data.SqlClient` concrete provider, and the SQL Server engine.
Passing this scenario establishes evidence for the recorded combination only;
it does not add a provider dependency or universal SQL Server support claim to
`NekoLib.Data`.

### Deliverables

Create `runtime_tests/Data/SqlServer/` with:

- an operational `README.md`;
- a pinned container definition and readiness probe;
- setup/cleanup PowerShell scripts that operate only on a uniquely labelled
  scenario container, network, database, and optional volume;
- a dual-target console project or shared core plus dual-target launchers;
- deterministic schema/seed creation with no dependency on legacy tracked
  fixtures;
- smoke, recovery-rehearsal, and soak modes;
- a result record containing server, image, provider, target, OS, and
  architecture versions.

### Core execution matrix

Execute real commands and assert values, not only generated SQL:

- open/close and repeated gateway-created connection use;
- explicitly supplied connection and session ownership boundaries;
- pool reuse under sequential and concurrent load;
- bounded concurrency near the configured pool limit without leaking checked
  out connections;
- clearing/recreating the pool only from scenario/provider code when required
  for cleanup, never from `NekoLib.Data`;
- raw, typed DTO, callback, dynamic, translator, and supported streaming read
  shapes over the same rows;
- nullable values, strings, integers, decimals, booleans, date/time values,
  aliases, and provider-returned scalar conversions;
- parameterized insert, update, delete, predicates, ordering, paging,
  aggregates, joins, and subqueries supported by the SQL Server translator;
- transaction commit, explicit rollback, exception-triggered rollback, and
  disposal without commit;
- repeated connection/session disposal and use-after-dispose behavior;
- reader/stream early exit, cancellation, enumeration failure, and cleanup;
- provider error propagation without duplicate query lifecycle terminals;
- successful ordinary work after every expected provider failure.

Truthfully separate QueryBuilder/translator construction, fake ADO.NET contract
tests, and commands executed by SQL Server. Do not cite SQLite, Access, or an
unused fixture as server-provider evidence.

### Mid-flight cancellation

Use a command whose execution is known to have started and remains active long
enough to cancel deterministically, such as a scenario-owned SQL batch with
`WAITFOR DELAY` followed by a harmless query. Coordinate start through a
server-visible or scenario synchronization point where practical; do not rely
only on a short wall-clock sleep.

For raw, typed, dynamic, callback, streaming, and session-bound paths that
support cancellation:

- begin the operation with a non-cancelled token;
- observe that execution has started;
- cancel from the scenario at the scheduled monotonic offset;
- require `OperationCanceledException` or the current documented cancellation
  terminal, not a generic provider failure;
- assert one cancellation/error lifecycle terminal and no success terminal;
- dispose the command/reader/session according to ownership;
- immediately perform a successful probe through the same gateway and through
  a newly acquired pooled connection;
- verify that an active transaction was rolled back or left in the explicitly
  documented state.

Also retain the already-cancelled-token matrix so refusal-before-start and
interruption-after-start remain distinct claims.

### Network and server recovery

The controller may pause/stop only the uniquely labelled scenario container or
its dedicated network. Cover:

- connection attempt while the server is unavailable;
- transport loss during a long-running command;
- transport loss during an active transaction;
- failure while streaming rows;
- container restart with an ephemeral database and, separately if configured,
  restart with a persistent volume;
- stale pooled connections after interruption;
- bounded reconnect/retry owned by the scenario, without inventing automatic
  retry behavior in NekoLib;
- schema recreation/readiness followed by successful ordinary commands.

Record the provider exception type/code and whether the failed connection was
returned to or removed from the pool. The scenario passes only if recovery uses
fresh valid work and all failed resources are disposed.

### Dynamic-result lifetime

Exercise `DynamicMode.IL` against the real provider with deterministic varying
row shapes. Generate shapes by changing aliases, selected-column sets, and
compatible SQL types without generating unbounded database objects.

The run must:

- establish the configured schema/type capacity;
- query below, at, and beyond the capacity boundary;
- retain some results and release others according to explicit phases;
- verify the documented fallback/failure behavior after the cap;
- sample managed memory and generated-type/schema counts through warm-up,
  boundary crossing, and continued steady work;
- confirm that ordinary dynamic and typed queries still succeed after the
  boundary;
- report process-wide non-unloadable IL type behavior truthfully rather than
  claiming collection of generated types.

Varying data values under one row shape does not satisfy this test.

### Interactive evidence

The SQL Server scenario may remain console/headless because its claims are
provider and resource behavior. Separately, perform the existing FarmDatabase
native UI procedure at the beginning and end of the campaign to verify that
the application remains navigable and presents SQLite/Access results correctly.
Record this as interactive evidence unless a versioned UI driver exists. Do not
imply that the FarmDatabase UI executed SQL Server unless it is deliberately
extended and verified for that provider.

### Acceptance

Both target frameworks must pass the smoke and cancellation matrices against
the same recorded server image. Recovery rehearsal must prove command,
transaction, streaming, pool, network, and container restart behavior. The
4-hour Data claim requires sustained sessions/transactions/reads plus the
dynamic-shape workload and deterministic failure schedule, with no leaked
connection, command, transaction, reader, stream, or container resource.

Promote a provider-native hook, data-source lifecycle adapter, or cancellable
factory expansion only when this evidence reproduces a concrete gap that cannot
be expressed through the current seam.

## Traceability to Phase E3 and E4

| Roadmap requirement | Owning scenario |
|---|---|
| Thousands of switches; forward/back; login/logout; reset; repeated start/shutdown | `E3-NAV` |
| Cache reuse; weak/strong lifetimes; background load; redirects; guard rejection | `E3-NAV` |
| Overlay and idle cycles; memory; retained handlers; disposal | `E3-NAV` |
| Sustained logging; sink isolation; rotation; retention; incident flush; shutdown; snapshots | `E3-OBS` |
| Telemetry retention; completion; abandonment; checkpoints; correlation; sustained snapshots | `E3-OBS` |
| Inspection retention; provider timeout/failure; enable/dispose; no actions | `E3-OBS` |
| Pipe request load; reconnect; churn; slow subscribers; frame failures; timeout; active dispose | `E3-PIPE` |
| Pipe memory/thread growth and bounded queue behavior | `E3-PIPE`, `E3-ORCH` |
| Child exit/restart; clean shutdown; fast crash loop; attach/bootstrap | `E3-WDOG` |
| Log forwarding; crash bundles; Host restart; no duplicate supervision | `E3-WDOG` |
| Device timeout/recovery; reconnect; delay; endpoint switching; serialization | `E3-DEV` |
| Device disposal; cancellation; late-response isolation | `E3-DEV` |
| Repeated Data connection/session use; disposal; transactions; streaming cleanup | `E4-SQL` plus existing FarmDatabase evidence |
| Data provider failures and cancellation | `E4-SQL` |
| Unattended script, schedule, deterministic cleanup, and aggregate exit code | `E3-ORCH` |
| Required four-hour deterministic seeded crash/failure campaign | `E3-ORCH` and each fault-owning scenario |
| Optional sixteen-hour extended confidence campaign | `E3-ORCH` and any selected fault-owning scenario |
| No unbounded memory/handler growth, leaked resources, deadlocks, or unreleased gates | Every scenario, aggregated by `E3-ORCH` |
| Real/emulated COM with an independent oracle and explicit physical limits | Existing Com0Com plus `E3-DEV` extension |
| SQLite baseline and Access positional binding | Existing FarmDatabase evidence |
| One initial real server provider | `E4-SQL`: local SQL Server container |
| Pool/data-source ownership; network failure; transactions; mapping | `E4-SQL` |
| Exact package, target, architecture, image, and server versions | `E4-SQL` evidence record |
| Provider packages remain outside relational core | `E4-SQL` project topology |
| Translator/fake/real execution claims remain distinct | `E4-SQL` result sections |
| Dynamic-result lifetime with varying shapes and `DynamicMode.IL` | `E4-SQL` |
| Mid-flight rather than only pre-cancelled cancellation | `E4-SQL` |
| Manual versus versioned automated UI evidence remains explicit | `E3-NAV`, `E4-SQL`, and the suite evidence rules |
| Watchdog package claims use deployed sidecar layout | `E3-WDOG` |

## Suggested implementation order

1. Implement `E4-SQL` smoke and recovery modes while the local container
   environment is being established.
2. Implement `E3-ORCH` against `E4-SQL` and the already-headless FarmDatabase
   mode, proving schedule, artifacts, exit-code aggregation, and cleanup.
3. Implement `E3-NAV` and `E3-OBS`; they have no external hardware dependency.
4. Extend the existing com0com scenario as `E3-DEV`.
5. Implement `E3-PIPE`, then use its real IPC boundary from `E3-WDOG` without
   merging the two scenarios' assertions.
6. Run smoke for every implemented target, then recovery rehearsal.
7. Freeze the exact scenario/source commit, dependency versions, environment,
   and schedule generator before starting the 4-hour campaign.

Do not start the final campaign merely because a project builds. Every selected
scenario must first pass its smoke and recovery rehearsal with automated exit
codes and clean resource reconciliation.
