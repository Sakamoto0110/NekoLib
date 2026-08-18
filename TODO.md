# NekoLib Live Roadmap

**Kind:** roadmap/status

**Lifecycle:** current

**Subject:** current direction, live work, accepted future work, active freezes,
phase gates, and completion criteria

**Direction decision date:** 2026-08-16

Audit snapshots are indexed under [`docs/audit/`](docs/audit/README.md).
Completed roadmap and validation history is indexed under
[`docs/history/`](docs/history/README.md). Neither directory is a live issue
tracker.

## Current project direction

Phase E confidence stabilization is complete and archived. Its implementation
log, outcome-first runtime evidence, residual confidence options, and final
commit-bound validation are preserved in the
[`Phase E completion snapshot`](docs/history/phase-e-confidence-stabilization-2026-08-12.md).

Phase G1 is complete at its deterministic, package, and real-provider runtime
boundaries. The optional TheCatAPI probe passed on both target families with
run-owned mutation, reconciliation, and complete cleanup. The Phase G2 design
review is complete and awaiting an explicit architecture decision; product
implementation remains gated. Phase F is active only for F1 public API and
release stability; F2-F7 remain gated.

The Phase G1 work preserved these rules, which continue to apply:

- preserve the validated Phase E behavior and evidence boundaries;
- prefer evidence over new abstractions and narrow fixes over new modules;
- keep project references shallow and preserve opt-in/NO-OP behavior;
- preserve Logging, Telemetry, Inspection, Diagnostics, and
  Diagnostics.Windows as distinct capabilities;
- keep Windows-specific crash behavior isolated in Diagnostics.Windows;
- keep Data, Devices, Mvvm, and Pipes independent of Core unless a separately
  accepted, module-scoped decision changes that graph;
- keep Watchdog a local process supervisor across a process/IPC boundary and
  preserve its existing application-log forwarding and crash-notification
  integration;
- distinguish automated, build-only, manual, interactive, package, provider,
  hardware, short-window, and duration evidence truthfully;
- do not generalize application-specific infrastructure into the framework;
- do not treat hypothetical fleet requirements as current product
  requirements.
- do not treat a completed review, passing unit tests, a successful build, or
  the absence of known findings as proof of real or long-running runtime
  behavior.

The current module map, targets, dependency graph, public entry points, and
package overview remain owned by [`README.md`](README.md), current project
files, and source. Package versions remain immutable, validation remains
manually triggered, and Windows is required for full dual-target validation.

## Phase G — Applied integrations

> **Status: G1 COMPLETE; later items remain gated.** This phase is independent
> of the gated scale-preparation work in Phase F. Its completed authorization
> was limited to the concrete HTTP catalog below and did not activate an API
> gateway, application host, dependency-injection framework, global registry,
> retry framework, secret store, or remote service.

### G1 — Typed HTTP endpoint catalog

**Promotion decision — 2026-08-12:** create `NekoLib.Http` as a small opt-in
module whose first executable provider model is TheCatAPI and whose next
intended consumer is a separately reviewed Pix payment adapter. The shared
contract exists to centralize HTTP method, relative route, request type, and
response type; it does not hide HTTP or claim that unrelated APIs share one
domain model.

Accepted boundaries:

- [x] Add a `net481`/`net9.0` package with no NekoLib project dependency. Keep
  endpoint catalogs instance-scoped and immutable after construction; do not
  add reflection scanning, attributes, source generation, or global state.
- [x] Provide typed GET, POST, PUT, PATCH, and DELETE descriptors, safe relative
  path/query construction, duplicate-name validation, and one consumer-owned
  `HttpClient` execution surface. The consumer owns base address, lifetime,
  authentication, certificates, timeout, handlers, and policy configuration.
- [x] Preserve HTTP evidence: return status, reason, headers, raw response body,
  and a typed success value. Bound response buffering and never log bodies,
  headers, credentials, or API keys. Do not retry automatically, especially for
  methods that may create or mutate state.
- [x] Cover URI escaping, registration, request construction, JSON bodies,
  success/error/no-content responses, size limits, and cancellation through
  deterministic dual-target tests backed by a controlled
  `HttpMessageHandler`; these tests must not depend on the public internet.
- [x] Add an optional standalone TheCatAPI scenario that models image search,
  image lookup, favourite creation/query/deletion, secret injection, bounded
  cleanup, and exit-code outcomes. A build is build evidence only; provider
  evidence requires an actual run with a maintainer-owned API key and must not
  expose the key or use personal data as `sub_id`.
- [x] Reconcile the module map, package workflow, test inventory, scenario
  inventory, and documentation verifier. Validate the focused tests and the
  full solution on both target families before closing G1.

**Completion record — 2026-08-16:** the implementation baseline is
`ae711fb51d27af29701d332a453912ad1f87a029`. Deterministic HTTP tests passed
16/16 on each target; the full serial solution gate passed 1,281 executions
with 0 failures and 0 skips; the rebuild emitted 515 existing warning
occurrences, no new normalized warning identity, and omitted five baseline
identities. Clean package flow `1.0.0-local.11` published 16 packages and passed
all external package-consumer probes. `NekoLib.Http.1.0.0-local.11.nupkg`
contains both target assets, records the implementation commit, and has SHA-256
`30464eca19e909a993d6e02e84d20b2cf3cb44b909cde3980ffc03cc44b81c1e`.
The TheCatAPI prerequisite path exited `3` without a request on both targets and
produced sanitized, cleanup-complete artifacts. The later real-provider runs
passed 10/10 with exit `0` on `net9.0` and `net481`: image search/lookup,
favourite creation/query/deletion, post-delete absence, and final
reconciliation all passed with zero cleanup problems. The scenario README owns
the artifact-specific evidence; no credential, header, or body was persisted.
The completed work log is archived in the
[`Phase G1 completion snapshot`](docs/history/phase-g1-http-integration-2026-08-16.md).

### G2 — Payments and Pix (design review complete; implementation gated)

**Review promotion — 2026-08-16:** the authorized code-first review is complete
at [`docs/audit/payments-pix-design-review-2026-08-16.md`](docs/audit/payments-pix-design-review-2026-08-16.md).
It changed no product code, solution membership, package topology, or public
API. Pix is the payment rail and API standard; a PSP supplies the merchant
account, credentials, certificates, endpoints, and sandbox.

