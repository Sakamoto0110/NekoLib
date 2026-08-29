# NekoLib.Core

**Document ID:** CORE-REFERENCE

**Schema version:** 1

**Kind:** reference

**Lifecycle:** current

**Subject:** shared capability contracts, ownership, completion, snapshots, null objects, extension seams, and the opt-in Inspection provider

**Surface:** technical-reference

**Boundary:** core

**Authority role:** normative

**Mutation:** authored

**Indexing:** include

`NekoLib.Core` is the zero-dependency contract foundation for Logging,
Telemetry, Inspection, Diagnostics, Navigation, and Watchdog. It targets
`net481` and `net9.0` with one identical public surface. Core contains no
pipeline, persistence, serializer, platform hook, IPC behavior, or feature-
module implementation.

## Package and target boundary

The shipped package ID and assembly name are `NekoLib.Core`. The project is in
`NekoLib.sln`, enables nullable annotations, and compiles the same source for
`net481` and `net9.0`. The accepted public API manifests are byte-identical at
the current baseline: Core has no target-specific type, member, nullable
annotation, optional value, enum value, or experimental marker.

The project declares no authored `PackageReference` or `ProjectReference`.
Framework reference assemblies restored by the SDK for `net481` are build
tooling, not a Core package dependency. The repository-wide managed-library
build configuration generates `NekoLib.Core.xml` beside each target assembly
and includes those XML files beside the DLLs in a package.

Core is a contracts package, not a host or a dependency-injection layer. It
contains no concrete pipeline, persistence, serializer, IPC, network, platform
hook, background worker, automatic discovery, retry policy, redaction engine,
or feature-module implementation. Those behaviors belong to the concrete
package or application composition root that implements a Core interface.

## Ownership and composition

The composition root creates and owns concrete capability services. Feature
modules receive only the narrow contracts they use:

```csharp
ILogger logger = new Logger();
ITelemetry telemetry = new TelemetryPipeline();
IInspectionRecorder inspection = new InspectionRuntime();
```

Supplying an interface does not transfer disposal ownership to a feature
module. Concrete packages define their own disposal and sink-ownership options.
Core deliberately has no global Logging or Telemetry provider.

`Disposable.Empty` is one shared, stateless, idempotent no-op handle. Disposing
it releases no external resource and transfers or implies no ownership. It is
the disabled-path registration returned by `NullInspection`.

## Supported consumer implementation seams

Core's public interfaces are explicit in-process composition contracts. They do
not provide assembly scanning, keyed discovery, dependency injection, retries,
serialization, transport, authentication, or automatic lifetime management.
The composition root constructs an implementation and passes the narrowest
interface required by each producer or consumer.

| Contract | Supported consumer use | Required boundary |
|---|---|---|
| `ILogger` | Supply a custom logging pipeline when the concrete `Logger` is not suitable. | `Log` is synchronous; the implementation owns filtering, failure isolation, concurrency, retention, redaction, and disposal. |
| `ILogSink` | Add a destination to the supplied logging pipeline. | Consume each entry without mutating it; return promptly or accept synchronous backpressure; protect sensitive message and exception data. |
| `IFlushableLogSink` | Add synchronous flushing to a buffering sink. | `Flush` has no cancellation. With the supplied pipeline it may continue after a budget expires and overlap a later `Write`, so internal state must be synchronized. |
| `ILogFlusher` | Expose bounded pipeline-level completion to an owner such as Diagnostics. | `false` means unconfirmed within the budget, not cancellation. This capability is not implied by `ILogger`. |
| `ILogSnapshotSource` | Expose a read-only recent-entry window. | Return a non-null newest window in chronological order; a non-positive limit returns empty. This capability is not implied by `ILogger`. |
| `ITelemetry` | Replace the operation engine while retaining the Core producer contract. | Return a non-null caller-owned operation, define identity generation and validation, and document concurrency and dispatch. Core supplies no global pipeline or lifetime management. |
| `ITelemetryOperation` | Implement the caller-owned checkpoint and terminal lifecycle returned by `ITelemetry`. | Completion is explicit and never implied by disposal. Define duplicate completion, post-completion checkpoints, concurrency, and failure behavior. |
| `ITelemetrySink` | Export or aggregate completed operations from the supplied pipeline. | The supplied pipeline dispatches inline and in registration order; return promptly, avoid recursive production through the same pipeline, and redact before persistence or transport. |
| `ITelemetrySnapshotSource` | Expose a read-only completed-operation window. | Return a non-null newest window in completion order; this capability is not implied by `ITelemetry`. |
| `IInspectionRecorder` | Supply an alternate opt-in recorder and registration owner. | Never evaluate lazy payloads while disabled; return caller-owned unregistration handles; keep callbacks bounded and document concurrency. `RegisterAction` remains experimental. |
| `IInspectionSnapshotSource` | Give bounded evidence consumers a read-only Inspection view. | Return a non-null snapshot, allow partial state, and treat the timeout as a caller completion budget rather than delegate cancellation. Do not expose action invocation. |

