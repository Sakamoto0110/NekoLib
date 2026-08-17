# NekoLib.Logging

**Kind:** reference

**Lifecycle:** current

**Subject:** synchronous logging pipeline composition, ownership, ordering,
snapshots, flush and disposal contracts, and the shipped sinks

`NekoLib.Logging` is the concrete pipeline behind the Core logging contracts. It
targets `net481` and `net9.0` with one identical public surface, references only
[`NekoLib.Core`](../../Core/NekoLib.Core/README.md), and has no global logger,
provider, registry, or static facade.

Core owns the interfaces — `ILogger`, `ILogSink`, `IFlushableLogSink`,
`ILogFlusher`, `ILogSnapshotSource`, `LogEntry`, `LogLevel`, `NullLogger`. This
document owns what the concrete pipeline and the shipped sinks actually do.

## Composition and ownership

The composition root builds the logger and decides its lifetime. Feature modules
receive `ILogger` and never dispose it.

```csharp
var fileSink = new RollingFileLogSink(new RollingFileLogSinkOptions
{
    FilePath = Path.Combine(AppContext.BaseDirectory, "logs", "app.log"),
    MaximumFileBytes = 2 * 1024 * 1024,
    RetainedFileCount = 4
});

using var logger = new Logger(LogLevel.Info, fileSink);
logger.Info("Application started", category: "Startup");
```

`Logger` is sealed. The extension seam is `ILogSink` / `IFlushableLogSink`, not
inheritance.

Both constructors accept sinks as `params`. There is no sinks-only overload: a
call that configures nothing still passes the options position, as
`new Logger(null, first, second)`.

`LoggerOptions` and `RollingFileLogSinkOptions` are read once, at construction.
Mutating an options object afterwards cannot affect a live pipeline or sink. The
sink set is likewise copied at construction, so a caller that passes its own
`ILogSink[]` cannot re-target dispatch by swapping an element later. Null
elements are dropped; a null array is treated as no sinks.

### `LoggerOptions`

| Property | Default | Contract |
|---|---|---|
| `MinimumLevel` | `LogLevel.Info` | Entries below it are dropped before an entry is even constructed, so they reach neither sinks nor the snapshot. |
| `RecentEntryCapacity` | `1024` | Bound of the in-memory recent window. Must be at least 1; the queue is pre-allocated, so an absurd value fails at construction. |
| `DisposeSinks` | `true` | Whether disposing the logger also disposes its sinks. |

These defaults are part of the supported contract and are covered by
regressions.

## Delivery contract

Accepted entries are dispatched inline, on the calling thread, under one lock,
in registration order. That gives four guarantees:

- every sink sees every accepted entry, exactly once;
- all sinks observe one identical delivery order;
- each writer's own entries stay in that writer's order;
- a sink that throws from `Write` is isolated, and later sinks still receive the
  entry. Logging never breaks feature behavior, so write failures are absorbed
  rather than propagated.

Entries are stamped before the dispatch lock is taken. Under concurrent writers
`TimestampUtc` may therefore disagree with delivery order — it is a wall-clock
record, not an ordering key.

Because dispatch is synchronous, a slow sink slows its callers. That is
deliberate: it is what makes ordering, crash-time completeness, and the flush
contract meaningful. There is no background queue.

## Recent snapshot

`GetRecentEntries(maxEntries)` implements `ILogSnapshotSource`. It returns the
newest retained entries in chronological order, bounded by both `maxEntries` and
`RecentEntryCapacity`. A non-positive request returns an empty list. The result
is always a fresh collection and never aliases pipeline state.

The snapshot stays readable after disposal, so an incident collector can still
take a post-shutdown window.

## Flush

`Flush(timeout)` implements `ILogFlusher`. It is a bounded completion request,
not a cancellation:

- every flushable sink is attempted; a sink that fails does not stop the
  remaining ones, so an unrelated broken sink cannot prevent the file sink from
  being flushed;
- `false` means completion was not confirmed for at least one sink inside the
  budget;
- a sink that outlives the budget **keeps running**. It may therefore observe a
  later `Write` concurrently with its own `Flush`. A custom `IFlushableLogSink`
  must tolerate that overlap;
- the pipeline observes the failure of such an abandoned flush itself, so a slow
  sink is not reported through `TaskScheduler.UnobservedTaskException` — which
  `NekoLib.Diagnostics` would otherwise record as a process crash;
- a negative timeout throws `ArgumentOutOfRangeException`. That includes
  `Timeout.InfiniteTimeSpan`: a bounded request has no unbounded form;
- once the logger is disposed, `Flush` returns `true` immediately without
  touching the sinks, because disposal already performed the final flush.

## Disposal

`Dispose` stops accepting entries, then performs one final flush of every
flushable sink and disposes them when `DisposeSinks` is set. It is idempotent
and never throws; sink failures during flush and dispose are isolated.

