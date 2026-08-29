# Telemetry Public API Review — 2026-08-17

**Document ID:** TEL-AUDIT-PUBLIC-API-20260817

**Schema version:** 1

**Kind:** audit

**Lifecycle:** historical

**Subject:** F1-TEL compiled public surface, pipeline and options ownership, operation lifecycle, dimension and measurement semantics, bounded retention, snapshot and sink-dispatch contracts, and compatibility boundaries

**Surface:** audit

**Boundary:** telemetry

**Authority role:** evidence

**Mutation:** snapshot

**Indexing:** include

**Status:** all dispositions accepted, implemented, and package-validated

**Reference date:** 2026-08-17

**Reference commit:** `6480c9e57a42af3490eeda55b0f66400e75782cd`

**Original path:** `docs/audit/telemetry-public-api-review-2026-08-17.md`

**Last reconciliation:** 2026-08-17

**Current state:** [`TODO.md`](../../../../TODO.md) F1-TEL

## Baseline and authority

This review covers committed `HEAD` on branch
`phase-e/sqlserver-and-orchestration`. Before the review artifact was added, the
worktree and index were clean and the branch was sixteen commits ahead of its
matching remote branch. Nothing from this branch was pushed by the review.

The reviewed authority is the Telemetry project, all of its source, its project
file, the compiled `net481` and `net9.0` assemblies, the dual-target focused
tests, the assembly-derived manifests, the accepted Core telemetry contracts,
the public API release policy, and the current repository consumer source.
Historical reviews and handoffs supplied architectural context only.

The review changes no product source, test, API baseline, package, changelog,
migration guide, or live roadmap item.

The latest immutable package family is `1.0.0-local.17`.
`NekoLib.Telemetry.1.0.0-local.17.nupkg` has SHA-256
`DCAAFFCA088EE5223331DA2CD00C832220A023E33356A670A58C35BD35035BD0` and was
produced from `dd0cfb8d9c0b69f234cb8cbe802ed8cac4b14213`. The two commits after
that package only completed the Logging documentation state, so those package
bits correspond to the reviewed Telemetry surface. That package is prior
pre-F1-TEL evidence; it is not evidence for any change proposed here.

## Scope

Included:

- both compiled public Telemetry type declarations and all five public member
  declarations, on both target frameworks;
- pipeline and options ownership, option read-once behavior, sink-array
  capture, and null sink elements;
- operation start, checkpoint, completion, abandonment, and `IsCompleted`
  semantics, including concurrent completion and post-completion checkpoints;
- module/name validation, generated and supplied operation IDs, and parent-ID
  handling;
- wall-clock versus monotonic time, checkpoint elapsed values, and completion
  versus start ordering;
- dimension copying, comparer, collision, duplicate-key, null-key, and
  shallow-value behavior, plus measurement permissiveness;
- retention capacity, completed-only retention, bounded newest window, ordering,
  non-positive limits, snapshot freshness, and model stability;
- sink dispatch timing, ordering, failure isolation, reentrancy, and
  backpressure;
- target parity, package boundary, and documentation ownership.

Excluded:

- implementing any recommendation, editing product source or tests, or updating
  an accepted API manifest;
- modifying `NekoLib.Core`, whose telemetry contracts are already accepted;
- packaging, package-consumer execution, and any package-backed claim;
- executing the Observability runtime scenario or any long-running scenario;
- Logging, Inspection, Diagnostics, and Navigation surfaces beyond their effect
  on Telemetry contracts.

## Package, ownership, and lifecycle boundary

[`NekoLib.Telemetry.csproj`](../../../../src/Telemetry/NekoLib.Telemetry/NekoLib.Telemetry.csproj)
targets `net481;net9.0`, enables nullable annotations, disables implicit usings,
declares `NEKOLIB` plus the conditional `NETFRAMEWORK` / `NET_9` symbols, and
references only `NekoLib.Core`. No source file uses `#if`, so there is no
target-conditional behavior to reconcile.

The ownership model matches the accepted Core decision and the Logging
precedent:

- the composition root constructs `TelemetryPipeline` and decides its lifetime;
- feature modules receive `ITelemetry` and never own the pipeline;
- the caller that starts an operation owns its one explicit terminal `Complete`;
- `TelemetryPipelineOptions` is read once into readonly fields at construction,
  so post-construction option mutation cannot affect a live pipeline;
- there is no global provider, registry, or static facade.

`TelemetryPipeline` is deliberately **not** `IDisposable`. It owns no handle, no
buffer that outlives a call, and no background worker: dispatch is inline and
synchronous, so there is nothing to flush and no shutdown step. This is a real
asymmetry with `Logger`, which owns `DisposeSinks` and a bounded `Flush`, and it
is correct for a pipeline that holds nothing.

`OperationScope` is a private nested type. `ITelemetryOperation` is the only
handle a consumer ever receives, and the pipeline never retains a reference to a
live scope — which is why an abandoned operation is collectable and does not
leak.

