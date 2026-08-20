# NekoLib.Watchdog

`NekoLib.Watchdog` provides Windows process supervision for unattended
applications on `net481` and `net9.0-windows`. It can bootstrap the separately
deployed Watchdog Host, control the Host for the current application, forward
operational logs, or be hosted directly by an advanced custom supervisor.

The library owns supervision behavior. The companion Host package owns sidecar
deployment, payload selection, executable arguments, and release layout; those
are separate contracts.

## Public composition paths

The ordinary application path starts the deployed Host near the beginning of
`Main`:

```csharp
static void Main(string[] args)
{
    WatchdogBootstrap.EnsureStarted(args);
    // normal application startup
}
```

`EnsureStarted` is synchronous and bounded by its handshake timeout. It derives
the target identity from the current executable, starts the deployed sidecar,
hands off the current PID plus a one-time correlation token, and terminates an
unconfirmed Host process it started. A restarted process sees
`NEKO_UNDER_WATCHDOG=1` and does not recursively bootstrap another Host.

`WatchdogController` is a static facade for the current application executable.
It is not a general controller for arbitrary targets. `WatchdogRuntime` is the
deliberate advanced surface for a custom supervisor:

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

The advanced owner must call `Stop` or `Dispose` and owns the policy decision to
terminate the supervised target. A stopped runtime is terminal; create a new
instance to supervise again.

## Configuration ownership

The constructor validates and captures a normalized internal snapshot. It does
not mutate `WatchdogOptions`, and later option or outer `LogSinks` array changes
do not affect the runtime. Sink and telemetry objects are references owned and
disposed by the caller; the runtime never disposes them.

Required and normalized values:

- `TargetPath` must identify an existing file when the runtime is constructed
  and is captured as a full path.
- `WorkingDirectory` defaults to the target directory.
- `MonitorPollMs`, `RestartDelayMs`, `GracefulKillTimeoutMs`,
  `ForceKillTimeoutMs`, and `MaxLogBytes` retain their documented lower bounds.
- `LogPath`, `PendingCrashRoot`, and `BundleRoot` receive full-path defaults.
  Their directories are created best effort; an unavailable directory is
  observed when the corresponding operation runs.
- `InitialProcessId` requires a positive PID and a non-blank `AttachToken`.
  Supplying a token without a PID is rejected.
- `PipeName` is an output of composition and is exposed by
  `WatchdogRuntime.PipeName`, not writable configuration.

`EnableHotkeys` defaults to `true` for compatibility. Set it before construction
for a headless or custom supervisor that must not claim global keys.

## Runtime lifecycle and process ownership

The lifecycle is one-shot and synchronized:

```text
created -> starting -> running -> stopping -> stopped
```

Only one caller can start an instance. `Stop` before `Start` is terminal;
concurrent `Stop` and `Dispose` calls join the same cleanup; a failed start also
ends in `stopped`. `WaitForExit` rejects use before a successful `Start` and then
waits for the complete terminal cleanup, not merely the monitor loop.

The runtime owns:

- the current target `Process` handle;
- the per-target named semaphore;
- the current-user RPC server and event hub;
- one foreground monitor thread;
- one bounded event-publisher thread; and
- an optional background hotkey thread.

Shutdown interrupts pause, restart-delay, and ten-second crash-loop cooldown
waits. It requests graceful window close, falls back to the absolute
`Environment.SystemDirectory\taskkill.exe` path with the configured bounds,
joins owned threads, disposes target/helper process handles and endpoints, and
releases the semaphore. Caller-supplied synchronous sinks can still extend a
logging call; they must not block indefinitely.

An initial attached process is the first supervised process, as is the first
process launched by the runtime. `restartCount` is zero for either initial mode
and increments only when a later replacement is launched.

## Control contract

The six deliberate public wire names are `Ping`, `Status`, `Pause`, `Resume`,
`Restart`, and `Stop` in `WatchdogCommands`. Attach, exception, replay, log-batch,
and update names are internal protocol details.

Controller calls are synchronous and use bounded connect/request timeouts:

- `Ping` returns `true` only for `pong`.
- `Pause`, `Resume`, `Restart`, and `Stop` return `true` only when the Host
  accepts the command and returns its expected acknowledgement.
- `Status` returns serialized status evidence, or `error=watchdog_not_running`,
  `error=pipe_io`, or `error=<protocol-code>`.
- `NotifyException`, `NotifyExceptionForTarget`, and `NotifyLog` are deliberately
  fail-soft and never turn diagnostic notification into an application failure.

The runtime retains an internal `update` response of `not_implemented` for Host
protocol compatibility. The library exposes no update option or supported
update command. Update orchestration requires a separate design and is not an
experimental Watchdog feature.

