# Watchdog Public API Review — 2026-08-18

**Kind:** audit

**Lifecycle:** historical

**Subject:** F1-WDOG compiled public surface, application and advanced-runtime
entry points, configuration ownership, process lifecycle, control IPC, log and
crash evidence, target behavior, security boundary, and library package

**Status:** review complete; all eight accepted dispositions implemented in
`6580b6f6ece4bd7c90f4cc80cd7e1f01d47eace6`

**Reference date:** 2026-08-18

**Reference commit:** `075bb7520dedd80dc853d6dac57c53e9e5b8aea7`

**Last reconciliation:** 2026-08-18

**Current state:** [`TODO.md`](../../TODO.md) F1-WDOG is complete; the
[Watchdog technical reference](../../src/Watchdog/NekoLib.Watchdog/README.md)
owns the current contract, while F1-WDOG-HOST remains open

## Baseline and worktree

This review covers committed `HEAD` on branch
`phase-e/sqlserver-and-orchestration`. At entry, `HEAD` was exactly
`075bb7520dedd80dc853d6dac57c53e9e5b8aea7`, the index and worktree were clean,
and the branch was 41 commits ahead of
`origin/phase-e/sqlserver-and-orchestration`. Recent history was:

```text
075bb75 docs(f1): close Pipes public API review
e608dc8 feat(pipes): finalize F1 public API
0086817 docs(f1): accept Pipes public API direction
882d224 docs(f1): review Pipes public API
2db588a docs(f1): close five-module package gate
63785cc fix(diagnostics): serialize install with disposal
a5e4ddc test(diagnostics): use Assert.Fail in the option-capture regression
63bb269 feat(devices): finalize F1 public API
```

The review used the NekoLib routing skill, the repository-hygiene workflow, and
the repository working agreements. Watchdog has no specialized module skill,
so current projects, source, tests, compiled manifests, and current
documentation are the operative authorities.

## Scope and authorities

Included, in authority order:

1. current source under `src/Watchdog/NekoLib.Watchdog`;
2. `NekoLib.Watchdog.csproj`, evaluated target properties, direct package and
   project references;
3. current tests under `tests/NekoLib.Watchdog.Tests/Unit`;
4. compiled manifests under `eng/public-api/NekoLib.Watchdog`;
5. the Host's direct construction of `WatchdogRuntime` and the versioned
   Supervisor481 source as consumer evidence only;
6. current root documentation and the public API/release policy;
7. `TODO.md` F1-WDOG and F1-WDOG-HOST boundaries;
8. `watchdog-first-pass.md` and the Pipes/Watchdog IPC hardening review as
   historical leads only.

Excluded:

- implementation of any proposed disposition;
- F1-WDOG-HOST deployment layout, build targets, argument protocol, RID
  selection, or package finalization;
- runtime-scenario execution, interactive hotkey input, crash injection,
  cross-user/elevation probes, package creation, publishing, or pushing;
- generic Pipes changes, broad observability instrumentation, remote
  supervision, and hostile-same-user authentication design.

The Host source was read only where required to decide whether the library
runtime is a real consumer surface. No Host finding is promoted here; Host-only
deployment findings belong to F1-WDOG-HOST.

## Project, target, and dependency facts

`NekoLib.Watchdog` is a packable library targeting `net481` and
`net9.0-windows7.0`. It enables nullable analysis, disables implicit usings,
and uses the actual custom symbols declared by its project:

| Target | Custom symbols | Direct project dependencies | Direct package dependencies |
|---|---|---|---|
| `net481` | `NETFRAMEWORK` | `NekoLib.Core`, `NekoLib.Pipes` | `Newtonsoft.Json 13.0.3` |
| `net9.0-windows7.0` | `NET_9`, `NET9` | `NekoLib.Core`, `NekoLib.Pipes` | none |

Core types are deliberately visible in the public API through `LogEntry`,
`ILogSink`, and `ITelemetry`. Pipes and Newtonsoft are implementation
dependencies at the signature level. The current `LogEvent.Meta` behavior can
still leak a Newtonsoft `JToken` instance at runtime on `net481`; that is a
behavioral target leak, not a compiled-signature dependency.

The library does not reference the Host. The deployment package references the
library and is consumed separately by executable projects. The supported Host
payload and argument protocol remain a separate F1-WDOG-HOST decision.

## Compiled public surface by target

The two approved manifests are identical for namespace types and members. Each
contains 111 public declarations: 12 type declarations (11 top-level types and
one nested type) plus 99 public constructors, methods, properties, and
constants. The modern manifest additionally records its Windows platform
attributes.

| Public type | Current role | Proposed classification |
|---|---|---|
| `CrashBundler` | Runtime-owned pending-crash finalizer | candidate for internalization |
| `CrashBundlerOptions` | Internal bundler configuration and callbacks | candidate for internalization |
| `WatchdogBootstrap` | Default application-side deployed-Host bootstrap | stable |
| `WatchdogCommands` | Raw control-command names, plus three Host-internal names | deliberate public extension after narrowing |
| `WatchdogController` | Current-application control, notification, and log-subscription facade | stable after outcome and DTO correction |
| `WatchdogController.LogEvent` | Serializer-neutral log subscription DTO in intent | stable after nullability and metadata correction |
| `WatchdogHotkeys` | Generic Win32 constants/helpers unused by the runtime | candidate for internalization or removal |
| `WatchdogLogFile` | Standalone rotation helper unused by the runtime | candidate for internalization or removal |
| `WatchdogLogPipeServer` | Obsolete superseded raw log pipe | obsolete; remove before the first stable baseline |
| `WatchdogOptions` | Runtime composition and supervision policy | stable after ownership and surface correction |
| `WatchdogPipeLogSink` | Non-blocking Core `ILogSink` adapter for application logs | deliberate public extension |
| `WatchdogRuntime` | Process supervisor used by the Host and custom supervisors | deliberate public extension; stable after lifecycle correction |

