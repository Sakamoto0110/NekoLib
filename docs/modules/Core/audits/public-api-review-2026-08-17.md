# Core Public API Review — 2026-08-17

**Document ID:** CORE-AUDIT-PUBLIC-API-20260817

**Schema version:** 1

**Kind:** audit

**Lifecycle:** historical

**Subject:** F1-CORE compiled public surface, ownership, lifecycle, extension, null-object, snapshot, payload, and compatibility boundaries

**Surface:** audit

**Boundary:** core

**Authority role:** evidence

**Mutation:** snapshot

**Indexing:** include

**Status:** all dispositions implemented

**Reference date:** 2026-08-17

**Reference commit:** `0ad3840b29d749c25e157ae15db450bf82d17011`

**Original path:** `docs/audit/core-public-api-review-2026-08-17.md`

**Last reconciliation:** 2026-08-17

**Current state:** F1-CORE completed; see the [current Core reference](../REFERENCE.md) and [F1 history](../../../history/phase-f1-public-api-release-stability-2026-08-21.md)

## Baseline and authority

This review covers committed `HEAD` on branch
`phase-e/sqlserver-and-orchestration`. Before the review artifact was added, the
worktree and index were clean and the branch was nine commits ahead of its
matching remote branch. Nothing from this branch was pushed by the review.

The reviewed authority is the Core project, all of its source, the dual-target
Core tests, the assembly-derived manifests, the public API release policy, and
the current downstream source. Historical reviews supplied architectural
context only. The review changes no product source, API baseline, package,
changelog, migration guide, or live roadmap item.

The latest immutable package family remains `1.0.0-local.15`, built from
`89d1ed1bbc7b4a42533150e808e4a03f868aff30`. The only committed difference from
that package source to the reviewed commit is the Data fluent-delete completion
record in `TODO.md`; Core source and Core manifests did not change. That package
is prior package evidence, not validation of this review or of any future
accepted Core change.

## Scope

Included:

- all 24 compiled public Core type declarations and every public member family;
- ownership, disposal, operation completion, registration, and global-provider
  lifecycle;
- null-object and singleton semantics;
- data-model nullability, structural mutability, collection aliasing, opaque
  object values, exception references, and formatting;
- bounded recent reads, snapshot ordering, flush and provider time budgets,
  and partial-result behavior;
- identical `net481` and `net9.0` surface plus compatibility-significant
  attributes, optional values, enum values, sealed/static shapes, and interface
  inheritance;
- current use by Logging, Telemetry, Inspection, Diagnostics, Navigation, and
  Watchdog;
- transitive package reachability and likely migration cost.

Excluded:

- implementing any recommendation or updating an accepted API manifest;
- changing a dependent capability package during the review;
- new dependencies, serialization models, global logging or telemetry,
  privileged control, or broad Inspection instrumentation;
- runtime execution, package creation, package-consumer execution, commit, or
  push.

## Package, ownership, and lifecycle boundary

[`NekoLib.Core.csproj`](../../../../src/Core/NekoLib.Core/NekoLib.Core.csproj) targets
`net481;net9.0`, enables nullable annotations, and declares no `PackageReference`
or `ProjectReference`. The SDK's automatic .NET Framework reference-assembly
restore is tooling, not an authored Core package dependency. Core contains
contracts and small value/null helpers only; it has no concrete pipeline,
feature-module knowledge, persistence, IPC, platform hook, or serializer.

The ownership model is intentionally instance-first:

- feature modules receive `ILogger`, `ITelemetry`, or `IInspectionRecorder` and
  do not own or dispose those application-supplied services;
- logging and telemetry sinks are consumer extension points; their owning
  concrete pipelines decide dispatch and disposal policy;
- the caller that starts an `ITelemetryOperation` owns its one explicit terminal
  `Complete` call; the contract does not imply completion through `IDisposable`;
- state/action registration handles own only unregistration;
- `InspectionProvider.Install` owns the global slot registration, not the
  installed recorder's lifetime. Disposing its handle conditionally restores
  the null recorder but does not dispose the recorder;