## Compiled-surface inventory and recommended classification

The accepted `net481` and `net9.0` manifests are byte-identical and share
SHA-256 `CFC823DA2CAC08C93CE27B27261416F2618830932C8D6DAA9F985E20275DCC86`.
Each declares 2 public types and 5 public members, with identical nullable
metadata, optional parameter values, `params` shape, sealed shapes, and
interface implementation lists. There is no target-specific Telemetry API, no
experimental marker, and no deprecation marker. Both public types are `sealed`;
there is no protected surface and no inheritance seam. Extension happens
exclusively through the Core `ITelemetrySink` interface.

| Type or member | Supported role | Recommended disposition |
|---|---|---|
| `TelemetryPipeline` (sealed; `ITelemetry`, `ITelemetrySnapshotSource`) | Supported consumer entry point and the module's only pipeline | **retain as stable, with behavioral corrections** (TEL-01, TEL-02, TEL-03) |
| `TelemetryPipeline(TelemetryPipelineOptions?, params ITelemetrySink[]?)` | Supported composition entry point | **retain as stable**; copy the sink array (TEL-01) |
| `TelemetryPipeline.StartOperation` | `ITelemetry` implementation; validation, identity, initial dimensions | **retain as stable**; normalize a blank parent ID (TEL-03) |
| `TelemetryPipeline.GetRecentOperations` | `ITelemetrySnapshotSource` implementation; bounded newest window | **retain as stable**; document ordering and freshness (TEL-06, TEL-07, TEL-08) |
| `TelemetryPipelineOptions` and `RecentOperationCapacity` | Supported configuration object; default `1024`, minimum `1` | **retain as stable**, default frozen (TEL-14) |

No Telemetry type or member is recommended for removal, renaming, namespace
movement, internalization, deprecation, or experimental classification. The
compiled surface is minimal and already expresses the intended ownership model.
As in F1-LOG, the problems found are behavioral and documentary, not structural,
and the low public-type count is not itself evidence of either quality or
deficiency.

## Downstream usage

`NekoLib.Telemetry` has no dependent `src/` project. Every consumer of the
concrete pipeline is a composition root; feature modules consume only the Core
contracts.

| Consumer | Current use | Compatibility consequence |
|---|---|---|
| `NekoLib.Navigation` — [`NavigationTelemetryObserver.cs`](../../../../src/Navigation/NekoLib.Navigation/Diagnostics/NavigationTelemetryObserver.cs) | Receives `ITelemetry`, starts one correlated `page_switch` operation per request, adds `page_switch_started` / `page_ready` checkpoints, and completes with an explicit outcome; isolates `Complete` failures and cancels outstanding operations at teardown. | The heaviest producer. It depends on explicit-terminal semantics, checkpoint ordering, and `Complete` never throwing into the navigation lifecycle. |
| `NekoLib.Watchdog` — [`WatchdogRuntime.cs:1017`](../../../../src/Watchdog/NekoLib.Watchdog/WatchdogRuntime.cs) | Optional `ITelemetry`; starts and immediately completes short operations. | Uses only the start/complete pair; unaffected by retention or snapshot shape. |
| `NekoLib.Diagnostics` — [`CrashHandler.cs`](../../../../src/Diagnostics/NekoLib.Diagnostics/CrashHandler.cs) | Consumes `ITelemetrySnapshotSource.GetRecentOperations(MaxRecentTelemetryOperations)` under a contributor budget and formats operations and checkpoints into the crash bundle. | Depends on the snapshot being non-null, bounded, cheap, and not blocked by sinks. TEL-06 matters directly here. |
| `runtime_tests/Observability/LongRunningRecovery` | Eight Telemetry checks: concurrency, bounded retention, checkpoint ordering, correlation chains, terminal outcomes, snapshot boundaries, abandoned scopes, and returned-model immutability. | The richest behavioral evidence in the repository. It is manual, is not run by `dotnet test`, and was **not** run for this review. |
| `runtime_tests/Data/FarmDatabase` | Builds one application `TelemetryPipeline` and hands `ITelemetry` to its metrics reporter. | Depends on construction and on retention capacity only. |
| `tests/NekoLib.PackageConsumers/WinFormsSmokeProgram.cs` | Loads `typeof(NekoLib.Telemetry.TelemetryPipeline)` as a reachability probe and writes a hand-built `TelemetryOperation` to a Core-typed sink. | Proves package load and compile reachability on both target families. It constructs no pipeline and completes no operation, so it is not behavioral evidence. |

## Observed facts, risks, and recommended dispositions

Each item was confirmed against current source and, where marked, against
executed behavior. The probe programs ran outside the repository against the
Release-built assemblies; they added no repository file.

### TEL-01 — The sink array is aliased rather than copied

