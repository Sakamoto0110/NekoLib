# NekoLib.Watchdog — Technical Reference

**Document ID:** WDG-REFERENCE

**Schema version:** 1

**Kind:** reference

**Lifecycle:** current

**Subject:** normative technical contract for the NekoLib.Watchdog boundary

**Surface:** technical-reference

**Boundary:** watchdog

**Authority role:** normative

**Mutation:** authored

**Indexing:** include

`NekoLib.Watchdog` is a Windows process-supervision library for unattended
applications on `net481` and `net9.0-windows`. It supervises exactly one target
executable: it attaches to an already-running instance or launches one, watches
it, replaces it after an exit, exposes a bounded control and event channel over
named pipes, forwards operational logs, and finalizes crash evidence.

The library owns supervision behavior. The companion `NekoLib.Watchdog.Host`
package owns sidecar deployment, payload selection, executable arguments, and
release layout; those are a separate distribution with separate evidence.

```csharp
static void Main(string[] args)
{
    WatchdogBootstrap.EnsureStarted(args);
    // normal application startup
}
```

## Targets, dependencies, and package boundary

| Target | Custom symbols | Project dependencies | Package dependencies |
|---|---|---|---|
| `net481` | `NETFRAMEWORK` | `NekoLib.Core`, `NekoLib.Pipes` | `Newtonsoft.Json` 13.0.3 |
| `net9.0-windows7.0` | `NET_9`, `NET9` | `NekoLib.Core`, `NekoLib.Pipes` | none |

The project does not declare `NEKOLIB`. Nullable analysis is enabled, implicit
usings are disabled, and `net481` additionally sets `NoWarn 1591`.

Core types are deliberately part of the public surface through
`NekoLib.Core.Logging.ILogSink`, `NekoLib.Core.Logging.LogEntry`, and
`NekoLib.Core.Telemetry.ITelemetry`. Pipes and Newtonsoft.Json are
implementation dependencies: no Pipes or Newtonsoft type appears in a public
signature. On `net481` the wire is produced by Newtonsoft.Json and on
`net9.0-windows` by `System.Text.Json`; `LogEvent.MetaJson` exists so that this
split never reaches the consumer.

The two accepted manifests under
[`eng/public-api/NekoLib.Watchdog/`](../../../eng/public-api/NekoLib.Watchdog/)
are identical for namespace types and members and differ only in their
assembly-level target and platform attributes. The modern assembly carries
`SupportedOSPlatform("Windows7.0")`.

The `NekoLib.Watchdog` package contains its two target assemblies with their
matching XML documentation files, plus dependency groups. It embeds no Host
payload and exposes no `tools/` or `build/` asset. `eng/pack-local.ps1` enforces
the XML pairing for every managed package as a permanent guard.

## Supported composition paths

There are exactly two supported ways to use this library.

**The application path** calls `WatchdogBootstrap.EnsureStarted` near the start
of `Main`. Bootstrap locates the deployed Host below the application base
directory, starts it, hands over the current process, and returns. The
application then uses `WatchdogController` to observe or control its own
supervisor.

**The advanced path** hosts `WatchdogRuntime` directly in a custom supervisor
process:

```csharp
var options = new WatchdogOptions
{
    TargetPath = targetPath,
    EnableHotkeys = false
};

using (var runtime = new WatchdogRuntime(options))
{
    Console.WriteLine(runtime.PipeName);
    runtime.Start();
    runtime.WaitForExit();
}
```

This is a deliberate public extension, not Host-only infrastructure. The owner
must reach a terminal state through `Stop` or `Dispose` and accepts that doing
so **terminates the supervised target**. A stopped runtime is terminal;
construct a new instance to supervise again.

`WatchdogController` is static and bound once to the current executable. It is
not a controller for arbitrary targets: only `ResolvePipeNameForTarget` and
`NotifyExceptionForTarget` accept another target path, and neither performs
control. A custom controller for a different target derives the pipe name and
sends the public `WatchdogCommands` constants through `NekoLib.Pipes` itself.

## Bootstrap and attach

`EnsureStarted` is synchronous and bounded by one total handshake budget
(default 5,000 ms; the two-argument overload requires a positive value).

Before anything else it checks the recursion guard: if `NEKO_UNDER_WATCHDOG` is
present it returns immediately, without validating its arguments. The guard
reads the variable and, because .NET Framework reports an explicitly empty
process variable as null, also inspects the environment block for the key. A
process launched as a replacement always carries it.