- `InspectionRuntime.EnableGlobal` in the dependent Inspection package combines
  the registration and runtime lifetimes for ordinary consumers.

A new public-to-internal facade is not justified. Core itself is the shared
contract boundary, and its public interfaces are the customization seams used
by the concrete capability packages. A static Logging or Telemetry provider
would add global state and obscure consumer ownership.

## Compiled-surface inventory and recommended classification

The accepted `net481` and `net9.0` manifests are byte-identical and have the
same SHA-256,
`44ADEC4DDA2FE7B27D93CACDA7A88323B3085DB57C0E351C80FDF32F577F409A`.
Each contains 24 public type declarations. Both include the same nullable
metadata, optional parameter values, enum integral values, sealed/static
shapes, interface inheritance, extension-method receivers, singleton fields,
and assembly `RepositoryUrl` metadata. There is no target-specific Core API and
no current experimental or deprecation marker.

The following table classifies every type and, where the classification differs
inside a type, the exact member family.

| Family | Public type or member family | Recommended disposition | Stable boundary or required change |
|---|---|---|---|
| General | `Disposable.Empty` | **retain as stable** | Shared, stateless, idempotent no-op registration handle. Repeated disposal has no ownership effect. |
| Logging | `ILogger.Log` | **retain as stable** | Minimal synchronous feature-facing emission contract. The service is supplied and owned by the composition root. |
| Logging | `LoggerExtensions` | **retain as stable** | Thin severity conveniences; null receivers throw and all behavior delegates to `ILogger.Log`. |
| Logging | `LogLevel` | **retain as stable** | Preserve names, ordering, and integral values `Trace=0` through `Fatal=5`. |
| Logging | `LogEntry` | **retain as stable** | Structurally read-only value with a deliberately retained `Exception` reference; persistence/redaction is not Core-owned. |
| Logging | `ILogSink` | **retain as stable** | Real consumer extension seam implemented by Logging and Watchdog sinks. |
| Logging | `IFlushableLogSink` | **retain as stable** | Sink-level synchronous flush capability, distinct from the budgeted pipeline-level flusher. |
| Logging | `ILogFlusher` | **retain as stable** | `false` means completion was not confirmed within the supplied budget; it does not promise cancellation of underlying work. |
| Logging | `ILogSnapshotSource` | **retain as stable** | Non-null bounded recent read, newest window returned in chronological order by the supported implementation. |
| Logging | `NullLogger` | **retain as stable** | Sealed shared singleton; drops writes, returns empty snapshots, and reports flush complete without evaluating payload behavior. |
| Telemetry | `ITelemetry` | **retain as stable** | Consumer-owned operation factory with explicit correlation and optional shallow dimensions. |
| Telemetry | `ITelemetryOperation` | **retain as stable** | Explicit checkpoint/terminal lifecycle; first completion wins in the supported pipeline and no implicit disposal terminal exists. |
| Telemetry | `ITelemetrySink` | **retain as stable** | Real custom sink seam receiving completed operations. |
| Telemetry | `ITelemetrySnapshotSource` | **retain as stable** | Non-null bounded completed-operation read with the same newest-window ordering contract as Logging. |
| Telemetry | `TelemetryCheckpoint` | **reshape before 1.0** | Preserve its signatures, but defensively copy and wrap the outer dimensions dictionary instead of exposing caller-owned mutable storage. |
| Telemetry | `TelemetryOperation` | **reshape before 1.0** | Preserve its signatures, but defensively copy and wrap checkpoints, dimensions, and measurements. Values remain intentionally shallow. |
| Telemetry | `TelemetryOutcome` | **retain as stable** | Preserve names and integral values `Unknown=0`, `Succeeded=1`, `Failed=2`, and `Cancelled=3`. |
| Telemetry | `NullTelemetry` and its returned operation | **retain as stable** | Sealed shared singleton; returns one already-completed operation with an empty ID, zero checkpoint elapsed time, no terminal effect, and empty snapshots. |
| Inspection | `IInspectionRecorder.IsEnabled`, `Record`, and `RegisterStateProvider` | **retain as stable** | Passive producer/registration boundary. Lazy payload work must not run when disabled; registration ownership is represented by `IDisposable`. |
| Inspection | `IInspectionRecorder.RegisterAction` | **explicitly mark experimental** | Use `NEKOEXP0001`; action authorization, async work, cancellation, timeout, and UI marshalling remain unresolved and no feature module registers an action. |
| Inspection | `IInspectionSnapshotSource` | **retain as stable** | Read-only consumer boundary with no action invocation. It returns the newest bounded operation window in chronological order; timeout is a shared best-effort provider budget, not cancellation. |
| Inspection | `InspectionOperation` | **retain as stable** | Structurally read-only envelope retaining one opaque payload reference; `ToString` isolates a throwing payload formatter. |
| Inspection | `InspectionSnapshot` | **reshape before 1.0** | Preserve its signatures, but defensively copy and wrap operations and state. State values remain intentionally shallow. |
| Inspection | `InspectionProvider` | **retain as stable** | Non-null, singleton-capable global bridge needed to resolve an opt-in recorder without a Navigation-to-Inspection dependency. |
| Inspection | `NullInspection` | **retain as stable** | Sealed shared singleton; disabled, never invokes payload/provider/action delegates, returns shared no-op registrations, and produces an empty snapshot. |