For most applications the narrower sink/provider seams are preferable to
reimplementing a whole capability. The supplied behavior and ownership rules
remain documented by the concrete
[`NekoLib.Logging`](../Logging/REFERENCE.md),
[`NekoLib.Telemetry`](../Telemetry/REFERENCE.md), and
[`NekoLib.Inspection`](../Inspection/REFERENCE.md) packages.

A minimal custom telemetry sink is ordinary explicit composition:

```csharp
public sealed class FailureCountingSink : ITelemetrySink
{
    private long _failures;

    public long Failures => Interlocked.Read(ref _failures);

    public void Write(TelemetryOperation operation)
    {
        if (operation == null)
            throw new ArgumentNullException(nameof(operation));

        if (operation.Outcome == TelemetryOutcome.Failed)
            Interlocked.Increment(ref _failures);
    }
}

var sink = new FailureCountingSink();
ITelemetry telemetry = new TelemetryPipeline(
    sinks: new ITelemetrySink[] { sink });
```

The sink is supplied directly; Core does not discover it or decide its
lifetime. The same explicit-composition rule applies to custom logging sinks,
snapshot sources, loggers, telemetry engines, and Inspection recorders.

`LoggerExtensions`, the model and enum types, the three `Null*` singletons, and
`Disposable.Empty` are conveniences or data contracts, not plug-in seams.
`InspectionProvider` is one explicitly installed process-wide recorder slot,
not a general service or plug-in registry. State-provider, action, and payload
delegates are callbacks owned by an `IInspectionRecorder`; their presence does
not create discovery, isolation, authorization, or remote-execution semantics.

## Implementing the interfaces

### Logging implementations

A custom `ILogger` owns its filtering, entry construction, concurrency,
delivery, retention, failure policy, redaction, and shutdown behavior. The
`Log` call is synchronous: Core neither queues the call nor catches an
implementation exception. Producers can call the same logger concurrently, so
an implementation must either support that use or impose and document a
narrower composition rule. Implementing `ILogger` does not automatically make
the object an `ILogFlusher` or `ILogSnapshotSource`; expose those optional
capabilities only when their contracts are real.

An `ILogSink` consumes the supplied `LogEntry` without mutating it. Core does
not call a sink directly and promises no dispatch order of its own. When used
with the shipped `Logger`, `Write` is inline, serialized, and registration-
ordered; a thrown sink exception is isolated by that pipeline. A sink that
persists or transmits owns redaction, truncation, access control, durability,
and its own failure diagnostics.

`IFlushableLogSink.Flush` is synchronous and carries no timeout or cancellation
token. With the shipped pipeline a budgeted flush can return while sink work
continues, after which `Flush` may overlap a later `Write`; a buffering sink
must synchronize its state. A custom pipeline defines whether sink failures are
isolated and whether it owns sink disposal.

`ILogFlusher.Flush(timeout)` is a pipeline-level completion query. Return
`true` only when completion is confirmed inside the budget; `false` means
unconfirmed, not cancelled. Define negative-timeout validation, concurrency,
and post-disposal behavior. `ILogSnapshotSource.GetRecentEntries` returns a
non-null newest window in chronological order and returns empty for a
non-positive request; it must not expose mutable retention state.

