# Logging Public API Review — 2026-08-17

**Document ID:** LOG-AUDIT-PUBLIC-API-20260817

**Schema version:** 1

**Kind:** audit

**Lifecycle:** historical

**Subject:** F1-LOG compiled public surface, pipeline ownership, sink lifecycle, flush and disposal contracts, rolling-file persistence, and compatibility boundaries

**Surface:** audit

**Boundary:** logging

**Authority role:** evidence

**Mutation:** snapshot

**Indexing:** include

**Status:** all dispositions accepted, implemented, and package-validated

**Reference date:** 2026-08-17

**Reference commit:** `c7967e784914b56863a1b2da97cfafecb32ea494`

**Original path:** `docs/audit/logging-public-api-review-2026-08-17.md`

**Last reconciliation:** 2026-08-17

**Current state:** [`TODO.md`](../../../../TODO.md) F1-LOG

## Baseline and authority

This review covers committed `HEAD` on branch
`phase-e/sqlserver-and-orchestration`. Before the review artifact was added, the
worktree and index were clean and the branch was eleven commits ahead of its
matching remote branch. Nothing from this branch was pushed by the review.

The reviewed authority is the Logging project, all of its source, its project
file, the compiled `net481` and `net9.0` assemblies, the packaged
`1.0.0-local.16` assemblies, the dual-target focused tests, the assembly-derived
manifests, the accepted Core contracts, the public API release policy, and the
current repository consumer source. Historical reviews and handoffs supplied
architectural context only.

The review changes no product source, test, API baseline, package, changelog,
migration guide, or live roadmap item.

The latest immutable package family is `1.0.0-local.16`, built from
`7ae62a23db4c8933f7db2cf783b227df21a59abe`.
`NekoLib.Logging.1.0.0-local.16.nupkg` has SHA-256
`84A953AFF75C1A3DDA81B6F23D54521A5D1DC43A4315C64DCD6FAC325712B39D`. Logging
source did not change between that commit and the reviewed commit, so those
package bits correspond to the reviewed surface. That package is prior
pre-F1-LOG evidence; it is not evidence for any change proposed here.

## Scope

Included:

- all 5 compiled public Logging type declarations and all 21 public member
  declarations, on both target frameworks;
- both `Logger` constructor families, sink-array ownership, and null handling;
- `LoggerOptions` defaults and every validation rule;
- minimum-level filtering, synchronous inline dispatch, registration ordering,
  concurrent writers, and delivery order versus timestamp order;
- sink-failure isolation in `Log`, `Flush`, and `Dispose`;
- recent-entry capacity, ordering, returned-collection ownership, and
  pre/post-disposal behavior;
- `Flush` timeout arithmetic, exceptions, abandoned work, concurrency, and
  post-disposal behavior;
- `DisposeSinks` ownership transfer, flush-before-dispose, idempotency, and
  exception isolation;
- `DebugLogSink` behavior in Debug, Release, and packaged builds;
- `RollingFileLogSinkOptions` defaults and validation;
- path normalization, directory creation, encoding, rolling threshold, archive
  naming, retention semantics, same-process and cross-process writers, failure
  propagation, level-dependent durability, and `Flush` semantics;
- target parity, package boundary, and documentation ownership.

Excluded:

- implementing any recommendation, editing product source or tests, or updating
  an accepted API manifest;
- modifying `NekoLib.Core`, whose logging contracts are already accepted;
- packaging, package-consumer execution, and any package-backed claim;
- executing the manual Observability runtime scenario or any long-running,
  smoke, rehearsal, or soak scenario;
- Telemetry, Inspection, Diagnostics, and Navigation surfaces beyond their
  effect on Logging contracts.

## Package, ownership, and lifecycle boundary

[`NekoLib.Logging.csproj`](../../../../src/Logging/NekoLib.Logging/NekoLib.Logging.csproj)
targets `net481;net9.0`, enables nullable annotations, disables implicit
usings, declares `NEKOLIB` plus the conditional `NETFRAMEWORK` / `NET_9`
symbols, and references only `NekoLib.Core`. No source file in the module uses
`#if` at all, so there is no target-conditional behavior to reconcile.

The ownership model is consumer-first and matches the accepted Core decision:

- the composition root constructs `Logger` and decides its lifetime;
- feature modules receive `ILogger` and never dispose it;
- sinks are consumer-constructed; `LoggerOptions.DisposeSinks` decides whether
  the pipeline takes disposal ownership;
- `LoggerOptions` and `RollingFileLogSinkOptions` are copied into readonly
  fields at construction, so post-construction option mutation cannot affect a
  live pipeline or sink;
- there is no global logger slot, provider, registry, or static facade.

Two existing pieces of process-wide state deserve explicit naming, because the
F1-LOG scope statement is "without adding global state" and one of them already
exists:

- `RollingFileLogSink` keeps a `static` path-gate dictionary
  ([`RollingFileLogSink.cs:15-17`](../../../../src/Logging/NekoLib.Logging/Sinks/RollingFileLogSink.cs)),
  which is what makes two sinks on one normalized path serialize inside a
  process;
- `DebugLogSink` writes to the process-wide debug/trace channel.

Neither is a service locator, and neither affects logger composition.

## Compiled-surface inventory and recommended classification

The accepted `net481` and `net9.0` manifests are byte-identical and share
SHA-256 `8FB0BDF736FFF0201AB57F32E983DBECFFA5133DE5D1DA0944D22ED32C88DB8A`.
Each declares 5 public types and 21 public members, with identical nullable
metadata, optional parameter values, `params` shapes, sealed shapes, and
interface implementation lists. There is no target-specific Logging API, no
experimental marker, and no deprecation marker. Every public type is `sealed`;
there is no protected surface and no inheritance seam. Extension happens
exclusively through the Core `ILogSink` / `IFlushableLogSink` interfaces.

