# NekoLib.Telemetry

**Kind:** reference

**Lifecycle:** current

**Subject:** operation timing pipeline composition, ownership, lifecycle,
dimension and measurement semantics, bounded retention, snapshots, and sink
dispatch

`NekoLib.Telemetry` is the concrete pipeline behind the Core telemetry
contracts. It targets `net481` and `net9.0` with one identical public surface,
references only [`NekoLib.Core`](../../Core/NekoLib.Core/README.md), and has no
global pipeline, provider, registry, or static facade.

Core owns the contracts — `ITelemetry`, `ITelemetryOperation`, `ITelemetrySink`,
`ITelemetrySnapshotSource`, `TelemetryCheckpoint`, `TelemetryOperation`,
`TelemetryOutcome`, `NullTelemetry`. This document owns what the concrete
pipeline does.

Version 1 keeps raw completed operations in bounded memory. It does not persist,
aggregate, export, or sample them.

## Composition and ownership

The composition root builds the pipeline and decides its lifetime. Feature
modules receive `ITelemetry`.

```csharp
var telemetry = new TelemetryPipeline(
    new TelemetryPipelineOptions { RecentOperationCapacity = 512 },
    myCustomSink);

var operation = telemetry.StartOperation("Catalog", "load", parentOperationId: requestId);
operation.Checkpoint("query_sent");
operation.Complete(
    TelemetryOutcome.Succeeded,
    new Dictionary<string, object> { ["rows"] = 42 },
    new Dictionary<string, double> { ["load.total_ms"] = 18.4 });
```

`TelemetryPipeline` is sealed. The extension seam is `ITelemetrySink`, not
inheritance.

`TelemetryPipelineOptions` is read once at construction; mutating it afterwards
cannot affect a live pipeline. The sink set is likewise copied at construction,
so a caller that passes its own `ITelemetrySink[]` cannot re-target dispatch by
swapping an element later. Null elements are dropped; a null array means no
sinks.

**The pipeline is deliberately not `IDisposable`.** It owns no handle, no buffer
that outlives a call, and no background worker, so there is nothing to flush and
no shutdown step. This is an intentional difference from
[`Logger`](../../../docs/modules/Logging/REFERENCE.md), which owns sink disposal
and a bounded flush because its sinks buffer.

| Option | Default | Contract |
|---|---|---|
| `RecentOperationCapacity` | `1024` | Bound of the retained completed-operation window. Must be at least 1; the queue is pre-allocated. |

The default is part of the supported contract and is covered by a regression.

## Operation lifecycle

The caller owns exactly one explicit terminal.

- `StartOperation` requires a non-blank `module` and `name`; either blank throws
  `ArgumentException`.
- A blank `operationId` is replaced by a generated 32-character identifier. A
  blank `parentOperationId` is normalized to `null`, so a root operation is
  always `ParentOperationId == null`.
- `Checkpoint` records a name plus the monotonic elapsed time and returns that
  elapsed value.
- `Complete` accepts the outcome, optional terminal dimensions, and optional
  measurements.
- **First completion wins.** A second `Complete` is ignored, so a duplicated call
  cannot inflate a measurement or produce a second record. This holds under
  concurrent completion from different threads.
- `IsCompleted` means "the terminal was accepted and no later `Complete` will
  win". It is not "recorded and dispatched": another thread can observe
  `IsCompleted == true` a moment before the operation appears in a snapshot.
- **A checkpoint taken after completion is ignored** and returns the operation's
  final duration rather than throwing.
- **An abandoned operation is simply never recorded.** `ITelemetryOperation` is
  not `IDisposable`, there is no finalizer, and no implicit outcome is invented.
  The pipeline keeps no reference to a live operation, so an abandoned one is
  collected normally. An application that needs abandonment detected must arrange
  that itself.
- Checkpoints are **not bounded per operation**. `RecentOperationCapacity`
  bounds completed operations only.