The review recommends one deliberately narrow first implementation:

- one dual-target `NekoLib.Payments` package referencing `NekoLib.Http`, with a
  Pix-specific public surface rather than a speculative universal payment
  provider interface;
- immediate Pix charge creation with a caller-persisted `txid`, charge lookup,
  copy-and-paste payload, tolerant status handling, and ambiguous-outcome
  reconciliation by lookup;
- the Efí Bank homologation environment as the first external provider model,
  because it closely follows the official API Pix, supplies separate OAuth2 +
  mTLS credentials, and simulates active and completed charges without real
  settlement;
- consumer-owned persistence, `HttpClient`, OAuth token handling, certificate,
  timeout, retry decisions, secrets, and authoritative reconciliation against
  the PSP;
- no webhook receiver, refund, due-date charge, Pix Automatic, outgoing Pix,
  split, production credential, real-money test, QR image renderer, provider
  SDK wrapper, or second provider in the first slice.

**DECISION REQUIRED — DO NOT IMPLEMENT YET.** Accept or revise the recommended
package boundary, Efí sandbox selection, and first-slice scope. Do not add G2
implementation checkboxes, projects, tests, scenarios, packages, credentials,
or certificates until that decision is explicit. No G2 decision may move Pix,
OAuth, mTLS, webhook, idempotency, or reconciliation policy into
`NekoLib.Http`.

## Active freezes

### Deferred Inspection module rollout (B4/B5)

**Freeze reason:** the Core contracts, global Inspection runtime, Navigation
producer, and Diagnostics read-only consumer are proven, but broad module
instrumentation and state-changing actions have not yet demonstrated enough
value or a safe common contract. This is live context, not completed history.

**Implemented state:**

- Core owns independent Logging, Telemetry, and Inspection contracts plus
  non-null NO-OP defaults.
- `InspectionRuntime.EnableGlobal(...)` provides deterministic singleton
  activation and teardown. Navigation is the only feature module that records
  Inspection operations.
- Diagnostics consumes only `IInspectionSnapshotSource`; incident collection
  cannot invoke Inspection actions.
- Navigation is the first accepted Phase D Telemetry producer and owns its
  bounded page-switch timing. That work does not authorize broader Inspection
  recording.

**Known gaps and traps:**

- Data, Pipes, Devices, Watchdog, and Diagnostics do not automatically record
  feature-module Inspection operations. A sample application calling
  `Record(...)` manually is application instrumentation, not module
  instrumentation.
- No feature module registers a real Inspection action. Navigation stays
  read-only until async execution, cancellation, timeout, and UI-marshalling
  semantics are explicitly accepted.
- Watchdog crash notification crosses IPC. Its log/crash integration must be
  designed separately from in-process module recording.
- Passive instrumentation may remain an architectural concept, but it does not
  require its own assembly. No action has demonstrated enough value to justify
  an action rollout.

**Existing seams:**

- Data: `QueryExecutionContext`.
- Pipes: `IPipeMetrics`.
- Devices: the serialized `HardwareEngine.SendAsync` transaction.
- Watchdog and Diagnostics: their existing incident and IPC boundaries, after a
  dedicated review.

**Resume order and unfreeze conditions:**

1. Explicitly unfreeze one bounded module and define the operational question
   its data must answer.
2. Validate the smallest real producer before copying a pattern elsewhere;
   Data or Pipes are the preferred first candidates.
3. Preserve disabled/NO-OP behavior, module boundaries, and both supported
   target families.
4. Demonstrate any future action case first in a runtime scenario or an
   application-owned test harness.
5. Restore the broad freeze after the authorized module scope is complete.

**Accepted deferral:** do not create an Instrumentation project family, a
TestControl project, a plugin loader, a privileged IPC host, or a reflection
activation system. Reflection is not a security boundary. Privileged remote
control is unnecessary for the current product scope, and no implementation
task for this idea is accepted by this roadmap.

### Navigation stability-sensitive core

The following components remain frozen after the accepted lifecycle and trace
correction:

- `NavigationContext`;
- `NavigationRuntime`;
- `PageRegistry`;
- `PageFactory`.

No adapter or Phase F candidate authorizes changes to these components. If a
finding appears to require a runtime change, it must first be confirmed, record
evidence, be promoted selectively to this roadmap, receive an explicit
module-scoped unfreeze, and preserve the canonical lifecycle invariants. The
freeze is restored after the authorized scope.

## Phase F — Scale preparation (F1 active; F2-F7 gated)

> **Status: PARTIALLY ACTIVE.** F1 was explicitly promoted on 2026-08-16.
> F2-F7 remain gated and must not be investigated or implemented without their
> own explicit promotion.

### F1 — Public API and release stability

**ACTIVE — promotion decision 2026-08-16.** Finalize the public package family
one module at a time under the accepted
[`public API and release policy`](docs/public-api-release-policy.md). F1 does
not activate F2 automation/CI, unrelated Phase F candidates, broad Inspection
instrumentation, or Navigation runtime changes in frozen components.

#### F1.0 — Policy foundation

- [x] Define the coordinated package-family SemVer rules, pre-stable candidate
  boundary, stable versus experimental classification, source/binary/behavioral
  compatibility, breaking-change approval, deprecation window, single active
  release-line policy, changelog, migration requirements, and stable-release
  completion evidence.
- [x] Register the current policy and changelog in the documentation authority
  index. No product assembly or public API changed in this block.

#### F1.1 — Automated candidate and stable API baselines

- [x] Select and implement an assembly-derived public API manifest and
  compatibility check for every library package and target framework. Keep the
  existing package validation; do not mistake it for comparison against a
  historical accepted surface.
- [x] Produce a reviewable candidate snapshot before the first module change.
  Baseline updates must accompany an accepted API decision, implementation,
  tests, changelog, and migration record; an unexplained public/protected diff
  fails validation.
- [x] Define the cross-target marker and documentation rule for experimental
  APIs before any API is classified that way. A namespace or naming convention
  alone is not a marker.