| Type or member family | Supported role | Recommended disposition |
|---|---|---|
| `Logger` (sealed; `ILogger`, `ILogSnapshotSource`, `ILogFlusher`, `IDisposable`) | Supported consumer entry point and the module's only pipeline | **retain as stable, with behavioral corrections** (LOG-02, LOG-03, LOG-04, LOG-05) |
| `Logger(LogLevel, params ILogSink[]?)` | Supported convenience entry point used by the root README example | **retain as stable** |
| `Logger(LoggerOptions? = null, params ILogSink[]?)` | Supported full-configuration entry point | **retain as stable**; copy the sink array (LOG-04) |
| `Logger.Log` | `ILogger` implementation; filtering, retention, inline ordered dispatch, failure isolation | **retain as stable**; document timestamp-versus-delivery ordering (LOG-08) |
| `Logger.GetRecentEntries` | `ILogSnapshotSource` implementation; bounded newest window | **retain as stable**; document post-disposal readability (LOG-05) |
| `Logger.Flush` | `ILogFlusher` implementation; bounded completion request | **retain the signature, correct the behavior** (LOG-02, LOG-03, LOG-05) |
| `Logger.Dispose` | Idempotent terminal; final flush plus conditional sink disposal | **retain as stable**; document the unbounded budget (LOG-06) and the default ownership transfer (LOG-07) |
| `LoggerOptions` and its three properties | Supported configuration object; defaults `Info`, `1024`, `DisposeSinks = true` | **retain as stable**, defaults frozen; document the ownership meaning of `DisposeSinks` (LOG-07) |
| `DebugLogSink` (sealed; `ILogSink`) | Supported debugger/diagnostic-channel sink | **correct before 1.0** — currently inert in every shipped package (LOG-01); align null handling (LOG-11) |
| `RollingFileLogSink` (sealed; `IFlushableLogSink`) | Supported persistence sink and the module's main production surface | **retain as stable**; document the boundaries in LOG-12 through LOG-17 |
| `RollingFileLogSink.FilePath` | Normalized absolute path, useful for artifact reporting | **retain as stable**; document the CWD binding (LOG-13) |
| `RollingFileLogSink.Flush` | Barrier over the path gate, not a buffer flush | **retain as stable**; document exactly what it guarantees |
| `RollingFileLogSinkOptions` and its four properties | Supported configuration object; defaults `""`, 4 MiB, 4, `UTF8Encoding(false)` | **retain as stable**, defaults frozen; document `RetainedFileCount` semantics (LOG-14) |

No Logging type or member is recommended for removal, internalization,
deprecation, renaming, namespace movement, or experimental classification. The
compiled surface is small, coherent, and already expresses the intended
ownership model; the problems found are behavioral and documentary, not
structural.

## Downstream usage

`NekoLib.Logging` has no dependent `src/` project. Every consumer is a
composition root.

| Consumer | Current use | Compatibility consequence |
|---|---|---|
| `tests/NekoLib.PackageConsumers/WinFormsSmokeProgram.cs` | Loads `typeof(NekoLib.Logging.Logger)` as a PackageReference reachability probe; exercises `ILogSink.Write` through a Core-typed sink. | Proves package load and compile reachability on both target families. It constructs no `Logger`, no options, and no shipped sink, so it is not behavioral evidence. |
| `runtime_tests/Observability/LongRunningRecovery` | Thirteen Logging checks: ordering, concurrent writers, filtering, bounded snapshots, sink-failure isolation, rotation, retention, bounded flush, `DisposeSinks` both ways, shutdown after sink failure, and repeated lifecycles. | The richest behavioral evidence in the repository. It is manual, is not run by `dotnet test`, and was **not** run for this review. |
| `runtime_tests/Data/FarmDatabase` | Builds one application `Logger` over a `RollingFileLogSink` with `DisposeSinks = true`, and hands `ILogger` to `SimMetrics`. | Depends on the current disposal-ownership default and on a windowed write cadence chosen because the file sink opens and closes per entry. |
| Root [`README.md`](../../../../README.md) | The "ordinary logging" example constructs `RollingFileLogSink` plus `Logger(LogLevel, params)` and calls `logger.Info`. | The published example is the de facto supported entry point and must keep compiling and behaving as written. |
| `.local/` legacy demos (ignored) | `new Logger(LogLevel.Debug, new DebugLogSink())`, described as output for the debugger/DebugView window. | Not versioned evidence, but it does establish that `DebugLogSink` had a real intended consumer use, which is the case LOG-01 breaks. |

No repository consumer constructs `DebugLogSink`, and no automated test
references it. That absence is why LOG-01 survived to a shipped package, and it
is precisely the case the release policy warns about: absence of repository
references is not proof that a public member is unused.

## Observed facts, risks, and recommended dispositions

Each item below was confirmed against current source and, where marked, against
executed behavior. The probe programs ran outside the repository against the
Release-built and packaged assemblies; they added no repository file.

### LOG-01 — `DebugLogSink` writes nothing in any shipped package

`DebugLogSink.Write` calls `System.Diagnostics.Debug.WriteLine`
([`DebugLogSink.cs:13`](../../../../src/Logging/NekoLib.Logging/Sinks/DebugLogSink.cs)).
That method is `[Conditional("DEBUG")]`, so the call is removed at the call
site's compilation whenever `DEBUG` is not defined. `DefineConstants` for this
project is `TRACE;NEKOLIB;NETFRAMEWORK;RELEASE` and `TRACE;NEKOLIB;NET_9;RELEASE`
in Release, and packages are produced from Release.

Verified by IL inspection rather than by inference:

| Assembly | `DebugLogSink.Write` IL |
|---|---|
| `bin/Debug/net481` | 34 bytes, containing `callvirt ToString` and `call Debug.WriteLine` |
| `bin/Release/net481` | 11 bytes: `ldarg.1; brtrue.s; ret; nop; leave.s; pop; leave.s; ret` |
| `lib/net481` inside `NekoLib.Logging.1.0.0-local.16.nupkg` | 11 bytes, byte-identical to the Release build |