A process-wide lock serializes concurrent in-process bootstrap calls. Inside
that lock the call:

1. resolves the current executable path and PID, and derives the target pipe
   name from the path;
2. spends a preflight slice — `min(500, max(1, budget / 4))`, further capped by
   the remaining budget — asking any already-running Host for `protocol_version`
   and then `attach_status`. A Host already supervising this exact PID means the
   call is satisfied and returns. A Host that answers with a different
   supervised PID, an error code, or an unparsable identity fails the call with
   an explanatory `InvalidOperationException`;
3. otherwise requires `<base>/NekoLib.Watchdog.Host/NekoLib.Watchdog.Host.exe`
   to exist, or throws `FileNotFoundException`;
4. generates a one-time token, re-encodes the supplied arguments with Windows
   command-line quoting, and starts the Host hidden with the sidecar directory
   as its working directory;
5. polls for the versioned attachment identity within the remaining budget,
   alternating a bounded `protocol_version` check and a bounded `attach_status`
   request with 50 ms pauses.

Success means the Host returned exactly `attached:v1:<pid>:<token>` for the
current PID and this call's token.

Failure is specific rather than generic:

| Condition | Result |
|---|---|
| Host answers with an unexpected protocol version or a protocol error | `InvalidOperationException` naming the observed and expected versions and instructing to align both package versions |
| Host process exited before confirming | `InvalidOperationException` naming the Host fatal-evidence path |
| Budget elapsed with a live Host | `TimeoutException` |
| Deployed Host missing | `FileNotFoundException` |
| Running Host supervises a different PID | `InvalidOperationException` naming both PIDs |

On any failure, bootstrap terminates the Host process **it launched** and could
not confirm. It never terminates a Host it did not start.

The launch command line is an internal contract between the coordinated
`NekoLib.Watchdog` and `NekoLib.Watchdog.Host` packages; its options, the
`protocol_version` exchange, and the attachment shape are owned by the
[Host reference](../WatchdogHost/REFERENCE.md).
Applications do not construct it.

## Configuration ownership and capture

The `WatchdogRuntime` constructor validates and captures a normalized internal
snapshot. It does not mutate `WatchdogOptions`, and later property or outer
`LogSinks` array changes have no effect on a constructed runtime. Sink and
telemetry objects are references owned by the caller; the runtime never disposes
them.

Construction rejects, in this order:

- a null options object (`ArgumentNullException`);
- a null, empty, or whitespace `TargetPath` (`InvalidOperationException`);
- an `InitialProcessId` below 1, an `InitialProcessId` without a non-blank
  `AttachToken`, or an `AttachToken` without an `InitialProcessId`
  (`InvalidOperationException`);
- a `TargetPath` that does not resolve to an existing file
  (`FileNotFoundException`).

Capture then normalizes:

| Value | Normalization |
|---|---|
| `TargetPath` | full path; must exist at construction |
| `WorkingDirectory` | full path; defaults to the target's directory |
| `TargetArguments` | `null` becomes empty |
| `LogPath` | full path; defaults to `watchdog.log` under the working directory **only when `EnableFileLogging` is true** |
| `MaxLogBytes` | at least 64 KiB |
| `MonitorPollMs` | at least 50 |
| `RestartDelayMs` | at least 200 |
| `GracefulKillTimeoutMs` | at least 0 |
| `ForceKillTimeoutMs` | at least 100 |
| `PendingCrashRoot` | full path; defaults to `crash/pending` under the working directory |
| `BundleRoot` | full path; defaults to `crash/bundles` under the working directory |
| `LogSinks` | outer array cloned; `null` becomes empty |
| `PipeName` | derived from the captured target path; not settable |

`HeartbeatIntervalMs` and `MaxBundles` are captured verbatim. A non-positive
`HeartbeatIntervalMs` disables the heartbeat; a non-positive `MaxBundles`
disables retention deletion.

Capture creates the pending-crash root, the bundle root, and the log directory
best effort. A directory that cannot be created does not fail construction; the
unavailability surfaces when the corresponding operation runs.

The effective pipe identity is read from `WatchdogRuntime.PipeName`. That value
names the RPC endpoint; the event endpoint is its `.events` sibling, created by
`NekoLib.Pipes`.

`EnableHotkeys` defaults to `true` for compatibility. Set it before construction
for a headless or custom supervisor that must not claim process-wide keys.

## Lifecycle and terminal state