No Watchdog API should be classified experimental. The default supported
application entry point is `WatchdogBootstrap`; direct `WatchdogRuntime`
composition is an advanced, instance-owned extension, not an experiment and not
Host-only infrastructure.

### Member-level disposition summary

- Keep the three `WatchdogBootstrap` constants and all three `EnsureStarted`
  overloads stable. The call is synchronous, bounded by the handshake timeout,
  and owns starting and terminating an unconfirmed Host process.
- Keep `WatchdogController.ResolvePipeNameForTarget`, exception notification,
  single-entry log notification, subscriptions, and read/control operations as
  the application facade, subject to WDOG-07 and WDOG-08.
- Keep only `Ping`, `Status`, `Pause`, `Resume`, `Restart`, and `Stop` as
  deliberate public `WatchdogCommands`. `LogHistory`, `ExceptionNotify`, and
  `AttachStatus` are implementation protocol names already wrapped by public
  APIs and should be internal.
- Internalize `NotifyLogBatch`; it exists to connect `WatchdogPipeLogSink` to
  the runtime and has no independent public contract.
- Keep the `WatchdogPipeLogSink` constructors, `Write`, `DroppedCount`, and
  `Dispose` stable after documenting that it is bounded, non-blocking,
  best-effort, starts one background thread at construction, and does not flush
  on disposal.
- Keep `WatchdogRuntime(WatchdogOptions)`, `Start`, `WaitForExit`, `Stop`, and
  `Dispose` as the advanced supervisor surface after resolving lifecycle and
  ownership findings. Remove the ineffective `exitHost` parameter rather than
  stabilizing a distinction the implementation does not provide.
- Keep implemented `WatchdogOptions` policies, but remove `PipeName`, public
  `Normalize`, and the four update-placeholder members. Add a read-only
  effective pipe name on `WatchdogRuntime` for advanced consumers.
- Internalize the crash-bundler types and the two generic Win32/file helpers.
  Remove the already obsolete raw log server in the pre-stable correction.

## Ownership, lifecycle, threading, process, and security boundaries

### Supported composition paths

The ordinary application calls `WatchdogBootstrap.EnsureStarted` near `Main`.
The deployed Host parses its arguments, creates `WatchdogRuntime`, calls
`Start`, and blocks in `WaitForExit`. The runtime owns the target `Process`,
the per-target named semaphore, RPC/event endpoints, a foreground monitor
thread, background event and hotkey threads, and crash/log evidence production.

An advanced consumer can instantiate the same runtime directly. This is not
hypothetical: the tracked Supervisor481 source hosts it in-process and controls
it over real named pipes. That consumer is why internalizing
`WatchdogRuntime` would remove a demonstrated supported mode rather than merely
hide Host implementation.

`WatchdogController` is intentionally static and bound once to the current
process executable. It is an application-side facade, not a general controller
for arbitrary targets. Advanced controller code can derive a target pipe name
and use the public control constants with Pipes.

### Threading and delivery

- `WatchdogRuntime.Start` creates the event publisher, monitor, and hotkey
  threads. The monitor thread is foreground and can keep the Host process alive.
- Runtime log and telemetry events enter one bounded 1,024-item producer queue;
  one event thread serializes publication. Queue-full drops are counted and
  rate-limited warnings are written outside that queue.
- The replay buffer keeps the latest 300 structured log entries. Oldest-entry
  eviction is silent and separate from the event-queue drop counter.
- `WatchdogPipeLogSink` starts a background thread in its constructor, drops on
  local queue saturation, sends bounded batches, swallows transport failures,
  and abandons pending entries on disposal.
- `SubscribeLogs` performs replay synchronously on the caller thread before
  starting the live event client. Live callbacks run on the event-client
  listener path. The handoff is best-effort and not gapless.

### IPC and security

The runtime explicitly selects `PipeAccessPolicy.CurrentUserOnly` for its RPC
and event endpoints on both targets. This is the accepted current-user boundary
from Phase E5. It is not authentication against a hostile process already
running as that user, and the attach token and deterministic pipe name are
correlation/identity values rather than secrets.

The pipe name is the lowercase full target path hashed with SHA-1 and truncated
to 16 hexadecimal characters. For the supported small local process set, the
collision risk is remote and changing it would break controller/Host identity.
The review therefore recommends documenting and retaining it, not pretending a
longer hash would create authorization.

### Package boundary

The future `NekoLib.Watchdog` package is expected to carry only its two library
assets and dependency groups: Core and Pipes for both targets, plus Newtonsoft
on `net481`. It must not embed the Host payload. The Host deployment package is
referenced separately and owns its sidecar files and build targets.

## Confirmed findings

### WDOG-01 — High API-boundary impact — `WatchdogRuntime` is a real advanced consumer surface