The `WriteLine` name string is absent from the metadata of both packaged target
assemblies and present in both Debug builds.

**Risk:** the shipped `DebugLogSink` is a documented, packaged, publicly
constructible sink that silently discards every entry. A consumer following the
module's own advertised surface gets no output and no error. This is the single
most consequential finding in the module.

**Recommended disposition:** correct the implementation while keeping the type
name, namespace, and signature. Write through a non-conditional channel:
`System.Diagnostics.Trace.WriteLine` is the closest match because `TRACE` is
defined in both configurations for this project, and the default trace listener
forwards to `OutputDebugString`, which is what both the IDE output window and
DebugView observe. Under the release policy this is a behavioral bug fix that
restores the documented contract, so it needs a changelog entry naming that
contract. Add a focused regression that asserts observable output from a
**Release** build, since a Debug-only test would pass against the broken code.

### LOG-02 — A timed-out flush is abandoned, and its failure can be reported as a crash

`Logger.Flush` runs each flushable sink as `Task.Run((Action)flushable.Flush)`
and waits with the remaining budget
([`Logger.cs:118-120`](../../../../src/Logging/NekoLib.Logging/Logger.cs)). When the
wait times out, the method returns `false` and the `finally` releases `_gate`,
but the task keeps running. Two consequences follow, both observed:

- the pipeline immediately accepts new writes, so the sink observes `Write`
  concurrently with its own still-running `Flush()`. The probe recorded one
  such overlapping write. This contradicts the natural reading of "dispatched
  inline under one lock", which is the mental model the class summary and the
  Observability scenario both use.
- if the abandoned flush later throws, nothing observes the task. The probe
  recorded exactly one `TaskScheduler.UnobservedTaskException`. NekoLib's own
  [`CrashHandler.cs:201-206`](../../../../src/Diagnostics/NekoLib.Diagnostics/CrashHandler.cs)
  subscribes to that event and dispatches it as a crash from source
  `TaskScheduler.UnobservedTaskException`.

**Risk:** in an application that composes Logging with Diagnostics — the exact
composition the root README demonstrates — a logging flush that merely exceeded
its budget can manufacture a crash record and a crash bundle. A slow sink
becomes a fake incident. Separately, custom sinks are being held to an
undocumented thread-safety requirement.

**Recommended disposition:** observe the abandoned task's exception explicitly
(a faulted continuation that reads `Task.Exception` is sufficient and adds no
API), and document that after `false` a sink may still be executing `Flush()`
concurrently with later `Write` calls, so `IFlushableLogSink` implementations
must tolerate that overlap. Do not attempt cancellation: the accepted Core
contract already states that `false` does not promise cancellation, and
`IFlushableLogSink.Flush()` takes no token.

### LOG-03 — `Flush` does not isolate sink failures, unlike `Log` and `Dispose`

`Log` isolates every sink exception and continues
([`Logger.cs:68-72`](../../../../src/Logging/NekoLib.Logging/Logger.cs)). `Dispose`
isolates every flush and dispose exception and continues
([`Logger.cs:143-153`](../../../../src/Logging/NekoLib.Logging/Logger.cs)). `Flush`
does neither: its `catch { return false; }`
([`Logger.cs:122-125`](../../../../src/Logging/NekoLib.Logging/Logger.cs)) exits the
whole loop.

Observed: with a throwing sink registered first and a healthy flushable sink
second, `Flush` returned `false`, the throwing sink was flushed once, and the
later sink was flushed **zero** times.

**Risk:** one misbehaving sink prevents the rolling file sink from being flushed
at all. This is worst precisely where flush matters — the pre-crash path, where
`CrashHandler` requests a bounded log flush before writing its bundle. It is
also inconsistent with the module's own stated principle that one failing sink
never suppresses later sinks.

**Recommended disposition:** isolate per-sink flush failures, continue to the
remaining sinks, and return `false` at the end because completion was not
confirmed for all of them. The `bool` contract is unchanged; only the isolation
behavior is corrected. Budget exhaustion should still stop the loop early, since
continuing past the budget would violate the bound.

### LOG-04 — The sink array is aliased rather than copied

The constructor stores the supplied array directly
([`Logger.cs:39`](../../../../src/Logging/NekoLib.Logging/Logger.cs)). With ordinary
`params` call syntax the compiler synthesizes a fresh array, so most calls are
safe by accident. A caller that passes an explicit `ILogSink[]` keeps a live
reference into the pipeline.

Observed: after `array[0] = otherSink`, the next `Log` dispatched to the
replacement sink, and the originally supplied sink stopped receiving entries.

**Risk:** the dispatch set can change under the pipeline lock without any
synchronization by the caller, and `Dispose` then flushes and disposes whatever
the array currently holds rather than what the pipeline was constructed with.
This is the same aliasing class that F1-CORE already accepted fixing for
`TelemetryCheckpoint`, `TelemetryOperation`, and `InspectionSnapshot`.

**Recommended disposition:** copy the array in the constructor. No signature
changes, no manifest diff, and no migration for any caller that was not
deliberately mutating a shared array.

### LOG-05 — `Flush` after `Dispose` flushes disposed sinks and reports success

`Log` becomes inert after disposal
([`Logger.cs:49`](../../../../src/Logging/NekoLib.Logging/Logger.cs)), but `Flush` has
no disposal check.

Observed: after `Dispose` with `DisposeSinks = true`, the sink had been flushed
once and disposed once; a subsequent `Flush(1s)` returned `true` and invoked
`Flush()` on the already-disposed sink, taking its flush count to two.

`GetRecentEntries` also still returns retained entries after disposal, because
`Dispose` does not clear the buffer. That behavior is useful — a post-shutdown
snapshot is exactly what an incident collector wants — but it is currently
accidental rather than stated.