The lifecycle is one-shot and every transition is serialized by one lock:

```text
created -> starting -> running -> stopping -> stopped
```

- `Start` is admitted exactly once. A second call, or a call after `Stop`,
  throws `InvalidOperationException`.
- `Stop` before `Start` is terminal: the instance becomes permanently stopped
  and can never be started.
- Concurrent `Stop` and `Dispose` callers serialize on the same lock and observe
  the same terminal state. `Dispose` is exactly `Stop`.
- A failed `Start` performs terminal cleanup and rethrows. Cleanup after a
  failed start does **not** terminate an initially attached process, because the
  runtime did not start it; it does terminate a process the runtime launched.
- `WaitForExit` throws `InvalidOperationException` unless `Start` completed
  successfully, then blocks until complete terminal cleanup — not merely until
  the monitor loop ends.

`Start` performs, in order: claim the per-target instance slot, attach the
initial process when configured, create and start the current-user RPC/event
server, start the event publisher thread, log `[watchdog_start]`, start the
foreground monitor thread, and — when hotkeys are enabled — start the hotkey
thread and **block until it signals readiness**. Only then does the runtime
answer `attach_status` and enter `running`.

Shutdown posts `WM_QUIT` to the hotkey thread, terminates the current target,
joins the monitor and hotkey threads, disposes the target process handle,
completes the event queue, joins the event thread, disposes the RPC server, and
releases and disposes the instance semaphore.

Thread joins during shutdown are unbounded. The monitor's pause, restart-delay,
and crash-loop cooldown waits are all interruptible by the shutdown flag, so the
monitor exits promptly. A caller-supplied synchronous sink can still extend a
logging call and therefore shutdown; sinks must not block indefinitely.

## Process ownership

While running, the runtime owns:

- the current target `Process` handle;
- the per-target named semaphore `Global\NekoLib.Watchdog::<pipe-name>`, taken
  with a non-blocking acquire;
- the current-user RPC server and its event hub, admitting at most 8 RPC clients
  and 16 event subscribers;
- one foreground monitor thread (`WDG-Monitor`);
- one background event-publisher thread (`WDG-Events`); and
- one optional background hotkey thread (`WDG-Hotkeys`).

The monitor thread is foreground and keeps the hosting process alive until
shutdown. That is what lets the deployed Host block in `WaitForExit`.

Losing the instance semaphore means another runtime already supervises this
target. `Start` then optionally brings the existing target window to the
foreground — best effort, controlled by `BringToFrontOnStartIfRunning` — and
throws `InvalidOperationException`. A semaphore rather than a mutex is used
deliberately: the permit is released from whichever thread runs shutdown, which
is frequently not the thread that acquired it, and mutex ownership is
thread-affine.

Attaching to an initial process requires the PID to exist, to be alive, and for
its main module path to equal the captured `TargetPath` (full path, ordinal
case-insensitive). The attach path materializes the native handle while the
process is alive so its exit code stays observable after it terminates even when
no launcher retains another handle. Any failure throws
`InvalidOperationException` naming the PID.

Launched replacements start with `UseShellExecute = false`,
`CreateNoWindow = false`, the captured working directory and arguments, and
`NEKO_UNDER_WATCHDOG=1` added to the child environment. `TargetArguments` apply
to launched instances only; an attached initial process keeps the command line
it was started with.

## Supervision, restart, and crash-loop behavior

The monitor loop runs until shutdown:

1. While paused, it sleeps in `MonitorPollMs` slices.
2. It ensures a live target, launching one if needed. A launch failure is logged
   as `[child_start_failed]`, waits `RestartDelayMs`, and retries indefinitely —
   it never terminates the monitor thread.
3. It waits for the target in `MonitorPollMs` slices, emitting a `[heartbeat]`
   log and a telemetry publication when `HeartbeatIntervalMs` has elapsed. The
   heartbeat is therefore observed on a poll boundary, not on an exact timer,
   and only while a target is alive.
4. On exit it records the exit code, logs `[child_exit]`, finalizes crash
   evidence, disposes the target handle, applies crash-loop accounting, waits
   `RestartDelayMs`, and publishes telemetry.

Crash-loop protection: an exit less than three seconds after start increments a
fast-crash counter; a longer run resets it. Five consecutive fast exits log
`[crash_loop] cooling 10s`, wait ten seconds, and reset the counter. That wait
is interruptible by shutdown.