No Core type or member family is recommended for deprecation or immediate
internalization/removal. The action-registration member is not proposed as
stable; its experimental marker is the narrow alternative to either pretending
its unresolved behavior is final or inventing a new action abstraction now.

## Downstream usage

| Consumer | Current Core use | Ownership and compatibility consequence |
|---|---|---|
| Logging | `Logger` implements `ILogger`, `ILogSnapshotSource`, `ILogFlusher`, and `IDisposable`; public debugger/file sinks implement `ILogSink` or `IFlushableLogSink`. | Core interface changes affect concrete implementation and external custom sinks. Pipeline/sink disposal remains Logging policy, not an `ILogger` requirement. |
| Telemetry | `TelemetryPipeline` implements `ITelemetry` and `ITelemetrySnapshotSource`; its operation scope implements `ITelemetryOperation` and dispatches `TelemetryOperation` to `ITelemetrySink`. | Operation lifecycle and model semantics are direct implementation contracts. Outer defensive copies do not require a source or binary migration. |
| Inspection | `InspectionRuntime` implements both recorder and snapshot-source contracts, installs through `InspectionProvider`, uses `Disposable.Empty` after disable/races, and materializes Core operation/snapshot models. | Provider, registration, timeout, and payload semantics cross the package boundary. The action marker must later be reconciled with the exact concrete action surface during F1-INSP. |
| Diagnostics | `CrashHandlerOptions` accepts `ILogger`, `ILogFlusher`, all three snapshot sources, and then formats `LogEntry`, `TelemetryOperation`, `TelemetryCheckpoint`, `InspectionSnapshot`, and `InspectionOperation` under per-contributor budgets and redaction. | Models and snapshot contracts are incident-evidence inputs. Core exposes raw in-memory data; Diagnostics owns safe formatting, redaction, truncation, and partial bundles. |
| Navigation | Bootstrap accepts `ILogger`, `ITelemetry`, and an explicit or global `IInspectionRecorder`. Observers log outcomes, own explicit telemetry completion, record scalar Inspection projections, register read-only state providers, and use `Disposable.Empty` when disabled. | All three feature-facing interfaces are active composition seams. Navigation registers no Inspection action and must remain unaffected by the experimental classification. |
| Watchdog | `WatchdogOptions` accepts `ILogSink[]` and optional `ITelemetry`; the runtime constructs Core `LogEntry`, writes sinks, and emits short completed telemetry operations. | Watchdog needs the sink/model/factory contracts but has no Inspection dependency or producer. Arbitrary telemetry values stay local to the supplied pipeline. |