**Risk:** a member is invoked on an object whose ownership was already
transferred and released. A well-behaved sink that throws `ObjectDisposedException`
turns a post-shutdown flush into a swallowed `false`; a sink that does not check
may touch released resources.

**Recommended disposition:** make `Flush` return `true` immediately once
disposed, matching both `Log`'s inertness and `NullLogger.Flush`, on the ground
that disposal already performed the final flush. Retain post-disposal
`GetRecentEntries` readability and document it as an intentional contract.

### LOG-06 — `Dispose` is unbounded while `Flush` is bounded

`Dispose` calls `flushable.Flush()` synchronously, on the caller's thread, with
no budget ([`Logger.cs:145`](../../../../src/Logging/NekoLib.Logging/Logger.cs)). A
sink that blocks in `Flush()` hangs shutdown indefinitely. The Observability
scenario exercises its blocking sink only through `Flush(timeout)`, never
through `Dispose`, so this path has no evidence at all.

**Recommended disposition:** document it rather than change it. State that
`Dispose` performs a final, unbounded, best-effort flush, and that an
application needing a bounded shutdown should call `Flush(budget)` first and
treat `false` as "persistence not confirmed". Adding a `Dispose(TimeSpan)`
overload or `IAsyncDisposable` would expand the API and introduce a new
partial-shutdown state for no observed consumer need.

### LOG-07 — `DisposeSinks` defaults to `true`, and borrowed sinks are flushed anyway

Two ownership facts, both observed and both currently documented only inside
scenario source comments:

- the default is `DisposeSinks = true`
  ([`LoggerOptions.cs:9`](../../../../src/Logging/NekoLib.Logging/LoggerOptions.cs)),
  so by default constructing a `Logger` transfers sink disposal ownership to it;
- `Dispose` flushes every flushable sink **regardless** of `DisposeSinks`, and
  disposes only when it is `true`. The probe recorded a borrowed sink flushed
  once and disposed zero times.

The second behavior is deliberate and valuable: it is what lets two loggers
share one file sink safely, which the Observability scenario relies on. The
first is a sharp edge, because a consumer sharing a sink between two loggers
gets it disposed by whichever finishes first.

**Recommended disposition:** retain both, freeze the default, and document them
explicitly. Under the compatibility policy a default value consumers rely on is
a contract, and both runtime scenarios already depend on these exact semantics.

### LOG-08 — Timestamp order is not delivery order

`LogEntry` is constructed before the dispatch lock is taken
([`Logger.cs:52-59`](../../../../src/Logging/NekoLib.Logging/Logger.cs)), so under
concurrent writers `TimestampUtc` ordering may disagree with delivery ordering.
The Observability scenario already records this as a deliberate observation
rather than an assertion.

**Recommended disposition:** retain and promote it to a stated contract:
delivery order is identical across all sinks and preserves each writer's own
order; `TimestampUtc` is not a delivery-order key. Stamping inside the lock
would serialize clock reads without making the timestamp a reliable ordering
key for consumers reading the file.

### LOG-09 — `Timeout.InfiniteTimeSpan` is rejected

`Flush` throws `ArgumentOutOfRangeException` for any negative `TimeSpan`
([`Logger.cs:93-94`](../../../../src/Logging/NekoLib.Logging/Logger.cs)), which
includes `Timeout.InfiniteTimeSpan`. Confirmed by probe.

**Recommended disposition:** retain — an unbounded "bounded completion request"
is a contradiction, and `Dispose` already provides the unbounded path. Document
it so callers do not discover it by exception.

### LOG-10 — Validation `paramName` names the option property, not the parameter

`nameof(options.RecentEntryCapacity)` evaluates to `"RecentEntryCapacity"`, not
`"options"`. Confirmed: `ArgumentOutOfRangeException.ParamName` is
`'RecentEntryCapacity'` from `Logger` and `'MaximumFileBytes'` from
`RollingFileLogSink`; `nameof(options.Encoding)` behaves the same way.

**Recommended disposition:** retain. The current strings identify the offending
setting, which is more useful to a consumer than the literal parameter name,
and changing them is an observable change with no consumer benefit. Document
the convention instead.

### LOG-11 — The two shipped sinks disagree on null entries

`DebugLogSink.Write(null)` returns silently
([`DebugLogSink.cs:10-11`](../../../../src/Logging/NekoLib.Logging/Sinks/DebugLogSink.cs));
`RollingFileLogSink.Write(null)` throws `ArgumentNullException`
([`RollingFileLogSink.cs:58-59`](../../../../src/Logging/NekoLib.Logging/Sinks/RollingFileLogSink.cs)).
Both confirmed by probe. `Logger` never passes null, so only a custom pipeline
or a direct call reaches either path.

**Recommended disposition:** align `DebugLogSink` on the throwing behavior.
`ILogSink.Write(LogEntry entry)` is already annotated non-null in the compiled
contract, sinks are public extension points invoked directly by custom
pipelines, and `Logger` isolates sink exceptions anyway. Silently swallowing a
null argument hides a caller defect. No repository consumer passes null, so the
migration cost is zero. The alternative — making both tolerant — is recorded
under rejected alternatives.

### LOG-12 — The rolling-file path-gate registry is process-wide and never pruned

`RollingFileLogSink` holds a `static Dictionary<string, object>` keyed by
normalized path with `OrdinalIgnoreCase`
([`RollingFileLogSink.cs:15-17, 41-51`](../../../../src/Logging/NekoLib.Logging/Sinks/RollingFileLogSink.cs)).
Entries are added on construction and never removed; the sink is not
`IDisposable`, so there is no natural removal point.

**Risk:** an application that rotates paths itself — date-stamped file names,
per-session directories — accumulates one entry per distinct path for the
process lifetime. Growth is bounded by distinct paths rather than by sink
count, so it is small, but it is unbounded in principle and it is genuine
process-global state.