## Log forwarding and subscriptions

`WatchdogPipeLogSink` is a public `ILogSink` adapter. Construction starts one
background flush thread. `Write` never waits for pipe I/O: it enqueues into a
bounded local queue and increments cumulative `DroppedCount` only when that
queue is full. Batches and transport failures are best effort. Disposal does
not promise a flush and can abandon queued entries after the in-flight batch.

The runtime retains the latest 300 structured log entries for replay and emits
live log events through Pipes. `SubscribeLogs` first performs synchronous replay
on the subscribing thread, then starts a live listener whose callbacks run on
the event-client thread. The handoff is not gapless. Ordering is preserved
within replay and within the live stream, but not across that handoff. Each
application callback is exception-isolated.

`WatchdogController.LogEvent` contains nullable `Level`, `Msg`, `Line`, and
`MetaJson`. `MetaJson` is compact raw JSON text for an object, array, scalar, or
string and is null when metadata is absent or JSON null. It never exposes
Newtonsoft `JToken` or `System.Text.Json.JsonElement`.

## Bounded events, metrics, and status

Runtime log and telemetry events enter one 1,024-item FIFO queue. Supervision
never blocks to make diagnostics lossless. Status exposes cumulative,
non-resetting boundaries separately:

- `historyEvictions`: entries evicted from the 300-entry replay history;
- `eventQueueDropped`: events rejected because the producer queue was full;
- `eventPublishFailures`: queued events whose bounded publish attempt failed;
- `eventsDropped`: compatibility alias for `eventQueueDropped`; and
- `restartCount`: actual replacement launches after the initial process.

`WatchdogPipeLogSink.DroppedCount` remains local to that sink's queue. Pipes
server/event/error snapshots are included under `metrics`; they retain the
Pipes module's cumulative snapshot semantics.

## Crash evidence

Crash finalization is runtime-owned internal implementation, not an independent
public filesystem API. The newest `crash-*` pending directory is copied to a
bounded `bundle-*` directory, optional Watchdog status and log-tail evidence is
added, a serializer-generated `manifest.json` is written, and retention is
applied.

The internal outcome is `complete`, `partial`, or `failed` (with a separate
no-pending result). Optional status, tail, manifest, pending cleanup, and
retention failures produce a partial outcome and truthful log entry. Mandatory
copy/setup failure produces a failed outcome. Reporting callback exceptions are
isolated and JSON control characters are escaped by the target serializer.

## Hotkeys

When enabled, the runtime attempts these fixed process-wide registrations:

| Key | Action |
|---|---|
| Ctrl+Alt+P | pause supervision |
| Ctrl+Alt+R | resume supervision |
| Ctrl+Alt+Q | stop the runtime and supervised target |

Registration failure is written to configured local logging with the Win32
error. There is intentionally no public general hotkey binding model.

## Security boundary

RPC and events explicitly use `PipeAccessPolicy.CurrentUserOnly` on both
targets. This prevents ordinary cross-user access; it is not authentication or
authorization against a hostile process already running as the same user. The
application owns any stronger authorization requirement.

The deterministic pipe name is the lowercase full target path hashed with SHA-1
and truncated to 16 hexadecimal characters. It is stable target identity, not a
secret. The attach token is also correlation, not a credential. Watchdog does
not provide remote control, credentials, replay protection, or privileged
administration.

## Targets, dependencies, and package boundary

| Target | Direct dependencies |
|---|---|
| `net481` | NekoLib.Core, NekoLib.Pipes, Newtonsoft.Json 13.0.3 |
| `net9.0-windows7.0` | NekoLib.Core, NekoLib.Pipes |

Core contracts are public through `ILogSink`, `LogEntry`, and `ITelemetry`.
Pipes and Newtonsoft remain implementation dependencies at the public-signature
level. The `NekoLib.Watchdog` library package contains only its two library
assets and dependency groups. It does not embed Host payloads.

The [`NekoLib.Watchdog.Host` reference](../NekoLib.Watchdog.Host/README.md) owns
the direct-reference deployment package, RID payload selection, build and
publish targets, protocol v1 launch/attachment contract, fatal startup evidence,
and sidecar release validation.

## Verification scope

The focused dual-target suite covers configuration capture, lifecycle races,
attach and restart semantics, current-user control pipes, command outcomes,
bounded history, log forwarding, crash finalization, manifest JSON, file
rotation, bootstrap budgets, and the obsolete-surface removal. Interactive
hotkey registration, cross-user/elevation denial, and long-running
crash/recovery remain explicit runtime evidence rather than unit-test claims.
Package-backed Host protocol startup is covered separately by the canonical
package-consumer flow.