Tight source evidence includes
[`Logger.cs`](../../../../src/Logging/NekoLib.Logging/Logger.cs),
[`TelemetryPipeline.cs`](../../../../src/Telemetry/NekoLib.Telemetry/TelemetryPipeline.cs),
[`InspectionRuntime.cs`](../../../../src/Inspection/NekoLib.Inspection/InspectionRuntime.cs),
[`CrashHandler.cs`](../../../../src/Diagnostics/NekoLib.Diagnostics/CrashHandler.cs),
[`PageNavBootstrap.cs`](../../../../src/Navigation/NekoLib.Navigation/Bootstrap/PageNavBootstrap.cs),
[`NavigationTelemetryObserver.cs`](../../../../src/Navigation/NekoLib.Navigation/Diagnostics/NavigationTelemetryObserver.cs),
[`InspectionNavigationObserver.cs`](../../../../src/Navigation/NekoLib.Navigation/Diagnostics/InspectionNavigationObserver.cs), and
[`WatchdogRuntime.cs`](../../../../src/Watchdog/NekoLib.Watchdog/WatchdogRuntime.cs).

### Package-consumer reachability

The PackageReference-only WinForms and WPF probes do not reference Core
directly; Core is reachable transitively through Navigation, Logging,
Telemetry, Inspection, Diagnostics, and Watchdog. Their programs currently
load only `typeof(NekoLib.Core.Logging.ILogger)` as an explicit Core symbol.
That proves compile/load reachability for both target families, but it does not
exercise custom implementations, model constructors, defensive-copy behavior,
the global provider, or null objects.

Any accepted Core change therefore requires one coordinated immutable package
family, even when the signature diff is confined to Core. The external probe
should compile representative stable contracts and the experimental action
warning policy after implementation; it must not be described as runtime
evidence.

## Observed facts and risks

### Structural immutability currently leaks caller-owned collections

[`TelemetryCheckpoint`](../../../../src/Core/NekoLib.Core/Telemetry/TelemetryCheckpoint.cs)
assigns the supplied dimensions directly. `TelemetryOperation` does the same
for checkpoints, dimensions, and measurements, including mutable dictionaries
as shared empty instances. `InspectionSnapshot` likewise assigns its supplied
operations and state directly. `IReadOnlyList` and `IReadOnlyDictionary`
restrict the static interface only; callers can retain and mutate the original
collection, and can cast properties back when their runtime type is an array or
`Dictionary`.

The concrete Telemetry pipeline copies producer dictionaries before model
construction, but its completed models still expose those mutable concrete
copies. The concrete Inspection runtime passes a fresh array and dictionary,
but those too remain castable and mutable by snapshot consumers. The public
constructors also support third-party sinks and snapshot sources, so concrete
pipeline behavior alone cannot close the contract.

**Risk:** snapshots documented as immutable/read-only can change after
publication; the shared empty dictionaries can even be contaminated across
otherwise unrelated instances.

**Recommended disposition:** reshape the three model types without changing
their compiled signatures. Copy the outer collections and expose non-mutable
wrappers on both targets. Preserve contained object/checkpoint/operation
references; deep cloning is outside Core's ownership and would require a
serializer or a closed value system.

### Exceptions and arbitrary objects are deliberately shallow and raw

`LogEntry.Exception`, telemetry dimension values, `InspectionOperation.Payload`,
and Inspection state values retain the supplied reference. The Core test
explicitly verifies `LogEntry` exception identity. Navigation supplies bounded
anonymous scalar projections; Watchdog supplies an anonymous telemetry payload;
Diagnostics calls `ToString` later and owns redaction/truncation before disk.

**Risk:** mutable values can change after recording, `ToString` may be slow or
throw, exception text can contain sensitive data, and a read-only collection
does not make its values immutable. `InspectionOperation.ToString` catches a
throwing payload formatter; `LogEntry.ToString` includes the full exception and
does not itself redact it. Core has no safe universal way to clone, serialize,
or redact arbitrary application objects.