Note also that `OrdinalIgnoreCase` is a Windows assumption. The package targets
`net9.0` rather than `net9.0-windows`, so on a case-sensitive filesystem two
genuinely different files can share one gate. That over-serializes; it does not
corrupt.

**Recommended disposition:** retain the mechanism and document it. It is what
makes two same-path sinks safe inside a process, which the Observability
scenario depends on. Reference-counting the gates would require making
`RollingFileLogSink` implement `IDisposable`, which adds public surface and a
new interaction with `DisposeSinks` for a leak no observed consumer can reach.
Record it here so the F1-LOG "no global state" claim stays honest about what
already exists.

### LOG-13 — A relative `FilePath` binds to the working directory at construction

`Path.GetFullPath(options.FilePath)`
([`RollingFileLogSink.cs:36`](../../../../src/Logging/NekoLib.Logging/Sinks/RollingFileLogSink.cs))
resolves against the process working directory at the moment the sink is built.
For an unattended shell whose working directory is not the install directory,
that is a trap.

**Recommended disposition:** retain and document; recommend absolute paths
rooted at `AppContext.BaseDirectory`, which is what the root README example
already does. `FilePath` exposes the resolved value, so a consumer can report
exactly where it landed.

### LOG-14 — `RetainedFileCount` counts archives, and neither bound can be disabled

`Rotate` deletes `path.N`, shifts `path.{N-1}` upward, then moves the live file
to `path.1`
([`RollingFileLogSink.cs:97-113`](../../../../src/Logging/NekoLib.Logging/Sinks/RollingFileLogSink.cs)).
`RetainedFileCount` is therefore the number of retained **archives**, excluding
the live file; with `4` a consumer ends up with five files. Validation rejects
`RetainedFileCount < 1` and `MaximumFileBytes < 1024`, so neither rotation nor
archiving can be switched off; the only way to suppress rotation is a very large
maximum. Rotation happens before the write that would exceed the maximum, and
the `length > 0` guard prevents rotating an empty file, so a single entry larger
than the maximum is written whole rather than looping.

**Recommended disposition:** retain all of it and document the exact meaning,
the `.1`…`.N` naming, the oldest-first eviction, and the two floors.

### LOG-15 — Write failures are silently absorbed, and cross-process writers are rejected

The append stream uses `FileShare.Read`
([`RollingFileLogSink.cs:77-81`](../../../../src/Logging/NekoLib.Logging/Sinks/RollingFileLogSink.cs)),
so a second process opening the same path fails with a sharing violation. The
resulting exception propagates out of `Write` into `Logger.Log`, which swallows
it. Rotation failures — a held archive, a denied delete — behave the same way.

**Risk:** the second process logs nothing, forever, with no error surface. This
is the intended trade-off ("logging must never break feature behavior") but the
consequence is currently stated only in an XML comment on the sink class.

**Recommended disposition:** retain the isolation and document the boundary
plainly in the module reference: one process owns a log path; a second process
must use a different path; and sink failures are absorbed, so persistence is
best-effort and `Flush` is the only completion signal.

### LOG-16 — Durability is level-dependent

`stream.Flush(true)` is issued only for `Error` and above
([`RollingFileLogSink.cs:86-87`](../../../../src/Logging/NekoLib.Logging/Sinks/RollingFileLogSink.cs)).
Lower levels rely on the managed flush plus stream close, which survives process
death but not necessarily machine death.

**Recommended disposition:** retain — forcing a disk flush per `Info` entry
would dominate the cost of the sink — and document the two tiers explicitly.

### LOG-17 — The encoding preamble is not counted toward the rolling threshold

The threshold uses `_encoding.GetByteCount(line)`
([`RollingFileLogSink.cs:62`](../../../../src/Logging/NekoLib.Logging/Sinks/RollingFileLogSink.cs)),
which excludes any preamble `StreamWriter` emits when it first creates the file.
The default `UTF8Encoding(false)` has no preamble, so the default path is exact;
a consumer supplying `Encoding.UTF8` or a UTF-16 encoding makes the threshold
approximate by the preamble length.

**Recommended disposition:** retain and document. Also worth documenting: with
`RecentEntryCapacity` there is no upper bound, and the constructor pre-allocates
`new Queue<LogEntry>(capacity)`, so an absurd capacity fails at construction.
That is a caller assertion, consistent with the accepted Core stance.

### LOG-18 — Logging has no current technical reference

Logging contracts are currently spread across a one-row entry and one snippet in
the root [`README.md`](../../../../README.md) and the *Core* contract descriptions in
[`docs/modules/Core/REFERENCE.md`](../../Core/REFERENCE.md).
Core legitimately owns the interfaces; nothing owns the concrete pipeline's
ownership, ordering, flush, disposal, rotation, retention, durability, and
failure semantics. Data, Core, HTTP, and Navigation each have a module README;
Logging does not, and almost every disposition above ends in "document this".

**Recommended disposition:** add
`src/Logging/NekoLib.Logging/README.md` as the module's current technical
reference and register it in [`docs/README.md`](../../../README.md). It is the only
place the corrected `DebugLogSink` contract, the flush overlap requirement for
custom sinks, the disposal-ownership default, and the rolling-file boundaries
can be owned without duplicating Core.

### LOG-19 — Automated coverage is thin where the findings are

The nine focused tests cover level filtering, registration ordering, write-path
sink isolation, bounded snapshot ordering, successful flush, flush timeout, a
rolling-file write, rotation with retention, and a write failure. Not covered by
any automated test:

- `DebugLogSink` — no automated test references it at all (LOG-01);
- flush isolation across a throwing sink (LOG-03);
- `Flush` and `GetRecentEntries` after `Dispose` (LOG-05);
- `DisposeSinks` in either mode, borrowed-sink flushing, and `Dispose`
  idempotency (LOG-07);