The constructor stores the supplied array directly
([`TelemetryPipeline.cs:30`](../../../../src/Telemetry/NekoLib.Telemetry/TelemetryPipeline.cs)).
Ordinary `params` call syntax is safe by accident because the compiler
synthesizes a fresh array, but a caller passing an explicit `ITelemetrySink[]`
keeps a live reference into the pipeline.

Observed: after `array[0] = otherSink`, the next completed operation was
dispatched to the replacement, and the originally supplied sink stopped
receiving. Null elements are separately tolerated, via `_sinks[i]?.Write(...)`.

**Risk:** the dispatch set can change under the dispatch gate without any
synchronization by the caller. This is the same aliasing class already accepted
and corrected for Core's collection-bearing models (F1-CORE) and for
`Logger`'s sink array (F1-LOG, LOG-04); leaving it here would make the two
capability pipelines disagree for no reason.

**Recommended disposition:** copy the array in the constructor and drop null
elements once, exactly as `Logger` now does. No signature change, no manifest
diff, and no migration for any caller that was not deliberately mutating a
shared array.

### TEL-02 — A failing terminal-dimension copy destroys the operation permanently

`Complete` sets `_completed = true` and stops the stopwatch **before** copying
the caller's terminal dimensions and measurements
([`TelemetryPipeline.cs:166-198`](../../../../src/Telemetry/NekoLib.Telemetry/TelemetryPipeline.cs)).
`Copy` materializes into a `Dictionary`, so a caller-supplied
`IReadOnlyDictionary` that enumerates a null key — or whose enumerator throws —
throws out of `Complete` after the operation has already been marked terminal
but before `Record` runs.

Observed, with a dictionary that yields one null key:

| Step | Result |
|---|---|
| `Complete(Succeeded, badDimensions)` | threw `ArgumentNullException` |
| `operation.IsCompleted` | `true` |
| `GetRecentOperations(10).Count` | `0` |
| retry `Complete(Failed)` | returns immediately; still `0` retained |

The operation is unrecoverable: it never reaches retention, never reaches any
sink, and can never be completed again. Contrast the same bad data supplied to
`StartOperation`, which throws cleanly and creates no operation at all — a
correct fail-fast because no state was committed.

**Risk:** a caller bug in diagnostic payload data silently and permanently
deletes the telemetry record it was attached to, and the loss is invisible: the
operation reports itself completed. For Navigation, whose observer isolates
`Complete` failures, this means a swallowed exception and a vanished
`page_switch` record.

**Recommended disposition:** materialize both copies **before** mutating
`_completed` and the stopwatch. The exception still surfaces to the caller — a
malformed dimension dictionary is a caller defect and should not be silently
absorbed — but the operation stays completable, so a corrected retry still
produces a record.

### TEL-03 — A blank parent operation ID is preserved verbatim

`StartOperation` normalizes a null-or-whitespace `operationId` into a generated
32-character GUID, but assigns `parentOperationId` directly
([`TelemetryPipeline.cs:49-52`](../../../../src/Telemetry/NekoLib.Telemetry/TelemetryPipeline.cs)).

Observed: a parent of `"   "` was retained as `"   "`; a blank `operationId`
became a generated ID.

**Risk:** `TelemetryOperation.ParentOperationId` is `string?`, and both the
runtime scenario and any correlation consumer test it against `null` to decide
whether an operation is a root. A whitespace parent reads as a real parent and
produces a broken correlation chain that points nowhere. The inconsistency is
also internal: the same method normalizes one identifier and not the other, and
Core's `LogEntry` already normalizes a blank category to `null`.

**Recommended disposition:** normalize a null-or-whitespace parent to `null`.
This is a small behavioral correction with no signature change.

### TEL-04 — `Complete` is synchronous through sink fanout, and a slow sink applies backpressure

`Record` holds `_dispatchGate` across both retention and the entire sink loop
([`TelemetryPipeline.cs:71-88`](../../../../src/Telemetry/NekoLib.Telemetry/TelemetryPipeline.cs)),
and `Complete` calls it before returning. There is no budget, queue, or timeout
anywhere — unlike Logging, which has a bounded `Flush`.

Observed with one deliberately blocking sink: a second operation completed on
another thread reported `IsCompleted == true`, its `Complete` call had **not**
returned, and it was **not** yet retained. Both unblocked as soon as the sink
was released.

**Risk:** one slow or blocking sink stalls every completing thread and delays
retention of every later operation. Since Navigation completes operations on
the navigation lifecycle path, a badly written sink becomes a UI stall.

**Recommended disposition:** retain the synchronous model and document it
plainly, including the explicit requirement that a sink must return promptly.
Synchronous dispatch is what produces the ordering guarantees in TEL-07 and the
crash-time completeness the module exists for; a background queue would trade
them away for a problem no consumer has reported.

### TEL-05 — `IsCompleted` turns true before the operation is observable

