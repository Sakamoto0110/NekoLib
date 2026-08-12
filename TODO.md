# NekoLib Live Roadmap

**Kind:** roadmap/status

**Lifecycle:** current

**Subject:** current direction, live work, accepted future work, active freezes,
phase gates, and completion criteria

**Direction decision date:** 2026-08-12

Audit snapshots are indexed under [`docs/audit/`](docs/audit/README.md).
Completed roadmap and validation history is indexed under
[`docs/history/`](docs/history/README.md). Neither directory is a live issue
tracker.

## Current project direction

Phase E confidence stabilization is complete and archived. Its implementation
log, outcome-first runtime evidence, residual confidence options, and final
commit-bound validation are preserved in the
[`Phase E completion snapshot`](docs/history/phase-e-confidence-stabilization-2026-08-12.md).

No implementation phase is currently active. Phase F remains gated and must be
promoted explicitly before any candidate below becomes live work. Completing
Phase E did not activate Phase F automatically.

Until another phase is promoted:

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

## Phase F — Scale preparation (gated)

> **Status: GATED — NOT ACTIVE.** Phase E is complete and archived, but that
> does not activate these candidates. Explicit promotion is still required.
> Until then, do not implement them, investigate them incidentally, or create
> files for them.

### F1 — Public API and release stability

**GATED — DO NOT START.** Define a SemVer policy; stable versus experimental
APIs; coordinated package-family compatibility; changelog and real migration
guidance; automated public API compatibility checks; breaking-change approval;
deprecation policy; and a support window if multiple package versions are
maintained.

**Recorded inputs — public-surface changes already made.** Not F1 work; these
are facts the policy will have to account for when it is written.

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

While Phase F remains gated, do not create a generic application host,
Neko-specific DI container, Microsoft DI wrapper, global service registry,
message bus, event bus, universal exception policy, HTTP client abstraction,
API gateway, ORM expansion, repository/unit-of-work framework, scheduler, job
engine, distributed cache, configuration framework, secret manager, plugin
platform, Instrumentation project family, TestControl project, generic remote
debugger, cloud backend, dashboard, updater inside Watchdog, or fleet-control
plane.

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