- sink-array aliasing (LOG-04);
- the constructor matrix: null options, null sink array, null sink elements;
- concurrent writers and delivery-order agreement across sinks (LOG-08);
- `RetainedFileCount = 1`, an entry larger than the maximum, and the four
  validation rules on `RollingFileLogSinkOptions`;
- cross-process rejection (LOG-15).

Several of these are covered by the manual Observability runtime scenario, which
is real evidence but is not run by `dotnet test`, and was not run for this
review. `tests/README.md` already classifies the rolling-file tests as
integration-scoped inside the Unit project; that classification stays correct.

## Target parity

Both manifests are byte-identical, no source file uses conditional compilation,
and no behavior differs by target. One cosmetic asymmetry exists: the csproj
adds `NoWarn 1591` on `net481` only
([`NekoLib.Logging.csproj:34`](../../../../src/Logging/NekoLib.Logging/NekoLib.Logging.csproj)).
Because `GenerateDocumentationFile` is not set, CS1591 cannot fire on either
target, so the suppression is inert. **Recommended disposition:** leave it
alone in F1-LOG; it changes no compiled surface and removing it belongs to a
build-hygiene pass, not an API decision.

## Facade, provider, and pipeline questions

Each of the following was considered explicitly and is **not** recommended:

- **a static `Log` facade** — Navigation's static facade exists because a
  single-window shell has exactly one navigation context. Logging has no such
  singleton truth: the Observability scenario constructs a separate `Logger`
  per check, and FarmDatabase composes one per application. A facade would
  contradict observed use.
- **a logging provider or registry in Logging** — Core deliberately has no
  global logging provider, and that is an accepted F1-CORE decision. Adding one
  in the concrete package would reintroduce exactly what Core rejected.
- **an asynchronous or queued pipeline** — the module's value proposition is
  synchronous ordered delivery with a bounded flush, and both runtime scenarios
  assert on that ordering. A background queue would change delivery ordering,
  crash-time completeness, and every documented guarantee, for a throughput
  problem no consumer has reported.
- **a Diagnostics, Telemetry, or Inspection dependency** — Logging must stay a
  leaf on Core. Diagnostics consumes Logging through Core contracts, never the
  reverse.
- **new global state** — none is needed; LOG-12 documents the only existing
  instance rather than adding to it.
- **symmetry with Telemetry or Inspection** — Logging's ownership model is
  already correct for its domain. Nothing here should change because a sibling
  module has a different shape.

## Likely migration cost

| Recommended disposition | Source impact | Binary / behavioral impact |
|---|---|---|
| LOG-01 `DebugLogSink` writes through a non-conditional channel | None. | No manifest diff. Behavioral fix: the sink starts producing the output it always claimed. A consumer that (unknowingly) relied on silence now sees debug-channel output. Changelog entry required. |
| LOG-02 observe the abandoned flush task; document the overlap | None. | No manifest diff. Removes a spurious `TaskScheduler.UnobservedTaskException`, so an application using `CrashHandler` stops recording a crash for a slow sink. |
| LOG-03 isolate per-sink flush failures | None. | No manifest diff. `Flush` still returns `false`, but later sinks — including the rolling file sink — now actually get flushed. |
| LOG-04 copy the sink array | None. | No manifest diff. Code that deliberately mutated a shared array to re-target a live pipeline stops working; no such consumer exists. |
| LOG-05 `Flush` becomes inert after disposal | None. | No manifest diff. A post-disposal `Flush` stops touching disposed sinks; it already returned `true` in the observed case, so the return value is unchanged for well-behaved sinks. |
| LOG-11 align `DebugLogSink` null handling | None. | No manifest diff. `Write(null)` throws instead of returning; only reachable from a custom pipeline, and no repository consumer does it. |
| LOG-06, LOG-07, LOG-08, LOG-09, LOG-10, LOG-12 – LOG-17 documentation | None. | None. Clarifies existing behavior. |
| LOG-18 module README + registration | None. | None. |
| LOG-19 focused regressions | None. | None. Would have caught LOG-01, LOG-03, and LOG-05. |

Every recommended change is confined to method bodies. No public type, member,
signature, nullability annotation, default value, namespace, target, or project
reference changes, so **no API manifest update is expected**. If implementation
nevertheless produces a manifest diff, that is a signal to stop and re-review,
not to run `-UpdateBaseline`.

## Package gate

Assembly bits **will** change if any of LOG-01 through LOG-05 or LOG-11 is
accepted. The existing `1.0.0-local.16` evidence therefore cannot be reused for
the accepted work, and the package gate must be left explicitly pending for
Codex: a new immutable coordinated family version from a clean implementation
commit, PackageReference consumer probes, and provenance/hash recording.

If only the documentation dispositions (LOG-06 through LOG-10 and LOG-12
through LOG-19) are accepted, no assembly bit changes and the existing
`local.16` evidence remains sufficient.

The Observability runtime scenario source is **not** affected by any recommended
disposition: it constructs no `DebugLogSink`, never mutates a supplied sink
array, never flushes after disposal, and its `ScheduledFailingLogSink` is only
armed for `Flush` in the `Dispose`-based check. It should still be rebuilt on
both targets if LOG-01 through LOG-05 are accepted, because it is the module's
richest behavioral consumer — but it must not be launched without explicit
authorization and an agreed worst-case duration.

## Rejected alternatives

- **Remove `DebugLogSink` instead of fixing it** — rejected. It is a documented
  public entry point with a real historical consumer; removing it would be a
  breaking change adopted to avoid a one-line fix.
- **Keep `Debug.WriteLine` and tell consumers to build in Debug** — rejected. A
  packaged library's behavior must not depend on the configuration the library
  itself was built in, and consumers cannot rebuild a NuGet package.
- **Make `Flush` cancel a timed-out sink flush** — rejected. `IFlushableLogSink.Flush()`
  takes no cancellation token, and the accepted Core contract explicitly states
  that `false` does not promise cancellation. Thread aborts are not an option.
