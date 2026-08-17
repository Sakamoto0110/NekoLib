# Changelog

**Kind:** reference

**Lifecycle:** current

**Subject:** consumer-visible package, public API, compatibility, and migration
changes

This changelog follows the rules in
[`docs/public-api-release-policy.md`](docs/public-api-release-policy.md).
`TODO.md` owns open work; historical implementation and validation narratives
remain under `docs/history/`.

## Unreleased

### Public API

- **NekoLib.Telemetry — behavioral pre-stable candidate correction for the first
  `1.0.0` stable family release.** No public type, member, signature,
  nullability annotation, default value, namespace, target, or dependency
  changed, and both accepted API manifests are unchanged; consumers need no
  source change. `ITelemetryOperation.Complete` now materializes the caller's
  terminal dimensions and measurements before committing completion state: a
  malformed dictionary previously left the operation marked terminal but never
  retained, never dispatched to a sink, and unable to be completed again, which
  silently destroyed the record. The exception still surfaces; the operation now
  survives it. `TelemetryPipeline.StartOperation` now normalizes a
  null-or-whitespace `parentOperationId` to `null`, matching how a blank
  `operationId` is already replaced and restoring the documented contract that a
  root operation has no parent — a whitespace parent previously read as a real
  correlation link. `TelemetryPipeline` now copies the supplied sink array, so a
  caller mutating its own array can no longer re-target a live pipeline. See the
  [F1-TEL migration guide](docs/migrations/f1-telemetry.md).
- **NekoLib.Logging — behavioral pre-stable candidate correction for the first
  `1.0.0` stable family release.** No public type, member, signature,
  nullability annotation, default value, namespace, target, or dependency
  changed, and both accepted API manifests are unchanged; consumers need no
  source change. `DebugLogSink` now writes through `Trace.WriteLine` instead of
  the `[Conditional("DEBUG")]` `Debug.WriteLine`, restoring the documented
  contract that it writes entries to the debug channel — the call was removed
  from every Release-built, packaged assembly, so the shipped sink silently
  discarded everything. `DebugLogSink.Write(null)` now throws
  `ArgumentNullException`, matching `RollingFileLogSink` and the non-null
  `ILogSink.Write` annotation. `Logger.Flush` now isolates a thrown sink failure
  and continues to later sinks while budget remains, while budget exhaustion
  still stops further flush admission; observes the failure of a sink that
  outlived the budget, so a slow sink is no longer reported through
  `TaskScheduler.UnobservedTaskException` and recorded as a crash by
  `NekoLib.Diagnostics`; and is inert after completed disposal instead of
  flushing already disposed sinks. A concurrent bounded flush now waits for the
  final disposal flush or returns `false` when its budget expires. `Logger` now
  copies the supplied sink array, so a caller
  mutating its own array can no longer re-target a live pipeline. See the
  [F1-LOG migration guide](docs/migrations/f1-logging.md).
- **NekoLib.Core — behavioral and experimental pre-stable candidate correction
  for the first `1.0.0` stable family release.** `TelemetryCheckpoint`,
  `TelemetryOperation`, and `InspectionSnapshot` now defensively copy and wrap
  their outer collections while preserving shallow contained values;
  `IInspectionRecorder.RegisterAction` is explicitly experimental as
  `NEKOEXP0001`, with concrete action behavior and module adoption deferred to
  each module's future F1 review. No Core type, member, target, dependency, or
  null-object behavior was removed. See the
  [F1-CORE migration guide](docs/migrations/f1-core.md).
- **NekoLib.Data — breaking pre-stable candidate correction for the first
  `1.0.0` stable family release.** Moved `DatabaseGateway` from
  `NekoLib.Data.Internal.Gateway` to `NekoLib.Data.Gateway` without a shim;
  removed `IUniversalQueryGateway` and the redundant `Get<TTranslator,T>`,
  `Read`, and `StreamData` families; normalized concrete/interface and session
  overloads; exposed parameterized `ContainsData`; removed the unusable
  `IDqlStreamingGateway` and `Microsoft.Bcl.AsyncInterfaces` dependency from
  `net481`; internalized `DbDataReaderExtensions`; sealed concrete types whose
  extension seams are interfaces/composition; and propagated net9 DTO
  reflection metadata through the public interface and mapping paths. See the
  [F1-DATA migration guide](docs/migrations/f1-data.md).
- **NekoLib.Data — additive fluent DELETE surface with a fail-closed behavioral
  guard.** Added `QueryBuilder.DeleteFrom`, `AllowAllRowsDelete`, and matching
  `IDmlGateway`/`DatabaseGateway` builder overloads. Deletes without predicates
  fail by default unless the current statement explicitly opts into all rows;
  builder deletes participate in translation and raise `OnSqlGenerated` before
  dispatch. Raw string overloads remain supported. See the
  [F1-DATA migration guide](docs/migrations/f1-data.md).

### Release governance

- Activated F1 public API and release stability work. Added the coordinated
  SemVer, stability classification, deprecation, compatibility baseline, and
  migration policy. This changes no product assembly or package API.
- Added assembly-derived candidate API snapshots for all 15 library packages
  and both supported targets, plus a deterministic comparison command and the
  cross-target experimental marker rule. This changes no product assembly or
  package API.

## Entry format

Future consumer-visible entries must identify:

- the affected package or package family;
- whether the change is additive, behavioral, deprecated, experimental, or
  breaking;
- the intended release version;
- the replacement or migration steps when consumer action is required.

The immutable `1.0.0-local.*` artifacts are pre-stable package candidates, not
individual stable releases. Their build and runtime evidence remains in the
owning completion or scenario records rather than being duplicated here.