### Telemetry implementations

A custom `ITelemetry` returns one non-null, caller-owned
`ITelemetryOperation`. It owns identifier generation, input validation,
concurrency, retention, sink dispatch, and failure isolation. Core has no
global telemetry slot, implicit shutdown, sampling, persistence, or operation
abandonment detector.

An `ITelemetryOperation` must keep the explicit lifecycle honest: checkpoints
are intermediate evidence and `Complete` is the only terminal. The interface
is intentionally not disposable. An implementation documents whether multiple
threads may use one operation, which completion wins, what a checkpoint after
completion does, how duplicate names behave, and what remains retryable after
an input or callback failure. The shipped pipeline is thread-safe per operation,
accepts only the first completion, ignores later completions, and returns the
final duration for a valid post-completion checkpoint.

`ITelemetrySink.Write` receives a structurally read-only completed model. Core
does not schedule or isolate that callback. The shipped pipeline retains first,
then dispatches inline in registration order and isolates ordinary sink
exceptions. A slow sink therefore applies backpressure to the completing
thread, and recursively completing telemetry through the same pipeline can
recurse without bound. Exporters should return promptly, buffer under their own
policy when needed, and redact before persistence or transport.

`ITelemetrySnapshotSource.GetRecentOperations` returns a non-null newest window
in completion order and an empty result for a non-positive request. The source
owns retention and synchronization. Implementing `ITelemetry` alone does not
imply that completed operations are retained or readable.

### Inspection implementations

An `IInspectionRecorder` is opt-in and feature-facing. `IsEnabled` is a
concurrent optimization hint, not a lease. A disabled implementation must not
evaluate a lazy `Record` payload. An enabled implementation owns validation,
ordering, retention, callback isolation, and concurrency. Producers must keep
payload factories bounded because an implementation may invoke them inline.

`RegisterStateProvider` returns a caller-owned unregistration handle; repeat
disposal is idempotent for the Core null object and the shipped runtime. An
implementation documents provider identity, ordering, duplicate registration,
concurrent capture, and whether a provider already admitted to a capture can
finish after unregistration. `RegisterAction` has the same explicit-
composition shape but remains experimental; its current signature does not
provide discovery, invocation, authorization, async work, cancellation,
timeout, or UI marshalling.

An `IInspectionSnapshotSource` is deliberately read-only and exposes no action
invocation. It returns a non-null snapshot, may return partial state, and treats
the timeout as a caller completion budget rather than cancellation of provider
code. A custom source owns input validation, provider ordering, markers or
error representation, synchronization, and failure isolation. Persistence and
transport consumers must be prepared for shallow, mutable, sensitive values.

## Logging contracts

- `ILogger` accepts severity, message, optional exception, and optional
  category. `LoggerExtensions` supplies the six severity conveniences.
- `LogLevel` has the stable ordered values `Trace = 0`, `Debug = 1`,
  `Info = 2`, `Warn = 3`, `Error = 4`, and `Fatal = 5`.
- `ILogSink` receives accepted `LogEntry` values. `IFlushableLogSink` adds a
  synchronous sink flush.
- `ILogFlusher` is the pipeline-level bounded completion request. A `false`
  result means completion was not confirmed within the caller budget; it does
  not promise cancellation of underlying sink work.
- `ILogSnapshotSource` returns a non-null newest window in chronological order.
- `LogEntry` is structurally read-only but deliberately retains the original
  `Exception` reference. Its text can contain sensitive data; persistence owns
  redaction and truncation.
- `NullLogger.Instance` drops writes, returns an empty snapshot, and reports
  flush completion.

## Telemetry contracts

`ITelemetry.StartOperation` starts one consumer-owned operation. The caller may
add checkpoints and must call `Complete` explicitly; `ITelemetryOperation` does
not imply a terminal through disposal. The supplied pipeline accepts only the
first completion and retains completed operations in a bounded recent window.

`TelemetryCheckpoint` and `TelemetryOperation` defensively copy and wrap their
outer lists and dictionaries. Mutating the source collections after
construction cannot change the published model. Values inside dimension
dictionaries remain shallow references: Core does not clone, serialize, or
redact application objects.