- **Flush sinks on the calling thread without `Task.Run`** — rejected. The
  budget could not be enforced at all; a blocking sink would make `Flush(timeout)`
  unbounded, which is the one thing the method exists to prevent.
- **Add `Dispose(TimeSpan)` or `IAsyncDisposable`** — rejected for F1-LOG. It
  expands the public surface and introduces a new partial-shutdown state for no
  observed consumer need; documenting "flush first, then dispose" covers it.
- **Change `DisposeSinks` to default `false`** — rejected. It is a
  compatibility-significant default that both runtime scenarios and the root
  README example depend on, and the sinks are usually constructed inline for
  exactly one logger.
- **Stop flushing borrowed sinks when `DisposeSinks = false`** — rejected. That
  behavior is deliberate and is what allows two loggers to share one file sink.
- **Make both sinks tolerate a null entry** — rejected in favor of aligning on
  throwing. The compiled contract already annotates the parameter non-null, and
  a silent return hides a caller defect at a public extension point.
- **Reference-count the rolling-file path gates via `IDisposable`** — rejected
  for now. It adds public surface and a new `DisposeSinks` interaction to bound
  a leak proportional to distinct paths, which no observed consumer reaches.
- **A static logging facade, a provider/registry, or an async pipeline** —
  rejected; see the section above.
- **Normalizing exception `paramName` to `"options"`** — rejected. It is an
  observable change that makes the diagnostic strictly less useful.
- **Splitting the rolling-file tests out of the Unit project** — rejected.
  `tests/README.md` already classifies them correctly, and their prerequisites,
  runtime, and canonical command do not materially differ.

## Proposed implementation block after acceptance

One narrow F1-LOG implementation should:

1. correct `DebugLogSink` to write through a non-conditional channel and align
   its null handling (LOG-01, LOG-11);
2. correct `Logger.Flush` to isolate per-sink failures, observe abandoned tasks,
   and become inert after disposal (LOG-02, LOG-03, LOG-05);
3. copy the sink array in the constructor (LOG-04);
4. add focused dual-target regressions for every corrected behavior plus the
   ownership and constructor gaps listed in LOG-19, ensuring the `DebugLogSink`
   regression is meaningful in a Release build;
5. add `src/Logging/NekoLib.Logging/README.md`, register it in
   [`docs/README.md`](../../../README.md), add its row to the `AGENTS.md` routing
   table, and reconcile the root `README.md` Logging entry (LOG-06 through
   LOG-10, LOG-12 through LOG-18);
6. update `CHANGELOG.md` with the behavioral corrections, naming the documented
   contract each one restores, and add `docs/migrations/f1-logging.md` because
   the `DebugLogSink` and null-handling corrections change observable consumer
   behavior;
7. promote only the accepted dispositions to `TODO.md`, run the focused tests on
   both targets, run `eng/verify-public-api.ps1 -PackageId NekoLib.Logging`
   expecting **no** baseline change, run the full solution gate proportionally,
   run `eng/verify-docs.ps1`, and run `git diff --check`;
8. build the Observability runtime scenario on both targets without launching
   it, and leave the package gate explicitly pending for Codex.

## Review validation

Validation is recorded by evidence class so one gate is not mistaken for
another:

| Evidence class | Review result | Claim and limit |
|---|---|---|
| Focused tests | `dotnet test tests\NekoLib.Logging.Tests\Unit\NekoLib.Logging.Tests.Unit.csproj -c Release`: 9/9 passed on `net481` and 9/9 on `net9.0`, 0 failures and 0 skips. | Confirms the stated baseline. Covers only the nine existing cases; see LOG-19 for what it does not reach. |
| Build | `eng/verify-public-api.ps1 -PackageId NekoLib.Logging` built both targets with 0 warnings and 0 errors. | Compilation only. |
| Compiled API comparison | Both accepted manifests matched; 2/2 baselines verified, none updated. Both files are byte-identical with SHA-256 `8FB0BDF736FFF0201AB57F32E983DBECFFA5133DE5D1DA0944D22ED32C88DB8A`. | Current assembly surface versus the accepted candidate manifests. |
| IL inspection | `DebugLogSink.Write` reflected from `bin/Debug/net481` (34 bytes, `Debug.WriteLine` present), `bin/Release/net481` (11 bytes, call absent), and `lib/net481` extracted from `NekoLib.Logging.1.0.0-local.16.nupkg` (11 bytes, identical to Release). `WriteLine` absent from the metadata of both packaged target assemblies. | Establishes LOG-01 on the shipped artifact, not by inference. Each assembly was loaded in its own process to avoid assembly-identity reuse. |
| Behavioral probe | A scratch `net9.0` console program, outside the repository, referencing the Release-built Core and Logging assemblies, confirmed LOG-02 (overlapping write during an abandoned flush; one `TaskScheduler.UnobservedTaskException`), LOG-03 (later sink flushed 0 times), LOG-04 (aliased array re-targeted the pipeline), LOG-05 (post-dispose `Flush` returned `true` and re-flushed a disposed sink; snapshot still readable), LOG-07 (borrowed sink flushed once, disposed zero times), null sink-element tolerance, `Timeout.InfiniteTimeSpan` rejection, both `ParamName` values, and the LOG-11 null-entry divergence. | Single-run, single-machine, `net9.0` only. It is deterministic observation of current behavior, not a regression suite and not `net481` evidence. |
| Documentation | Not run for the review body; `eng/verify-docs.ps1` applies to the artifact and index registration. | See the commit record. |
| Runtime evidence | Not run. The Observability, FarmDatabase, and every other runtime scenario were not executed. | No real or long-running runtime claim is made. |
| Package evidence | Not run. No package was created, modified, or consumed. `local.16` was read only. | `local.16` remains prior evidence from `7ae62a2`; it is not evidence for any proposed change. |