**Completion record — 2026-08-17:** `eng/verify-public-api.ps1` builds and
reflects the 15 library assemblies through the versioned dual-target
`NekoLib.PublicApiTool`, then compares 30 package/TFM outputs with the accepted
snapshots under `eng/public-api/`. The initial candidate snapshot was taken
before any F1 module implementation; product sources match `df654b1`. A full
Release solution build completed with 515 existing warning occurrences and no
errors. Experimental APIs now require the cross-target `Obsolete` marker and
the module-documentation record defined by the release policy; no current API
was classified experimental by this infrastructure block.

#### F1.2 — Module-by-module public API finalization

Use one narrow review/decision/implementation/validation block per entry. An
inventory does not authorize a breaking change, and the absence of repository
consumers does not prove that a public member is unused.

1. [x] **F1-DATA — Data.** Reverify DATA-016 against the compiled dual-target
   surface and supported gateway use cases. Decide one coherent public gateway
   contract before any namespace move, overload removal, internal executor, or
   compatibility shim is implemented.
   **Accepted 2026-08-17:** move the concrete gateway to the public Gateway
   namespace without a shim; retain the explicit capability interfaces and
   remove the redundant universal family; align concrete/interface overloads;
   expose streaming only where it is implemented; propagate the net9 DTO
   reflection contract through interfaces and mapping entry points; internalize
   the leaked reader helper; and close accidental concrete inheritance while
   preserving factory/translator/interface extension seams. The accepted
   rationale and rejected alternatives are recorded in
   [`docs/audit/data-public-api-review-2026-08-17.md`](docs/audit/data-public-api-review-2026-08-17.md).
   **Completed 2026-08-17:** implementation landed in `59d1faf`, with package
   consumer and manifest refinements in `bced326` and `3e58df2`. Data passed
   111/111 tests on `net481` and 119/119 on `net9.0`; the full solution passed
   1280/1280 tests; both Data API manifests, documentation verification, and
   the affected Farm and SQL Server scenario builds passed. Immutable package
   `NekoLib.Data.1.0.0-local.14.nupkg` was built from `3e58df2`, exercised by
   PackageReference-only consumers on both targets with zero consumer warnings,
   and has SHA-256
   `B5B03AD0CA92C8F7EB5BF413EB2242945E1222011FC1667684821E106173ECDC`.
   **Accepted follow-up 2026-08-17:** add fluent `DeleteFrom` and symmetric
   builder `Delete` overloads so ordinary deletes participate in translation
   and `OnSqlGenerated`; keep the raw overloads for provider-specific SQL and
   compatibility. Unconstrained deletes are fail-closed by default and require
   the statement-local `AllowAllRowsDelete()` opt-in. Tracked Data scenarios
   must use the builder, including explicit opt-in at intentional table wipes.
   **Follow-up completed 2026-08-17:** implementation landed in `89d1ed1`.
   Data passed 119/119 tests on `net481` and 127/127 on `net9.0`; the full
   solution passed 1296/1296 tests; both Data API manifests and documentation
   verification passed; and the Farm and SQL Server scenarios compile on both
   target families. Immutable package `NekoLib.Data.1.0.0-local.15.nupkg` was
   built from `89d1ed1bbc7b4a42533150e808e4a03f868aff30`, and the
   PackageReference-only consumers passed on both targets with zero consumer
   warnings. Its SHA-256 is
   `977D8F3DAABB63ECF3855F1F43ED60CCD8DE40E5981591B6E5F9DC7F266B8E70`.
2. [x] **F1-CORE — Core.** Finalize the shared contracts and null-object
   surface before the dependent capability packages.
   **Accepted 2026-08-17:** retain the existing ownership, lifecycle,
   extension, null-object, snapshot, bounded-read, payload, and global-provider
   contracts as stable candidates; make `TelemetryCheckpoint`,
   `TelemetryOperation`, and `InspectionSnapshot` structurally read-only by
   defensively copying and wrapping their outer collections while preserving
   shallow contained values; and mark only
   `IInspectionRecorder.RegisterAction` as experimental `NEKOEXP0001`.
   Action registration, invocation, authorization, async/cancellation/timeout,
   UI-marshalling, and module adoption remain subject to each module's future
   F1 review. This decision does not unfreeze broad Inspection instrumentation
   or authorize changes to dependent-module action behavior. The accepted
   rationale and rejected alternatives are recorded in
   [`docs/audit/core-public-api-review-2026-08-17.md`](docs/audit/core-public-api-review-2026-08-17.md).
   **Completed 2026-08-17:** implementation landed in `7ae62a2`. Core passed
   13/13 tests on `net481` and 13/13 on `net9.0`; the full solution passed
   1310/1310 tests; the post-test incremental Release solution build completed
   with 0 warnings and 0 errors; and both Core API manifests, documentation
   verification, and diff hygiene passed. No runtime scenario was required or
   run. Immutable package
   `NekoLib.Core.1.0.0-local.16.nupkg` was built from
   `7ae62a23db4c8933f7db2cf783b227df21a59abe`, exposes both supported target
   assemblies, and was exercised through PackageReference-only WinForms and
   WPF consumers on both target families with zero consumer warnings. Its
   SHA-256 is
   `0C26641F8D28779665F13DB407EC07C49AF75105D3A302496F1A5C95F568167E`.