`Complete` sets `_completed` under the operation lock and calls `Record`
afterwards, outside that lock. On the completing thread the sequence is
strictly ordered — `Complete` returns only after retention and fanout — but
another thread can observe `IsCompleted == true` for an operation that is not
yet in any snapshot. TEL-04's blocking probe widens that window arbitrarily.

**Recommended disposition:** document that `IsCompleted` means "the terminal was
accepted, and no later `Complete` will win", not "recorded and dispatched". Do
not move `Record` inside the operation lock: that would hold a per-operation
lock across third-party sink code and invite lock-order problems.

### TEL-06 — Retention precedes sink fanout, so snapshots are never blocked by sinks

Retention happens inside the dispatch gate but **before** the sink loop, and
`GetRecentOperations` takes only `_recentGate`.

Observed: a sink calling `GetRecentOperations` from inside its own `Write`
already saw the operation being dispatched.

**Recommended disposition:** retain and promote to a stated guarantee. This is
what keeps `CrashHandler` able to collect a telemetry snapshot under its
contributor budget while a sink is slow, and it is not accidental.

### TEL-07 — Ordering and first-completion-wins are strong and worth stating

The dispatch gate serializes `Record`, so retention order, sink order, and
cross-thread completion order are one order.

Observed:

- 8 threads completing 4,000 operations: both sinks saw the identical sequence,
  and the retained snapshot order matched that sequence element for element;
- 200 rounds of two threads racing to `Complete` the same operation: exactly one
  record every time, with no round producing zero or two.

**Recommended disposition:** retain and document: every sink observes one
identical order, retention order equals that order, and the first completion
wins under contention. Sink failures are isolated
([`TelemetryPipeline.cs:84-85`](../../../../src/Telemetry/NekoLib.Telemetry/TelemetryPipeline.cs))
and the existing focused test covers that.

### TEL-08 — Retained order is dispatch order and is not derivable from the models

`TelemetryOperation` carries `StartedUtc` — a wall-clock reading taken when the
scope is constructed — and `Duration`, a monotonic `Stopwatch` measurement.
There is no completion timestamp. `StartedUtc + Duration` therefore mixes two
clocks and is not a reliable completion time; a wall-clock adjustment shifts
`StartedUtc` relative to every `Duration`. Operations also complete in an order
unrelated to their start order.

**Recommended disposition:** document precisely that the snapshot's sequence is
the authority for completion order and that `StartedUtc` is a wall-clock
annotation, not an ordering key. **Do not** add a completion timestamp:
`TelemetryOperation` is a frozen Core type, and this is an observation about its
accepted shape, not a defect requiring a Core change. Never claim a stronger
clock guarantee than `Stopwatch` plus one `DateTime.UtcNow` reading provides.

### TEL-09 — Sink reentrancy permits unbounded recursion

`lock` is reentrant for the same thread, so a sink that starts and completes an
operation on the same pipeline from inside `Write` re-enters the dispatch gate
rather than deadlocking. Observed: a self-limiting sink reached depth 3; an
unconditional one would recurse until the stack overflows.

Lock ordering is otherwise sound: `Record` takes `_dispatchGate` then
`_recentGate`, and `GetRecentOperations` takes only `_recentGate`, so there is
no inversion and a sink may safely read snapshots.

**Recommended disposition:** document the rule — a sink may read snapshots but
must not start or complete operations on the pipeline that is dispatching to it.
Do not add reentrancy detection: it costs per-dispatch state to guard against a
caller defect the documentation can name.

### TEL-10 — Checkpoints are unbounded per operation; abandoned operations do not leak

`RecentOperationCapacity` bounds completed operations only. The per-operation
checkpoint list has no bound. Observed: 50,000 checkpoints on one operation were
all retained and all copied into the completed model.

The complementary fact is reassuring and worth stating: the pipeline never holds
a reference to a live scope, so an abandoned operation is simply collected. The
runtime scenario confirms that 200 abandoned scopes produce no record, no sink
write, and no error, including after a forced collection.

**Recommended disposition:** document both. Do not bound checkpoints — silently
dropping them would repeat the data-loss problem of TEL-02 in a different place —
and do not invent an implicit terminal outcome, `IDisposable`, or a finalizer for
abandoned operations. An application that needs abandonment detected must
arrange it itself.

### TEL-11 — A checkpoint after completion is ignored and returns the final duration

`Checkpoint` returns `_watch.Elapsed` without recording when the operation is
already complete
([`TelemetryPipeline.cs:146-157`](../../../../src/Telemetry/NekoLib.Telemetry/TelemetryPipeline.cs)).
Because the stopwatch is stopped, that value is exactly the operation's final
duration. Observed: one checkpoint recorded, and the late call returned the same
value as `Duration`.

**Recommended disposition:** retain — not throwing is correct for telemetry —
and document both halves: the checkpoint is dropped, and the returned value is
the total duration rather than an elapsed-at-checkpoint reading.

