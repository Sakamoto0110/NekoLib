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

- **NekoLib.Http — breaking, additive, and behavioral pre-stable candidate
  correction for the first `1.0.0` stable family release.** Reduced the
  `HttpEndpoint` constructor from `protected` to `private protected`, removing it
  from both manifests: it advertised an extension point that never existed,
  because `CreateRequest` is `internal abstract` and an external subclass failed
  with `CS0534`. An unresolvable response charset no longer throws — .NET
  Framework ships the full code-page set and .NET does not, so a `windows-1252`
  response succeeded on `net481` and threw a bare `ArgumentException` out of
  `SendAsync` on `net9.0`, destroying the status, headers and body the module
  exists to preserve; it now falls back to UTF-8 and returns the response intact,
  and applications needing byte-accurate legacy decoding register
  `CodePagesEncodingProvider` themselves.
  `HttpResponseContentTooLargeException` gained `StatusCode`, `ReasonPhrase` and
  `Headers`, captured before the body is read, so an oversized `502` with
  `Retry-After` stays actionable. `HttpApiClientOptions` validation now reports
  one exception type — `ArgumentException` naming `options` — instead of
  `InvalidOperationException` and `ArgumentOutOfRangeException`. Sending an
  endpoint that is not the registered instance now says so, instead of claiming
  the name is unregistered. See the
  [F1-HTTP migration guide](docs/migrations/f1-http.md).
- **NekoLib.Diagnostics.Windows — behavioral pre-stable candidate correction for
  the first `1.0.0` stable family release.** No public type, member, signature,
  target, or dependency changed, and both accepted API manifests are unchanged;
  consumers need no source change. `WindowsCrash.HookWinForms()` now attempts the
  `Application.ThreadException` subscription independently of the application-wide
  unhandled-exception mode change. Setting that mode throws once a window exists
  on the thread, and both calls previously shared one `try` block, so an
  application that hooked after creating its shell silently got **no UI-thread
  crash reporting at all**; the subscription alone is sufficient on both targets.
  Call it before creating any window for deterministic behavior. The minidump
  writer now passes a NULL exception parameter when no native exception is in
  flight on the calling thread — `NekoLib.Diagnostics` runs the dump writer on its
  own contributor thread, so the previous structure labelled the dump with a
  bystander thread and a null exception context — and deletes the file it created
  when the native call does not succeed, instead of leaving an empty `crash.dmp`
  beside a bundle that reports no dump was written.
  `CrashSuppressor.Enable()` now merges into the current process error mode
  instead of replacing it, preserving flags set by the host. `UseMiniDump()` must
  be applied before **constructing** the handler; see the
  [F1-DIAG migration guide](docs/migrations/f1-diagnostics.md).
- **NekoLib.Diagnostics — breaking, additive, and behavioral pre-stable candidate
  correction for the first `1.0.0` stable family release.** Removed the obsolete
  `CrashHandlerOptions.NotifyWatchdog` gate, whose Watchdog-specific policy was
  already moved to composition in Phase E6 and whose stated preservation window
  closed with that phase; leave `ExternalNotifier` null instead. Added
  `CrashHandler.CrashBundleFailed` and `CrashBundleFailedEventArgs`, so an
  unattended application can observe that incident evidence was lost — previously
  a failed bundle was indistinguishable from a successful one because
  `CrashDetected` and the notifier still fired. `CrashHandlerOptions` values are
  now captured by the constructor and `TailFiles` is copied, so validation holds
  and a caller mutating its own options object cannot re-target a live handler;
  `WindowsCrash.UseMiniDump()` must therefore be applied before **constructing**
  the handler. `Dispose()` is now terminal and idempotent, `Install()` after
  disposal throws `ObjectDisposedException`, and disposing the last installed
  handler releases the `AppDomain` and `TaskScheduler` subscriptions instead of
  keeping them — and their `SetObserved()` behaviour — for the process lifetime.
  Contributors are abandoned after their budget plus a 50 ms settle margin, so a
  flusher or snapshot source that correctly consumes its whole budget reports its
  own result instead of being recorded as a hang. The three evidence limits are
  now enforced locally as well as passed to the source, a record whose
  `ToString()` throws no longer destroys its section, colliding tail file names
  are disambiguated and recorded rather than overwriting each other, and the
  crash-text block is redacted as one bounded batch instead of one thread per
  line. In `crash.txt`, `ThreadId` is now `ManagedThreadId`. See the
  [F1-DIAG migration guide](docs/migrations/f1-diagnostics.md).
- **NekoLib.Inspection - behavioral and experimental pre-stable candidate
  correction for the first `1.0.0` stable family release.** No public type,
  member, signature, nullability annotation, default value, namespace, target,
  or dependency was removed or changed. `RegisterAction`, `TryInvokeAction`,
  `ActionKeys`, and `InspectionRuntimeDiagnostics.ActionCount` now carry the
  exact `NEKOEXP0001` marker on both targets; deliberate callers gain `CS0618`
  and must opt into this in-process experiment narrowly. Valid passive APIs
  retain `module::key` output while rejecting blank or delimiter-ambiguous
  identities, and identity comparison is explicitly ordinal and case-sensitive.
  Provider invocation and key discovery now follow registration order. Repeated
  budgeted snapshots share one outstanding task per provider registration and
  observe late failures without adding cancellation. Invalid capacity now
  reports `Capacity`, and clear is inert after disposal while enabled empty
  clears still count. See the
  [F1-INSP migration guide](docs/migrations/f1-inspection.md).
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