3. [x] **F1-LOG — Logging.** Finalize the consumer-owned pipeline, options,
   sink, snapshot, and disposal contracts without adding global state.
   **Accepted 2026-08-17:** retain the entire compiled surface — all 5 public
   sealed types and 21 members, on both targets — as stable candidates with no
   removal, rename, namespace move, internalization, deprecation, or
   experimental marker, and correct behavior only. `DebugLogSink` writes through
   a non-conditional trace channel instead of the `[Conditional("DEBUG")]`
   `Debug.WriteLine` that was removed from every shipped Release assembly, and
   rejects a null entry like the file sink does. `Logger.Flush` isolates
   per-sink failures instead of stopping at the first one, observes the failure
   of a sink that outlived its budget so it is not reported as a process crash,
   and is inert after disposal. `Logger` copies the supplied sink array.
   `LoggerOptions` and `RollingFileLogSinkOptions` defaults are frozen. A
   dedicated Logging technical reference now owns the ownership, ordering,
   flush, disposal, rotation, retention, durability, and failure contracts that
   previously had no owner. A facade, provider, registry, asynchronous pipeline,
   new global state, and any Diagnostics/Telemetry/Inspection dependency were
   all rejected. The accepted rationale, evidence, and rejected alternatives are
   recorded in
   [`docs/audit/logging-public-api-review-2026-08-17.md`](docs/audit/logging-public-api-review-2026-08-17.md).
   **Completed 2026-08-17:** the initial implementation landed in `3ae27fe`.
   Independent packaging review corrected the accepted total-budget wording and
   serialized concurrent `Flush`/`Dispose` completion in `dd0cfb8`, adding two
   focused regressions. Logging passed 30/30 tests on `net481` and 30/30 on
   `net9.0`; the full solution passed 1,352/1,352 tests with no failures or
   skips; the canonical clean package rebuild emitted the existing 515-warning
   baseline and no errors; both Logging API manifests verified **unchanged**;
   documentation verification and diff hygiene passed; and the Observability
   runtime scenario compiled with no warnings on both targets without being
   launched. Immutable package
   `NekoLib.Logging.1.0.0-local.17.nupkg` was built from
   `dd0cfb8d9c0b69f234cb8cbe802ed8cac4b14213`, contains both supported target
   assemblies, and declares the aligned `NekoLib.Core` dependency. The canonical
   PackageReference-only WinForms and WPF consumers restored, built, and ran on
   both target families with zero consumer warnings, and the remaining package,
   deployment, publish, and clean probes passed. The package SHA-256 is
   `8378290431CA1036BD8E70E254C6995415DC755B6A91D82D7CAEDD7802AF3991`.
4. [x] **F1-TEL — Telemetry.** Finalize operation, checkpoint, outcome,
   dimensions, bounded-retention, and snapshot contracts.
   **Accepted 2026-08-17:** retain the entire compiled surface — both public
   sealed types and all 5 members, on both targets — as stable candidates with
   no removal, rename, namespace move, internalization, deprecation, or
   experimental marker, and correct behavior only. `Complete` materializes the
   caller's terminal dimensions and measurements before committing completion
   state, so a malformed dictionary no longer marks the operation terminal while
   destroying its record; `StartOperation` normalizes a blank
   `parentOperationId` to `null` as it already did for a blank `operationId`;
   and the pipeline copies the supplied sink array. `TelemetryPipelineOptions`
   defaults are frozen. Synchronous inline dispatch, retention before sink
   fanout, one identical order across sinks and retention,
   first-completion-wins, non-terminal abandonment, permissive measurements,
   shallow dimension values, and unbounded per-operation checkpoints are all
   retained and documented rather than changed. A dedicated Telemetry technical
   reference now owns the lifecycle, ordering, backpressure, reentrancy,
   time, dimension-merge, and retention contracts that previously had no owner.
   A facade, global provider, registry, asynchronous queue, persistent store,
   aggregation or metrics subsystem, `IDisposable`/finalizer lifecycle, implicit
   terminal for abandoned operations, and any new cross-module dependency were
   all rejected. No Core-contract conflict was found. The accepted rationale,
   evidence, and rejected alternatives are recorded in
   [`docs/audit/telemetry-public-api-review-2026-08-17.md`](docs/audit/telemetry-public-api-review-2026-08-17.md).
   **Completed 2026-08-17:** implementation landed in `4934859`, with final
   implementation validation recorded in `518c078`. Telemetry passed 22/22
   tests on `net481` and 22/22 on `net9.0`; the canonical clean package flow
   passed the full 1,384/1,384-test solution with no failures or skips and built
   with the existing 515-warning baseline and no errors. Both Telemetry API
   manifests verified **unchanged**, documentation verification and diff hygiene
   passed, and the Observability runtime scenario compiled unchanged on both
   target families without being launched. Immutable package
   `NekoLib.Telemetry.1.0.0-local.18.nupkg` was built from
   `518c078abc9bd9b340fbb7200470de47cde93452`, contains both supported target
   assemblies, and declares the aligned `NekoLib.Core` dependency. The canonical
   PackageReference-only WinForms and WPF consumers restored, built, and ran on
   both target families with zero consumer warnings; the multitarget, package,
   deployment, publish, and clean probes also passed. The package SHA-256 is
   `8B3DDABA5B16A91D0518258B8246022688E3E8E042BF03BC079A7D4AFE9BA185`.
5. [x] **F1-INSP — Inspection.** Finalize the passive runtime, recorder,
   snapshot-source, provider, and opt-in lifecycle surface without unfreezing
   broad module instrumentation or privileged actions.
   - **Accepted 2026-08-17:** keep the three concrete public types and their
     passive recorder, snapshot, diagnostics, provider-registration, and
     opt-in global lifecycle members stable. Keep only `RegisterAction`,
     `TryInvokeAction`, `ActionKeys`, and `InspectionRuntimeDiagnostics.ActionCount`
     explicitly experimental under `NEKOEXP0001`; do not expand the action
     channel or introduce remote/control infrastructure. Tighten identity
     validation, preserve ordinal case-sensitive `module::key` identities,
     expose provider and key order by registration, share one outstanding
     budgeted capture task per provider registration, make post-disposal clear
     inert, correct the capacity exception parameter, and retain the internal
     post-install hook solely for deterministic lifecycle testing.
   - **Completed 2026-08-17:** implementation landed in `9f878df`. Inspection
     passed 40/40 tests on `net481` and 40/40 on `net9.0`; the full solution
     passed 1,438/1,438 tests with no failures or skips. The canonical clean
     package build emitted the existing 515-warning baseline and no errors.
     Both API manifests changed only by the four accepted experimental
     attributes; scoped API verification, documentation verification, warning
     identity comparison, and diff hygiene passed. The Observability scenario
     removed passive action probes, then compiled with zero warnings on both
     targets without being launched. Immutable package
     `NekoLib.Inspection.1.0.0-local.19.nupkg` was built from
     `9f878dfd78d997732a010c2d4996396cb0d567fa`, contains both supported target
     assemblies, and declares the aligned `NekoLib.Core` dependency. The
     canonical PackageReference-only WinForms and WPF consumers restored,
     built, and ran on both target families with zero consumer warnings; the
     multitarget, package, deployment, publish, and clean probes also passed.
     The package SHA-256 is
     `8B553A3B7DCC605CB6470E495EAF21E15BD646927C2EB9F5B8BE15E45750E7AF`.