### TEL-12 — Dimension merge semantics are unstated

All dictionaries use `StringComparer.Ordinal`, so keys are case-sensitive.
Initial dimensions are captured at `StartOperation`; terminal dimensions are
merged at `Complete` with assignment, so **terminal values win** on collision
while initial-only keys survive. A custom dictionary that enumerates the same
key twice takes the last value without throwing.

Observed: a `k` present in both resolved to the terminal value, an initial-only
key survived, and a duplicate-yielding dictionary produced the second value.

**Recommended disposition:** retain and document. The precedence rule is not
guessable from the signature, and it is the behavior Navigation relies on when
it enriches an operation at completion.

### TEL-13 — Measurements are permissive

No validation rejects `NaN`, infinities, or negative values. Observed: all three
were accepted and round-tripped verbatim.

**Recommended disposition:** retain as a caller assertion and document it. This
matches the accepted Core stance that counters, durations, timestamps, and
dimension values supplied to public evidence models are caller-owned. Validating
here would break custom producers without improving the ownership boundary, and
telemetry that throws on a bad number is worse than telemetry that records one.

### TEL-14 — Options are read once, and validation names the option property

`RecentOperationCapacity` is copied into a readonly field; observed capacity
stayed at 2 after the options object was mutated to 100. `RecentOperationCapacity < 1`
throws with `ParamName` `'RecentOperationCapacity'` rather than `'options'`,
matching the convention explicitly retained in F1-LOG.

**Recommended disposition:** retain both, freeze the `1024` default as part of
the supported contract, and cover them with regressions.

### TEL-15 — Telemetry has no current technical reference

Telemetry contracts are currently split between a one-row entry in the root
[`README.md`](../../../../README.md) and the *Core* contract descriptions in
[`src/Core/NekoLib.Core/README.md`](../../../../src/Core/NekoLib.Core/README.md). Core
legitimately owns the interfaces; nothing owns the concrete pipeline's ordering,
backpressure, reentrancy, retention, dimension-merge, time, and abandonment
semantics. Core, Data, Logging, HTTP, and Navigation each have a module README;
Telemetry does not, and most dispositions above end in "document this".

**Recommended disposition:** add
`src/Telemetry/NekoLib.Telemetry/README.md` as the module's current technical
reference and register it in [`docs/README.md`](../../../README.md) and the
`AGENTS.md` routing table, exactly as F1-LOG did. Historical audits must not
become the live contract.

### TEL-16 — Automated coverage is thin where the findings are

The six focused tests cover correlation identifiers, checkpoint monotonicity,
double completion, dimension and measurement round-trip, bounded retention, and
sink-failure isolation. Not covered by any automated test:

- sink-array aliasing (TEL-01);
- the destroyed-operation path (TEL-02);
- blank parent normalization (TEL-03);
- null sink elements, and blank/null `module` and `name` validation;
- `RecentOperationCapacity` validation and the frozen default (TEL-14);
- `GetRecentOperations` non-positive and above-capacity bounds;
- concurrent completion under contention (TEL-07);
- cross-thread ordering agreement between sinks and retention (TEL-07);
- options read-once (TEL-14);
- checkpoint after completion (TEL-11);
- terminal-versus-initial dimension precedence (TEL-12);
- retention-before-fanout visibility (TEL-06).

Several are covered by the manual Observability scenario, which is real evidence
but is not run by `dotnet test` and was not run for this review.
`tests/README.md` classifies `NekoLib.Telemetry.Tests/Unit` as pure unit scope
with no external prerequisites; that classification stays correct.

## Target parity

Both manifests are byte-identical, no source file uses conditional compilation,
and no behavior differs by target. One cosmetic asymmetry exists: the csproj
adds `NoWarn 1591` on `net481` only. Because `GenerateDocumentationFile` is not
set, CS1591 cannot fire on either target, so the suppression is inert — the same
harmless inconsistency recorded for Logging. **Recommended disposition:** leave
it alone in F1-TEL; it changes no compiled surface.

## Facade, provider, and pipeline questions

Each of the following was considered explicitly and is **not** recommended:

- **a static telemetry facade or global provider** — Core deliberately has no
  process-global telemetry provider, and that is an accepted F1-CORE decision.
  Observed use is plural: the Observability scenario constructs a separate
  pipeline per check, and FarmDatabase composes one per application.
- **a registry or named-pipeline lookup** — no consumer resolves a pipeline by
  name; every one is handed an `ITelemetry` by its composition root.
- **an asynchronous or queued dispatch pipeline** — it would forfeit the
  ordering guarantees in TEL-07, the snapshot-before-fanout guarantee in TEL-06,
  and crash-time completeness, to solve a backpressure problem that documentation
  and a well-behaved sink already address.
- **a persistent store, aggregation layer, or metrics subsystem** — v1
  deliberately keeps raw completed operations in bounded memory. Aggregation is a
  different product with different retention, cardinality, and export concerns.