Two points are easy to get wrong:

- **`DisposeSinks` defaults to `true`.** Constructing a logger transfers sink
  disposal ownership to it by default. Set it to `false` when a sink is shared
  between loggers or outlives one.
- **Borrowed sinks are still flushed.** `DisposeSinks = false` suppresses
  disposal, not the final flush. That is what lets two loggers share one file
  sink safely.

The final flush carries **no time budget**. A sink that blocks in `Flush` blocks
shutdown indefinitely. Call `Flush(budget)` first and treat `false` as
"persistence not confirmed" when shutdown must be bounded.

After disposal, `Log` is inert and `Flush` returns `true`; `GetRecentEntries`
still works.

## `DebugLogSink`

Writes the formatted entry to the process trace channel, which the default
listener forwards to the attached debugger and to the Windows debug output
stream. `Write(null)` throws `ArgumentNullException`.

It deliberately uses `Trace.WriteLine` and not `Debug.WriteLine`. The latter is
`[Conditional("DEBUG")]`, so it is removed from the Release assembly that ships
in the package, leaving a sink that silently discards everything. The project
sets `DefineTrace` explicitly to keep that from recurring, and the regression
asserts observable output from a Release build.

## `RollingFileLogSink`

Bounded file persistence. Each `Write` opens the file, appends one line, flushes
and closes, so the sink holds no handle between writes and is not `IDisposable`.

| Option | Default | Contract |
|---|---|---|
| `FilePath` | `""` | Required. Normalized with `Path.GetFullPath` at construction, so a **relative** path binds to the working directory in force at that moment. Prefer an absolute path under `AppContext.BaseDirectory`. The resolved value is exposed as `FilePath`. |
| `MaximumFileBytes` | 4 MiB | Rolling threshold. Must be at least 1024. |
| `RetainedFileCount` | `4` | Number of retained **archives**, excluding the live file. Must be at least 1. |
| `Encoding` | `UTF8Encoding(false)` | Must not be null. |

**Rotation.** The file rotates *before* the write that would exceed
`MaximumFileBytes`, so the live file stays under the limit. An empty file is
never rotated, so a single entry larger than the maximum is written whole rather
than looping. Neither bound can be switched off; suppress rotation with a very
large maximum instead.

**Archive naming and eviction.** Archives are `<path>.1` … `<path>.N`, newest
first. Rotation deletes `<path>.N`, shifts each archive up by one, and moves the
live file to `<path>.1`. With the default `RetainedFileCount = 4` a consumer ends
up with five files.

**Byte accounting.** The threshold counts the encoded line. It does not count a
preamble, so a BOM-bearing `Encoding` makes the threshold approximate by the
preamble length. The default encoding has no preamble and is therefore exact.

**Durability is level-dependent.** `Error` and `Fatal` force a disk flush;
lower levels rely on the managed flush and stream close, which survives process
death but not necessarily machine death.

**One process owns a log path.** The append stream uses `FileShare.Read`, so a
second process writing the same path fails with a sharing violation. That
failure — like a failed rotation — is absorbed by the pipeline, so the second
process logs nothing with no error surface. Give each process its own path.

**Same-process writers serialize.** Sinks whose normalized paths match share one
process-wide gate, so two sinks or two loggers can target one file safely. That
gate table is keyed case-insensitively, is scoped to the loaded assembly, and is
never pruned; it grows by one small entry per distinct path a process ever uses.

`Flush()` is a barrier over that gate — it waits for an in-progress write to
finish. Nothing is buffered between writes, so there is nothing else to flush.

## Writing a custom sink

Implement `ILogSink`, or `IFlushableLogSink` when the sink buffers. The pipeline
requires:

- `Write` is called under the pipeline lock, one entry at a time;
- `Flush` may run concurrently with `Write` after a flush budget expires, so a
  buffering sink must guard its own state;
- exceptions from `Write`, `Flush` and `Dispose` are isolated by the pipeline,
  but a sink that throws on every write silently loses everything — surface
  persistent failure through your own channel;
- `LogEntry.Message` and `Exception` can contain sensitive data. The sink owns
  redaction and truncation before persisting or transmitting.

## Validation

```powershell
dotnet test tests\NekoLib.Logging.Tests\Unit\NekoLib.Logging.Tests.Unit.csproj -c Release -f net481
dotnet test tests\NekoLib.Logging.Tests\Unit\NekoLib.Logging.Tests.Unit.csproj -c Release -f net9.0
.\eng\verify-public-api.ps1 -PackageId NekoLib.Logging
```

The rolling-file tests are integration-scoped even though they live in the Unit
project; see [`tests/README.md`](../../../tests/README.md). The manifests under
`eng/public-api/NekoLib.Logging/` are the compiled dual-target compatibility
oracle. Package-backed evidence requires the canonical immutable package flow
and PackageReference-only consumers.