`restartCount` counts actual replacement launches. It is zero for the first
supervised process in both attach and launch modes and increments only when a
later replacement starts.

Termination is two-stage. `TryKill` requests `CloseMainWindow()` and waits
`GracefulKillTimeoutMs`. If the process is still alive it starts the absolute
`Environment.SystemDirectory\taskkill.exe` path with `/PID <id> /T /F`, waits
`ForceKillTimeoutMs`, and disposes the helper process. Resolving the absolute
path is deliberate: the forced path never honors an alternate `taskkill` from
the process search path. Because the graceful request is issued
unconditionally, a target with no main window always spends the full
`GracefulKillTimeoutMs` before the forced path runs. `/T` terminates the
target's process tree, not only the target.

Every stage of termination is best effort and swallows its own failures; the
graceful and forced attempts are logged.

## Control contract

`WatchdogCommands` publishes exactly six wire names: `ping`, `status`, `pause`,
`resume`, `restart`, and `stop`. `log_history`, `exception_notify`,
`protocol_version`, `attach_status`, `log_write`, `log_write_batch`, and
`update` are internal protocol names already wrapped by public APIs.

Controller calls are synchronous, with a 1,500 ms connect timeout and a 3,000 ms
request timeout per call:

| Member | Result |
|---|---|
| `Ping()` | `true` only for `pong` |
| `Status()` | serialized status evidence, or `error=watchdog_not_running` on timeout, `error=pipe_io` on any other transport failure, or `error=<protocol-code>` for a structured error |
| `Pause()` | `true` only for `paused` |
| `Resume()` | `true` only for `running` |
| `Restart()` | `true` only for `restarting` |
| `Stop()` | `true` only for `stopped` |
| `NotifyException`, `NotifyExceptionForTarget`, `NotifyLog` | fail-soft; never throw for pipe or protocol failure |

The acknowledgement describes command **acceptance**, not completion:

- `pause` and `resume` are applied before the reply.
- `restart` attempts termination of the current target before replying; the
  replacement launches after `RestartDelayMs`.
- `stop` replies first and performs shutdown on a pooled thread after a short
  delay. A `true` result therefore means the Host accepted the request, not that
  supervision has already ended. Poll `Ping` to observe the endpoint closing.

The runtime answers the internal `update` command with a structured pipe
**error** whose code is `not_implemented`. This exists only so a coordinated
Host/library pair answers deterministically; the library exposes no update
option and no supported update command. Update orchestration requires a separate
design and is explicitly not an experimental Watchdog feature.

## Log forwarding and subscriptions

`WatchdogPipeLogSink` is a public `NekoLib.Core.Logging.ILogSink` adapter.
Construction starts one background flush thread (`WDG-LogFlush`) and normalizes
invalid bounds to their minimums rather than rejecting them. `Write` never waits
for pipe I/O: it enqueues into a bounded local queue and increments cumulative
`DroppedCount` only when that queue is full. Entries are sent in batches so one
connection is amortized over many entries. Transport failures are swallowed.
Disposal completes the queue and waits at most three seconds for the flush
thread; it does not promise delivery of queued entries, and entries abandoned at
disposal or lost in a failed batch are **not** counted by `DroppedCount`.

The runtime deliberately skips any configured `LogSinks` element that is a
`WatchdogPipeLogSink`. Such a sink would push the runtime's own entries back
onto the control pipe, and the runtime would record them again. Placing one in
`WatchdogOptions.LogSinks` is therefore silently ineffective; use it in the
supervised application, not in the supervisor.

Entries received over the wire from `NotifyLog`, the internal batch path, or a
`WatchdogPipeLogSink` are recorded into the runtime's own stream without being
re-emitted to sinks, which closes the same loop from the other direction. Such
an entry is recorded at **info** severity regardless of its original level; the
original level and the category travel in the entry's metadata. The exception
text is transmitted but is not retained by the runtime.

The runtime keeps the latest 300 structured log entries for replay and emits
live log events through Pipes. `SubscribeLogs` first performs synchronous replay
on the subscribing thread, then starts a live listener whose callbacks run on
the event-client thread. **The handoff is not gapless**: entries produced
between the end of replay and the start of the live listener can be missed.
Ordering is preserved within replay and within the live stream, but not across
the handoff. Each application callback is exception-isolated, individually, in
both phases. `SubscribeLogLines` is `SubscribeLogs` projected to the
preformatted line, or the message when no line is present. The returned handle
is caller-owned and stops the live subscription when disposed; it does not undo
replay.