`ITelemetrySink` is the custom completed-operation extension seam.
`ITelemetrySnapshotSource` returns a non-null newest window in chronological
order. `NullTelemetry.Instance` returns one shared already-completed operation
and an empty snapshot without enumerating dimensions.

`TelemetryOutcome` preserves the caller-supplied terminal classification with
the stable values `Unknown = 0`, `Succeeded = 1`, `Failed = 2`, and
`Cancelled = 3`. Core does not infer an outcome from elapsed time, exceptions,
abandonment, or disposal.

## Inspection contracts

`IInspectionRecorder` is the feature-facing opt-in surface:

- `IsEnabled` allows producers to avoid optional work;
- `Record` accepts a lazy payload factory, which a disabled null recorder never
  invokes;
- `RegisterStateProvider` returns a caller-owned unregistration handle;
- `RegisterAction` exists only as the experimental contract described below.

`IInspectionSnapshotSource` is the separate read-only consumer surface. It has
no action invocation. `maxOperations` bounds the newest retained operation
window; a concrete provider may return partial state when its shared provider
budget expires. A timeout is a completion budget, not cancellation of a
third-party delegate.

`InspectionSnapshot` defensively copies and wraps its outer operations and
state collections. `InspectionOperation.Payload` and snapshot state values stay
shallow references. Producers should emit bounded diagnostic projections;
persistence consumers such as Diagnostics own safe formatting, isolation,
redaction, and truncation.

## Callback, concurrency, and failure boundaries

Core interfaces are synchronous unless their signature says otherwise; none
contains a task, cancellation token, retry policy, or scheduler. Core does not
wrap calls made through an arbitrary consumer implementation. An exception from
a custom `ILogger`, `ITelemetry`, `ITelemetryOperation`, snapshot source, or
recorder therefore follows that implementation's documented policy and can
reach its caller.

The concrete packages deliberately add narrower guarantees: the shipped
Logging and Telemetry pipelines isolate ordinary sink exceptions; the shipped
Inspection runtime converts payload and state-provider failures into bounded
evidence markers; Diagnostics isolates snapshot contributors under its own
budget. Those guarantees belong to the linked concrete references and do not
become requirements of every custom Core implementation merely because the
same interface is used.

Core itself isolates only behavior it owns. `InspectionOperation.ToString`
catches a throwing payload formatter and records its exception type marker;
`LoggerExtensions` rejects a null receiver; required model names throw where
documented; and `InspectionProvider.Install` rejects invalid or competing
recorders while rolling back a recorder that disables during installation.
The null objects avoid optional-observability side effects rather than
validating ignored input.

## Process-wide Inspection provider

`InspectionProvider.Current` is always non-null and defaults to
`NullInspection.Instance`. `Install` admits at most one enabled recorder and
returns an idempotent handle that conditionally restores the null recorder.
The handle unregisters the recorder but does not dispose it.

The slot is process-wide for the loaded Core assembly context. Ordinary users
of the supplied implementation should prefer `InspectionRuntime.EnableGlobal`,
whose disposal combines runtime cleanup with provider unregistration.
Navigation also accepts an explicit non-global `IInspectionRecorder`.

`NullInspection.Instance` is disabled, never invokes payload, state-provider,
or action delegates, returns `Disposable.Empty` registrations, and supplies an
empty snapshot.

## Data-model boundaries

- Members named `*Utc` are caller assertions; constructors do not rewrite
  `DateTime.Kind`.
- Required strings are non-null in the compiled contract. `LogEntry` preserves
  its compatibility behavior of normalizing a runtime-null message to empty
  and a blank category to null.
- Evidence counters, enum values, timestamps, durations, and sequences supplied
  to public model constructors remain caller-owned assertions.
- Read-only outer collections do not make contained application objects deeply
  immutable.

## Security and sensitive-data responsibilities

Core models can retain log messages, exception objects, dimension values,
measurement names, Inspection payloads, state values, module names, operation
names, and callback delegates. Core does not classify, redact, truncate,
encrypt, serialize, persist, transmit, or authorize any of them.