These review gates establish the current candidate baseline and the observed
defects only. They do not validate any accepted future change.

## Residual validation limits

- The behavioral probe ran on `net9.0` only. `net481` behavior for LOG-02
  through LOG-05 is inferred from identical source with no conditional
  compilation, and from the identical compiled manifests; it was not executed.
  The LOG-01 IL evidence *was* taken on `net481`, including the packaged
  assembly.
- No cross-process rolling-file test was executed; LOG-15 rests on the
  `FileShare.Read` mode in source and the sink's own class comment.
- No long-running or soak behavior was observed. The LOG-12 registry growth is a
  code fact, not a measured leak.
- The `TaskScheduler.UnobservedTaskException` observation required forced
  collections; in a real application the timing depends on GC scheduling, so the
  crash-record consequence is possible rather than guaranteed on every timeout.
- Timestamp-versus-delivery ordering (LOG-08) was not re-measured here; it is
  taken from source and from the Observability scenario's recorded observation.

## Decision gate

Nothing in this review authorizes product, test, API-baseline, changelog,
migration, roadmap, or package changes. F1-LOG must stop here until the user
explicitly accepts, modifies, or rejects the recommended dispositions —
especially LOG-01, the behavioral corrections LOG-02 through LOG-05 and LOG-11,
and the new module reference in LOG-18.

## Reconciliation — 2026-08-17

The user accepted the recommended dispositions without modification. The
implementation is confined to method bodies and documentation:

- **LOG-01** — `DebugLogSink` writes through `Trace.WriteLine`, and the project
  states `DefineTrace` explicitly so the shipped sink cannot silently become a
  no-op again. The Release `net481` body went from the 11-byte no-op to 32
  bytes containing the `ToString` and trace calls.
- **LOG-11** — `DebugLogSink.Write(null)` throws `ArgumentNullException`.
- **LOG-02** — `Logger.Flush` observes the fault of a sink that outlived the
  budget through a faulted continuation.
- **LOG-03** — `Flush` isolates a thrown sink failure and continues to later
  sinks while budget remains; budget exhaustion still stops the loop and the
  result is `false` whenever completion was not confirmed.
- **LOG-04** — the constructor copies the sink array and drops null elements
  once instead of re-checking on every write.
- **LOG-05** — `Flush` returns `true` after disposal completes; a concurrent
  bounded request waits on the same gate or returns `false` when its budget
  expires first.
- **LOG-06 to LOG-10 and LOG-12 to LOG-17** — retained and documented, with
  XML summaries on the contract-significant members.
- **LOG-18** — [`src/Logging/NekoLib.Logging/README.md`](../../../../src/Logging/NekoLib.Logging/README.md)
  is the module's current technical reference, registered in the documentation
  index and the `AGENTS.md` routing table.
- **LOG-19** — twenty-one focused regressions were added, taking the suite from
  9 to 30 per target. The `DebugLogSink` regression asserts observable trace
  output, which is meaningful only because the canonical command runs in
  Release.

As the review predicted, **both accepted API manifests verified unchanged**; no
baseline was updated. The accepted work therefore carries no source or binary
compatibility break, only the behavioral corrections recorded in
[`CHANGELOG.md`](../../../../CHANGELOG.md) and
[`docs/modules/Logging/migrations/f1.md`](../migrations/f1.md).

No repository consumer or runtime scenario source required migration. The
Observability `LongRunningRecovery` scenario compiles unchanged against the
corrected pipeline on both target families; it was built, not launched, and no
runtime scenario was executed.

The package gate remains **explicitly pending for Codex**. Assembly bits
changed, so `1.0.0-local.16` is prior evidence only and cannot be reused: a new
immutable coordinated family version from the implementation commit,
PackageReference consumer probes, and provenance/hash recording are still
required. No package was created, modified, or consumed by this work, and
F1-LOG is deliberately not marked complete.

The residual validation limits recorded above still stand, with one narrowing:
the corrected behaviors now have executable dual-target regressions, so the
`net9.0`-only probe limitation no longer applies to LOG-01 through LOG-05 or
LOG-11.

## Package reconciliation — 2026-08-17

Independent packaging review found two pre-package inconsistencies in the
accepted implementation. Documentation said every sink was attempted without
stating the already accepted total-budget boundary, while the implementation
correctly stopped admitting later flushes after budget exhaustion. More
importantly, `Dispose` published `_disposed` before its final flush acquired the
pipeline gate, allowing a concurrent `Flush` to report success before disposal
completed or to reach already disposed sinks. Commit
`dd0cfb8d9c0b69f234cb8cbe802ed8cac4b14213` aligned the wording, moved disposal
admission under the pipeline gate, and added regressions for both budget
exhaustion and concurrent disposal.

The final Logging suite passed 30/30 on `net481` and 30/30 on `net9.0`; the full
solution passed 1,352/1,352 tests with no failures or skips. Both Logging API
manifests remained unchanged, documentation and diff hygiene passed, and the
Observability scenario compiled without warnings on both targets without being
launched. The canonical clean package build emitted the existing 515-warning
baseline and no errors.

The canonical package flow created coordinated immutable family version
`1.0.0-local.17` from `dd0cfb8d9c0b69f234cb8cbe802ed8cac4b14213`.
`NekoLib.Logging.1.0.0-local.17.nupkg` contains `net481` and `net9.0`
assemblies, declares `NekoLib.Core` at the same version, records that source
commit in its NuGet metadata, and has SHA-256
`8378290431CA1036BD8E70E254C6995415DC755B6A91D82D7CAEDD7802AF3991`.

PackageReference-only WinForms and WPF consumers restored, built, and ran on
both target families with zero consumer warnings. The multitarget consumer,
package structure, Watchdog Host payload, deployment opt-out, stale-payload
replacement, publish, and clean probes also passed. This is package and
package-consumer evidence, not a long-running application scenario; none was
launched or required. F1-LOG is complete, and this review is historical.