6. [x] **F1-DIAG — Diagnostics.** Finalize incident collection, crash bundle,
   dump-writer, external notification, ownership, and partial-evidence
   contracts.
   **Accepted 2026-08-17:** retain all six public types as stable candidates with
   one removal and one addition. Remove the obsolete `NotifyWatchdog` gate, whose
   Watchdog policy moved to composition in E6 and whose stated preservation window
   closed with Phase E; add `CrashBundleFailed`/`CrashBundleFailedEventArgs` so a
   lost bundle is observable. Capture option values at construction and copy
   `TailFiles`; make `Dispose` terminal with `ObjectDisposedException` on a later
   `Install`; release the `AppDomain`/`TaskScheduler` subscriptions when the last
   handler is disposed and call `SetObserved` only when a handler recorded the
   report; abandon a contributor after its budget plus a 50 ms settle margin;
   enforce the three evidence limits locally; guard per-record `ToString`;
   disambiguate colliding tail names; and redact the crash-text block as one
   bounded batch. Subscriber budgeting, the redaction boundary, concurrent-report
   de-duplication, the reserved `DumpPath`, and CRASH-01's retained Windows
   vocabulary are documented rather than changed. WIN-03b was resolved as
   documentation: a native thread id is not obtainable portably, so `crash.txt`
   labels its value `ManagedThreadId` and the reference explains stack-trace
   correlation instead. A platform-neutral artifact contract, a total incident
   budget, budgeted subscribers, `MaxExtraLines`, a nullable `DumpPath`, and a
   reversible `Dispose` were all rejected. The accepted rationale, evidence, and
   rejected alternatives are recorded in
   [`docs/audit/diagnostics-public-api-review-2026-08-17.md`](docs/audit/diagnostics-public-api-review-2026-08-17.md).
   **Implementation landed 2026-08-17.** Diagnostics passed
   16/16 tests on `net481` and 16/16 on `net9.0-windows`, up from 7 per target;
   the module rebuilt with 0 warnings and 0 errors on both targets. Both
   `NekoLib.Diagnostics` API manifests changed by exactly the accepted delta — the
   new event and event args added, `NotifyWatchdog` and its `[Obsolete]` removed —
   and scoped API verification, documentation verification, and diff hygiene
   passed. A dedicated Diagnostics reference now owns the ownership, lifecycle,
   budget, bounds, redaction, and bundle-layout contracts for both this package
   and `NekoLib.Diagnostics.Windows`.
   - **Completed 2026-08-18:** independent final review found and fixed the
     `Install()`/`Dispose()` registry race in `63785cc`; Diagnostics then passed
     22/22 tests on each target and its API remained unchanged. The coordinated
     clean package flow passed 1,538/1,538 tests, rebuilt with 464 warnings and
     zero errors, introduced no warning identity, and stopped emitting 25
     baseline identities. PackageReference-only WinForms and WPF consumers
     restored, built, and ran on both target families with zero warnings; all
     multitarget, package, deployment, publish, and clean probes passed.
     `NekoLib.Diagnostics.1.0.0-local.20.nupkg`, built from
     `63785cc8bb801f1d4a90ade6cffb7f0b42c6bc1b`, contains `net481` and
     `net9.0` assemblies with aligned `NekoLib.Core` dependencies. SHA-256:
     `D97024B5E7D486D71F4F00A9244C482028997535088EA03E5795789363B7C2D7`.
7. [x] **F1-WIN — Diagnostics.Windows.** Finalize Windows crash hooks,
   minidump composition, platform behavior, and the Diagnostics package
   boundary.
   **Accepted 2026-08-17:** retain both public types and all three members as
   stable candidates with no addition, removal, internalization, or experimental
   marker, and correct behavior only. `HookWinForms` best-efforts the
   application-wide mode change in its own guard and always attempts the
   forwarding subscription, because a window created before the call made the
   mode change throw and silently skipped the subscription, leaving a WinForms
   application with no UI-thread crash reporting at all. `MiniDumpWriter` passes a
   NULL exception parameter when no native exception is in flight on the calling
   thread, instead of labelling the dump with the Diagnostics contributor thread
   and a null exception context, and deletes the file it created when the native
   call does not succeed. `CrashSuppressor.Enable` merges into the current process
   error mode instead of replacing it. `MiniDumpWriter` stays internal behind the
   `WindowsCrash` facade, `Nullable` stays disabled, and no reversible unhook is
   added — `CrashHandler`'s registry already controls recipients. The dump-level
   mapping, its non-cumulative nature, the out-of-range fallback, the
   non-terminating UI-thread policy, and the missing nullability annotations are
   documented rather than changed. WIN-01 was reverified and is closed. The
   accepted rationale, evidence, and rejected alternatives are recorded in
   [`docs/audit/diagnostics-windows-public-api-review-2026-08-17.md`](docs/audit/diagnostics-windows-public-api-review-2026-08-17.md).
   **Implementation landed 2026-08-17.** The three
   Diagnostics assumptions the review depended on were reverified against the
   landed F1-DIAG implementation before any code was written: the dump writer
   still runs on a `CrashHandler` contributor thread, and option values are
   captured by the constructor. The shared Diagnostics suite passed 21/21 on
   `net481` and 21/21 on `net9.0-windows`, up from 16 per target, and both
   projects rebuilt with 0 warnings and 0 errors. Both `NekoLib.Diagnostics.Windows`
   API manifests verified **unchanged**, as did both `NekoLib.Diagnostics`
   manifests; documentation verification and diff hygiene passed. The package's
   contracts are owned by the Diagnostics reference, following the Navigation
   adapter precedent.
   - **Completed 2026-08-18:** the coordinated `local.20` gate above validated
     the unchanged Windows API and produced
     `NekoLib.Diagnostics.Windows.1.0.0-local.20.nupkg` from `63785cc`. It
     contains `net481` and `net9.0-windows7.0` assemblies with aligned
     `NekoLib.Diagnostics` dependencies. SHA-256:
     `3D2AFE1F3178E5A9212C93BDF0EA1FBFECAE22E4C79C731A94B27D96B9B7CA64`.
     This is build/package evidence only: no minidump, crash, WER dialog, or
     live WinForms message-loop crash was exercised.