Producers should supply bounded diagnostic projections and avoid secrets or
live UI/domain graphs. A sink or evidence consumer that formats, persists, or
transmits values owns formatter isolation, size limits, redaction, access
control, retention, and delivery diagnostics. A structurally read-only outer
collection does not make a contained object safe or immutable. `ToString()` on
`LogEntry`, `InspectionOperation`, or an opaque value is not a sanitization
boundary.

## Experimental APIs

### NEKOEXP0001 — `IInspectionRecorder.RegisterAction`

- **Exact symbol:**
  `IInspectionRecorder.RegisterAction(string, string, Func<object?, object?>)`.
- **Targets:** `net481` and `net9.0`.
- **Supported entry point:** in-process registration with an explicitly supplied
  Inspection recorder.
- **Instability boundary:** authorization, action discovery/invocation,
  asynchronous work, cancellation, timeout, UI-thread marshalling, and
  module-specific adoption are not stable contracts.
- **Security boundary:** the delegate is not authorization and must not be
  exposed through IPC, reflection, or a remote control surface as privileged
  access.
- **Migration/removal path:** callers that deliberately use the experimental
  interface member must accept `NEKOEXP0001` locally. The stable deprecation
  window does not apply while the member remains experimental; promotion,
  incompatible evolution, or removal requires a reviewed API diff, a changelog
  entry, and migration guidance under the public API policy.

The marker survived the `1.0.0` stable-family declaration without becoming
stable. It does not unfreeze broad Inspection instrumentation. The dependent
Inspection runtime applies the same experiment identity to its concrete action
family; no feature module registers an action.

## Explicit non-goals

Core is not a logging framework, telemetry exporter, diagnostics runtime,
Inspection host, serializer, message bus, service locator, dependency-injection
container, plug-in loader, reflection discovery system, authorization layer,
remote-control surface, or test-control bypass. Convenience models and null
objects do not imply discovery seams, and `InspectionProvider` is not a pattern
to copy into Logging or Telemetry.

## Validation

```powershell
dotnet test tests/NekoLib.Core.Tests/Unit/NekoLib.Core.Tests.Unit.csproj
.\eng\verify-public-api.ps1 -PackageId NekoLib.Core
```

The API manifests under `eng/public-api/NekoLib.Core/` are the compiled
dual-target compatibility oracle. Package-backed evidence requires the
canonical immutable package flow and PackageReference-only consumers.

[`VALIDATION_REQUIREMENTS.md`](VALIDATION_REQUIREMENTS.md) owns the qualifying
evidence contract and [`VALIDATIONS.md`](VALIDATIONS.md) records what actually
ran, with its gaps. Core has no standalone runtime scenario because concrete
dispatch, persistence, transport, UI, and process behavior belongs to its
dependent implementations.

## Related surfaces

| Need | Owner |
|---|---|
| Identity, package, targets, API oracles, and evidence routes | [`MANIFEST.md`](MANIFEST.md) |
| Consumer introduction | [`README.md`](README.md) |
| Consumer-visible evolution | [`CHANGELOG.md`](CHANGELOG.md) |
| Chronology | [`HISTORY.md`](HISTORY.md) |
| Confirmed defects | [`ISSUES.md`](ISSUES.md) |
| Unconfirmed observations | [`FINDINGS.md`](FINDINGS.md) |
| Candidate-to-stable transition | [`migrations/f1.md`](migrations/f1.md) |
| Historical F1 review | [`audits/public-api-review-2026-08-17.md`](audits/public-api-review-2026-08-17.md) |
| Concrete Logging behavior | [`../Logging/REFERENCE.md`](../Logging/REFERENCE.md) |
| Concrete Telemetry behavior | [`../Telemetry/REFERENCE.md`](../Telemetry/REFERENCE.md) |
| Concrete Inspection behavior and action family | [`../Inspection/REFERENCE.md`](../Inspection/REFERENCE.md) |
| Inspection rollout and action freeze | [`ROADMAP.md`](../../../ROADMAP.md) |