**Recommended disposition:** retain these shallow reference contracts as
stable and document them explicitly. Producers must emit bounded diagnostic
projections; persistence consumers must isolate formatting and apply their own
redaction. Do not add JSON, reflection serialization, a new scalar union, or a
Core-to-Diagnostics dependency.

### Bounded reads and time budgets are completion budgets, not cancellation

The supported Logging and Telemetry pipelines treat non-positive recent-count
requests as empty and return the newest requested window in chronological
order. Inspection uses `maxOperations` only for the operation window; state
providers are still evaluated when it is zero. Its concrete runtime rejects a
negative count or timeout, shares one elapsed-time budget across providers,
returns placeholders for failures/timeouts, and may leave a timed-out provider
task running because the delegate contract has no cancellation token.

Likewise, `ILogFlusher.Flush(timeout)` can return `false` while a sink-level
`Flush()` invocation continues. Diagnostics adds an outer contributor timeout,
but that isolates the crash path rather than cancelling third-party work.

**Recommended disposition:** retain the signatures and stabilize the precise
behavior in XML/current documentation and regression tests. Never describe the
timeout as forced cancellation or the snapshot as a deep immutable capture.
Providers and sinks remain responsible for bounded work.

### Null objects deliberately avoid optional-observability side effects

All three null implementations are sealed singletons with private constructors.
They do not validate or enumerate ignored payload data. `NullLogger` reports a
successful flush and an empty log snapshot. `NullTelemetry` returns one shared,
already-completed operation and an empty operation snapshot. `NullInspection`
reports disabled, never invokes any supplied delegate, returns
`Disposable.Empty`, and returns an empty snapshot.

**Recommended disposition:** retain this fail-silent disabled behavior rather
than making feature control flow depend on whether optional observability is
installed. Add direct singleton, delegate non-evaluation, empty-read, and
already-completed-operation contract tests during implementation.

### The global Inspection slot has a real cross-package purpose

[`InspectionProvider`](../../../../src/Core/NekoLib.Core/Inspection/InspectionProvider.cs)
starts at `NullInspection.Instance`, publishes through volatile/interlocked
access, rejects null or disabled recorders, admits only one enabled recorder,
rolls back a recorder that disables during installation, and returns an
idempotent conditional-uninstall handle. Navigation resolves `Current` only
when the consumer opts into global Inspection; an explicit recorder overload
remains available.

**Recommended disposition:** retain `Current` and `Install` as stable. Document
that the slot is process-wide for the loaded Core assembly context, the install
handle does not dispose the recorder, and ordinary consumers should prefer
`InspectionRuntime.EnableGlobal` when using the supplied implementation. Do not
copy this static pattern to Logging or Telemetry.

### Nullability and caller assertions

The compiled nullable surface is identical on both targets. Required contract
strings are non-null, optional correlation/category/exception/payload values are
annotated nullable, and collection-returning APIs are non-null. Current
constructor behavior is intentionally not uniform: `LogEntry` normalizes a
runtime-null message to empty and blank category to null, while Telemetry and
Inspection models throw for required null names. `DateTime` members named
`*Utc` trust the caller and do not rewrite `DateTime.Kind`; duration, capacity,
sequence, and counter values are likewise caller assertions in public model
constructors.

**Recommended disposition:** preserve these signatures and behaviors. Tight
validation of every externally constructed evidence model would break custom
snapshot sources without improving the Core ownership boundary. Document the
UTC/value obligations instead.

## Action registration classification

`IInspectionRecorder.RegisterAction` is the sole Core member that exposes a
state-changing delegate registration. The read-only snapshot contract cannot
invoke it, Diagnostics receives only that snapshot contract, Navigation uses
only recording and state providers, and no feature module registers an action.
The current roadmap explicitly leaves action authorization, asynchronous
execution, cancellation, timeout, and UI-thread marshalling unresolved.