8. [x] **F1-HTTP — HTTP.** Finalize the typed catalog, relative URI, request,
   response-evidence, ownership, and bounded-buffer contracts without adding
   policy or credentials.
   **Accepted 2026-08-17:** retain all 16 public types as stable candidates with
   no experimental marker, one accessibility reduction, three additive
   properties, and behavior corrections only. Reduce the `HttpEndpoint`
   constructor to `private protected`, since `CreateRequest` is
   `internal abstract` and external derivation already failed with `CS0534`; make
   charset resolution non-throwing with a UTF-8 fallback, because the runtime
   code-page difference made the same response succeed on `net481` and throw on
   `net9.0`; add `StatusCode`, `ReasonPhrase`, and `Headers` to
   `HttpResponseContentTooLargeException`; normalize option validation to
   `ArgumentException` naming `options`; and distinguish "name not registered"
   from "a different instance is registered under this name". The two identity
   models, introspection-only `Get`, relative-URI edge behaviors, the
   absolute-route escaping guarantee, message ownership, per-target timeout
   shapes, emitted content type, header merging, the manifest's nullability blind
   spot for `Value`, and the public Newtonsoft boundary are documented rather than
   changed. **`System.Text.Encoding.CodePages` was explicitly not added**; the
   application registers the provider when it needs byte-accurate legacy
   decoding. Retries, credentials, resilience, a process-wide registry, a
   `byte[]` body, and `System.Text.Json` on `net9.0` were all rejected. The
   accepted rationale and rejected alternatives are recorded in
   [`docs/audit/http-public-api-review-2026-08-17.md`](docs/audit/http-public-api-review-2026-08-17.md).
   **Implementation landed 2026-08-17.** HTTP passed 29/29
   tests on `net481` and 29/29 on `net9.0`, up from 16 per target, and the module
   rebuilt with 0 warnings and 0 errors. Both `NekoLib.Http` API manifests changed
   by exactly the accepted delta — the protected constructor removed, the three
   evidence properties added — and scoped API verification, documentation
   verification, and diff hygiene passed. The existing module reference gained the
   identity, relative-URI, ownership, encoding, header, timeout, and dependency
   sections it lacked. No external request was sent, no credential was
   configured, and the TheCatAPI scenario was neither built nor run.
   - **Completed 2026-08-18:** the coordinated `local.20` gate above produced
     `NekoLib.Http.1.0.0-local.20.nupkg` from `63785cc`, containing `net481` and
     `net9.0` assemblies and `Newtonsoft.Json 13.0.3` in both dependency groups.
     SHA-256:
     `545833DC1303B32ABF6C4A25FE753B9D8B19CA7555896C426D28F3133A1423D5`.
     No external request was sent and no credential was configured.
9. [x] **F1-MVVM — Mvvm.** Finalize the deliberately small binding helper and
   command surface.
   **Accepted 2026-08-17:** the entire surface — 3 public types and 15 public or
   protected member declarations — is intentionally stable, with no removal,
   addition, rename, namespace move, internalization, deprecation, or
   experimental marker. Correct the public nullability contract so it matches
   `ICommand`, `INotifyPropertyChanged`, and the module's own behavior, and make
   `OnPropertyChanged` virtual so one override intercepts every notification.
   Parameter coercion, `Execute` not consulting `CanExecute`, propagating
   subscriber exceptions, `SetProperty` equality semantics,
   `OnPropertyChanged(null)` meaning every property, caller-driven
   `CanExecuteChanged`, and unguarded reentrancy are documented rather than
   changed. A framework-wide MVVM architecture, DI, service location, async
   commands, Navigation coupling, WPF/WinForms adapters, a forwarding facade,
   `CommandManager` integration, `Convert.ChangeType` coercion, gating `Execute`,
   isolating subscriber exceptions, and a reentrancy guard were all rejected. The
   accepted rationale and rejected alternatives are recorded in
   [`docs/audit/mvvm-public-api-review-2026-08-17.md`](docs/audit/mvvm-public-api-review-2026-08-17.md).
   **Implementation landed 2026-08-17.** Mvvm passed 34/34
   tests on `net481` and 34/34 on `net9.0-windows`, up from 22 per target, and the
   module rebuilt with **0 warnings**, down from 20 nullable warnings. Both
   `NekoLib.Mvvm` API manifests changed by exactly the accepted delta —
   annotations across all three types plus `virtual` — and scoped API
   verification, documentation verification, and diff hygiene passed. A dedicated
   Mvvm reference now owns the coercion table, notification, threading, exception,
   and nullability contracts. A measured correction to the review is recorded in
   its reconciliation: the annotation change is not warning-free for consumers,
   because `Action<object?>` surfaces a true-positive `CS8602` in a lambda that
   dereferences the command parameter. MVVM-10's test-target observation was
   deliberately **not** acted on; the F1 validation contract names
   `net9.0-windows`. No WinForms or WPF binding pipeline was driven.
   - **Completed 2026-08-18:** the coordinated `local.20` gate above produced
     `NekoLib.Mvvm.1.0.0-local.20.nupkg` from `63785cc`, containing dependency-free
     `net481` and `net9.0` assemblies. SHA-256:
     `4A44B2EC7D519EB658619A37F82EB8463112D360C4BF21F2AFAD86BAB56CBBBE`.
