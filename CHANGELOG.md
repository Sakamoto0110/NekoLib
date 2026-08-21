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

- **NekoLib.Navigation — breaking, additive, and behavioral pre-stable
  candidate correction for the first `1.0.0` stable family release.** The
  intentional static facade remains, but `SwitchPage` now accepts
  `NavigationArgs?` directly and returns a call-scoped `NavigationResult` for
  success, denial, and redirect. Removed ineffective request-mode factories,
  transient aliases, forged public back requests, the nonfunctional
  `UseRegistered`, inert presentation/timeout-target metadata, two unused page
  interfaces, and `DefaultUserContext`. Registration rules now compose and
  descriptors copy their collections. Guard attributes honor `RedirectTo`
  uniformly and role/permission/session inputs are validated and copied.
  History is a top-first read-only snapshot surface; framework evidence can be
  subscribed to but no longer fabricated through public constructors, emitters,
  or sinks. Optional payload, state, result, event, and platform-factory
  nullability now matches runtime behavior. Lifecycle ordering, UI dispatch,
  navigation gate, guard bound, redirect correlation, rollback, caching,
  surfaces, passive Inspection, targets, and dependencies remain unchanged. See
  the [F1-NAV migration guide](docs/migrations/f1-navigation.md).
- **NekoLib.Watchdog.Host — breaking and behavioral pre-stable deployment
  correction for the first `1.0.0` stable family release.** The Host remains a
  separate framework-dependent tools/build package with no compile-time API,
  but deployment is now direct-only: the accidental `buildTransitive` asset was
  removed, so each executable must reference the Host package explicitly.
  Coordinated Host/library pairs now use internal protocol v1 with a required
  launch version, version check, and `attached:v1:<pid>:<token>` identity;
  stale or mismatched pairs fail clearly. Explicit working directories must
  exist before supervision. Fatal startup evidence moved from an unbounded
  relative sidecar file to a fail-soft per-user LocalApplicationData log with a
  256 KiB active bound, one backup, UTC timestamp, and process identity. The
  AnyCPU `net481` and framework-dependent x86/x64 .NET 9 payload roots, owned
  output directory, opt-out property, runtime requirements, and cooperative
  same-user security boundary remain. See the
  [F1-WDOG-HOST migration guide](docs/migrations/f1-watchdog-host.md).
- **NekoLib.Watchdog — breaking, additive, and behavioral pre-stable candidate
  correction for the first `1.0.0` stable family release.** Retained
  `WatchdogBootstrap` as the ordinary application entry and
  `WatchdogRuntime` as a deliberate advanced supervisor surface. Runtime
  construction now captures normalized configuration without mutating the
  caller's `WatchdogOptions`, copies the sink array, exposes the effective
  `PipeName`, and implements one terminal, race-safe start/wait/stop/dispose
  lifecycle. Removed the ineffective `Stop(bool)` distinction, unsupported
  update options, obsolete raw-log server, and public implementation helpers;
  internalized batching and Host-only protocol constants. Mutating
  `WatchdogController` operations now return whether the exact acknowledgement
  was accepted; notification remains fail-soft. `LogEvent.Meta` became nullable
  serializer-neutral `MetaJson`, and nullable event fields now match wire data.
  Added `WatchdogOptions.EnableHotkeys`, defaulting `true`, with observable
  registration failure when enabled. Shutdown now interrupts crash-loop
  cooldown, drains owned workers, disposes process handles, and resolves the
  system `taskkill.exe` explicitly. Status evidence distinguishes history
  eviction, event-queue drops, and publish failures; crash finalization records
  complete, partial, or failed outcomes internally. Targets, dependencies,
  deterministic target identity, current-user pipe policy, cooperative
  same-user security model, and separate Host packaging remain unchanged. See
  the [F1-WDOG migration guide](docs/migrations/f1-watchdog.md).
