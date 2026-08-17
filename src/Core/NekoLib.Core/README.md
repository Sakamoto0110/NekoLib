# NekoLib.Core

**Kind:** reference

**Lifecycle:** current

**Subject:** shared capability contracts, ownership, snapshots, null objects,
and the opt-in Inspection provider

`NekoLib.Core` is the zero-dependency contract foundation for Logging,
Telemetry, Inspection, Diagnostics, Navigation, and Watchdog. It targets
`net481` and `net9.0` with one identical public surface. Core contains no
pipeline, persistence, serializer, platform hook, IPC behavior, or feature-
module implementation.

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

## Logging contracts

- `ILogger` accepts severity, message, optional exception, and optional
  category. `LoggerExtensions` supplies the six severity conveniences.
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
- **Migration/removal path:** every dependent module will review action use in
  its own future F1 block. Until then, modules must not add action producers;
  callers that deliberately use the experimental interface member must accept
  `NEKOEXP0001` locally. The member may evolve or be removed before the first
  stable family release with recorded migration guidance.

The marker does not unfreeze broad Inspection instrumentation and does not
classify the dependent `NekoLib.Inspection` action family; F1-INSP owns that
future decision.

## Validation

```powershell
dotnet test tests/NekoLib.Core.Tests/Unit/NekoLib.Core.Tests.Unit.csproj
.\eng\verify-public-api.ps1 -PackageId NekoLib.Core
```

The API manifests under `eng/public-api/NekoLib.Core/` are the compiled
dual-target compatibility oracle. Package-backed evidence requires the
canonical immutable package flow and PackageReference-only consumers.