10. [x] **F1-DEV — Devices.** Finalize `HardwareEngine`, transport/protocol
    extension contracts, configurations, timeouts, cancellation, and public
    models without adding a general forwarding facade.
    **Accepted 2026-08-17, DEV-01 remedy revised at the gate:** retain 17 of 18
    public types as stable candidates. Remove `HardwareProtocol`, a public
    abstract type with one meaningless member and no derived type anywhere. Add
    the opt-in `HardwareEngine.CloseTransportOnNoResponse` — **replacing the
    originally recommended pre-write drain**, which narrowed but never closed the
    hazard and required a new optional interface, whereas closing on an empty
    response uses `Close()` and the buffer-clearing `Open()` that already exist —
    plus `SerialPort.DiscardInBuffer` on open so the serial boundary is
    symmetric. Add `HardwareResponse.Failure` so fail-soft responses keep their
    exception evidence; stop writing the resolved endpoint back into a
    caller-owned `SerialConfig` and hand the transport a copy; annotate the
    null-returning reads, `ParseResponse`, and `Log`; gate serial disposal; and
    make `Checksum` null handling consistent. Logging ownership transfer, the
    `PortName` setter asymmetry, lossy `RawText` on binary payloads, ASCII text
    paths, public mutable model fields, serial thread occupancy, and the
    `System.IO.Ports` public dependency are documented rather than changed.
    **No project reference was added**: no Core, no Pipes, no facade, and the B4
    Inspection freeze is untouched. The accepted rationale, the withdrawn drain,
    and rejected alternatives are recorded in
    [`docs/audit/devices-public-api-review-2026-08-17.md`](docs/audit/devices-public-api-review-2026-08-17.md).
    **Implementation landed 2026-08-18.** Devices passed
    50/50 tests on `net481` and 50/50 on `net9.0`, up from 40 per target,
    including both operation-boundary regressions over a real loopback TCP peer —
    one pinning the unchanged default, one proving the opt-in keeps a late reply
    out of the next operation. A clean rebuild emitted 22 warnings, **down from
    40 and introducing no new identity**. Both `NekoLib.Devices` API manifests
    changed by exactly the accepted delta; scoped API verification, documentation
    verification, and diff hygiene passed. The com0com scenario **compiles with 0
    warnings** against the new nullable contract — build-only evidence; it was
    not launched, and no serial port was opened at any point. A dedicated Devices
    reference now owns the ownership, boundary, failure, encoding, and disposal
    contracts.
    - **Completed 2026-08-18:** the coordinated `local.20` gate above produced
      `NekoLib.Devices.1.0.0-local.20.nupkg` from `63785cc`, containing `net481`
      and `net9.0` assemblies. Its dependency groups declare
      `Microsoft.Bcl.AsyncInterfaces 10.0.1` and `System.IO.Ports 9.0.0`,
      respectively. SHA-256:
      `659A076F85B11038A9C988E9ECE863BC814422166A9E4CD84768763DE0011CC6`.
      The gate adds package evidence, not serial evidence: com0com was not
      launched and no serial port was opened.
11. [ ] **F1-PIPE — Pipes.** Finalize client/server, event, metrics, framing
    boundary, shutdown, security-policy, and error contracts.
    **Accepted 2026-08-18:** capture and validate client/server configuration;
    make server, event-hub, and event-client shutdown terminal, race-safe, and
    awaitable across both targets; remove the stateless `PipeClient` disposal
    surface; isolate all transport-owned metrics callbacks and seal
    `SimplePipeMetrics`; preserve bounded best-effort event queues while
    rejecting oversized events before enqueue; add isolated event-client error
    observation and ordered connection notifications; publish constants for the
    four framework wire-error codes; and make in-flight `net481` connects honor
    cancellation. Retain the target-specific `PipeMessage.Data` types and the
    `net481` Newtonsoft dependency, subject to a mixed-target separate-process
    wire probe before the first stable release. Retain opt-in
    `CurrentUserOnly`, compatibility-default `PlatformDefault`, and
    application-owned authorization; do not add authentication, replay, remote,
    privileged-control, Core, or Inspection infrastructure. Classify the
    remaining surface as stable, with no experimental API or deprecation window.
    The accepted rationale, compatibility impact, migration, and rejected
    alternatives are recorded in
    [`docs/audit/pipes-public-api-review-2026-08-18.md`](docs/audit/pipes-public-api-review-2026-08-18.md).
    Keep this item open until the implementation, dual-target regressions,
    reviewed API manifests, current technical reference, changelog, migration
    guidance, and focused validation are complete.
12. [ ] **F1-WDOG — Watchdog.** Decide whether `WatchdogRuntime` is a supported
    advanced consumer surface or Host infrastructure, then finalize bootstrap,
    controller, options, IPC, process, and ownership contracts.
13. [ ] **F1-WDOG-HOST — Watchdog Host.** Finalize the deployment package,
    payload layout, build targets, bootstrap arguments, and Host/application
    protocol rather than treating the executable as a compile-time API.
14. [ ] **F1-NAV — Navigation.** Finalize the public facade, contracts,
    registration, lifecycle, history, guard, session, surface, and diagnostic
    API last. Review does not unfreeze `NavigationContext`,
    `NavigationRuntime`, `PageRegistry`, or `PageFactory`.
15. [ ] **F1-NAV-WF — Navigation.WinForms.** Finalize the adapter, native host,
    base-view, dispatcher, timer, interaction, and surface contracts against the
    accepted Navigation API.
16. [ ] **F1-NAV-WPF — Navigation.Wpf.** Finalize the matching WPF adapter and
    base-view contracts, preserving intentional platform differences.

#### F1 completion gate

- [ ] Every shipped library package and the Watchdog Host deployment contract
  has an accepted classification, current documentation, and a reviewed API or
  protocol baseline for each supported target.
- [ ] Every accepted breaking change has a changelog entry, migration guidance,
  and coordinated release target; no accidental diff remains.
- [ ] The final clean package-family candidate passes the canonical package
  flow and external PackageReference consumer probes without `-SkipTests`.
- [ ] The first stable family release and its baseline are declared explicitly;
  completing module reviews alone does not publish or promise that release.

**Recorded inputs — public-surface changes made before F1.** These are
historical facts for the first candidate baseline and changelog reconciliation,
not new implementation items.

- **2026-08-03, NAV-008(c).** `NekoLib.Navigation.Wpf.Adapters.InteractionObserver`
  and `NekoLib.Navigation.Wpf.Adapters.EventSubscriptionAdapter` were removed.
  Both were public, both were dead: `WpfPlatformAdapter` produces
  `WpfInteractionObserver` and `WpfEventSubscriptionAdapter` instead, and a
  repository-wide search found no other reference. An external consumer that
  constructed either type directly would break.
- **2026-08-03, NAV-008(g).** `IPageView.Name` seeding on the WinForms base
  classes changed from `GetType().FullName` to `GetType().Name`, matching WPF.
  Anything keying off the fully qualified value — including WinForms
  `Controls.Find` by name — would break. The descriptor name was and remains
  authoritative for registration and history.