- **NekoLib.Pipes — breaking, additive, and behavioral pre-stable candidate
  correction for the first `1.0.0` stable family release.** Removed the no-op
  `IDisposable`/`IAsyncDisposable` surface from stateless `PipeClient`; each
  `SendAsync` still owns and closes its stream, so remove `using` around the
  client. Sealed `SimplePipeMetrics`; custom collectors implement
  `IPipeMetrics`. Added cross-target `ShutdownAsync` to `PipeServer`,
  `PipeEventHub`, and `PipeEventClient`, made their lifecycle terminal and
  race-safe, corrected modern async disposal, and added modern server async
  disposal. Constructors now capture and validate options instead of retaining
  live mutable configuration. Metrics callback exceptions are isolated from
  transport outcomes. Oversized events are rejected before enqueue without
  disconnecting subscribers or incrementing `Published`. `PipeEventClient`
  gained isolated `OnError` observation and now raises `OnDisconnected` only
  for an established connection. Added `PipeErrorCodes` for the four framework
  wire codes, and made in-flight `net481` connect observe cancellation. The
  target-specific `JToken?`/`JsonElement?` payload contract, `net481`
  Newtonsoft.Json dependency, access-policy defaults, bounded event policies,
  application-defined error codes, and application-owned authorization remain
  unchanged. See the [F1-PIPE migration guide](docs/migrations/f1-pipes.md).
- **NekoLib.Devices — breaking, additive, and behavioral pre-stable candidate
  correction for the first `1.0.0` stable family release.** Removed
  `Protocols.HardwareProtocol`, a public abstract class whose single `Template`
  property nothing read, wrote, or derived from, and which participated in no
  contract; implement `IHardwareProtocol` directly. Added the opt-in
  `HardwareEngine.CloseTransportOnNoResponse`, default off: a timed-out operation
  leaves the transport in an indeterminate receive state, and by default a late
  reply can still be returned as the next operation's successful response, which
  is now documented and testable. Added `HardwareResponse.Failure`, so a
  fail-soft response carries the real exception instead of only `ex.Message` in
  the same `Status` field a protocol uses for `"Ok"` — a disposed transport, a
  caller bug and a silent device were previously indistinguishable. The engine
  now hands the transport a copy of `IHardwareProtocol.PortConfig` and neither
  shipped transport writes the resolved endpoint back into a caller-owned config,
  so a single send no longer rewrites the protocol's own configuration; read
  `ICommTransport.PortName` or `PortInfo` instead. `SerialCommTransport` discards
  the port input buffer on open, matching the stream transports, and its
  `Dispose` now takes the transport gate so it cannot race an in-flight read.
  `Checksum.Sum` and `Checksum.Xor` both reject null with
  `ArgumentNullException`. `ReadLine`, `ReadExact` and `ReadAll` now declare the
  null they always returned on timeout, `ParseResponse` takes `byte[]?`, and
  `Log` is nullable across the contracts. See the
  [F1-DEV migration guide](docs/migrations/f1-devices.md).
- **NekoLib.Mvvm — behavioral and binary-breaking pre-stable candidate correction
  for the first `1.0.0` stable family release.** No type, member, signature,
  default value, namespace, target, or dependency was added or removed. The
  public nullability contract now matches `ICommand`,
  `INotifyPropertyChanged`, and the module's own behaviour: `CanExecute` and
  `Execute` take `object?`, both events and every optional delegate and
  `propertyName` parameter are nullable, and `RelayCommand` takes
  `Action<object?>`. The surface previously declared non-nullable exactly where
  `null` is the documented, default and correct value, so a nullable-enabled
  consumer was warned for `OnPropertyChanged(null)`, `Execute(null)` and an
  explicit null predicate; those are now clean, while a lambda dereferencing the
  command parameter without a null check gains a true-positive `CS8602`. The
  module itself went from 20 nullable warnings to 0. `OnPropertyChanged` is now
  `virtual`, giving the single funnel `SetProperty` routes through — the seam a
  WinForms view-model needs to marshal notifications to the UI thread. Adding
  `virtual` is classified binary-breaking by the repository's NAV-009(b) rule and
  requires recompiling an external assembly that derives from `ViewModelBase`;
  measured on both targets, an un-recompiled consumer still loads and runs. See
  the [F1-MVVM migration guide](docs/migrations/f1-mvvm.md).
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
  `Install()` and `Dispose()` now serialize their registry transition, so a race
  cannot register an already-disposed handler after disposal removed it.
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