- **`IDisposable` or a finalizer on the pipeline or the operation** — the
  pipeline owns nothing disposable, and adding a lifecycle contract to
  `ITelemetryOperation` would be a Core change plus an implicit terminal outcome
  the brief explicitly excludes.
- **a Logging-style `DisposeSinks` option** — Telemetry sinks are consumer-owned
  and the pipeline has no disposal step to hang it on.
- **any new cross-module dependency** — Telemetry stays a leaf on Core.
- **symmetry with Logging for its own sake** — the two modules legitimately
  differ: Logging buffers in sinks and therefore needs flush and disposal
  ownership; Telemetry does not.

## Likely migration cost

| Recommended disposition | Source impact | Binary / behavioral impact |
|---|---|---|
| TEL-01 copy the sink array | None. | No manifest diff. Code that deliberately mutated a shared array to re-target a live pipeline stops working; no such consumer exists. |
| TEL-02 copy terminal payloads before committing state | None. | No manifest diff. `Complete` still throws for malformed caller data, but the operation stays completable instead of being destroyed. |
| TEL-03 normalize a blank parent to `null` | None. | No manifest diff. A caller that passed whitespace and then compared `ParentOperationId` to `""` would see `null`; that comparison was already broken correlation. |
| TEL-04 – TEL-14 documentation and regressions | None. | None. Clarifies existing ownership, ordering, backpressure, reentrancy, time, dimension, measurement, and retention behavior. |
| TEL-15 module README + registration | None. | None. |
| TEL-16 focused regressions | None. | None. Would have caught TEL-01, TEL-02, and TEL-03. |

Every recommended change is confined to method bodies. No public type, member,
signature, nullability annotation, default value, namespace, target, or project
reference changes, so **no API manifest update is expected**. If implementation
nevertheless produces a manifest diff, that is a signal to stop and re-review,
not to run `-UpdateBaseline`.

## Core-contract conflict

**None found.** Every finding lives in the concrete `NekoLib.Telemetry`
pipeline. TEL-08 is an observation about the shape of the frozen Core
`TelemetryOperation` model and is explicitly **not** proposed as a Core change;
it is resolved by documenting the ordering authority rather than by adding a
completion timestamp. No accepted Core semantic — composition-root ownership,
absence of a global provider, explicit checkpoint/terminal lifecycle,
first-completion-wins, non-terminal abandonment, the sink extension seam,
bounded chronological snapshots, defensive outer collections, shallow dimension
values, or `NullTelemetry` inertness — is contradicted by the recommendations
above.

## Package gate

Assembly bits **will** change if TEL-01, TEL-02, or TEL-03 is accepted. The
existing `1.0.0-local.17` evidence could not then be reused for the accepted
work, and the package gate must be left explicitly pending for Codex: a new
immutable coordinated family version from a clean implementation commit,
PackageReference consumer probes, and provenance/hash recording.

If only the documentation dispositions (TEL-04 through TEL-14, plus TEL-15 and
TEL-16) are accepted, no assembly bit changes and the existing `local.17`
evidence remains sufficient.

No recommended disposition affects the Observability runtime scenario source: it
constructs no aliased sink array, supplies no malformed dimension dictionary, and
passes no blank parent. It should still be rebuilt on both targets if the
behavioral corrections are accepted, but must not be launched without explicit
authorization and an agreed worst-case duration.

## Rejected alternatives

- **Absorbing the exception in `Complete` instead of reordering the copies** —
  rejected. Swallowing a malformed dimension dictionary would hide a caller
  defect and record a partially populated operation; reordering keeps the
  diagnostic while removing the state corruption.
- **Recording the operation with the pre-terminal dimensions when the copy
  fails** — rejected. It invents a terminal payload the caller never
  successfully supplied.
- **Validating dimension keys eagerly at `StartOperation` and `Complete`** —
  rejected as redundant: the `Dictionary` copy already rejects a null key, and
  the only real problem is when that rejection happens relative to state
  mutation.
- **Bounding checkpoints per operation** — rejected. Silent truncation is the
  same class of data loss as TEL-02, and no consumer produces unbounded
  checkpoints.
- **An implicit terminal outcome, `IDisposable`, or a finalizer for abandoned
  operations** — rejected, and explicitly out of scope. A finalizer-driven
  terminal would record an outcome the caller never chose, at a nondeterministic
  time, on the finalizer thread.
- **Validating measurements against `NaN`/infinity/negative** — rejected;
  caller-owned assertions, consistent with the accepted Core stance.
- **Adding a completion timestamp to the retained model** — rejected; it
  requires changing a frozen Core type, and the snapshot sequence already
  answers the ordering question.
- **Reentrancy detection in the dispatch path** — rejected; per-dispatch state
  to guard a caller defect that documentation can name.