- **2026-08-03, NAV-009(b).** `Dispose()` on the four WPF surface bases
  (`DialogViewBase`, `PromptViewBase<TResult>`, `PopoverViewBase`,
  `ToastViewBase`) became `virtual`. Source-compatible in both directions, but
  non-virtual to virtual is a binary-breaking change: an external assembly
  compiled against the old signature must be recompiled. WPF `PageView` was
  already virtual.

### F2 — Automated release confidence

**GATED — DO NOT START.** Evaluate Windows CI by reusing the existing `eng/`
scripts to build `net481` and `net9.0`/`net9.0-windows`, run full tests and
documentation verification, compare warning identities, produce disposable
package validation, run package-consumer probes, and verify Watchdog Host
payloads. Do not create a second build/pack pipeline. CI would prepare for more
consumers and contributors, not create an enterprise platform.

### F3 — Measured performance and resource budgets

**GATED — DO NOT START.** Derive budgets from Phase E measurements. Benchmark
only confirmed hot paths and define evidence-based expectations for Navigation
latency, Logging overhead, Telemetry/Inspection memory bounds, Pipes throughput
relevant to actual use, meaningful Data mapping/query overhead, Watchdog
recovery, and memory growth during unattended operation. Do not redesign
synchronous Logging, Telemetry retention, or Navigation lifecycle without
measured evidence.

### F4 — Optional external evidence export

**GATED — DO NOT START.** Only after a demonstrated deployment requirement,
evaluate crash-bundle upload, log export, telemetry export, offline buffering,
retry/backoff, redaction, application/device identity, and transport ownership.
Keep boundaries small. Do not add a backend implementation to Core, dashboard
implementation to Diagnostics, remote administration to Watchdog, cloud SDK
dependency to feature modules, or an enterprise observability stack by
default.

### F5 — Fleet-management assessment

**GATED — DO NOT START.** This is an assessment, not an assumed framework
feature. If managing many installed terminals becomes an accepted product goal,
evaluate a companion product or agent for installation/device identity,
enrollment, authentication, remote configuration, update distribution,
rollback, credential rotation, crash/log/telemetry collection, offline
behavior, a central API, and an operator dashboard.

NekoLib base remains a local application framework. Fleet management must not
be forced into Core, Navigation, Diagnostics, or Watchdog. Watchdog remains a
local supervisor unless a separate product direction is accepted. Update
orchestration being `not_implemented` is not a confidence blocker for the
current application-framework scope. A future fleet agent may consume NekoLib
packages without becoming part of the base framework.

### F6 — Intentional portability preparation

**GATED — DO NOT START.** Only if Linux or another platform becomes an accepted
target, preserve platform-neutral Core contracts, keep Diagnostics.Windows
isolated, audit actual WinAPI use, and define a platform adapter only for a real
runtime target. Do not dilute current Windows behavior prematurely or claim
portability merely because a library targets plain `net9.0`.

### F7 — Opt-in Navigation surface regions and toast orchestration

**GATED — DO NOT START.** Design an opt-in surface-region model whose first
concrete consumer is stacked toast notification. A region is a visual and
lifetime scope—sometimes described as a pseudo-page—but it must not participate
in `Current`, navigation history, guards, page reuse, or the canonical page
lifecycle.

The accepted direction is:

- allow shell-, page-, dialog-, prompt-, or other surface-owned regions, with
  explicit ownership so closing an owner removes its visible child views,
  cancels timers, and disposes pending entries;
- keep the existing single/replacement toast behavior as the default and make
  region-based stacking and queuing explicitly opt in;
- let a toast region define its visible capacity, deterministic stack/reflow
  order, and a bounded pending queue with an explicit overflow rule; queued
  entries should defer native-view creation where practical, and their lifetime
  timer starts only when they become visible;
- give independently dismissible entries stable identity or a handle rather
  than extending the ambiguous `DismissCurrentToast()` model to a collection;
- add opt-in surface capabilities such as `IsMovable` without expanding page
  presentation enums. Region-managed views remain presenter-positioned unless
  an explicit detach/reorder interaction is later accepted;
- make auto-dismiss opt in. When enabled without a more specific dismissal
  strategy, use the bounded timer as the default fallback; owner teardown and
  explicit dismissal always cancel it deterministically;
- keep hit testing, drag capture, positioning, DPI/resize behavior, and native
  Z-order in the WinForms/WPF adapters. Empty region space must not consume
  input, and a drag must not be interpreted as click-to-dismiss;
- preserve UI-thread mutation, transactional setup/cleanup, bounded state,
  surface diagnostics, current teardown guarantees, and dual-target tests.

Do not introduce nested `NavigationContext` instances, generic region routing,
or a new feature-module family for the toast use case. If implementation proves
that a frozen Navigation component must change, stop at the confirmed boundary
and require an explicit, narrowly scoped unfreeze before modifying it.

## Explicit non-goals

Outside the explicitly active F1 and promoted Phase G1 boundaries, and while
F2-F7 remain gated, do not create a generic application host,
Neko-specific DI container, Microsoft DI wrapper, global service registry,
message bus, event bus, universal exception policy, broader HTTP client
abstraction, API gateway, ORM expansion, repository/unit-of-work framework,
scheduler, job engine, distributed cache, configuration framework, secret
manager, plugin platform, Instrumentation project family, TestControl project,
generic remote debugger, cloud backend, dashboard, updater inside Watchdog, or
fleet-control plane.

These candidates require a real use case and an explicit decision before they
may enter the roadmap.

## Completed phases and history

- Phases A, B, and D are complete and historical. See the
  [`architecture roadmap through Phase D`](docs/history/architecture-roadmap-through-phase-d-2026-08-01.md).
- Phase C repository hygiene is complete. Its commit-bound validation remains
  historical evidence in the
  [`Phase C completion snapshot`](docs/history/phase-c-repository-hygiene-2026-08-01.md).
- Phase E confidence stabilization is complete. Its full work log, evidence
  classification, validation, and optional residual confidence are preserved in
  the
  [`Phase E completion snapshot`](docs/history/phase-e-confidence-stabilization-2026-08-12.md).

Historical test, warning, project, package, and runtime counts remain in their
dated, commit-bound snapshots and are not repeated in this live roadmap.