If a caller-supplied dimension or measurement dictionary is malformed — a null
key, or an enumerator that throws — `Complete` surfaces that exception and the
operation stays completable, so a corrected retry still produces a record.

## Time

`StartedUtc` is a single wall-clock reading taken when the operation starts.
`Duration` and every checkpoint elapsed value come from a monotonic `Stopwatch`.

The two are not interchangeable: `StartedUtc + Duration` mixes clocks and is not
a reliable completion time, because a wall-clock adjustment shifts `StartedUtc`
relative to every `Duration`. The retained model carries no completion timestamp.

**The snapshot's sequence is the authority for completion order.** Operations
also complete in an order unrelated to the order they started in.

## Dimensions and measurements

- All keys use `StringComparer.Ordinal`, so they are case-sensitive.
- Initial dimensions are copied at `StartOperation`; terminal dimensions are
  merged at `Complete`. **On a key collision the terminal value wins**, and
  initial-only keys survive.
- A custom dictionary that enumerates the same key twice keeps the last value
  without throwing.
- Dimension values stay shallow references. The pipeline does not clone,
  serialize, or redact application objects, so a mutable value can change after
  recording. Emit bounded diagnostic projections.
- Measurements are permissive: `NaN`, infinities, and negative values are
  recorded verbatim. Numeric validity is a caller assertion, consistent with the
  Core stance on caller-supplied evidence values. Telemetry that throws on a bad
  number is worse than telemetry that records one.
- Dimension text and values can contain sensitive data. Whoever persists or
  transmits them owns redaction.

## Retention and snapshots

`GetRecentOperations(maxOperations)` implements `ITelemetrySnapshotSource`:

- only **completed** operations are retained;
- the newest window is returned in completion order, bounded by both
  `maxOperations` and `RecentOperationCapacity`;
- a non-positive request returns an empty list;
- the result is a fresh collection over `TelemetryOperation` models that never
  change again — a retained model survives later production and eviction
  unchanged, and Core's defensive copies mean a caller's dictionary mutation
  cannot reach it;
- **retention happens before sink dispatch and takes a separate lock**, so a
  snapshot is never blocked by a slow sink. This is what lets
  `NekoLib.Diagnostics` collect telemetry evidence under its crash-time
  contributor budget.

## Sink dispatch

Dispatch is synchronous and inline: `Complete` retains the operation and then
writes it to every sink, in registration order, before returning.

That gives three guarantees:

- every sink observes every completed operation, exactly once;
- all sinks observe one identical order, which is also the retained order;
- a sink that throws is isolated, and later sinks still receive the operation.

It also has two consequences a sink author must respect:

- **A slow sink applies backpressure.** It blocks the completing thread and
  delays retention of every later completion. Navigation completes operations on
  the navigation lifecycle path, so a slow sink becomes a UI stall. Return
  promptly; do your own buffering if you need to.
- **A sink must not start or complete operations on the pipeline dispatching to
  it.** The dispatch lock is reentrant, so this does not deadlock — it recurses,
  and an unconditional producer recurses until the stack overflows. Reading
  snapshots from inside a sink is safe.

There is no flush or timeout, because nothing is buffered: once `Complete`
returns, every sink has already been given the operation.

## Validation

```powershell
dotnet test tests\NekoLib.Telemetry.Tests\Unit\NekoLib.Telemetry.Tests.Unit.csproj -c Release -f net481
dotnet test tests\NekoLib.Telemetry.Tests\Unit\NekoLib.Telemetry.Tests.Unit.csproj -c Release -f net9.0
.\eng\verify-public-api.ps1 -PackageId NekoLib.Telemetry
```

These tests are pure unit scope with no external prerequisites; see
[`tests/README.md`](../../../tests/README.md). The manifests under
`eng/public-api/NekoLib.Telemetry/` are the compiled dual-target compatibility
oracle. Package-backed evidence requires the canonical immutable package flow
and PackageReference-only consumers.