- **Moving `Record` inside the operation lock to close the TEL-05 window** —
  rejected; it would hold a per-operation lock across third-party sink code.
- **Case-insensitive dimension keys** — rejected; `StringComparer.Ordinal` is
  used consistently across Core and both capability pipelines, and changing it
  would silently merge distinct keys.
- **A facade, global provider, registry, async queue, persistent store,
  aggregation subsystem, or new cross-module dependency** — rejected; see the
  section above.

## Proposed implementation block after acceptance

One narrow F1-TEL implementation should:

1. copy the sink array and drop null elements at construction (TEL-01);
2. materialize terminal dimensions and measurements before committing
   completion state (TEL-02);
3. normalize a null-or-whitespace parent operation ID to `null` (TEL-03);
4. add focused dual-target regressions for those three corrections plus the
   coverage gaps listed in TEL-16;
5. add `src/Telemetry/NekoLib.Telemetry/README.md`, register it in
   [`docs/README.md`](../../../README.md) and the `AGENTS.md` routing table, and
   reconcile the root `README.md` Telemetry entry (TEL-04 through TEL-15);
6. update `CHANGELOG.md` with the behavioral corrections, and add
   `docs/migrations/f1-telemetry.md` only if the accepted corrections actually
   require consumer action;
7. promote only the accepted dispositions to `TODO.md`, run the focused tests on
   both targets, run `eng/verify-public-api.ps1 -PackageId NekoLib.Telemetry`
   expecting **no** baseline change, run the full solution build and test, run
   `eng/verify-docs.ps1`, apply the repository warning-identity policy, and run
   `git diff --check`;
8. build the Observability runtime scenario on both targets without launching
   it, and leave the package gate explicitly pending for Codex.

## Review validation

Validation is recorded by evidence class so one gate is not mistaken for
another:

| Evidence class | Review result | Claim and limit |
|---|---|---|
| Focused tests | `dotnet test tests\NekoLib.Telemetry.Tests\Unit\NekoLib.Telemetry.Tests.Unit.csproj -c Release`: 6/6 passed on `net481` and 6/6 on `net9.0`, 0 failures and 0 skips, exit code 0 on both. | Confirms the stated baseline. Covers only the six existing cases; see TEL-16. |
| Build | `eng/verify-public-api.ps1 -PackageId NekoLib.Telemetry` built both targets with 0 warnings and 0 errors. | Compilation only. |
| Compiled API comparison | Both accepted manifests matched; 2/2 baselines verified, none updated. Both files are byte-identical with SHA-256 `CFC823DA2CAC08C93CE27B27261416F2618830932C8D6DAA9F985E20275DCC86`. | Current assembly surface versus the accepted candidate manifests. |
| Behavioral probe | Two scratch `net9.0` console programs, outside the repository, referencing the Release-built Core and Telemetry assemblies, confirmed TEL-01 (aliased array re-targeted dispatch), TEL-02 (throw left `IsCompleted` true, zero retained, retry inert), TEL-03 (`"   "` parent preserved), TEL-06 (snapshot already populated inside a sink's `Write`), TEL-07 (4,000 operations across 8 threads: both sinks agreed and retention matched dispatch order; 200 concurrent completion races each produced exactly one record), TEL-09 (reentrant depth 3), TEL-10 (50,000 checkpoints retained), TEL-11 (late checkpoint dropped, returned the final duration), TEL-12 (terminal precedence, initial-only survival, duplicate-key last-wins), TEL-13 (`NaN`, `+∞`, negative accepted), TEL-14 (capacity held after option mutation; `ParamName`), and TEL-04/TEL-05 (a blocking sink left a later operation `IsCompleted` but unretained with `Complete` still blocked). | Deterministic observation of current behavior on a single machine, `net9.0` only. Not a regression suite and not `net481` evidence. |
| Documentation | Not run for the review body; `eng/verify-docs.ps1` applies to the artifact and index registration. | See the commit record. |
| Runtime evidence | Not run. The Observability, FarmDatabase, and every other runtime scenario were not executed, and no scenario was built. | No real or long-running runtime claim is made. |
| Package evidence | Not run. No package was created, modified, or consumed. | `local.17` remains prior evidence from `dd0cfb8`; it is not evidence for any proposed change. |

These review gates establish the current candidate baseline and the observed
defects only. They do not validate any accepted future change.

## Residual validation limits

- The behavioral probes ran on `net9.0` only. `net481` behavior is inferred from
  identical source with no conditional compilation and from byte-identical
  compiled manifests; it was not executed.
- The TEL-07 ordering evidence is one run of 4,000 operations across 8 threads
  and 200 completion races on one machine. It is strong evidence that the
  dispatch gate serializes correctly, not a proof of absence of a rare
  interleaving.
- TEL-04 and TEL-05 were observed with an artificially blocking sink. The width
  of the window in a real application depends entirely on sink behavior and was
  not measured.
- TEL-10's unbounded checkpoint growth is a code fact plus one 50,000-checkpoint
  observation; no long-running memory measurement was taken.
- The abandoned-operation non-leak is taken from source and from the
  Observability scenario's recorded check; it was not independently re-measured
  here.
- No cross-process, persistence, or export behavior exists in this module, so
  none was evaluated.

## Decision gate

Nothing in this review authorizes product, test, API-baseline, changelog,
migration, roadmap, or package changes. F1-TEL must stop here until the user
explicitly accepts, modifies, or rejects the recommended dispositions —
especially the behavioral corrections TEL-01, TEL-02, and TEL-03, and the new
module reference in TEL-15.

## Reconciliation — 2026-08-17

The user accepted the recommended dispositions without modification. The
implementation is confined to method bodies and documentation:

- **TEL-01** — the constructor copies the sink array and drops null elements
  once, so `Record` no longer needs a per-dispatch null check.
- **TEL-02** — `Complete` materializes the terminal dimensions and measurements
  before setting `_completed` and stopping the stopwatch. The exception still
  surfaces to the caller; the operation now survives it and a corrected retry
  records normally. One deliberate consequence: because the stopwatch is no
  longer stopped before the copies, `Duration` now includes the cost of copying
  the caller's terminal payload — normally sub-microsecond. Stopping it earlier
  was rejected, because a failed attempt would then freeze the duration of an
  operation that is still in flight.
- **TEL-03** — `StartOperation` normalizes a null-or-whitespace
  `parentOperationId` to `null`.
- **TEL-04 to TEL-14** — retained and documented, with XML summaries on the
  contract-significant members and the full contract in the new module
  reference.
- **TEL-15** — [`src/Telemetry/NekoLib.Telemetry/README.md`](../../../../src/Telemetry/NekoLib.Telemetry/README.md)
  is the module's current technical reference, registered in the documentation
  index and the `AGENTS.md` routing table.
- **TEL-16** — sixteen focused regressions were added, taking the suite from 6
  to 22 per target and covering every gap listed in that finding.

As the review predicted, **both accepted API manifests verified unchanged**; no
baseline was updated. The accepted work therefore carries no source or binary
compatibility break, only the behavioral corrections recorded in
[`CHANGELOG.md`](../../../../CHANGELOG.md) and
[`docs/modules/Telemetry/migrations/f1.md`](../migrations/f1.md).

One process note worth preserving: the first draft of the concurrency
regressions introduced two `xUnit1031` analyzer warnings by blocking on tasks
inside test methods. They were converted to `async` tests awaiting
`Task.WhenAll` before any validation was reported, so the accepted change adds
no new warning identity.

No repository consumer or runtime scenario source required migration. The
Observability `LongRunningRecovery` scenario compiles unchanged against the
corrected pipeline on both target families; it was built, not launched, and no
runtime scenario was executed.

The package gate remains **explicitly pending for Codex**. Telemetry assembly
bits changed, so `1.0.0-local.17` is prior evidence only and cannot be reused: a
new immutable coordinated family version from the implementation commit,
PackageReference consumer probes, and provenance/hash recording are still
required. No package was created, modified, or consumed by this work, and F1-TEL
is deliberately not marked complete.

The residual validation limits recorded above still stand, with one narrowing:
TEL-01, TEL-02, TEL-03, TEL-06, TEL-07, TEL-11, TEL-12, and TEL-14 now have
executable dual-target regressions, so the `net9.0`-only probe limitation no
longer applies to them. TEL-04, TEL-05, TEL-09, TEL-10, and TEL-13 remain
documented behavior backed by probe observation rather than by a regression.

## Package reconciliation — 2026-08-17

An independent review of the accepted implementation found no additional
product defect or contract mismatch. The final Telemetry suite passed 22/22 on
`net481` and 22/22 on `net9.0`; both compiled API manifests remained unchanged.
The canonical clean package flow passed the full solution's 1,384/1,384 tests
with no failures or skips, built with the existing 515-warning baseline and no
errors, and preserved a clean tracked worktree.

The canonical package flow created coordinated immutable family version
`1.0.0-local.18` from
`518c078abc9bd9b340fbb7200470de47cde93452`.
`NekoLib.Telemetry.1.0.0-local.18.nupkg` contains `net481` and `net9.0`
assemblies, declares `NekoLib.Core` at the same version, records that source
commit in its NuGet metadata, and has SHA-256
`8B3DDABA5B16A91D0518258B8246022688E3E8E042BF03BC079A7D4AFE9BA185`.

PackageReference-only WinForms and WPF consumers restored, built, and ran on
both target families with zero consumer warnings. The multitarget consumer,
package structure, Watchdog Host payload, deployment opt-out, stale-payload
replacement, publish, and clean probes also passed. This is package and
package-consumer evidence, not a long-running application scenario; none was
launched or required. F1-TEL is complete, and this review is historical.