**Observed fact.** The Host directly constructs `WatchdogRuntime`, but so does
the tracked Supervisor481 consumer, which embeds the runtime and drives its RPC
and event endpoints. The root module table already lists `WatchdogRuntime` and
`WatchdogOptions` as main entry points
([`Program.cs:16`](../../src/Watchdog/NekoLib.Watchdog.Host/Program.cs#L16),
[`MainForm.cs:65`](../../runtime_tests/Watchdog/Supervisor481/NekoLib.Watchdog.RuntimeTests.Supervisor481/MainForm.cs#L65),
[`README.md`](../../README.md)).

**Risk or hypothesis.** Internalizing the runtime would force custom
supervisors through the deployed Host and remove the only instance-owned
composition surface. Treating it as stable without lifecycle correction would
instead stabilize the defects below.

**Proposed decision.** Retain `WatchdogRuntime` as a deliberate public advanced
extension. Keep `WatchdogBootstrap` as the default application entry point and
state that direct runtime owners must stop/dispose it and own the supervised
target's lifecycle.

**Compatibility and migration.** No type removal. Advanced consumers may need
to move from reading mutated `WatchdogOptions.PipeName` to a new read-only
`WatchdogRuntime.PipeName`.

**Rejected alternatives.** Internalizing the runtime ignores a versioned
consumer. Adding another static facade duplicates `WatchdogBootstrap` and
`WatchdogController` without expressing new ownership.

**Validation if accepted.** Compile the tracked advanced consumer on `net481`,
retain Host construction tests on both targets, and add a package-consumer
compile probe for direct runtime construction.

### WDOG-02 — High reliability — runtime configuration is mutated and retained instead of captured

**Observed fact.** The constructor stores the caller's exact options instance,
calls public `Normalize` on it, and reads that same object throughout every
background loop. `Normalize` overwrites paths, `PipeName`, timing values, and
creates directories. The class comment says options are immutable at runtime,
but every property remains settable and `LogSinks` exposes the caller's mutable
array
([`WatchdogRuntime.cs:35`](../../src/Watchdog/NekoLib.Watchdog/WatchdogRuntime.cs#L35),
[`WatchdogRuntime.cs:86`](../../src/Watchdog/NekoLib.Watchdog/WatchdogRuntime.cs#L86),
[`WatchdogOptions.cs:18`](../../src/Watchdog/NekoLib.Watchdog/WatchdogOptions.cs#L18),
[`WatchdogOptions.cs:119`](../../src/Watchdog/NekoLib.Watchdog/WatchdogOptions.cs#L119)).

**Risk to consumers.** Post-construction mutation can change the executable,
working directory, kill waits, logging paths, sinks, or polling intervals while
threads are running. Invalid live values can cause a busy loop or terminate the
foreground monitor thread. Constructor-side mutation also surprises callers
and is currently used as an undocumented output channel for `PipeName`.

**Proposed decision.** Validate and normalize into an internal immutable
snapshot at construction without mutating the caller. Copy the `LogSinks` outer
array while retaining consumer ownership of sink and telemetry instances.
Expose the effective name through `WatchdogRuntime.PipeName` and keep
`ResolvePipeNameForTarget` stable.

**Compatibility and migration.** Later mutations stop taking effect. Code that
reads `options.PipeName` after constructing the runtime moves to
`runtime.PipeName` or `ResolvePipeNameForTarget`. This is a deliberate pre-stable
behavioral and surface correction.

**Rejected alternatives.** Documentation alone cannot make a mutable retained
object immutable. Locking every read still permits unsupported live policy
changes. Deep-disposing supplied sinks would wrongly transfer ownership.

**Validation if accepted.** Add invalid-value, constructor non-mutation,
post-construction mutation, outer-array mutation, and sink/telemetry ownership
tests on both targets.

### WDOG-03 — High reliability — start, stop, and disposal are not one race-safe terminal lifecycle

**Observed fact.** `Start` reads and writes `_started` without synchronization.
`Stop` uses a different lock and can run before or concurrently with `Start`.
A pre-start `Stop` completes the event queue and marks shutdown, but a later
`Start` is still admitted because `_started` remains false. Concurrent `Start`
calls can overwrite shared semaphore, RPC, and thread fields before the named
semaphore loser runs cleanup
([`WatchdogRuntime.cs:97`](../../src/Watchdog/NekoLib.Watchdog/WatchdogRuntime.cs#L97),
[`WatchdogRuntime.cs:183`](../../src/Watchdog/NekoLib.Watchdog/WatchdogRuntime.cs#L183),
[`WatchdogRuntime.cs:562`](../../src/Watchdog/NekoLib.Watchdog/WatchdogRuntime.cs#L562)).

**Risk to consumers.** A public advanced owner can observe partial startup,
cleanup of another start attempt, a completed event queue attached to a
nominally started runtime, leaked ownership, or inconsistent exceptions.
`WaitForExit` before `Start` silently returns and communicates no lifecycle
state.

**Proposed decision.** Implement a single synchronized terminal state machine:
created, starting, running, stopping, stopped. Admit one start, make stop before
start terminal, make concurrent stop/dispose join the same completion, and make
`WaitForExit` reject use before a successful start.

**Compatibility and migration.** Unsupported races become deterministic.
Sequential `Start`/`WaitForExit`/`Stop` remains. Consumers must construct a new
runtime after shutdown or failed startup.

**Rejected alternatives.** Relying on the cross-process semaphore does not
serialize calls on one instance. Separate booleans and locks cannot define an
atomic public state transition.

**Validation if accepted.** Concurrent start, start/stop, stop-before-start,
dispose-before-start, repeated stop/dispose, failed-start cleanup, and
post-terminal method tests on both targets.

### WDOG-04 — High shutdown impact — `Stop` can return while the foreground monitor remains alive

**Observed fact.** Five fast exits enter an unconditional ten-second
`Thread.Sleep`. `Stop` waits only three seconds for the foreground monitor and
then returns. The shutdown path kills but does not reliably dispose the current
child `Process`, and the force-kill `Process` is not disposed. The optional
`exitHost` value changes only `_exiting`; both values otherwise stop the runtime,
post the hotkey quit message, complete events, and tear down RPC
([`WatchdogRuntime.cs:152`](../../src/Watchdog/NekoLib.Watchdog/WatchdogRuntime.cs#L152),
[`WatchdogRuntime.cs:183`](../../src/Watchdog/NekoLib.Watchdog/WatchdogRuntime.cs#L183),
[`WatchdogRuntime.cs:217`](../../src/Watchdog/NekoLib.Watchdog/WatchdogRuntime.cs#L217),
[`WatchdogRuntime.cs:666`](../../src/Watchdog/NekoLib.Watchdog/WatchdogRuntime.cs#L666),
[`WatchdogRuntime.cs:862`](../../src/Watchdog/NekoLib.Watchdog/WatchdogRuntime.cs#L862)).

**Risk to consumers.** `Dispose` can return while a foreground thread still
keeps the process alive for roughly seven more seconds. Native process handles
can remain open. The boolean parameter promises a Host distinction the runtime
does not implement.

**Proposed decision.** Use the existing shutdown-aware wait for crash-loop
cooldown, make `Stop()` await or join all owned work within documented process
termination bounds, dispose target and helper process handles, and remove the
`exitHost` parameter. `Dispose` delegates to that one terminal path.

**Compatibility and migration.** Calls to `Stop(false)` become invalid and
must move to `Stop()`. Repository search found no such call. Calls to `Stop()`
continue to compile after recompilation.

**Rejected alternatives.** Increasing the join timeout hides but does not fix
uninterruptible work. Making the monitor background would allow abrupt loss of
supervision/evidence rather than truthful shutdown.

**Validation if accepted.** Force five fast crashes, request stop during the
cooldown, assert a bounded foreground-thread join, and verify child/helper
process handle disposal. No such scenario ran during this review.

### WDOG-05 — Medium product-truth impact — update policy is public and enabled although update orchestration does not exist

**Observed fact.** `EnableUpdates` defaults true; `UpdateStagingRoot`,
`UseAtomicDirectorySwap`, and `BackupFolderName` are public; normalization
creates the update directory; status reports updates as enabled. The only
`update` handler always returns `not_implemented` when enabled, and the public
command class does not expose that command
([`WatchdogOptions.cs:90`](../../src/Watchdog/NekoLib.Watchdog/WatchdogOptions.cs#L90),
[`WatchdogOptions.cs:191`](../../src/Watchdog/NekoLib.Watchdog/WatchdogOptions.cs#L191),
[`WatchdogRuntime.cs:334`](../../src/Watchdog/NekoLib.Watchdog/WatchdogRuntime.cs#L334),
[`WatchdogRuntime.cs:457`](../../src/Watchdog/NekoLib.Watchdog/WatchdogRuntime.cs#L457)).

**Risk to consumers.** The compiled package advertises configuration and an
enabled status for a feature that cannot perform an update. It also creates a
directory for nonexistent work. Stabilizing these members would reserve an
unproven update architecture.

**Proposed decision.** Remove all four update-placeholder properties from the
library candidate surface and stop creating/reporting an update staging policy.
Keep explicit `not_implemented` wire behavior only until F1-WDOG-HOST decides
the protocol compatibility requirement.

**Compatibility and migration.** Consumers delete assignments to these
properties. There is no replacement because no behavior exists. Record the
pre-stable removal in changelog and migration guidance if accepted.

**Rejected alternatives.** Marking the members experimental would preserve a
fictional feature. Implementing update orchestration is materially outside F1
and requires a separate design/security decision.

**Validation if accepted.** Reviewed API diff, absence of update-directory
side effects, status/protocol compatibility tests coordinated with
F1-WDOG-HOST, and a repository search for removed members.

### WDOG-06 — Medium API-design impact — five public helpers are not supported consumer boundaries

**Observed fact.** `CrashBundler` and `CrashBundlerOptions` are used by the
runtime and tests; `WatchdogHotkeys` has no current runtime or repository
consumer; `WatchdogLogFile` is used only by its tests while the runtime has a
separate rotation implementation; and `WatchdogLogPipeServer` is already
obsolete and has no product consumer. `NotifyLogBatch` is called only by
`WatchdogPipeLogSink`. The three non-control command constants are used only by
Watchdog internals/tests
([`CrashBundler.cs:17`](../../src/Watchdog/NekoLib.Watchdog/CrashBundler.cs#L17),
[`WatchdogHotkeys.cs:9`](../../src/Watchdog/NekoLib.Watchdog/WatchdogHotkeys.cs#L9),
[`WatchdogLogFile.cs:24`](../../src/Watchdog/NekoLib.Watchdog/WatchdogLogFile.cs#L24),
[`WatchdogLogPipeServer.cs:9`](../../src/Watchdog/NekoLib.Watchdog/WatchdogLogPipeServer.cs#L9),
[`WatchdogController.cs:187`](../../src/Watchdog/NekoLib.Watchdog/WatchdogController.cs#L187)).

**Risk to consumers.** Stabilizing them promises low-level crash filesystem,
Win32 key enumeration, file rotation, raw pipe lifecycle, internal batching,
and attach protocol contracts independently of the runtime that owns them.

**Proposed decision.** Internalize the bundler pair and reusable implementation
helpers; remove the obsolete raw pipe before the first stable baseline;
internalize `NotifyLogBatch` and the three internal command constants. Reuse or
delete the file/hotkey helpers internally so one implementation remains.

**Compatibility and migration.** Direct crash finalization moves to
`WatchdogRuntime` configuration. Raw-log consumers move to
`WatchdogController.SubscribeLogs`/`SubscribeLogLines`. Custom supervisors keep
the six public control constants. No deprecation window is needed before the
first stable baseline beyond the existing obsolete warning and migration note.

**Rejected alternatives.** Keeping public utilities because they are tested
confuses testability with product support. A compatibility shim has no observed
consumer or declared support window.

**Validation if accepted.** Repository-wide source search, compiled API diff,
internal unit coverage, obsolete-consumer compile migration, and both target
builds.

### WDOG-07 — Medium control-contract impact — mutating controller methods discard every outcome

**Observed fact.** The private sender converts transport failures into string
sentinels. `Status` returns that string and `Ping` reduces it to `bool`, while
`Pause`, `Resume`, `Stop`, and `Restart` discard it entirely. A call therefore
returns normally whether the Host accepted the operation, rejected it, timed
out, or was absent
([`WatchdogController.cs:103`](../../src/Watchdog/NekoLib.Watchdog/WatchdogController.cs#L103),
[`WatchdogController.cs:144`](../../src/Watchdog/NekoLib.Watchdog/WatchdogController.cs#L144)).

**Risk to consumers.** An application can report that supervision was paused,
resumed, restarted, or stopped when no command reached the Host. This is more
dangerous for process-changing calls than for the explicitly best-effort crash
and log notification methods.

**Proposed decision.** Keep `NotifyException`, `NotifyLog`, and the sink
fail-soft. Change the four mutating convenience methods to return `bool`
indicating an accepted response; keep `Ping` and the current status evidence,
and document the bounded synchronous waits and `error=<code>` status sentinel.

**Compatibility and migration.** Existing source calls can ignore a returned
value, but assemblies must be recompiled because the return type changes. New
consumers check the result before claiming success.

**Rejected alternatives.** Throwing all transport failures is inappropriate
for crash/log notification. A process-wide `LastError` would be race-prone.
A new general RPC abstraction duplicates Pipes.

**Validation if accepted.** Host present/absent, timeout, protocol error, and
accepted command tests for every method on both targets, plus a source/binary
API review.

### WDOG-08 — Medium target and nullability impact — `LogEvent` is not serializer-neutral in behavior

**Observed fact.** `LogEvent.Level`, `Msg`, `Meta`, and `Line` are declared
non-null but its public constructor initializes none of them. Missing wire
fields assign null. On `net481`, `Meta` is a Newtonsoft `JToken`; on the modern
target it is a string produced from `JsonElement`. Replay has the same split
([`WatchdogController.cs:25`](../../src/Watchdog/NekoLib.Watchdog/WatchdogController.cs#L25),
[`WatchdogController.cs:237`](../../src/Watchdog/NekoLib.Watchdog/WatchdogController.cs#L237),
[`WatchdogController.cs:304`](../../src/Watchdog/NekoLib.Watchdog/WatchdogController.cs#L304)). The current build emits corresponding nullable warnings.

Replay runs before live subscription, so events produced in between may be
missed. Replay callbacks run synchronously on the subscribing thread; live
callbacks run on the event-client listener. Neither gapless delivery nor one
callback thread is implemented.

**Risk to consumers.** The same `object` property requires target-specific type
tests and can fail a non-null assumption. A consumer may also mistake replay
plus live subscription for a gapless ordered stream.

**Proposed decision.** Replace `Meta` with nullable serializer-neutral
`string? MetaJson`; correct nullable annotations for optional text. Keep the
bounded replay/live stream best-effort and document the handoff gap, callback
threads, ordering within each source, and callback isolation.

**Compatibility and migration.** Consumers parse `MetaJson` explicitly if they
need structure and null-check optional fields. This removes an accidental
runtime Newtonsoft boundary.

**Rejected alternatives.** Exposing `JToken` on both targets adds an avoidable
modern public dependency. Exposing `JsonElement` preserves the opposite target
split. A gapless sequence protocol is disproportionate for operational logs.

**Validation if accepted.** Missing/null metadata, object/array/scalar metadata,
Unicode, identical target output, replay callback thread, live callback thread,
replay/live gap documentation, and compiled nullability diff.

### WDOG-09 — Medium application-policy impact — every runtime registers fixed global hotkeys

**Observed fact.** Every successful start creates the hotkey thread and attempts
to register global Ctrl+Alt+P, Ctrl+Alt+R, and Ctrl+Alt+Q. There is no option to
disable them, registration results are ignored, and Ctrl+Alt+Q invokes stop.
The public `WatchdogHotkeys` helper is not used by this code
([`WatchdogRuntime.cs:159`](../../src/Watchdog/NekoLib.Watchdog/WatchdogRuntime.cs#L159),
[`WatchdogRuntime.cs:1086`](../../src/Watchdog/NekoLib.Watchdog/WatchdogRuntime.cs#L1086)).

**Risk to consumers.** A custom supervisor always claims process-wide key
combinations and exposes a physical stop path. Conflicts fail silently, so the
runtime cannot truthfully report whether its controls exist.

**Proposed decision.** Add `WatchdogOptions.EnableHotkeys`, defaulting true to
preserve current behavior, skip the thread when false, and report registration
failure to configured local logs/sinks. Keep fixed chords for this release;
do not stabilize a general Win32 binding model.

**Compatibility and migration.** Existing behavior remains by default.
Headless/service/custom supervisors can opt out explicitly.

**Rejected alternatives.** Stabilizing `WatchdogHotkeys` as a generic key model
adds breadth without integrating it. Silently ignoring registration remains
unobservable. Changing the default to false would alter established Host
behavior without deployment evidence.

**Validation if accepted.** Build-only disabled-path tests on both targets and
an explicit interactive Windows registration/conflict probe before claiming
the enabled chords work. No interactive probe ran here.

### WDOG-10 — Medium process-integrity impact — force termination relies on executable-name search

**Observed fact.** The force path starts `taskkill` by bare name, inherits the
runtime environment/search behavior, waits for it, and does not dispose the
returned `Process`
([`WatchdogRuntime.cs:862`](../../src/Watchdog/NekoLib.Watchdog/WatchdogRuntime.cs#L862)).

**Risk or hypothesis.** A conflicting executable earlier in Windows process
search can be launched instead of the system utility, particularly when a
consumer supplies a writable working directory or modified environment. Even
under the accepted cooperative same-user model, this is avoidable process
ambiguity. Handle ownership is also incomplete.

**Proposed decision.** Resolve the executable from
`Environment.SystemDirectory`, use invariant PID formatting, dispose the helper
process, and preserve the configured force-kill bound and best-effort logging.

**Compatibility and migration.** No public signature change. Observable
behavior becomes deterministic and no longer honors an alternate `taskkill`
from search paths.

**Rejected alternatives.** Shell execution expands ambiguity. Directly adding
Job Objects is a larger supervision redesign and does not replace the current
graceful path.

**Validation if accepted.** Inject a fake search-path `taskkill`, verify it is
not executed, verify timeout/exit logging, and ensure helper handles are closed.

### WDOG-11 — Medium metrics/evidence impact — counters do not describe all bounded loss or restart semantics

**Observed fact.** The 300-entry replay buffer silently evicts its oldest item.
`eventsDropped` counts only failure to enter the 1,024-item local event queue;
publication failures are swallowed without increment. `WatchdogPipeLogSink`
counts only local queue saturation, not failed batches or entries abandoned by
disposal. `restartCount` increments on the first launched child, but remains
zero for an initially attached child until its first restart
([`WatchdogRuntime.cs:32`](../../src/Watchdog/NekoLib.Watchdog/WatchdogRuntime.cs#L32),
[`WatchdogRuntime.cs:696`](../../src/Watchdog/NekoLib.Watchdog/WatchdogRuntime.cs#L696),
[`WatchdogRuntime.cs:737`](../../src/Watchdog/NekoLib.Watchdog/WatchdogRuntime.cs#L737),
[`WatchdogRuntime.cs:781`](../../src/Watchdog/NekoLib.Watchdog/WatchdogRuntime.cs#L781),
[`WatchdogRuntime.cs:937`](../../src/Watchdog/NekoLib.Watchdog/WatchdogRuntime.cs#L937),
[`WatchdogPipeLogSink.cs:56`](../../src/Watchdog/NekoLib.Watchdog/WatchdogPipeLogSink.cs#L56)).

**Risk to consumers.** Status can understate lost operational evidence and gives
different restart-count meaning depending on bootstrap mode. Consumers cannot
derive whether a gap came from retention, local queue saturation, or transport
publication failure.

**Proposed decision.** Define cumulative non-resetting counters separately for
history eviction, event-queue drops, and event publication failures. Keep sink
`DroppedCount` explicitly scoped to its local queue. Define `restartCount` as
actual restarts after the first supervised process in both launch and attach
modes. Coordinate the serialized status-field change with F1-WDOG-HOST.

**Compatibility and migration.** Existing status JSON semantics change and new
fields are additive; the Host protocol review must record that wire delta.
`DroppedCount` keeps its current meaning and signature.

**Rejected alternatives.** One aggregate loss counter cannot identify the
failing boundary. Unbounded history or lossless events would let diagnostics
backpressure supervision.

**Validation if accepted.** Force each loss boundary independently, verify
cumulative snapshots, verify no reset, and compare launch/attach restart
sequences on both targets.

### WDOG-12 — Medium incident-evidence impact — crash finalization can report success after partial evidence and can escape its `Try` boundary

**Observed fact.** Optional status, tail, manifest, deletion, and retention
failures are swallowed. A finalized log is still emitted after optional
failures. If the caller's `log` callback throws, the top-level catch calls that
same callback again and a second throw escapes. The manifest uses handwritten
escaping that handles backslash and quote but not all JSON control characters
([`CrashBundler.cs:47`](../../src/Watchdog/NekoLib.Watchdog/CrashBundler.cs#L47),
[`CrashBundler.cs:66`](../../src/Watchdog/NekoLib.Watchdog/CrashBundler.cs#L66),
[`CrashBundler.cs:76`](../../src/Watchdog/NekoLib.Watchdog/CrashBundler.cs#L76),
[`CrashBundler.cs:82`](../../src/Watchdog/NekoLib.Watchdog/CrashBundler.cs#L82),
[`CrashBundler.cs:151`](../../src/Watchdog/NekoLib.Watchdog/CrashBundler.cs#L151)).

**Risk to consumers.** Runtime logs can claim a complete bundle while evidence
is missing or the manifest is invalid. A supposedly fail-soft `Try` helper can
replace the caller's control flow with a callback exception.

**Proposed decision.** Internalize the bundler surface, isolate the reporting
callback, generate valid JSON through the target serializer, and return an
internal structured complete/partial/failed outcome so the runtime can log
truthfully without preventing supervision.

**Compatibility and migration.** External direct bundler use is removed under
WDOG-06. Runtime bundle layout remains unless F1-WDOG-HOST or a crash-evidence
contract explicitly changes it.

**Rejected alternatives.** Keeping the public void `Try` API cannot communicate
partial evidence. Adding another public crash-bundle result duplicates the
Diagnostics ownership boundary. The serializers are already dependencies, so
handwritten JSON brings no package-size benefit.

**Validation if accepted.** Throwing callbacks, control characters, optional
artifact failures, partial outcome logging, valid JSON parsing, pending-folder
retention, and maximum-bundle enforcement on both targets.

## Analyzed and rejected directions

The review does not recommend these directions for F1-WDOG:

1. **Internalize `WatchdogRuntime`.** The tracked advanced supervisor is direct
   consumer evidence, and the package description promises process supervision.
2. **Add a process-wide runtime singleton or builder.** Bootstrap and controller
   already cover the ordinary application path; advanced ownership should stay
   instance-based.
3. **Implement or mark update orchestration experimental.** No implementation
   or validated architecture exists. Remove the placeholder library API instead.
4. **Treat the path hash or attach token as authentication.** Both are
   deterministic/observable correlation values. A longer hash does not create a
   security boundary.
5. **Add hostile-same-user authentication, replay protection, credentials, or
   remote administration.** Phase E5 explicitly accepted cooperative processes
   under the current user. Expanding that threat model needs a separate decision.
6. **Make logs and events lossless.** Supervision must not block behind
   diagnostics. Bounded best-effort delivery with accurate counters is the
   supported direction.
7. **Expose Newtonsoft or `JsonElement` in `LogEvent`.** Target-neutral JSON text
   is smaller and keeps serializers out of the public DTO.
8. **Turn F1-WDOG into F1-WDOG-HOST.** Sidecar RID selection, deployment targets,
   arguments, fatal-log placement, and package payload remain the next separate
   item.
9. **Add Core Inspection/DebugUtils emission.** Broad feature instrumentation
   remains frozen and is unrelated to public API finalization.

## Consolidated proposal for the decision gate

Nothing below is accepted or scheduled by this review.

1. **Public boundary:** retain `WatchdogBootstrap` as the default entry,
   `WatchdogController` as the current-application facade, and
   `WatchdogRuntime` as a stable deliberate advanced extension (WDOG-01).
2. **Surface reduction:** internalize `CrashBundler`,
   `CrashBundlerOptions`, `WatchdogHotkeys`, `WatchdogLogFile`,
   `NotifyLogBatch`, and internal-only command constants; remove the obsolete
   `WatchdogLogPipeServer`; keep six public control constants; add no
   experimental API (WDOG-06, WDOG-12).
3. **Configuration:** capture an immutable normalized snapshot, stop mutating
   caller options, copy the sink array, expose `WatchdogRuntime.PipeName`,
   internalize normalization, remove `WatchdogOptions.PipeName`, correct public
   nullability, and remove all update placeholders (WDOG-02, WDOG-05).
4. **Lifecycle and process ownership:** make start/stop/dispose terminal and
   race-safe, reject invalid ordering, interrupt crash-loop cooldown, drain all
   owned threads, dispose process handles, remove `Stop(bool)`, and resolve the
   system `taskkill.exe` explicitly (WDOG-03, WDOG-04, WDOG-10).
5. **Application policy:** add `EnableHotkeys` with compatibility-default true,
   allow headless owners to opt out, and report registration failures without
   introducing a general binding model (WDOG-09).
6. **Controller and log DTO:** return observable success from mutating control
   methods, keep crash/log notification fail-soft, replace target-specific
   `Meta` with nullable `MetaJson`, correct optional text nullability, and
   document bounded synchronous calls plus replay/live threading and gaps
   (WDOG-07, WDOG-08).
7. **Evidence and counters:** keep bounded best-effort delivery, distinguish
   history eviction, event-queue drops, and publish failures, define restart
   count consistently, and make crash finalization report complete/partial/fail
   internally (WDOG-11, WDOG-12).
8. **Target, security, and package:** retain Windows-only dual targets, public
   Core contracts, internal Pipes use, `net481` Newtonsoft, deterministic
   target identity, `CurrentUserOnly`, and the cooperative same-user threat
   model. Add no authentication or remote control. Defer Host payload and wire
   release finalization to F1-WDOG-HOST.

The recommended direction is to accept this bounded package before
implementation. It preserves both real supported composition modes while
removing accidental helpers and correcting supervision ownership before those
behaviors become stable. The two decisions requiring the closest scrutiny are
the deliberate support commitment for direct `WatchdogRuntime` and removal of
the pre-stable utility surface.

## Validation

Executed on Windows from the clean reference commit, without updating an API
baseline:

| Command | Result |
|---|---|
| `git status --short --branch` | clean; expected branch; ahead 41 |
| `git rev-parse HEAD` | `075bb7520dedd80dc853d6dac57c53e9e5b8aea7` |
| `dotnet test tests/NekoLib.Watchdog.Tests/Unit/NekoLib.Watchdog.Tests.Unit.csproj -f net481 -m:1 --no-restore` | **84 passed**, 0 failed, 0 skipped |
| `dotnet test tests/NekoLib.Watchdog.Tests/Unit/NekoLib.Watchdog.Tests.Unit.csproj -f net9.0-windows -m:1 --no-restore` | **84 passed**, 0 failed, 0 skipped |
| `.\eng\verify-public-api.ps1 -PackageId NekoLib.Watchdog` | both manifests verified; builds succeeded with 0 errors; first build emitted 124 existing warning occurrences, dominated by nullable analysis |
| `.\eng\verify-docs.ps1` | passed after the new audit was added to the Git index; the first pre-stage run correctly rejected both index links because their destination was still untracked |
| `git diff --check` and `git diff --cached --check` | passed |

The focused suite includes real filesystem, named-pipe, and child-process
integration despite its `Unit` project location. It covers configuration
normalization, bundling, file rotation, bootstrap identity and timeout budget,
initial attach and restart, duplicate runtime exclusion, RPC, logs/events,
current-user pipe selection, crash-handler notification, bounded sink
saturation, and obsolete raw-server shutdown.

## Residual limitations and validation gaps

- No runtime scenario was launched. In particular, global hotkeys, visible
  window activation, taskkill behavior, packaged sidecar launch, crash/recovery
  campaigns, and interactive process behavior were not refreshed.
- No concurrent lifecycle, stop-during-crash-loop, pre-start stop/dispose, or
  post-construction option-mutation regression currently exists.
- No cross-user or cross-elevation denial probe ran. No hostile same-user,
  endpoint-squatting, impersonation, credential, authorization, or replay probe
  was attempted.
- No event-queue saturation/publish-failure split, replay-buffer eviction
  counter, sink transport-failure counter, or launch-versus-attach restart-count
  parity test exists.
- No target-parity test asserts `LogEvent.Meta` runtime type or nullable fields.
- No fake search-path `taskkill`, helper-process handle, throwing bundler
  callback, JSON control-character, or partial-bundle truthfulness test exists.
- No full solution test or rebuild ran for this review. No Host project/package
  finalization, immutable package, PackageReference consumer campaign,
  `-AllowDirty`, publish, or push was performed.
- Existing warnings are not new review changes, but the nullable warnings in
  public DTOs corroborate WDOG-08 and the options surface gap. Warning identity
  cleanup belongs only to an accepted implementation.

## Review-only declaration

F1-WDOG remains open. This review produces only this audit and its two current
index entries. It implements no correction, accepts no proposal, changes no
public API baseline, produces no module reference, migration, changelog, Host
payload, package, publish, or push, and runs no runtime scenario.

## Implementation reconciliation — 2026-08-18

The user accepted all eight decision-gate proposals. Commit
`6580b6f6ece4bd7c90f4cc80cd7e1f01d47eace6` implemented WDOG-01 through WDOG-12
within the accepted boundaries:

- `WatchdogBootstrap` remains the default application entry,
  `WatchdogController` remains the current-application facade, and direct
  `WatchdogRuntime` is a deliberate advanced extension.
- Runtime options are captured without mutating the caller, the sink array is
  copied, and the effective pipe name is read from `WatchdogRuntime.PipeName`.
  Public update placeholders were removed; the internal update wire command
  remains explicitly `not_implemented` pending F1-WDOG-HOST.
- The obsolete raw log server was removed. Crash-bundling options, hotkey and
  file helpers, batch forwarding, and non-control command constants became
  implementation details. The public surface contains no experimental API.
- Start, wait, stop, and dispose now share one terminal lifecycle. Shutdown
  interrupts cooldown, drains owned workers, releases process handles, and
  resolves the system `taskkill.exe`; `Stop(bool)` became `Stop()`.
- Hotkeys remain enabled by default and can be disabled through
  `WatchdogOptions.EnableHotkeys`; enabled registration failures are logged.
- Controller mutations return exact acknowledgement success. Notifications
  remain fail-soft. `LogEvent.MetaJson` is nullable serializer-neutral JSON
  text, optional wire fields are nullable, and callback failures are isolated.
- Status evidence distinguishes replay-history eviction, live event-queue
  drops, and publication failures. Restart count is consistent across launch
  and attach, and crash finalization reports complete, partial, failed, or no
  pending evidence internally.
- Targets, project/package dependencies, deterministic identity,
  `CurrentUserOnly`, cooperative same-user security, and separate Host-package
  ownership were preserved.

Validation after implementation:

| Command | Result |
|---|---|
| focused Watchdog tests, `net481` | **92 passed**, 0 failed, 0 skipped |
| focused Watchdog tests, `net9.0-windows` | **92 passed**, 0 failed, 0 skipped |
| `dotnet test NekoLib.sln -m:1 --no-restore` | **1,614 passed**, 0 failed, 0 skipped |
| `dotnet build NekoLib.sln -t:Rebuild --no-restore -m:1` | succeeded with 340 existing warning occurrences and 0 errors |
| `eng/verify-docs.ps1 -BuildLogPath <captured-rebuild-log>` | passed; no new warning identity and 68 baseline identities not emitted |
| four versioned Watchdog runtime-scenario project builds | succeeded for applicable targets with 0 warnings and 0 errors; not launched |
| `eng/verify-public-api.ps1 -PackageId NekoLib.Watchdog` | both reviewed manifests verified after the accepted baseline update |
| `git diff --check` and `git diff --cached --check` | passed |

The focused regressions close the original lifecycle, option-mutation,
counter-split, metadata-parity, callback-isolation, system-taskkill-path, JSON,
and crash-outcome gaps. Residual evidence is intentionally narrower than a
release claim: no interactive hotkey or window-activation probe, actual
`taskkill` termination, cross-user/elevation or hostile same-user probe,
packaged-sidecar run, long-running crash/recovery campaign, immutable package,
publish, or push was produced. Host deployment layout and protocol release
evidence remain F1-WDOG-HOST work.