`WatchdogController.LogEvent` carries `TsUnixMs` plus nullable `Level`, `Msg`,
`Line`, and `MetaJson`. `MetaJson` is compact raw JSON text for an object,
array, scalar, or string, and is null when metadata is absent or JSON null. It
never exposes a Newtonsoft `JToken` or a `System.Text.Json.JsonElement`.

## Bounded events, counters, and status

Runtime log and telemetry events enter one 1,024-item FIFO queue drained by the
single event thread, which publishes each event with a 1,000 ms bound.
Supervision never blocks to make diagnostics lossless.

Queue-full drops are counted, and a warning is written on the first drop and
then every thousandth. That warning is written directly to the file log and to
configured sinks, bypassing the saturated queue.

`Status()` exposes cumulative, non-resetting counters that distinguish the loss
boundaries:

| Field | Meaning |
|---|---|
| `historyEvictions` | entries evicted from the 300-entry replay history |
| `eventQueueDropped` | events rejected because the producer queue was full |
| `eventPublishFailures` | queued events whose bounded publish attempt failed |
| `eventsDropped` | compatibility alias for `eventQueueDropped` |
| `restartCount` | replacement launches after the first supervised process |

Status also reports `state` (`running`, `paused`, or `stopped`), `uptimeMs`,
`childUptimeMs`, `restartReason`, `childPid`, `attachedInitialProcessId`,
`lastExitCode`, and a `metrics` object carrying the Pipes server, event, and
error snapshots with the Pipes module's cumulative snapshot semantics.

`WatchdogPipeLogSink.DroppedCount` is scoped to that sink's own queue and is not
part of status.

Telemetry, when supplied, receives one completed `Watchdog` operation per log
entry (`watchdog.log`) and per publication (`watchdog.telemetry`), with the
payload as a dimension. Telemetry failures are swallowed.

## Crash evidence

Crash finalization is runtime-owned internal implementation, not an independent
public filesystem API. It runs after every non-shutdown target exit when
`EnableCrashBundling` is true.

The newest `crash-*` directory under `PendingCrashRoot`, selected by creation
time, is copied recursively into `BundleRoot/bundle-<utc-timestamp>`. The
runtime then adds, each optionally:

- `watchdog-status.txt` — the serialized status snapshot;
- `watchdog.log.tail` — the last 600 lines of the runtime log, when file logging
  is enabled and the log exists;
- `manifest.json` — schema version 1, bundle ID, UTC timestamp, application and
  Watchdog version blocks, the restart reason and count, the checksum flag, and
  one entry per top-level bundle file with its name, size, and — when
  `EnableBundleChecksums` is true — its SHA-256.

The pending directory is then deleted and `MaxBundles` retention is applied to
the bundle root, newest first.

The internal outcome is `complete`, `partial`, or `failed`, plus a separate
no-pending result. Optional status, tail, manifest, pending-cleanup, and
retention failures produce a partial outcome and a truthful log entry naming the
failed parts. A mandatory copy or setup failure produces a failed outcome.
Reporting-callback exceptions are isolated and can never replace the outcome,
and the manifest is emitted by the target's JSON serializer, so control
characters are escaped correctly.

The restart reason recorded in the manifest is the reason carried by a preceding
`exception_notify` when one arrived, and `child_exit` otherwise. The
notification flag is reset after every finalization so one crash's reason cannot
contaminate the next.

The runtime supplies the Watchdog version and status callbacks but supplies no
application-version callback, so `application.version` in a runtime-produced
manifest is always null. Producing the pending `crash-*` content is the
application's responsibility — typically through `NekoLib.Diagnostics` — and is
outside this boundary.

## Hotkeys

When `EnableHotkeys` is true the runtime creates a message-only window on a
dedicated thread and attempts three fixed process-wide registrations:

| Key | Action |
|---|---|
| Ctrl+Alt+P | pause supervision |
| Ctrl+Alt+R | resume supervision |
| Ctrl+Alt+Q | stop the runtime and the supervised target |

Registration failure is written to configured local logging with the Win32 error
code; it does not fail startup. `Start` waits for the hotkey thread to signal
readiness, so a failure to create the window does not hang startup. There is
intentionally no public hotkey binding model, and the chords are not
configurable.

## Security boundary