**Recommended disposition:** mark this exact member as experimental with
`NEKOEXP0001` under the accepted cross-target `Obsolete` policy. The current
documentation must state that it is in-process only, has no authorization
boundary, and must not be exposed through IPC or reflection as privileged
control. F1-INSP must later apply the same experiment identity consistently to
the concrete action-registration/invocation family or choose a migration before
the first stable release.

This marker is a source warning and manifest change, not a runtime expansion.
It does not authorize a feature producer, privileged host, Instrumentation or
TestControl package, plugin loader, or broad Inspection rollout.

## Likely migration cost

| Accepted recommendation | Source impact | Binary/behavior impact |
|---|---|---|
| Defensive outer copies for telemetry models | None for signatures or ordinary callers. | No binary diff expected; code that intentionally relied on post-construction collection aliasing stops changing published models. |
| Defensive outer copies for `InspectionSnapshot` | None for signatures or ordinary callers. | No binary diff expected; casts/mutations of the exposed concrete collection cease to work. Opaque state values remain shared references. |
| `NEKOEXP0001` on `RegisterAction` | Existing calls through `IInspectionRecorder` compile with an experimental warning and need an explicit disposition. | Attribute-only API-manifest diff; no removal or runtime behavior change. Concrete Inspection action APIs require coordinated F1-INSP classification. |
| Stable-contract documentation/tests | No migration. | Clarifies existing ownership, timeout, null, ordering, shallow-value, and global-slot behavior. |

Core is a transitive dependency of six shipped capability packages, so even a
low-signature-cost change has wide package reach. A future implementation must
build and test the dependent family, update only accepted manifests, create a
new immutable coordinated package version from a clean commit, and run external
PackageReference consumers. The existing `local.15` artifacts cannot be
overwritten or reused as proof for changed bits.

## Rejected alternatives

- **A new Core facade over internal contracts:** rejected. Core already is the
  contract boundary; another layer would not express ownership or lifecycle.
- **Global Logging or Telemetry providers:** rejected because their pipelines
  are consumer-owned and independently composable.
- **Removing sink/snapshot interfaces because repository callers are few:**
  rejected. The concrete packages, Diagnostics, and custom-consumer boundary
  use them directly; public type counts and repository references alone are not
  removal evidence.
- **Deep-cloning or serializing arbitrary values in Core:** rejected because no
  universal safe clone exists and it would add policy/dependencies to a zero-
  dependency contract package.
- **Replacing `Exception` with preformatted text:** rejected because structured
  sinks need the original exception; persistence owners already control
  formatting and redaction.
- **Treating actions as stable now:** rejected because their execution and
  authorization contract is unresolved.
- **Removing actions or introducing a new action-capability abstraction now:**
  rejected for this review. The existing Inspection runtime has an operational
  in-process action surface, while no proven feature case justifies a new
  abstraction. The explicit experimental marker preserves honest optional use
  without freezing the design.
- **Marking all Inspection contracts experimental:** rejected. Passive record,
  state-provider, snapshot, null-object, and singleton lifecycle paths have
  concrete producers/consumers and bounded tests; only actions lack a stable
  contract.
- **Strictly validating every timestamp, counter, and enum in public evidence
  constructors:** rejected because these models also support custom sinks and
  snapshot sources. The caller owns those assertions.

## Proposed implementation block after acceptance

One narrow F1-CORE implementation should:

1. apply defensive outer copies/read-only wrappers to
   `TelemetryCheckpoint`, `TelemetryOperation`, and `InspectionSnapshot` on
   both targets without deep-copying values;
2. apply `NEKOEXP0001` to the accepted action member and add the policy-required
   current documentation, while leaving action behavior and broad
   instrumentation frozen;
3. add focused Core regressions for collection isolation, shared-empty
   isolation, null singletons, operation completion, registration handles,
   bounded reads, and global install ownership;
4. update the current Core documentation, `CHANGELOG.md`, migration guidance,
   `TODO.md`, and only the accepted Core API manifests when the user accepts the
   dispositions;
5. build/test Core and its dependent capability packages, run the full solution
   gate, compare both Core manifests, validate documentation and diffs, and
   compile representative external PackageReference Core usage;
6. commit the clean implementation and documentation, then use the canonical
   pack flow with the next unused immutable family version and record Core
   package ID/version/hash/source plus package-consumer results. Do not push.

No runtime scenario is automatically required for the collection wrappers or
attribute-only classification. If implementation changes time-budget,
threading, persistence, provider execution, or global lifecycle behavior, that
claim changes and the relevant Observability runtime evidence must be rerun and
reported separately.

## Review validation

Validation is recorded by evidence class so one gate is not mistaken for
another:

| Evidence class | Review result | Claim and limit |
|---|---|---|
| Focused tests | `dotnet test tests/NekoLib.Core.Tests/Unit/NekoLib.Core.Tests.Unit.csproj`: 6/6 passed on `net481` and 6/6 passed on `net9.0`, with 0 failures and 0 skips. | Core contract behavior only; not downstream runtime evidence. |
| Build | The scoped API verifier built Core once per target; both builds passed with 0 warnings and 0 errors. | Compilation only. |
| Compiled API comparison | `eng/verify-public-api.ps1 -PackageId NekoLib.Core`: both accepted manifests matched; 2/2 baselines verified and no baseline was updated. | Current assembly surface versus accepted candidate manifests. |
| Documentation | `eng/verify-docs.ps1`: passed after registration. No build log was supplied, so the warning-baseline comparison was explicitly not run. | Metadata, registration, links, topology, and Markdown checks only. |
| Diff hygiene | `git diff --check`: passed for the final review change. | Whitespace/conflict-marker hygiene and final review scope only. |
| Runtime evidence | Not run. | No real or long-running runtime claim is made. |
| Package evidence | Not run. | No package was created or consumed; `local.15` remains prior evidence from its recorded source commit. |

These review gates can establish the current candidate baseline only. They do
not validate an accepted future API change.

## Decision gate

Nothing in this review authorizes product, API-baseline, changelog, migration,
roadmap, or package changes. F1-CORE must stop here until the user explicitly
accepts, modifies, or rejects the recommended dispositions, especially the
three model reshapes and `NEKOEXP0001` action classification.

## Reconciliation — 2026-08-17

The user accepted the recommended dispositions. The three collection-bearing
models now take defensive outer copies, the remaining Core contracts stay
stable candidates, and `IInspectionRecorder.RegisterAction` received only the
`NEKOEXP0001` marker in this block. Concrete action behavior and adoption remain
deferred to each dependent module's future F1 review; this acceptance does not
unfreeze broad Inspection instrumentation.

The accepted implementation passed 13/13 focused Core tests on each target,
the post-test incremental Release solution build with 0 warnings and 0 errors,
and 1,310/1,310 solution tests (651 `net481`, 659 modern-target) with no failures
or skips. Both updated Core manifests then matched the compiled assemblies;
documentation and diff hygiene also passed. No runtime scenario was run; the
accepted change does not alter runtime time-budget, threading, persistence,
provider, or global-lifecycle behavior.

The canonical clean-tree package gate then created coordinated family version
`1.0.0-local.16` from implementation commit
`7ae62a23db4c8933f7db2cf783b227df21a59abe`. The published
`NekoLib.Core.1.0.0-local.16.nupkg` contains both `net481` and `net9.0`
assemblies, records that source commit in its NuGet metadata, and has SHA-256
`0C26641F8D28779665F13DB407EC07C49AF75105D3A302496F1A5C95F568167E`.
Inspection of the packaged `net481` assembly confirmed the exact
`NEKOEXP0001` warning marker with `IsError=false`.

PackageReference-only WinForms and WPF consumers restored, built, and ran on
both target families; the WinForms program compiled representative direct Core
contracts, and all consumer builds reported zero warnings and zero errors. The
multitarget consumer, package structure, target assemblies, Watchdog Host
payloads, deployment opt-out, stale-payload replacement, publish, and clean
probes also passed. This is package and package-consumer evidence, not a real
application runtime scenario; none was run or required for the accepted Core
change.