RPC and event endpoints explicitly use `PipeAccessPolicy.CurrentUserOnly` on
both targets. This prevents ordinary cross-user access. It is **not**
authentication or authorization against a hostile process already running as the
same user: any such process can open the endpoint and issue `pause`, `restart`,
or `stop`, inject crash and log records, or read status and history. Command
dispatch is by name only; there is no per-command authorization, caller
allowlist, or session protocol.

The deterministic pipe name is the lowercase full target path hashed with SHA-1
and truncated to 16 hexadecimal characters. It is stable target identity, not a
secret, and a same-user process can compute or squat it. The attach token is
bootstrap correlation, not a credential: it is returned over the same
unauthenticated endpoint by `attach_status`, and PIDs and command lines are
observable.

The application or installer owns filesystem ACLs on the application, working,
log, and crash directories, binary signing and integrity, elevation topology,
and service identity. Watchdog provides no remote control, credentials, replay
protection, or privileged administration, and adding any of them requires an
accepted threat-model change rather than a new option.

Log, status, and crash payloads are transmitted and written verbatim. A secret
placed in a log message, a telemetry dimension, or pending crash content is
disclosed to whoever can open the endpoint or read the bundle directory.

## Extension points and non-goals

Deliberate extension surfaces:

- `WatchdogRuntime` plus `WatchdogOptions` for a custom supervisor process;
- `WatchdogPipeLogSink` as an `ILogSink` in an application's logging pipeline;
- `WatchdogOptions.LogSinks` and `WatchdogOptions.Telemetry` for
  application-owned observation of the runtime's own stream;
- `WatchdogCommands` plus `WatchdogController.ResolvePipeNameForTarget` for a
  controller written directly against `NekoLib.Pipes`.

Not provided, by decision: update or self-update orchestration, remote or
cross-machine supervision, fleet control, authentication or authorization,
supervision of more than one target per runtime instance, a configurable hotkey
model, Watchdog self-supervision, and Host deployment. The library also emits no
Inspection instrumentation; the broad rollout remains frozen in
[`ROADMAP.md`](../../../ROADMAP.md).

## Target differences

Behavior is intentionally identical across targets. The differences are:

| Concern | `net481` | `net9.0-windows7.0` |
|---|---|---|
| JSON | Newtonsoft.Json 13.0.3 package dependency | platform `System.Text.Json` |
| Platform annotation | none | `SupportedOSPlatform("Windows7.0")` |
| Missing-XML-comment diagnostic | suppressed by `NoWarn 1591` | active |
| Recursion guard | also inspects the environment block, because an explicitly empty variable reads as null | variable read is sufficient |

The public surface, the wire vocabulary, the acknowledgement strings, the
counters, and the crash-bundle layout are the same on both.

## Verification

```powershell
dotnet test tests\NekoLib.Watchdog.Tests\Unit\NekoLib.Watchdog.Tests.Unit.csproj -f net481 -m:1
dotnet test tests\NekoLib.Watchdog.Tests\Unit\NekoLib.Watchdog.Tests.Unit.csproj -f net9.0-windows -m:1
.\eng\verify-public-api.ps1 -PackageId NekoLib.Watchdog
```

The focused suite starts real child processes and binds real named pipes despite
its `Unit` project location, and it covers the Host argument parser and fatal
log as well, which belong to the deployment boundary rather than to this one.
The [deployed-Host crash and recovery scenario](../../../runtime_tests/Watchdog/CrashRecovery/README.md)
adds unattended supervision across real deployment and IPC boundaries, and the
[interactive supervisor scenario](../../../runtime_tests/Watchdog/Supervisor481/README.md)
is the only exercise of the advanced in-process composition.

[`VALIDATION_REQUIREMENTS.md`](VALIDATION_REQUIREMENTS.md) owns the evidence
contract derived from the profiles the [manifest](MANIFEST.md) inherits;
[`VALIDATIONS.md`](VALIDATIONS.md) records what has actually run and what has
not. Confirmed defects live in [`ISSUES.md`](ISSUES.md) and unverified
observations in [`FINDINGS.md`](FINDINGS.md); neither is scheduled work until it
is promoted to [`TODO.md`](../../../TODO.md).

Two limits are worth stating here, because they bound how far this document's
claims have been demonstrated. The `CurrentUserOnly` boundary has never been
observed denying a cross-user or cross-elevation peer; present evidence is
same-user success plus the Pipes implementation. Global hotkey registration,
window activation, and real forced termination have never been observed
interactively — the focused suite asserts the resolved system `taskkill` path
rather than an actual kill.
