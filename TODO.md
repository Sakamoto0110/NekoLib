# NekoLib Live Roadmap

**Kind:** roadmap/status

**Lifecycle:** current

**Subject:** current direction, live work, accepted future work, active freezes,
phase gates, and completion criteria

**Direction decision date:** 2026-08-01

Audit snapshots are indexed under [`docs/audit/`](docs/audit/README.md).
Completed roadmap and validation history is indexed under
[`docs/history/`](docs/history/README.md). Neither directory is a live issue
tracker.

## Current project direction

> NekoLib is not feature-incomplete; it is confidence-incomplete in a few
> areas.

NekoLib has the feature families required for its current PDV/DM application
scope. The active objective is to stabilize confidence in the existing
framework through current reviews, real integration evidence, long-running
scenarios, recovery validation, stability contracts, and targeted hardening.
New feature families are not the current priority.

**Current intention:** stabilize confidence in the existing framework.

**Future intention:** prepare the stabilized framework to scale for larger and
more critical consumer applications. This future intention is separate from
confidence stabilization and does not imply fleet management, a central
backend, or a universal application runtime.

Future scale preparation is deliberately gated. No Phase F task may start
until every Phase E exit criterion is complete, Phase E is archived, and Phase
F is explicitly promoted.

During confidence stabilization:

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
- do not generalize application-specific infrastructure into the framework;
- do not treat hypothetical fleet requirements as current product
  requirements;
- distinguish automated, build-only, manual, interactive, package, provider,
  and hardware evidence truthfully;
- do not treat a completed review, passing unit tests, a successful build, or
  the absence of known findings as proof of real or long-running runtime
  behavior.

The current module map, targets, dependency graph, public entry points, and
package overview remain owned by [`README.md`](README.md), current project
files, and source. The coordinated package workflow uses immutable versions,
package-consumer probes already exist, validation remains manually triggered,
and Windows is required for full dual-target validation.

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
control is unnecessary for confidence stabilization, and no implementation
task for this idea is accepted by this roadmap.

### Navigation stability-sensitive core

The following components remain frozen after the accepted lifecycle and trace
correction:

- `NavigationContext`;
- `NavigationRuntime`;
- `PageRegistry`;
- `PageFactory`.

The WinForms/WPF adapter review does not authorize changes to these components.
If an adapter finding appears to require a runtime change, it must first be
confirmed by the review, record evidence, be promoted selectively to this
roadmap, receive an explicit module-scoped unfreeze, and preserve the canonical
lifecycle invariants. The freeze is restored after the authorized scope.

## Phase E — Confidence stabilization

**Status:** active.

**Authorization:** active review and validation planning. This roadmap does not
confirm speculative findings, start the listed reviews or scenarios merely by
existing, or authorize product-code fixes before a finding is confirmed, its
impact is understood, a direction is accepted, and implementation is genuinely
intended. Phase E creates no new feature modules.

Execute E1-E7 in order unless an explicitly recorded dependency justifies a
different sequence.

### E1 — NekoLib.Data review and accepted stabilization

- [x] Complete a current, code-first, commit-bound review of `NekoLib.Data`.
  The review and its executable evidence are preserved in the historical
  [`Data stabilization review`](docs/audit/data-stabilization-review-2026-08-01.md)
  against `master` at
  `628442a58cdf2e2374cc7e48fa10d394d3fc3b87`. Both target frameworks built,
  and the existing 23 tests passed on each target. No real provider execution
  was claimed.

**Promotion decision — 2026-08-01:** the confirmed findings below have accepted
implementation directions and genuine implementation intent. They are now
authoritative only in this roadmap; the historical review retains evidence,
alternatives, provider research, and the complete reconciliation without
owning implementation status. This promotion does not unfreeze broad Inspection
instrumentation, add a Core reference to Data, add a provider package to the
relational core, or authorize a MongoDB project.

**Accepted audit conclusion:** the Data foundation is small and understandable,
but its current confidence gaps are correctness, lifecycle, mapping, ownership,
and executable provider evidence—not missing database brands. Phase E therefore
stabilizes fail-closed query construction, authoritative operation outcomes, one
typed-mapping contract, the minimum provider/session seam, dynamic-result
lifetime, and contract tests. E4 owns real-provider execution and may promote
only adaptations demonstrated by that evidence.

#### E1.1 — Fail-closed query construction

- [x] **DATA-001 — Make collection predicates and DML fail closed.** Empty
  `IN` produces a constant-false predicate; empty `NOT IN` produces a
  constant-true predicate. Null collections and empty column names are caller
  errors. Reject an `UPDATE` without a predicate unless the caller uses an
  explicit all-rows opt-in. Cover empty, null, single, and multiple values and
  the all-rows guard on both target frameworks.
- [x] **DATA-008 — Make QueryBuilder state transitions explicit.** Start with
  an undefined query kind, preserve repeated `Build()` idempotence, and make
  each query/projection mode replace or reject incompatible state rather than
  retaining columns, `Distinct`, `Count`, `Top`, joins, or predicates from an
  earlier statement.
- [x] **DATA-009 — Validate condition-template placeholders.** Require exact
  placeholder arity, tokenize only the supported template grammar, and reject
  missing or unused values before translation. Keep raw fragments explicitly
  trusted; defer public member renames to the API-stability phase.
- [x] **DATA-010 — Fail fast for unsupported limited subqueries.** Until nested
  query models are translated recursively, reject `Top` inside a subquery with
  an actionable exception instead of silently dropping it. Preserve subquery
  parameter isolation.
- [x] **DATA-020 — Make the raw-fragment trust boundary explicit.** Update
  public XML documentation and tests so table, column, join, grouping,
  ordering, and raw-condition strings are never described as protected by
  value parameterization. Provider-specific identifier quoting remains part of
  an accepted provider adapter, not one universal quoting rule.

#### E1.2 — Authoritative operation and stream outcomes

- [x] **DATA-002 — Isolate query-event subscribers.** Keep event delivery
  synchronous and ordered, invoke subscribers individually, and ensure a
  throwing observer cannot prevent dispatch, turn a committed operation into a
  failure, or mask the provider exception. Synchronous subscriber latency
  remains part of the call and must be documented; this fix does not introduce
  a background queue. Capture observer failures through a bounded,
  non-recursive Data-local mechanism while preserving SQL and result redaction
  defaults. Do not add Core or Inspection dependencies.
- [x] **DATA-011 — Give every stream exactly one terminal outcome.** Report
  completed, failed, cancelled, or disposed-before-completion from the stream
  lifetime, including early consumer disposal and empty-schema termination.
  Resource cleanup and the database outcome remain authoritative even if a
  terminal-outcome subscriber fails.

#### E1.3 — One strict typed-mapping contract

- [x] **DATA-004 — Stop suppressing mapping failures.** Add a structured
  mapping exception with column/property/source/target evidence and a
  deliberate conversion matrix. Strict failure is the default; legacy lenient
  behavior is available only through an explicit compatibility option.
- [x] **DATA-005 — Remove the invalid universal typed fallback.** Keep DTO,
  `DynamicRow`, and raw paths separate; validate target construction and
  delegate shape before opening a connection; never pass or cast a
  `DynamicRow` as an unrelated DTO.
- [x] **DATA-006 — Use one reader-to-DTO pipeline.** Compile and reuse one
  binding plan per schema and target type across buffered, callback, and
  streaming APIs, with parity tests for nulls, enums, binary values, date/time,
  numeric overflow, and unsupported conversions.

**Accepted compatibility boundary:** DATA-017 does not redefine `RecordItem`.
It remains an explicitly lossy display/export model. A separate lossless raw
row type is not implementation work until a concrete consumer requires it.

#### E1.4 — Provider, command, connection, and session seam

- [x] **DATA-003 — Bind positional providers by SQL occurrence.** Introduce a
  small marker/binder seam; for OleDb, tokenize placeholders outside literals
  and comments, render positional markers, and bind once per occurrence.
  Reject missing parameters and define unused-value handling. Cover reversed,
  repeated, quoted, commented, and prefix-colliding placeholders before the
  real Access validation in E4.
- [x] **DATA-007 — Make synchronous fallback explicit.** Native async is the
  default requirement. A provider that needs synchronous open/execute/read must
  use an explicit opt-in policy, disabled by default, with cancellation checked
  before the blocking call and its in-flight limitation documented. Do not use
  `Task.Run` as a cancellation guarantee.
- [x] **DATA-013 — Add portable parameter and command policy.** Introduce a
  multi-target ordinary-class parameter specification for `DbType`, size,
  precision, scale, direction, and null value. Add explicit command-timeout
  defaults and overrides. Keep all concrete provider packages outside
  `NekoLib.Data`; introduce a provider-native hook only if E4 evidence proves
  that the portable metadata cannot express an accepted provider requirement.
- [x] **DATA-014 — Make factory and session ownership explicit.** Support
  explicit context-owned versus externally owned factories and validate session
  connection state and affinity before command creation. Preserve the existing
  generic string factory as a compatibility adapter. Add data-source-style or
  cancellable create/open contracts only when an E4 provider demonstrates the
  need and the ownership behavior is covered by provider-independent tests.
- [x] **DATA-015 — Make DML and transaction participation symmetric.** Add
  session-aware QueryBuilder DML overloads, align concrete and interface
  behavior without a namespace move, and allow a valid new transaction after
  commit or rollback while preserving nested-depth rules. Defer new async
  transaction APIs until real-provider evidence establishes a dual-target
  contract.

#### E1.5 — Dynamic-result stability

- [x] **DATA-012 — Align dynamic null and type-lifetime behavior.** Keep
  Expando as the production default. Make IL and Expando null semantics
  equivalent, replace eviction with a process-wide non-evicting schema cap so a
  new schema falls back or fails after the cap instead of causing type
  re-emission, and measure IL value before deciding whether the mode remains
  supported. Per-context options must not pretend to own process-global emitted
  types.

#### E1.6 — Contract tests and source hygiene

- [x] **DATA-018 — Add provider-independent gateway contract tests.** Use fake
  ADO.NET objects to cover connection/command/reader disposal, cancellation,
  observer isolation, mapping, streaming, session affinity, transactions, and
  failure paths on both target frameworks. These tests do not count as real
  provider coverage.
- [x] **DATA-019 — Remove dead and mixed-language source material.** Delete the
  fully commented duplicate `Connection/DbSession.cs` after a final reference
  search and translate public Data XML documentation to English. Keep the
  cleanup separate from behavioral commits where practical.
- [x] After E1.1-E1.5, run Data tests on both targets and the full solution,
  compare warning identities, and append implementation reconciliation to the
  dated Data stabilization review.

#### E1.7 — Explicitly deferred or non-promoted review items

- [x] **DATA-016 — Deferral confirmed.** A namespace move, overload removal,
  and broad public API cleanup are breaking-change work gated by F1. Only the
  non-breaking session parity delivered by DATA-015 was active in E1.
- [x] **DATA-017 — Existing contract confirmed.** Preserve the documented lossy
  `RecordItem` contract; no new raw model is accepted without a consumer.
- [x] **Relational provider boundary confirmed.** Provider candidates remain a
  validation matrix, not support claims or dependencies to add speculatively.
  E4 owns real-provider execution.
- [x] **Non-relational boundary confirmed.** MongoDB remains an
  application-owned native-driver integration during Phase E. Do not model it
  as `IDbQueryTranslator`, emulate SQL over it, or create
  `NekoLib.Data.MongoDB` without a later explicit use-case decision. Redis,
  LiteDB, Elasticsearch/OpenSearch, and specialized stores remain native
  application capabilities, not interchangeable providers for this SQL gateway.

**E1 closure — 2026-08-03:** complete. E1.1-E1.6 are implemented and reconciled
in the dated Data stabilization review. The E1.7 dispositions above were
revalidated against the current Data source, tests, and project topology; no
breaking API cleanup, new raw model, concrete provider dependency, or
non-relational Data module was promoted. Real-provider execution remains open
only under E4, and public API cleanup remains gated by F1.

The completed implementation order was E1.1, then E1.2/E1.3, E1.4, E1.5, and
E1.6. Each behavioral change preserved the accepted shared seam and included
dual-target coverage.

### E2 — Navigation WinForms/WPF adapter review

- [x] Complete a deep native-adapter review without automatically reopening
  the Navigation core.
  **Closed 2026-08-04.** NAV-001…NAV-011 are all closed, including NAV-001's
  native repeat, and both smoke scenarios have been walked end to end — the
  WinForms procedure on `net481` and the WPF procedure on `net9.0-windows`. No
  finding required a change to `NavigationContext`, `NavigationRuntime`,
  `PageRegistry`, or `PageFactory`.

  **→ The complete record is
  [`docs/audit/navigation-adapter-review-2026-08-03.md`](docs/audit/navigation-adapter-review-2026-08-03.md).**
  It holds the reviewed baseline, all eleven findings with their evidence and
  accepted dispositions, the commit that implemented each, the rejected
  alternatives, the automated / build-only / interactive evidence split, the
  public-surface changes recorded for F1, and the residual gaps. Per-step
  interactive results live in each scenario's own verification record:
  [`runtime_tests/Navigation/WinFormsSmoke/README.md`](runtime_tests/Navigation/WinFormsSmoke/README.md)
  and
  [`runtime_tests/Navigation/WpfSmoke/README.md`](runtime_tests/Navigation/WpfSmoke/README.md).

  **Remark — missing more scenarios relatable to the real world.** This is a
  statement of **scope, not of shortfall**. E2 asked for one thing: a deep review
  of the native navigation adapters. PDV/DM-realistic scenarios were never inside
  it, and deliberately so — a review that also had to build a representative
  shell would have gone shallower on the adapters. What was validated is the
  adapter behaviour, through purpose-built smoke scenarios plus one real consumer
  application. Neither resembles a working PDV/DM shell with dense pages,
  competing surfaces, sustained operator input, or hardware in the loop, so
  **read the adapters as reviewed and corrected, not as exercised under realistic
  load.** Closing this entry does not close that gap: E3 owns long-running and
  recovery scenarios, and E4 owns real integration. The residual list in the
  audit artifact names what is still uncovered.

  One caution worth carrying forward: two of the smoke procedures' own steps were
  wrong — WinForms step 6 wants a popover alive while a prompt opens, which
  focus-driven light dismissal forbids, and WPF step 4 wants a guarded page the
  scenario never declares — and neither was noticed until the procedures were
  actually walked by hand. A written procedure is not evidence until someone
  executes it.

WinForms scope:

- UI-thread dispatch, `Control.Invoke`/`BeginInvoke`, handle creation and
  destruction, host lifecycle, and page attach/detach;
- overlay-host behavior for Dialog, Prompt, Toast, and Popover;
- idle interaction tracking, including dynamically added and removed controls;
- shutdown while surfaces are open, form-close behavior, and repeated
  mount/shutdown;
- DPI, resize, focus, multi-monitor positioning where applicable, disposed
  controls, exception propagation, and designer/runtime interaction.

WPF scope:

- Dispatcher behavior, Window lifecycle, host and view attach/detach, overlays,
  idle integration, shutdown, and repeated mounting;
- focus, DPI/resize, exception propagation, parity with intended Navigation
  contracts, and behavior that legitimately differs from WinForms.

**Confirmed design-time finding — 2026-08-06, reconciled 2026-08-08:** the
`designer/runtime interaction` item above is delivered for the WinForms overlay
bases. The
[`Navigation design-time loadability`](docs/audit/navigation-design-time-2026-08-06.md)
review found two defects that no compiler, and no test as the suite then stood,
could see: every surface base was `abstract`, which the WinForms designer cannot
instantiate, and the bases scheduled work through `BeginInvoke` before a handle
existed. The second was never only a design-time problem. Both are implemented in
`73ddbdb` and locked by `SurfaceBaseDesignTimeTests`.

Worth keeping, because it decides where the next such defect gets found: the
review came out of **building a real consuming application against the module**
and opening its pages in the designer, not out of reading the module. The
consumer was the `runtime_tests/Data/FarmDatabase` scenario.

Two residual gaps stay deliberately unscheduled and are recorded in the review
rather than here: `PromptViewBase<TResult>` remains generic, which is why that
scenario carries two shims, and `[DesignerCategory("Code")]` remains an unguarded
trap — applying it to a page instead of a custom-painted control silently makes
the page undesignable.

**Confirmed runtime finding — 2026-08-02:** the PCB emulation scenario observed
the configured 30-second idle tick sign the session out without navigating to
Home. Current source confirms that `NavigationBootstrapLifetime` validates the
interaction generation, calls `Session.SignOut()`, and validates the same
generation again. A synchronous application UI update caused by sign-out can be
reported by the WinForms interaction observer and make that second validation
abort `GoIdleAsync()`.

- [x] **NAV-001 — Preserve an admitted idle transition across sign-out UI
  mutation.** Keep genuine interaction before sign-out capable of invalidating
  the stale idle tick. Once the pre-sign-out continuation check succeeds, treat
  the idle transition as admitted: after `Session.SignOut()`, revalidate
  disposal, `StopIdle()`, and current-context ownership, but do not let an
  interaction-generation increment caused synchronously by sign-out UI updates
  cancel navigation to the resolved idle/Home page. Preserve the documented
  `SignOut()` then `GoIdleAsync()` order and timer rearming on denied or failed
  navigation. Add dual-target regressions in
  `PageNavBootstrapLifetimeTests` for sign-out-induced interaction, genuine
  interaction before admission, and stop/shutdown boundaries, then repeat the
  native 30-second idle scenario. Keep the implementation scoped to
  `NavigationBootstrapLifetime`; if evidence requires a frozen component, stop
  and request a narrow explicit unfreeze.
  **Status 2026-08-03:** implemented. The post-sign-out check now revalidates
  disposal, `StopIdle()`, and context ownership through `CanHandleIdle` but no
  longer compares the interaction generation, so an interaction caused by the
  application reacting to sign-out cannot cancel an admitted transition. The
  pre-admission check still compares the generation, so genuine interaction before
  sign-out still invalidates a stale tick. Three regressions added to
  `PageNavBootstrapLifetimeTests`; the sign-out-induced case was confirmed to fail
  against the previous implementation, and the other two pin that the relaxation
  did not go too far. Navigation suite 244/244 on `net481` and `net9.0-windows`.
  **Outstanding:** the native 30-second repeat. It cannot be reproduced from a
  smoke scenario, because `NavigationSession.Changed` is `internal` and its only
  subscriber is the Inspection observer — there is **no public session-changed
  event**, so no application can react to sign-out the way this finding describes.
  The regressions therefore drive that exact internal seam, and the native repeat
  remains a task for the originating PCB application. This item stays open until
  that repeat is performed.
  **Closed 2026-08-04 — the native repeat was performed.** The originating
  application, `NekoPcbMiddleware`, was moved to the packaged `1.0.0-local.8`
  (provenance `cc89f94`, which descends from this work) and driven on its real
  WinForms shell with a 30 s configured timeout.
  - **Interactive, anonymous session:** two independent idle transitions, each
    left untouched on a non-idle page. `SessionLabPage -> HomePage` and
    `PolicyMatrixPage -> HomePage`, both `trigger = Idle`, `success = True`,
    `decision = Navigated`; the second measured `durationMs = 29993` against the
    30 000 ms interval, and the Inspection idle provider ended at
    `status = Completed, decision = Navigated, navigations = 2`.
  - **Automated, authenticated session** — the app's own `S16.1` scenario, run as
    `--run S16.1 --include-long`, signs in through code and so needs no
    credential entry. It passed 6/6: authenticated before the window, then
    **`the idle timeout signed the session out` → `IsAuthenticated=False`** and
    **`the idle transition navigated to the idle page` → `HomePage`**. Both halves
    of the original symptom, on one authenticated run.
  **Honest limit on what this repeat proves.** It confirms the behaviour now
  holds in the native application; it is **not** a discriminating test. The
  original 2026-08-02 symptom was intermittent — it depended on a synchronous UI
  update racing the tick — and that same day `S16.1` was recorded as passing
  against `1.0.0-local.7`, before the fix. The discrimination for this item
  therefore rests where it always did: on the `PageNavBootstrapLifetimeTests`
  regression that was confirmed to fail against the previous implementation.
  Also observed in the same run: the guard denial reported
  `reason=Authentication required.`, which is NAV-002 reaching a real consumer.

**Confirmed guard-diagnostics finding — 2026-08-03:** the external NuGet
consumer scenario observed `[RequireAuthenticated]` deny an unauthenticated
request with no diagnostic reason, while `[RequireRole]` reports a specific
reason such as `Missing role: administrator`. Current source confirms that
`RequireAuthenticatedGuard` returns `GuardResult.Deny()` without a reason. This
does not change authorization behavior, but leaves `GuardDeniedEvent`, Logging,
and Inspection with an empty or null explanation.

- [x] **NAV-002 — Give authentication denial a stable diagnostic reason.**
  Return `Authentication required.` from `RequireAuthenticatedGuard` when the
  user is not authenticated, while preserving the current allow/deny behavior
  and redirect semantics. Add dual-target regression coverage for authenticated
  and unauthenticated evaluation and for the reason propagated to
  `GuardDeniedEvent` and the Navigation diagnostics bridge. Keep the change
  scoped to guard diagnostics; do not change session or authorization policy.
  **Closed 2026-08-03:** `RequireAuthenticatedGuard` now returns
  `Authentication required.`, matching the reasons the role and permission guards
  already report. The guard was also made non-async, since it had nothing to
  await, aligning it with `RequireRoleGuard`. Six regressions added in
  `RequireAuthenticatedGuardTests` covering authenticated and unauthenticated
  evaluation, a missing user context, the reason reaching `GuardDeniedEvent`
  through a real navigation, an authenticated navigation not being denied, and the
  reason reaching the logging bridge; three of them were confirmed to fail against
  the previous implementation. Navigation suite 241/241 on `net481` and
  `net9.0-windows`. Allow/deny and redirect behaviour are unchanged.

**Confirmed adapter-review findings — 2026-08-03:** the code-first E2 adapter
review ran against `ae1781086b3858cdc9cb025473ed18e3445ee1eb`, on a clean
worktree on branch `navigation-claude`. Both
adapters built on `net481` and `net9.0-windows`, the 222 Navigation unit tests
passed on both target families, and the WPF smoke scenario built. Only eight of
those tests touch a native adapter, so every item below was confirmed from
current source plus executed framework probes, never from interactive evidence.
The items are ordered by impact. None of them requires a change to
`NavigationContext`, `NavigationRuntime`, `PageRegistry`, or `PageFactory`, and
none of them authorizes one.

- [x] **NAV-003 — Give WPF surfaces real keyboard focus.**
  `WpfLayeredPageHostBase.Focus(object)` guards on `UIElement.Focusable`, and
  `System.Windows.Controls.UserControl` overrides that default to `false`, so the
  guard makes `Focus` an unconditional no-op for `PageView`, `DialogViewBase`,
  `PromptViewBase<TResult>`, `PopoverViewBase`, `ToastViewBase`, and
  `DefaultLoadingMask`. Measured on instances: `Control` and `ContentControl`
  report `Focusable = true`, `UserControl` reports `false`, and a direct
  `UserControl.Focus()` returns `false`. After `ShowDialogAsync`,
  `Keyboard.FocusedElement` is still the `Window`, so keyboard input never
  reaches the modal until the user clicks it. WinForms is correct: the container
  forwards focus to the surface's first selectable child. The WPF smoke scenario
  hides the gap because `SamplePrompt` and `SamplePopover` focus an inner control
  from `OnShownAsync`, and `SampleDialog` relies on `IsDefault`/`IsCancel`, which
  act at window scope. Make the WPF `IViewHost.Focus(object)` place keyboard
  focus inside the target subtree — first focusable descendant, falling back to
  the element itself only when it is genuinely focusable. Preserve the documented
  surface order (`AddView` → `OnViewAdded` → `BringToFront` → `Focus` →
  `OnShownAsync`), and keep a view that focuses its own control from
  `OnShownAsync` winning. Add dual-target regressions asserting that focus lands
  inside the surface for dialog, prompt, and popover, then rerun the WPF smoke
  procedure interactively. Keep the change scoped to `NekoLib.Navigation.Wpf`; do
  not change `IViewHost` or the shared surface services.
  **Status 2026-08-03:** implemented in `WpfLayeredPageHostBase.Focus`, which now
  resolves the first focusable element inside the surface and retries once the
  surface is `Loaded`, because the service focuses it before layout. Four
  dual-target regressions added to `PlatformPageLifecycleTests`; the Navigation
  suite passes 226/226 on `net481` and `net9.0-windows`. Driving the real WPF
  smoke app confirmed `Keyboard.FocusedElement` moves from `MainWindow` to the
  dialog's Confirm button, and that a view focusing its own control from
  `OnShownAsync` still wins. **Closed 2026-08-03 at `822b51b`:** verified
  interactively on `net9.0-windows`. With the dialog open and the mouse untouched,
  pressing Space activated the focused Confirm button and logged `Dialog -> True`;
  before this change nothing inside the dialog held focus and Space did nothing.
  Typing immediately into the prompt without clicking also landed in its field.

- [x] **NAV-004 — Make WinForms focus-loss dismissal observe the surface
  subtree.** `WinFormsFocusObserverAdapter.Track` subscribes `Control.LostFocus`
  on the surface container, but `IViewHost.Focus` forwards focus to a child, so
  the container never holds focus, and WinForms focus events do not bubble.
  Measured with a real message pump and an activated form, following
  `PopoverService`'s exact order, the unfocus callback fired zero times when
  focus moved between the popover's own children and zero times when focus moved
  to a control outside the popover. Only `Form.Deactivate` can dismiss a WinForms
  popover today, so `AutoDismissPopoverBase` does not honor the auto-dismiss
  contract the Navigation README documents. Observe subtree focus instead of
  container focus and raise unfocus only when focus actually leaves the surface
  subtree, matching the rule the WPF adapter already implements. Keep the
  `Form.Deactivate` subscription, keep the notification single-shot per surface,
  and keep `IFocusObserverAdapter` unchanged. Add dual-target regressions driving
  the real WinForms adapter on an STA thread, then execute step 5 of the WinForms
  smoke scenario. Keep the change scoped to `NekoLib.Navigation.WinForms`.
  **Status 2026-08-03:** implemented by observing `Control.Leave` instead of
  `Control.LostFocus`; `Form.Deactivate` is unchanged. Three dual-target
  regressions added to `PlatformPageLifecycleTests`, driving real focus
  transitions on an off-screen shown form; the leaving case was confirmed to fail
  against the previous implementation and pass against the new one. Navigation
  suite passes 229/229 on `net481` and `net9.0-windows`. Driving the real
  WinForms smoke app confirmed the popover holds focus while open and is
  dismissed with `Popover -> False` once focus moves outside it. **Closed
  2026-08-03:** step 5 was driven by hand and every path behaved. Tabbing between
  the popover's field and its Fechar button did not dismiss it, and Space on the
  focused button completed it with `true`. Clicking the Dashboard counter (inside
  the host) and clicking left-panel controls such as "Limpar log" (outside the
  host) both dismissed it with `Popover -> False`. Switching away from the
  application also dismissed it through the retained `Form.Deactivate`
  subscription. Clicking the Idle page dismissed nothing, which is correct: that
  page contains only labels, so no focus moves — see NAV-007 for the documentation
  of that boundary.

- [x] **NAV-005 — Stop the WPF interaction blocker from destroying `IsEnabled`
  bindings.** `WpfInteractionBlocker` assigns `element.IsEnabled` directly in
  `Disable`, `Restore`, and `RestoreDisabledElements`. In WPF that writes a local
  value and permanently clears any `Binding` or style setter on the property.
  Measured over one block/unblock cycle: the binding reported alive before
  `Block()`, dead after it, still dead after `Unblock()`, and a later view-model
  change no longer moved the element; the same sequence expressed with
  `SetCurrentValue` kept the binding alive and responsive. Any WPF page or
  overlay that binds `IsEnabled` therefore stops following its view model after
  the first dialog or prompt. Use `SetCurrentValue` for the temporary disable and
  restore through the captured local value so a binding or style setter survives
  a modal cycle. Preserve the existing depth, modal-stack, and late-view rules.
  Add a dual-target regression that binds `IsEnabled` on a host child and asserts
  the binding still drives the element after a full cycle; the current
  `PlatformPageLifecycleTests` cases use unbound elements, which is why this was
  invisible. Keep the change scoped to `NekoLib.Navigation.Wpf`.
  **Status 2026-08-03:** implemented. Every write now goes through
  `SetCurrentValue`, the blocker restores only the elements it actually disabled
  — so an element that merely inherited a disabled state is never pinned — and a
  restored element with a live binding re-reads its source, so a value that
  changed while the modal was up is honoured. Three dual-target regressions added
  to `PlatformPageLifecycleTests`, all three confirmed to fail against the
  previous implementation; the two pre-existing blocker tests are unchanged and
  still pass. Navigation suite 232/232 on `net481` and `net9.0-windows`, whole
  solution builds, no new warning identity. Driving the real WPF smoke app shows
  the page disabled while a dialog is open and re-enabled after it closes.
  **Closed 2026-08-03 at `822b51b`:** the interactive `net9.0-windows` run
  confirmed no regression — clicking the page behind an open modal left focus in
  the modal, and the Dashboard counter worked again once the modal closed. The
  binding-preservation behaviour itself is not observable in the smoke scenario,
  which has no bound `IsEnabled`; it rests on the three dual-target regressions
  that were confirmed to fail against the previous implementation.

- [x] **NAV-006 — Make WinForms UI dispatch truthful when the host handle does
  not exist.** `Control.InvokeRequired` returns `false` from a worker thread
  whenever no handle exists in the parent chain; this was measured both for a
  bare `Control` and for a `Panel` inside an unshown `Form`.
  `WinFormsEventDispatcherAdapter.Invoke` and `BeginInvoke` therefore run the
  action on the calling thread instead of the UI thread, the
  `InvalidOperationException` the adapter documents is unreachable in exactly the
  case it describes, and the comment claiming that a non-UI thread will throw is
  wrong. `NavigationRuntime.ExecuteSafeOnUiAsync` depends on `BeginInvoke`
  throwing to detect a dead message pump, so its inline teardown fallback is not
  reached either. Determine UI-thread identity explicitly — capture the owning
  thread at construction rather than inferring it from `InvokeRequired` — then
  marshal, run inline on the real UI thread, or fail loudly, and state the chosen
  rule for an unreachable UI thread in the `IEventDispatcherAdapter`
  documentation. Preserve current behavior whenever the handle exists. Add
  dual-target regressions covering handle-created and handle-absent against UI
  thread and worker thread. Keep the change scoped to
  `NekoLib.Navigation.WinForms` plus contract documentation; do not change
  `NavigationRuntime`.
  **Closed 2026-08-03:** `WinFormsEventDispatcherAdapter` now captures the owning
  thread in its constructor and uses `InvokeRequired` only where it is
  authoritative — while the handle exists. With the handle created, behaviour is
  unchanged on both methods. Without a handle, the action runs inline only on the
  captured UI thread and both `Invoke` and `BeginInvoke` throw
  `InvalidOperationException` on any other thread, so the failure the adapter
  documents is finally reachable and `ExecuteSafeOnUiAsync`'s inline teardown
  fallback can trigger. The chosen rule is stated on `IEventDispatcherAdapter`:
  decide UI-thread identity, never infer it; on an unreachable UI thread run
  inline only for the UI thread itself and throw otherwise, never substituting
  the calling thread. Eight dual-target regressions added in
  `WinFormsEventDispatcherAdapterTests` covering handle-created and handle-absent
  against UI thread and worker thread for both methods, driving a real off-screen
  form, a real worker thread, and a real message pump. **The two handle-absent
  worker-thread cases were confirmed to fail against the previous implementation
  on `net481` and `net9.0-windows`**, with no exception raised and the action
  executed on the worker thread — which re-measures the `InvokeRequired` premise
  on this machine. The other six pass against both implementations and exist to
  pin the preserved behaviour. Navigation suite 252/252 on `net481` and
  `net9.0-windows`, the whole solution builds, and no warning identity mentions
  either changed file. `NavigationRuntime` was not touched. **Behaviour change to
  be aware of:** an application that starts navigation from a worker thread
  before the host window exists used to run page lifecycle on that worker thread
  silently and now gets an exception naming the fix. No interactive evidence was
  taken for this item; its validation does not ask for any, and the WinForms
  smoke scenario was only confirmed to still build.
  **Observed, not acted on:** `WpfEventDispatcherAdapter` runs the action inline
  from *any* thread once its `Dispatcher` reports shutdown, which is a different
  answer to the same unreachable-UI-thread question. The contract documentation
  records that divergence as a platform teardown fallback rather than silently
  changing WPF, because this item is scoped to the WinForms adapter.

- [x] **NAV-007 — Define and document surface dismissal reachability per
  platform.** (a) The WinForms `ToastViewBase` binds `Control.Click` on the
  container, and WinForms click events do not bubble: a measured
  `PerformClick()` on a child control raised the container's `Click` zero times,
  so only the toast's own background dismisses. The WPF `ToastViewBase` binds
  `MouseLeftButtonDown`, which is re-raised along the bubble route of
  `Mouse.MouseDownEvent`, so most children dismiss but controls that mark the
  event handled — `Button` among them — do not. "Tap anywhere to dismiss" is
  therefore accurate on neither platform and differs between them. Decide and
  document the real contract: keep the current click binding for compatibility,
  describe the actual reachability per platform in the Navigation README overlay
  section, and treat an explicit close affordance as the supported dismissal for
  a toast that contains child controls. This is the evidence that feeds the
  ready-made close-button toast proposal; it does not authorize a change to
  `IToastView` or `ToastService`. (b) Popover light dismissal is driven by
  **focus, not hit testing**, on both platforms, and this is undocumented.
  Observed on 2026-08-03 during the WinForms step 5 run: clicking a control that
  can take focus dismisses the popover, but clicking inert page area does not —
  the Idle page contains only labels, so no focus moves and the popover correctly
  stays open. WPF has the same boundary, because clicking a non-focusable element
  does not move keyboard focus there either. Document it on `IUnfocusAware`,
  `IFocusObserverAdapter`, and the Navigation README overlay table so "closes when
  you click away" is not read as hit-test dismissal. A true click-outside model
  would need mouse capture or a hit-test scrim and is **not** accepted by this
  entry. Validate by documentation review plus the toast and popover steps of both
  smoke scenarios, recording which regions dismiss on each platform.
  **Status 2026-08-03:** documented; **stays open for its interactive
  validation.** Both claims were re-confirmed against current source: the WinForms
  `ToastViewBase` still binds `this.Click` on the container, the WPF one still
  binds `MouseLeftButtonDown`, and both focus observers are purely focus-driven —
  `Control.Leave` + `Form.Deactivate` on WinForms, subtree-filtered
  `LostKeyboardFocus` + `Window.Deactivated` on WPF — with no hit testing
  anywhere. A new "Dismissal reachability" subsection in the Navigation README
  overlay section states the per-platform toast table and the four popover rules,
  the overlay table now reads "keyboard-focus loss … not hit testing" for
  Popover, and the reachability notes were added to `IUnfocusAware`,
  `IFocusObserverAdapter`, and both `ToastViewBase` classes. An explicit close
  affordance calling `Dismiss()` is now the documented supported dismissal for a
  toast with child controls. A follow-up sweep also corrected both
  `AutoDismissPopoverBase` classes, which still said the notification is raised
  "when the user clicks a sibling control" (WinForms) and "when the user clicks
  elsewhere" (WPF) — the exact imprecision this item exists to remove, in the
  class consumers actually derive from. A repository-wide sweep of `src/` and
  every `*.md` found no other surviving claim. `IToastView` and `ToastService` were not touched;
  the click bindings are unchanged. **Outstanding — interactive:** the toast and
  popover steps of both smoke scenarios, recording which regions dismiss on each
  platform. Only the WinForms popover half exists today, recorded under NAV-004
  on 2026-08-03 and now quoted in the README; the WinForms toast, the WPF toast
  and the WPF popover have no per-region record. No automated coverage was added:
  a synthetic WPF mouse test would be misleading, because
  `UIElement.MouseLeftButtonDown` is a Direct routed event that only travels the
  bubble route when the input system promotes it, so `RaiseEvent` on a child
  would fail to reach the toast for a reason unrelated to the documented one.
  **Closed 2026-08-03 on interactive evidence at `03f5760`.** The toast and
  popover steps were driven by hand on the WinForms smoke scenario on **both**
  `net9.0-windows` and `net481`, and on the WPF smoke scenario on
  `net9.0-windows`. Regions recorded:
  - **WinForms toast — nothing dismisses by click.** Clicking the message text
    did not dismiss, on either target family, and neither did clicking the
    toast's extreme top-left and bottom-right corners. `SampleToast`'s label is
    `Dock = Fill`, so it covers the container's entire client area and **no**
    pixel of that toast reaches the container's `Click`. Only the 3 s timer
    dismissed it. This is stronger than the finding predicted — the finding said
    "only the toast's own background dismisses", and with a filling child there
    is no reachable background at all.
  - **WPF toast — the message text dismisses.** Opened at 23:57:27.252 and gone
    roughly 0.8 s later, well inside the 3 s timer, so the click and not the
    timer closed it. This is the documented per-platform divergence, now
    measured on both sides.
  - **Popover, both platforms.** Clicking the inert Idle page (labels only) left
    the popover open on WinForms `net9.0-windows`, WinForms `net481` and WPF —
    the focus-versus-hit-testing boundary, confirmed on every combination.
    Clicking `SignIn("admin")`, a focusable control outside the host, dismissed
    it with `Popover -> False` in all three. On WPF, Tab moved focus from the
    field to the Fechar button without dismissing.
  **Residual gap:** the WPF "a child that marks the event handled does not
  dismiss" case was **not** exercised, because the scenario's toast contains only
  a `TextBlock` — there is no `Button` inside a toast anywhere in the scenarios.
  That half of the WPF row rests on framework semantics plus the source, not on
  interactive evidence.

- [x] **NAV-008 — Correct the small confirmed adapter and bootstrap defects.**
  Each is independent and low risk; land them separately from the behavioral
  items above. (a) `WinFormsTimerAdapter` never assigns its `intervalMilis`
  constructor parameter, leaving the WinForms default of 100 ms measured at
  construction, while `WpfTimerAdapter` honors it; `NavigationBootstrapLifetime`
  always assigns `IntervalMilliseconds`, so only the public constructor is
  affected. (b) `PageNavBootstrap` uses raw `Assembly.GetTypes()` for the
  custom-loading-mask probe that decides whether each adapter's
  `DefaultLoadingMask` is auto-registered, bypassing the tolerant
  `GetLoadableTypes` the Navigation README credits to that step, so a partially
  loadable assembly aborts `Start()`. (c) `NekoLib.Navigation.Wpf` ships two dead
  public types, `InteractionObserver` and `EventSubscriptionAdapter`, neither
  produced by `WpfPlatformAdapter`; the latter also wraps failures in a bare
  `Exception`. Removing them is a public-surface removal with no consumer in this
  repository — record it for the future F1 policy. (d) `PageNavBootstrap` never
  registers anything with `PageFactory` and `PageFactory.Warn` has no subscriber
  anywhere, so every page and every surface is created through the
  migration-only default-constructor fallback silently; give the existing `Warn`
  event a real consumer instead of modifying the frozen `PageFactory`. (e)
  `WinFormsPlatformAdapter` throws `new ArgumentException(nameof(nativeHost))` in
  three places, putting the parameter name in the message slot. (f) Calling any
  navigation API while the facade is unmounted throws
  `NavigationService.Initialize must be called first.`, but no `Initialize`
  member exists on `NavigationService`; the public entry point is
  `PageNavBootstrap.Start()`. This is the message a consumer sees on the most
  common misuse, and it was observed while exercising the smoke scenario's
  Shutdown control. (g) `IPageView.Name` is seeded differently per platform: the
  WPF `PageView` constructor uses `GetType().Name` while the WinForms one uses
  `GetType().FullName`, so the same navigation logs `IdlePage -> DashboardPage`
  on WPF and the fully qualified names on WinForms. Pick one and apply it to
  both platform base classes; the descriptor name remains authoritative for
  registration and history either way. Add dual-target coverage for the timer
  interval and the tolerant scan.
  **Closed 2026-08-03:** all seven landed. (a) `WinFormsTimerAdapter` now assigns
  the constructor interval, so a directly constructed timer no longer ticks at
  100 ms. (b) The custom-loading-mask probe goes through the new internal
  `AssemblyTypeScanner.GetLoadableTypes`, which `PageMetadataBuilder` now shares,
  so one unloadable type no longer aborts `Start()` on its first line. (c) Both
  dead public WPF types were removed; the removal is recorded under F1 as a
  public-surface input. (d) `PageNavBootstrap` subscribes `PageFactory.Warn` to
  the configured `ILogger`, falling back to `Debug` output when logging is not
  configured — the frozen `PageFactory` was not modified, and the handler
  captures a local so it does not root the builder. (e) The three
  `WinFormsPlatformAdapter` throws now carry a real message plus the parameter
  name. (f) The unmounted-facade message now names `PageNavBootstrap.Start()`
  and `NavigationService.Shutdown()` instead of a nonexistent
  `NavigationService.Initialize`. (g) Both platforms seed `IPageView.Name` from
  `GetType().Name`; the WinForms `PageView`, `ToastViewBase`, `DialogViewBase`,
  `PromptViewBase` and `PopoverViewBase` all changed, and WPF was already
  correct — WPF cannot use `FullName`, because a WPF `Name` must be a valid
  identifier. Five regressions added in `BootstrapSmallDefectTests`; **the three
  timer cases and the tolerant-scan bootstrap case were confirmed to fail against
  the previous implementation on `net481` and `net9.0-windows`** — the timer read
  100 ms instead of the requested interval, and `Start()` threw
  `ReflectionTypeLoadException`. The fifth pins the extracted scanner directly and
  does not discriminate. Navigation suite 257/257 on both target families, whole
  solution builds. (c), (e) and (f) carry no automated coverage: they are a
  deletion and two message strings. No interactive evidence was taken; this
  item's validation does not ask for any.

- [x] **NAV-009 — Resolve the surface-DPI and ergonomics dispositions.**
  Choose correct, keep-and-document, or remove for each, and record the rejected
  alternatives. (a) `WinFormsNavigationSurface.Scale` calls
  `Control.CreateGraphics()`, which was measured to force host handle creation as
  a side effect and to throw `ObjectDisposedException` once the host is disposed,
  while `WpfNavigationSurface.Scale` degrades to `1f`. Replace it with a
  side-effect-free DPI read and define the unrealized and disposed behavior
  explicitly, so an anchor consumer can read `Scale` at any time. The Toolkit's
  purpose is settled — see NAV-010 — so this is a correction, not a
  keep-or-remove question. (b) The WPF view bases expose a
  non-virtual `public void Dispose()`, so a subclass cannot extend disposal the
  way the WinForms `protected override void Dispose(bool)` pattern allows. (c)
  `WpfLayeredPageHostBase.BringToFront(IPageView)` assigns the same z-index to
  every page and therefore cannot order two simultaneously attached pages, while
  WinForms genuinely reorders; the difference is currently masked because
  keep-attached hidden pages are collapsed. Nothing here justifies a core or
  frozen change.
  **Closed 2026-08-03 — all three disposed as "correct".** (a)
  `WinFormsNavigationSurface.Scale` now reads `Control.DeviceDpi`, a plain field
  read. The two behaviours are stated on the property: **unrealized** reports the
  DPI captured when the control was constructed and creates no handle;
  **disposed** keeps reporting the last known value instead of throwing. So an
  anchor consumer can read `Scale` at any point in the host's life, which is what
  NAV-010 needs. The WPF property gained the matching documentation; its `1f`
  degradation was already correct and is unchanged. *Rejected:* keeping
  `CreateGraphics()` and merely documenting the side effect — realizing a window
  as a consequence of reading a scale factor is not a documentable behaviour, it
  is a defect. (b) `Dispose()` is now `virtual` on `DialogViewBase`,
  `PromptViewBase<TResult>`, `PopoverViewBase` and `ToastViewBase`; WPF
  `PageView` already was. Overriding requires calling `base.Dispose()`, which is
  documented on each. Recorded under F1 as a binary-breaking public-surface
  change. *Rejected:* mirroring the full WinForms `protected virtual void
  Dispose(bool)` pattern — it adds a protected member to four public types to
  express a finalizer contract none of them has. (c)
  `WpfLayeredPageHostBase.BringToFront(IPageView)` now orders within the page
  band, taking the highest z-index below `OverlayZIndex` and adding one, clamped
  so a page can never reach the overlay band. Two simultaneously attached pages
  can therefore be ordered, matching what WinForms already did. *Rejected:*
  keep-and-document, since the divergence is only masked by hidden keep-attached
  pages being collapsed and would resurface the moment two pages are visible at
  once. Five regressions added in `SurfaceToolkitAndErgonomicsTests`. **Confirmed
  against the previous implementation on `net481` and `net9.0-windows`:** the
  disposed-host case threw `ObjectDisposedException`, the unrealized-host case
  reported a created handle, and the z-order case measured `first=0, second=0`.
  (b) discriminates at compile time — against the old bases the subclass fails
  with CS0506, "cannot override inherited member … because it is not marked
  virtual", which is precisely the finding; it was therefore proven in a separate
  pass from (a) and (c). The `ResolveAnchor` case is a pin and passes against
  both. Navigation suite 262/262 on both target families, whole solution builds,
  `verify-docs.ps1` passes. No interactive evidence was taken; this item's
  validation does not ask for any, though (c) is the kind of change the WPF smoke
  scenario would exercise.

**Confirmed surface-positioning finding — 2026-08-03:** while the WinForms smoke
scenario was being written, a stock WinForms toast was observed to cover the
whole navigation host. `WinFormsLayeredPageHostBase.AddView` docks every added
view to `Fill`, and the WinForms `ToastViewBase` is the only surface base that
never undoes it — `DialogViewBase`, `PromptViewBase<TResult>`, and
`PopoverViewBase` each undock and place themselves. The WPF `ToastViewBase` sets
`HorizontalAlignment = Right`, `VerticalAlignment = Bottom`, and a 20px margin in
its constructor, so the same toast is correctly parked bottom-right there. The
scenario's `SampleToast` therefore performs the undock and anchoring itself, with
a comment recording why. The intended mechanism for this is the existing Toolkit:
`INavigationSurface.ResolveAnchor(SurfaceAnchor)` already returns the nine anchor
points, `Scale` reports the DPI factor, both platform implementations exist, and
the contract documents itself as being "used to position overlays, dialogs,
keyboards, debug panels". The gap is wiring, not purpose — `PageNavBootstrap`
never constructs or registers an `INavigationToolkit`, so no view can obtain one.

- [x] **NAV-010 — Give native surfaces an anchored default position and wire the
  Toolkit as its seam.** Register the platform toolkit during bootstrap so a
  surface can resolve it, then make the WinForms `ToastViewBase` undock and place
  itself at the `BottomRight` anchor with a documented default inset, matching the
  visual result the WPF base already produces. **Accepted registration shape
  (2026-08-03):** let the platform layered host also implement
  `INavigationToolkit` and have
  `PageNavBootstrap` register it with the same `host as INavigationToolkit`
  probe it already uses for `host as IViewHost`. **Rejected alternative:** adding
  a `CreateNavigationToolkit` factory method to `IPlatformAdapter`, because that
  breaks every third-party adapter implementation for no gain over the probe.
  Keep the WPF base's declarative alignment unless the shared anchor path proves
  strictly better; DPI-correct placement depends on the `Scale` correction in
  NAV-009(a). Do not turn the Toolkit into a layout engine, do not add
  positioning to `IViewHost`, and do not change `SurfaceAnchor`. Add dual-target
  regressions asserting that a WinForms toast is not host-sized after attach and
  sits at the bottom-right inset, plus toolkit resolution from a mounted context;
  then run the toast step of both smoke scenarios on both target families. Keep
  the change scoped to `PageNavBootstrap` registration and the two adapters.
  **Status 2026-08-03:** implemented; **stays open for its interactive
  validation.** Both layered hosts now implement `INavigationToolkit`, delegating
  to the existing `WinFormsNavigationToolkit` / `WpfNavigationToolkit` rather than
  duplicating them, and `Start()` registers the host under that contract with the
  accepted `host as INavigationToolkit` probe, placed next to the `host as
  IViewHost` probe it mirrors. No `IPlatformAdapter` member was added, so a
  third-party adapter still compiles and simply leaves the toolkit unregistered.
  The WinForms `ToastViewBase` now undocks in `IToastView.OnShown` — after the
  virtual `OnShown`, so a subclass that resizes from its payload is placed at its
  final size — restores the size it had when the host added it, anchors
  bottom-right, and positions itself at `SurfaceAnchor.BottomRight` minus a
  `protected virtual int AnchorInset` of 20 scaled by `INavigationSurface.Scale`.
  The geometry comes from the Toolkit contract read off the toast's own parent, so
  it works whether or not a toolkit was registered, and it depends on the NAV-009(a)
  `Scale` correction. `IViewHost`, `SurfaceAnchor` and the WPF base's declarative
  alignment are unchanged. Five regressions added in `AnchoredToastAndToolkitTests`,
  including toolkit resolution from a really mounted context via
  `WinFormsPlatformAdapter`. **Confirmed against the previous implementation on
  `net481` and `net9.0-windows`:** the toast reported `Dock=Fill` and `Right=800`
  on an 800px host instead of 780 — it covered the whole navigation host. The
  toolkit half discriminates at compile time, since `Surface` did not exist on
  either host, so the toast geometry was proven in a separate stash pass.
  Navigation suite 267/267 on both target families, whole solution builds, both
  smoke scenarios build. **The WinForms smoke `SampleToast` lost its own
  undock-and-anchor compensation**, which is now the base's job — so the toast
  step finally exercises the fix instead of the workaround.
  **Closed 2026-08-03 on interactive evidence at `03f5760`.** The toast step was
  driven by hand on the WinForms smoke scenario on **both** `net9.0-windows` and
  `net481`, and on the WPF smoke scenario. On every run the toast appeared parked
  at the host's bottom-right corner at its own designed size — not stretched over
  the navigation host — and the WinForms runs prove the base does it, because
  `SampleToast`'s own undock-and-anchor compensation was removed in this commit.
  The WPF toast, whose code did not change, looks identical.
  **Residual gap:** the WPF smoke scenario is `net9.0-windows` only, while the WPF
  adapter also targets `net481`, so "both target families" could not be satisfied
  on the WPF side — there is no net481 WPF scenario to run. That is the
  pre-existing open question about multi-targeting the WPF scenario, not a new
  finding, and the WinForms half — the half this item actually changed — was
  recorded on both families.

**Confirmed idle finding — 2026-08-03:** the interactive WPF smoke run left the
shell blank after `ResetAsync` and the idle timeout never recovered it, while
dialogs and toasts still opened normally. Reproduced by driving the WinForms
scenario and timing the idle system: after a successful idle transition the timer
is stopped and never rearmed, so `Reset` at t+25s produced a blank shell that was
still blank a full interval later at t+52s; clicking a control **inside** the
navigation host at t+55s rearmed it and idle fired on schedule at t+76s. Current
source confirms it — `NavigationBootstrapLifetime` stops the timer when a tick
starts and only `TryRearmIdle` (the denied and failed paths) or the interaction
handler restart it. Any programmatic move off the idle page — `ResetAsync`, an
IPC event, background work — therefore leaves an unattended terminal with no idle
timeout until a person touches the host.

- [x] **NAV-011 — Keep the idle watchdog armed after a successful transition.**
  **Accepted direction (2026-08-03):** rearm after every completed tick, not only
  after a denied or failed one. **Rejected alternative:** rearming only when the
  runtime is not on the idle page, which leaves the same hole — once the timer is
  disarmed while idle, no tick ever comes to observe that the shell moved away.
  A bare "always rearm" is not enough on its own: `NavigationSession.SignOut()`
  raises `Changed` on every call and the idle page is `Transient` by default, so
  an armed timer would re-run the whole transition every interval and dispose and
  recreate the idle page for as long as the terminal stayed unattended. A tick is
  therefore skipped entirely — silently, with no trace and no navigation — when
  the runtime already shows the idle page and the session is already signed out.
  That check is gated on this lifetime owning the real `GoIdleAsync`, because a
  caller-supplied navigation is opaque and must never be skipped. Keep the change
  scoped to `NavigationBootstrapLifetime`; it does not touch a frozen component
  and shares its file with NAV-001.
  **Status 2026-08-03:** implemented. Three regressions added to
  `PageNavBootstrapLifetimeTests`; the two behavioural ones were confirmed to fail
  against the previous implementation. Navigation suite 235/235 on `net481` and
  `net9.0-windows`. Re-running the timed reproduction shows the blank shell
  recovering on its own at t+39s with no host interaction, and the spurious
  re-navigation that previously fired while already idle is gone.

Validation requirements:

- fake-based automated tests do not replace native interactive evidence;
- build success does not equal runtime success;
- create or refresh a versioned WinForms runtime scenario;
- refresh and execute the versioned WPF smoke scenario;
- record the last verified date and commit, distinguishing automated, manual,
  build-only, and interactive evidence;
- preserve lifecycle ordering, navigation-gate behavior, overlay teardown
  asymmetry, and static-facade semantics;
- keep any finding that requires a core change in the review until a separate
  unfreeze decision is accepted.

### E3 — Long-running and recovery confidence

- [x] Create small, specific, reproducible scenarios for unattended execution
  over long periods without creating a new runtime framework.
- [x] **Make the evidence unattended.** Every Phase E scenario reports automated
  outcomes through process exit codes and artifacts, and E3-ORCH provides the
  deterministic script, persisted schedule, ownership, and aggregate verdict.

The accepted implementation brief is the versioned
[`Phase E scenario suite`](runtime_tests/PHASE_E_SCENARIO_SUITE.md). It divides
the work into independent Navigation, Observability, Pipes, Watchdog, Devices,
Data/SQL Server, and orchestration scenarios without creating a new runtime
framework. Faults are generated deterministically from an integer seed,
persisted before launch as monotonic offsets, and injected only by
scenario-owned processes or the orchestrator.

**Outcome-first acceptance decision, 2026-08-11:** fixed 15–30 minute smoke,
60–90 minute rehearsal, and four-hour soak windows are operational modes and
confidence tools, not universal Phase E closure gates. A scenario closes when:

1. every declared target/platform combination builds and its isolated contracts
   pass;
2. every declared workload class and fault kind has executed at least once with
   an automated exit-code verdict, its expected terminal, successful recovery,
   complete artifacts, and reconciled cleanup;
3. every distinct runtime topology boundary — native adapter, real provider,
   real IPC/process boundary, real COM transport, or deployed package layout —
   has direct evidence; and
4. target-specific or conditionally compiled behavior has direct parity
   evidence rather than being generalized from another target.

The representative runtime matrix follows distinct behavior, not a Cartesian
product of every mode and target. Historical artifacts keep their recorded
duration and `belowSpecifiedWindow` value and are never relabelled. A four-hour
or optional sixteen-hour soak is required only when closing a duration-dependent
claim or investigating an observed drift, leak, queue-pressure, or boundedness
signal. Interactive and automated-UI claims remain separate evidence and do not
block a headless runtime scenario unless visible behavior is itself the claim.

#### Phase E runtime scenario delivery

The checkbox for a scenario closes only after its source is versioned and the
outcome-first conditions above are met. A pre-existing smoke or partial consumer
is useful evidence but does not close an unexercised fault, topology, cleanup,
or provenance boundary.

**Sequencing decision, 2026-08-10: build every scenario before starting the
execution phase.** Long runs are not interleaved with implementation. Each
scenario is taken to implemented, isolated-test-verified and buildable, and
nothing is executed beyond what its implementation requires; the smoke,
rehearsal, soak and campaign runs happen afterwards, as one deliberate phase.
This is why a scenario can be *ready for execution* while carrying items marked
pending validation — those are gaps in evidence awaiting that phase, not open
defects. It also keeps the host free, which the recorded load finding shows
matters for any measurement of drift.

Every E3 scenario now has source, and **under the outcome-first decision every
E3 scenario has complete automated runtime evidence as of 2026-08-12.** E3-ORCH,
E3-NAV, E3-OBS, and E4-SQL closed earlier. E3-WDOG closed its final topology
boundary on 2026-08-11 with an exact package-backed deployed-Host pass from
immutable version `1.0.0-local.10`. E3-PIPE and E3-DEV closed on 2026-08-12 with
one compact representative recovery sweep each: E3-PIPE 37/37 with all six
scheduled faults, and E3-DEV 33/33 with all five peer faults against the real
com0com pairs.

What remains for every E3 scenario is optional confidence rather than a gate:
additional target parity, the nominal 15-30 minute smoke and 60-90 minute
rehearsal windows, four-hour soaks, interactive parity, and `campaign.json`
registration. The compact sweeps deliberately ran for ten minutes and record
`belowSpecifiedWindow: true`; they close fault, terminal, recovery, artifact and
cleanup coverage, and are **not** relabelled as nominal rehearsals.

- [x] **E3-ORCH — deterministic campaign orchestration.** **Implemented
  2026-08-08** at [`runtime_tests/Confidence/LongRunning/`](runtime_tests/Confidence/LongRunning/README.md):
  a thin PowerShell orchestrator with a versioned configuration schema,
  deterministic seeded schedule generation persisted before launch, explicit
  scenario selection, strict process ownership verified by name and start time,
  aggregate exit codes, and stale-campaign reconciliation. It carries no
  business assertions; those stay in the workers.
  **All three acceptance criteria were exercised on 2026-08-08:** the same seed
  produced `fnv1a64:cfc084039abb71b8` on consecutive runs; a deliberately failed
  worker made the campaign exit 4 while the other worker completed and exited 0;
  and an orchestrator killed mid-campaign left its schedule behind and the next
  run identified the orphaned process, reporting it without touching it and
  ending it only on request. A two-worker smoke campaign exited 0.
  **A recovery campaign ran on 2026-08-10:** single-worker, `-Duration 70m`,
  driving `E3-OBS` for 59 minutes through all eight phases, aggregate exit 0. It
  is also the first proof that a worker dispatches from the orchestrator's
  schedule rather than only parsing it — the worker recorded the campaign's hash
  `fnv1a64:57d4189e5a941ecf` and fired its faults in the orchestrator's order.
  Under the 2026-08-11 outcome-first decision this closes E3-ORCH: multi-worker
  aggregation and cleanup, failure propagation, stale ownership, deterministic
  planning, and real worker fault dispatch all have direct evidence. A
  multi-worker non-smoke campaign and a four-hour campaign remain optional load
  and duration confidence, not closure gates.
  **Artifact layout v2 implemented and verified on 2026-08-10:** orchestrated
  scenarios now receive explicit campaign and worker identities and write below
  `<campaign-id>/workers/<worker-id>/<scenario-id>/`. Process capture uses
  separate `process.stdout.log` / `process.stderr.log` files, and reconciliation
  requires and indexes the worker's `result.json`. An `E3-OBS` regression exited
  0 with 91 checks, 0 failed, matching worker/aggregate schedule hash and the
  indexed v2 result. A direct standalone regression exited 0 with 61 checks and
  retained v1. Historical artifact directories are not moved.
  **Recorded load finding:** the first concurrent campaign saturated the host —
  around 670 MB free, SQL Server logins past 15 seconds — and the provider's
  pool blocking period then reported one slow login as seven consecutive check
  failures. That is machine capacity, not a product defect, and it is why the
  multi-hour campaign should not share this host with other heavy work.
- [x] **E3-NAV — Navigation long-running and recovery.** The versioned WinForms
  and WPF smoke applications exist and have interactive evidence; the dedicated
  unattended scenario was delivered build-first on **2026-08-10** at
  [`runtime_tests/Navigation/LongRunningRecovery/`](runtime_tests/Navigation/LongRunningRecovery/README.md).
  A shared dual-target core owns the plan, assertions, workload, passive
  Inspection evidence, resource sampling and cleanup; the smallest native
  WinForms and WPF hosts supply only pages, surfaces, adapter composition and
  their UI loops. The required WinForms `net481`, WinForms `net9.0-windows`, and
  WPF `net9.0-windows` combinations build. Nine isolated contracts pass on both
  core targets, and repeated recovery previews are stable across all three
  combinations at `fnv1a64:2af9145c9ebf63ec` for seed `20260810`; seed `99`
  differs. Fault/workload controls remain scenario-owned, no Inspection action
  is registered, the shared harness was not expanded, and frozen Navigation
  source was not changed. **Corrected two-minute standalone development probes
  passed on all three combinations on 2026-08-11 at clean `e7c86a4`.** Each
  passed 11/11 checks with zero skipped, exit 0, awaited
  `NavigationService.Shutdown()`, zero cleanup problems, and no remaining
  process or window. WinForms `net481`, WinForms `net9.0-windows`, and WPF
  `net9.0-windows` completed 50, 51, and 552 sustained cycles respectively.
  The first `net481` run exposed three scenario defects: an incorrect
  current-page redirect setup and two releases of scenario-owned blocked work
  before the public reset/shutdown cutoff. Awaiting those lifecycle boundaries
  also removed the following idle/shutdown cascade. The public history
  assertion now expects exactly one entry for the current Idle page. No frozen
  Navigation or shared harness source changed. After the standalone gate, the
  three combinations were registered as separate, disabled E3-ORCH workers.
  Their scenario-owned `--scenario-id` keeps the campaign schedule and v2
  result path aligned without changing the shared harness. Build + preflight
  passed for all three entries, and repeated 60-minute recovery previews planned
  14 faults per worker with stable hash `fnv1a64:320060b0d5392105`; seed `99`
  changed it to `fnv1a64:d1fafc3b8bc85288`. **Qualifying standalone smoke
  passed on all three combinations on 2026-08-11 at clean `897e17b`.** Each ran
  for 20 minutes, passed 11/11 checks with zero failed or skipped, exited 0,
  awaited shutdown, reported no cleanup problem or native child, and left no
  process or window. WinForms `net481`, WinForms `net9.0-windows`, and WPF
  `net9.0-windows` completed 130,919, 131,717, and 2,198,271 operations. The
  smoke hash was `fnv1a64:57d706d0bd6c8799`. WinForms resource series were
  broadly flat during the sustained phase. WPF private bytes moved from 152.6
  MiB at sustained-phase entry to 170.8 MiB at 20 minutes while handles
  oscillated and managed heap was non-monotonic; owned state still returned to
  zero. Treat that as a soak observation, not a confirmed leak. The three
  E3-ORCH entries remain opt-in solely because campaign preflight cannot prove
  an interactive desktop. **The automated runtime gate closed on 2026-08-11 at
  clean `9f8f781`.** A WinForms `net481` recovery run requested 70 minutes,
  persisted hash `fnv1a64:1d07125271766371` before launch, exercised all 14
  fault kinds, passed 25/25 checks with zero failed/skipped, exited 0 after
  3681.8 seconds, awaited shutdown, reported zero cleanup problems, and left no
  process or window. Together with the three qualifying native-host smokes and
  dual-target isolated contracts, this covers every shared workload/fault and
  every distinct adapter/target boundary without repeating the same recovery
  matrix three times. Interactive parity remains separate manual evidence. The
  WPF private-byte movement remains an open observation that can justify a
  targeted longer run, not a confirmed leak or an E3-NAV gate.
- [x] **E3-OBS — Logging, Telemetry, and passive Inspection.** **Scenario source
  delivered 2026-08-09** at
  [`runtime_tests/Observability/LongRunningRecovery/`](runtime_tests/Observability/LongRunningRecovery/README.md):
  a dual-target console scenario with smoke, recovery-rehearsal and soak modes,
  the three capabilities given independent phases and result sections, seven
  scenario-owned fault kinds, and the artifact and exit-code contracts. It needs
  no container, no service and no hardware, and writes only inside its own run
  directory. Both targets build with no warnings. Schedule determinism is
  verified: the same seed produces `fnv1a64:af14ff69cf61b022` on `net481` and
  `net9.0`.
  **Smoke passed on both targets with exit code 0 on 2026-08-09**, over the
  specified 15-minute window rather than merely executing the matrices once:
  4951 checks on `net9.0` and 4591 on `net481`, **zero failed and zero skipped
  on either target**, across 164 and 152 cycles of the full matrix under
  concurrent steady traffic, totalling roughly 1.8M and 1.6M operations with no
  unexpected failure. Nothing here is target-conditional, so unlike E4-SQL
  neither target skips a check. Threads moved 14 → 18 and 15 → 27 and handles
  284 → 314 and 302 → 389 over those runs, the managed heap was non-monotonic on
  both, and every bounded structure ended exactly at capacity. Deleting the
  working directory is the handle assertion, and it removed 1819 and 1687 files
  cleanly.
  **Recovery rehearsal passed inside the specified window on 2026-08-09:**
  62.3 minutes elapsed on `net9.0`, exit 0, 68 checks, 0 failed, 0 skipped, with
  **all seven fault kinds proven** — each with its documented terminal, a
  successful post-recovery probe, and provider and registration counts back to
  baseline — across 8071 operations and zero unexpected failures.
  **The interrupt contract is proven, not assumed:** a real `CTRL_BREAK` to a
  running soak produced exit 8 with the workspace disposed, the process-wide
  Inspection slot restored, the working directory removed, and the in-flight
  checks recorded as skipped rather than failed.
  **A reporting defect found and fixed while recording this:** the
  below-specified-window flag compared the *requested* duration, so a rehearsal
  asking for 60 minutes and elapsing 52.9 reported itself compliant. It now
  judges elapsed time, and the scenario documents that a rehearsal must request
  about 70 minutes to land inside 60–90, because the schedule reserves a quiet
  window at each end.
  **Every failure it injects is scenario-owned** — failing and blocking sinks, a
  file lock, and misbehaving state providers — so no product module acquired a
  fault-injection or `TestControl` surface. It registers no Inspection action
  and asserts that its own action count stays zero, keeping the frozen action
  channel out of scope.
  **Registered in `E3-ORCH`:** `campaign.json` carries the scenario and its
  seven fault kinds. The end-to-end recovery evidence below supersedes the
  earlier parse-only registration check.
  **The soak path is proven as of 2026-08-09:** `--soak 15m` exited 0 with 3848
  checks, 0 failed, 0 skipped, 128 cycles and 1 324 827 operations at zero
  unexpected failures, and **all seven fault kinds fired while the assertion
  cycles were running**, each passing. Thread and handle drift matched the
  smoke's despite the added fault traffic. It completed on the first attempt,
  where E4-SQL's soak needed three — because the two concurrency defects the
  *sustained smoke* had already exposed were fixed before any fault and
  assertion first overlapped. A sustained smoke is far cheaper than a soak and
  finds the same class of problem.
  **Executed evidence, in chronological order:**
  **The target matrix is complete as of 2026-08-10:** the `net481` rehearsal
  also exited 0 at 62.3 minutes with 68 checks and all seven faults, so both
  targets have now passed both smoke and rehearsal with nothing skipped on
  either. It produced the same schedule hash and byte-identical counters as the
  `net9.0` rehearsal — 8071 operations, 8046 successes, 23 expected failures,
  2 cancellations — so the rehearsal is deterministic in its workload and not
  only in its plan.
  **Orchestrated end to end as of 2026-08-10:** an `E3-ORCH` recovery campaign
  ran this scenario as its worker and returned aggregate exit 0 with the worker
  at 68 checks and 0 failed. That single run also closed the external-schedule
  gap: the worker's recorded `scheduleHash` is the orchestrator's own
  `fnv1a64:57d4189e5a941ecf`, and the faults fired in the orchestrator's order,
  which differs from the order the scenario generates for itself. At 59.1
  minutes elapsed it is correctly flagged below the rehearsal window, so it is
  orchestration evidence rather than a third rehearsal.
  **Harness defect fixed 2026-08-10, with no runtime evidence produced.** The
  four-hour soak would have measured the test harness rather than the libraries:
  `CheckRunner` held every `CheckResult` alive, this scenario emits about 3848
  of them in 15 minutes — roughly 61 500 over four hours — and its own
  `resources` check fails a managed heap that rises at every periodic sample. So
  the instrument would have produced exactly the signal it exists to detect.
  Counts are now counters and stay exact, failures and skips are never sampled
  away, successful detail is a bounded per-check sample, and the complete detail
  is streamed to `checks.ndjson`. Short modes keep their previous behaviour and
  artifact set. `ResourceMatrix` is untouched; the point was to let it measure
  the libraries. Verified by a new console test project,
  [`runtime_tests/Shared/NekoLib.RuntimeTests.Harness.Tests/`](runtime_tests/Shared/NekoLib.RuntimeTests.Harness.Tests/NekoLib.RuntimeTests.Harness.Tests.csproj),
  9 of 9 assertions passing on both targets. **No scenario or campaign was
  executed for this change.**
  **Retention follow-up, 2026-08-10, also with no runtime evidence.** The first
  pass left failures and skips unbounded, so a failure storm — a soak whose
  subject breaks early and fails every check of every cycle for hours — could
  still exhaust memory and cost the run the two things worth having at that
  point, a written summary and a clean cleanup. Every category is now bounded
  with its own budget. A second defect was that a failed write to
  `checks.ndjson` was swallowed while the artifacts went on asserting the log
  held every result; a failed write now keeps the checks running, is reported
  boundedly, marks the log incomplete in `result.json` and `summary.md`, and
  returns the new `ExitCodes.EvidenceIncomplete` (9). The log's cost was
  measured rather than assumed: 65 000 results in 0.64 s on `net9.0` and 0.68 s
  on `net481`, so flush-per-line was kept — buffering would have traded 0.005%
  of a four-hour run for the property that a killed process still leaves
  everything up to its last complete line. 14 of 14 harness assertions pass on
  both targets.
  **Bounded retention executed on 2026-08-11.** A `net9.0 --soak 3m` probe
  exited 0 after 181.5 seconds with 908 checks, zero failed/skipped, 908 valid
  `checks.ndjson` lines matching the exact phase totals in `result.json`, all
  seven faults passing, 330,928 operations with zero unexpected failures, and
  complete cleanup. This closes the only unobserved wiring change. Under the
  outcome-first decision E3-OBS is complete. A 25-minute heap comparison or a
  four-hour soak remains optional targeted confidence; neither is needed to
  repeat fault or target coverage already proven.
  **Artifact layout finding closed on 2026-08-10:** layout v2 places this
  worker's result at
  `<campaign-id>/workers/E3-OBS-net9.0/E3-OBS/result.json`, records the layout
  and worker id in its evidence, and lets the orchestrator reconcile the exact
  indexed path. A 12-second orchestrated regression exited 0 with 91 checks and
  matching worker/aggregate schedule hash. The standalone v1 path remains
  supported and its historical artifacts were not moved. This regression is
  layout evidence, not a replacement for the completed runtime modes. A
  four-hour soak is now optional targeted confidence.
  **Runtime findings recorded, not fixed** (product changes need separate
  authorization, and none of these is a defect): `LogEntry.TimestampUtc` is
  stamped before the dispatch lock, so under concurrent writers it is not a
  delivery-order key — the scenario asserts the documented delivery order and
  records the inversion count as an observation; an abandoned
  `ITelemetryOperation` is invisible, with no `IDisposable` and no record, which
  the scenario asserts as current behaviour rather than as a guarantee;
  `InspectionProvider.Current` is typed `IInspectionRecorder`, so the
  process-wide slot is push-only and reading needs `IInspectionSnapshotSource`;
  and `CaptureState()` applies no budget where `CaptureSnapshot(max, timeout)`
  does.
- [x] **Shared harness boundary — validated by its second consumer, 2026-08-09.**
  The [`runtime_tests/Shared/NekoLib.RuntimeTests.Harness/`](runtime_tests/Shared/NekoLib.RuntimeTests.Harness/README.md)
  boundary was accepted on 2026-08-08 with one consumer, which made it a guess
  that happened to compile. E3-OBS was written against it and the split
  survived, with one piece moved out, one moved in, and two candidates refused:
  - **Out:** `ResourceSample.ConnectionsCreated` and `.ServerSessions`, which
    are SQL Server concepts the shared sampler had no business owning. The
    suite requires "active/retained item counts for bounded components", which
    is a real requirement with no shared answer, so scenarios now declare their
    own columns through `IScenarioSamples`. `ServerSessions` turned out to be
    **dead** — no caller ever passed it, so every E4-SQL row recorded `-1`.
  - **In:** `RunSummary` — the exit-code precedence and the `result.json`,
    `summary.json` and `summary.md` documents, which the suite specifies as one
    contract and which E3-OBS needed byte-identically. About 200 lines left
    E4-SQL's `Program.cs`. A per-phase breakdown was added so a scenario whose
    phases are independent capabilities shows one failing at a glance.
  - **Also in:** `CheckRunner` now takes the run's cancellation token. Without
    it, Ctrl+C during a soak reported every in-flight check as a failure —
    E3-OBS's first interrupt test produced "5 failed" for a run that was merely
    stopped. E4-SQL had the same flaw.
  - **Refused:** the Ctrl+C handler stays duplicated in both scenarios, because
    moving it would mean `ScenarioHost.Run(...)` and the harness would start
    driving, which its README promises it does not. `--smoke-duration` stays in
    E3-OBS because rule 2 has no exception for symmetry with
    `--rehearsal-duration`.
  **Regression evidence:** E4-SQL keeps every check and assertion it had, and
  its recorded determinism hash is still `fnv1a64:49a3ab65b5f249e9` on both
  targets. Two artifact details changed and neither is an assertion: the dead
  `server_sessions` column is gone, and `result.json` gained `checksByPhase`.
  **Third-consumer check closed, 2026-08-11.** E3-DEV, E3-PIPE, E3-WDOG, and
  E3-NAV subsequently became real consumers, bringing the scenario count to six.
  `WorkloadCounters` survived serial operations, pipe traffic, process
  generations, and Navigation cycles without a shared vocabulary change.
  E3-NAV reused the existing schedule, artifact, check, sampling, summary, and
  counter contracts without expanding the harness; its native host and workload
  controls stayed scenario-owned.
- [x] **E3-PIPE — Pipes long-running and recovery.** **First pass delivered
  2026-08-10** at [`runtime_tests/Pipes/LongRunningRecovery/`](runtime_tests/Pipes/LongRunningRecovery/README.md):
  one executable in three roles — controller, server child, client child — so the
  suite's separate processes are real processes on a real named pipe without
  three project files. Twelve checks across four phases cover payload sizes,
  concurrent correlation, the `not_found` and `exception` error contracts,
  timeout without corrupting the next response, reconnect cycles, event ordering
  and subscriber churn, over-limit inbound and outbound frames, malformed and
  truncated raw peers, and disposal followed by endpoint release and rebinding.
  Both targets build with no warnings.
  **It answered the harness question negatively, which is the useful outcome:**
  the harness gained nothing. The controller owns the single `RunArtifacts` and
  writes the one `result.json` the suite specifies; children are workload rather
  than workers. Multi-process support would have been speculative generality for
  a shape one scenario needs.
  **Determinism defect caught by its own check:** the first fault vocabulary
  interpolated the run's pipe name into each fault target, and that string is
  covered by the schedule hash while the pipe name derives from a campaign id
  carrying the target framework and a millisecond timestamp — so the same seed
  produced a different hash on every run. A fault target must describe the class
  of resource, never the instance. Fixed; the schedule is now
  `fnv1a64:42db44086ce556a2` across runs and both targets.
  **Completed the same day.** All three modes and all six fault kinds are
  implemented: killing the server and a client, a raw peer closing mid-frame, a
  handler delay forcing a timeout, a slow subscriber overflowing its queue, and
  disposal with a connection, a request and a subscriber all live. Client
  children run for the two fault-bearing modes and their own totals become a
  check, so a killed client is part of the verdict rather than decoration. The
  later additions also closed the specified gaps: both bounded-queue overflow
  policies with the truthful dropped count (`EventMetrics.Failed`, since the hub
  completes an overflowed delivery as unsuccessful), server-initiated
  disconnect, and token cancellation as distinct from a deadline expiring.
  **Still not covered:** metric stability under sustained traffic is sampled but
  not asserted, for want of a baseline to derive a threshold from — the same
  position E3-OBS took on memory; `MaxClients` saturation; and event delivery
  across a server restart via `AutoReconnect`.
  **First execution, 2026-08-11: exit 4, and it found a real product defect.**
  A 2-minute `--smoke` probe on `net9.0` — deliberately below the specified
  window and correctly self-flagged as such — passed 21 of 30 checks. Every
  `request` check and every `protocol` check passed in both cycles, so the main
  pipe, the real process boundary, correlation, the error contracts, the frame
  limits and the malformed peers are all sound across processes. The nine
  failures were the four event-hub checks and the private-endpoint rebind. The
  run also gives the first runtime confirmation of the `Finish()`/cleanup
  ordering fix from `4f8980b`: a run *with* failures still completed cleanup,
  finalized a `result.json` carrying every failure, and returned 4 rather than a
  cleanup code. Cleanup was truthful — server child exited 0, endpoint released,
  no process or pipe left behind.
  **Confirmed product defect — `PIPE-EVENTHUB-SLOTS`.** `PipeEventHub` retains a
  subscriber slot after the subscriber disconnects, unless an event is published
  afterwards, so the hub stops accepting subscribers after `MaxEventSubscribers`
  lifetime connections and does not recover on its own. Confirmed on 2026-08-11
  by an authorized minimal reproduction using only the public API: with
  `MaxEventSubscribers = 8`, subscribers 1–8 connected immediately and
  subscriber 9 never connected within 20 s on `net9.0` and 21.6 s on `net481`,
  with `AutoReconnect` retrying throughout and a 750 ms settle after each
  disconnect — longer than the hub's own 500 ms poll. Publishing a single event
  then freed the slots at once, which identifies the mechanism: the keep-alive
  loop polls `pipe.IsConnected`, and on an outbound pipe that only becomes false
  once a write is attempted. It is not target-specific. The practical exposure
  is an application whose subscribers come and go during quiet periods.
  **Accepted decision, 2026-08-11:** fix the slot lifetime inside
  `PipeEventHub` under a narrow unfreeze, without touching the public API,
  `MaxEventSubscribers`, `PipeServer`, framing, ACL, metrics or the overflow
  policies, and without exposing any heartbeat to the consumer.
  **Fixed under the authorized narrow unfreeze, 2026-08-11.** A local design
  spike first proved the managed design across `net481` and `net9.0`, under both
  `PlatformDefault` and `CurrentUserOnly`: the event server can use
  `PipeDirection.InOut` while the existing `In`-only `PipeEventClient` remains
  unchanged, event frames remain intact, a server read stays pending while the
  client is live and returns EOF when it closes. The hub now parks that read,
  discards any subscriber input, and lets the existing idempotent removal path
  stop the single writer and release the limiter slot. The former 500 ms
  `IsConnected` poll is gone. No public API, client, framing, ACL policy, metric,
  queue or overflow contract changed. The alternative native zero-byte
  `WriteFile` probe was rejected: it passed on `net481` but killed the `net9.0`
  process asynchronously with `0xC0000005` in the runtime's IOCP callback.
  Focused regressions pass 3/3 on each target, and the full Pipes suite passes
  44/44 on each target. The original public-API reproduction now exits 0 with
  all 9 subscribers connecting on both targets and both pipe names released.
  Explicit product and scenario builds emit zero warnings.
  **Scenario defect fixed the same day — `PIPE-REBIND-RACE`.**
  `dispose-and-rebind-a-private-endpoint` asserted `Endpoint.IsBound` on the
  line after `server.Start()`, but `Start()` hands its accept loop to the thread
  pool, so the name appears shortly afterwards. The check now waits boundedly
  for the bind, in the same style the check already used for the release. A
  re-run confirmed it: the rebind check passes in both cycles at 237 ms, and the
  probe's failures dropped from nine to seven, leaving only the event-hub
  cluster. `PipeServer.Start()`'s contract was not changed.
  **Second scenario defect fixed — `PIPE-OVERFLOW-ENDPOINT`.** Both private
  overflow checks constructed `PipeEventHub` from a base name, which exposes
  `<base>.events`, but attempted to connect their raw subscriber to `<base>`.
  The subscriber never reached the hub and each check expired in `Connect(5000)`.
  Both now resolve the canonical event endpoint through `Endpoint.EventsFor`.
  **Standalone development gate passed on both targets after the fixes:** the
  same two-minute command completed four atomic cycles in 133 seconds with
  75/75 checks passing, zero skipped, exit 0 and complete child/endpoint cleanup
  on `net9.0` and `net481`. These deliberately short runs remain recorded as
  development probes.
  **The outcome-first gate closed on 2026-08-12**, and closing it took three
  attempts that are all preserved. The compact `net9.0` recovery sweep
  (`--recovery-rehearsal --rehearsal-duration 10m --seed 20260808`) finally
  passed **37/37 checks, zero failed, zero skipped, exit 0 in 530.6 seconds**,
  with the **recovery phase 6/6**: every scheduled fault reached its expected
  terminal and its post-recovery probe. Counters recorded 347 operations, 317
  successes, 28 expected failures and **zero unexpected failures**; the two
  surviving client children reported 3,497,665 requests with **zero mismatched
  correlation**, the third having been destroyed by `kill-client-process` by
  design. `cleanupProblems` and `setupGaps` were both empty, the replacement
  server exited 0, the endpoint was released, and no scenario process or pipe
  remained. Artifact:
  `artifacts/validation/phase-e/e3pipe-recovery-net9.0-s20260808-20260812T135339773Z`,
  schedule `fnv1a64:9bb70da48460e7bd` persisted before launch. The run records
  `belowSpecifiedWindow: true`, which is expected and preserved: it closes fault
  coverage, not the nominal 60-90 minute rehearsal window.
  **The first two attempts found two more scenario-oracle defects, and neither
  was a `NekoLib.Pipes` finding.** `PIPE-KILLSERVER-TERMINAL`: the
  `kill-server-process` check captured only the exception from the in-flight
  request and read "no exception" as the request having survived, but
  `PipeClient.SendAsync` substitutes a `PipeMessage` carrying `Ok=false` and
  `Error.Code="connection_closed"` when the pipe closes before the response
  frame. `PIPE-RECOVERY-CASCADE`: that failed assertion aborted the check before
  `RestartServer`, so the run continued with no server and sixteen further checks
  failed against an absent endpoint. A third, `PIPE-CHILD-REPORT-RACE`, only
  became visible once the topology stayed healthy — the client children then ran
  to their full lifetime instead of quitting early against a dead server, so
  `client-children-correlation` read result documents that did not exist yet.
  All three were fixed in the scenario only and committed in `698960a`; the
  terminal classification is pinned by 18 isolated `--contracts` assertions that
  open no pipe and start no process, passing on both targets. No file under
  `src/Pipes`, the shared harness or `campaign.json` changed, and the schedule
  hash is unchanged.
  **Provenance, stated exactly:** the three 2026-08-12 runs executed the fix
  before it was committed, so their artifacts record `repository.dirty: true`.
  That is honest runtime evidence of the code that later became `698960a`; the
  artifacts were not produced from a clean worktree, and a clean-provenance
  repeat is optional confidence rather than a gate.
  **Optional, not required:** duplicate full smoke/recovery windows, a
  `net481` runtime repeat, and a four-hour soak, absent a new duration-dependent
  finding. E3-PIPE remains out of `campaign.json`; the standalone eligibility
  condition is met, but registration is a separate decision.
- [x] **E3-WDOG — deployed-Host crash and recovery.** **Scenario source
  delivered 2026-08-10** at
  [`runtime_tests/Watchdog/CrashRecovery/`](runtime_tests/Watchdog/CrashRecovery/README.md):
  an independent controller owns the single artifact/result contract and exact
  PID/path registry; a separate application child calls the public bounded
  bootstrap/attach API; and the separately deployed Host owns supervision,
  restarts, public Pipes RPC, log forwarding and crash-bundle finalization. The
  deterministic plan is persisted before first launch, and the child writes a
  durable campaign/event/generation `armed` record before each planned terminal.
  Six fault kinds cover repeated ordinary exits, unhandled crashes, a
  twelve-terminal fast loop that guarantees two current ten-second cooling
  windows, Host shutdown/restart, paused clean child shutdown, and repeated
  bootstrap. Every recovery requires one exact Host/child pair, a newer durable
  generation, a real health RPC and one unique forwarded log token. Final checks
  cover pending finalization, bundle identity/completeness, manifest checksums,
  Host status/log tail, retention at ten and exact-process cleanup. No product
  module or shared harness source changed; the scenario health pipe and terminal
  plan are workload-owned controls rather than a product `TestControl` surface.
  `net481` and `net9.0-windows` build, 7/7 isolated contract checks pass per
  target, and repeated
  `--print-schedule` output for recovery seed `20260810` is hash-stable across
  targets (`fnv1a64:677fcab193b16fbf`). **Corrected two-minute source-layout
  probes passed on both targets on 2026-08-11.** Each persisted the same smoke
  schedule before its first process (`fnv1a64:7d3f49941df33843`), exercised all
  six planned faults and seven healthy generations, passed 20/20 checks with
  nothing skipped, finalized its bundle and artifacts, released both endpoints,
  left no process behind, and exited 0 in about 123 seconds.
  The first two `net9.0-windows` attempts had exposed one confirmed product
  defect: `Process.GetProcessById()` did not retain the initially attached
  application's handle, so its later `ExitCode` read became null after a normal
  exit. The narrowly authorized fix materializes that handle while the process
  is alive. A focused self-bootstrap regression that discards the launcher's
  handle failed before the fix and passes on both targets after it; the complete
  Watchdog suite passes 84/84 per target. No public API, pipe protocol,
  bootstrap, restart policy or scenario oracle changed.
  `net481` then exposed two scenario defects: immediate exact-identity adoption
  could observe a transient unavailable `MainModule.FileName`, and child-owned
  persisted integer strings were sent through the shared JSON numeric reader.
  Adoption now retries the same exact PID/path/start-time identity for a bounded
  five seconds, and E3-WDOG parses only its own persisted integer-string
  contract with invariant checked `Int64`. The shared harness is unchanged.
  These deliberately short source runs remain development probes, and their
  source-staged layout is explicitly not package evidence. **The final
  outcome-first gate passed on 2026-08-11:** canonical `eng\pack-local.ps1`
  created immutable version `1.0.0-local.10` from clean commit `46befc6` with a
  captured exit 0, and a disposable PackageReference consumer deployed the
  exact Host payload. The `net9.0-windows` package-backed run at
  `artifacts/validation/phase-e/e3wdog-smoke-net9.0-s20260810-20260811T235447397Z`
  passed 20/20 checks with all six faults, seven healthy generations, bundle
  integrity/retention, released endpoints, zero cleanup problems, and process
  exit 0. `result.json` records package SHA-256
  `acc31d9f2450cc14d36ba6e723357a706dcf0b90d2ed1116f11201787b574710` and
  proves that the deployed bytes match
  `tools/net9.0-windows7.0/win-x64/NekoLib.Watchdog.Host.exe` inside the package.
  Its truthful `belowSpecifiedWindow: true` means it is not smoke-window
  evidence; it does not weaken the distinct package-topology proof. Existing
  dual-target source evidence already covers target behavior, so a `net481`
  package repeat, full mode windows, and a four-hour soak are optional. E3-WDOG
  remains out of `campaign.json`; registration is a separate decision, not an
  outcome-first closure gate.
- [x] **E3-DEV — Devices virtual-COM soak and recovery.** **Automated modes
  delivered 2026-08-10** inside the existing
  [`runtime_tests/Devices/Com0Com/`](runtime_tests/Devices/Com0Com/README.md)
  project: the harness reference, the three-mode contract, a scenario-owned peer
  on the far end of each pair, and the fault dispatcher. All five fault kinds are
  implemented — peer delay, silence, malformed frame, disconnect and restart —
  together with open/close/reopen cycles, finite timeouts, cancellation under
  both infinite and finite configured port timeouts, endpoint switching through
  all three entry points, operation serialization, chunked delivery on both sides
  of the quiet period, configuration and encoding parity, and disposal during an
  active operation. Both targets build with no warning from the scenario, and
  the seeded schedule is hash-stable: `fnv1a64:7496700bf4b75339` on `net481` and
  `net9.0` and across repeated runs, with no COM name in any fault target.
  **The interactive path is intact**: without a mode flag the executable runs
  the original oracle parity pass with the same options, output and exit codes,
  so the 2026-08-01 evidence still describes it. Its code moved from `Program.cs`
  to `OracleParity.cs` unchanged.
  **Two requirements were met differently from the literal wording, on purpose.**
  The suite asks that a timed-out operation's response not be consumed by the
  next one; a serial line is a byte stream with no correlation, so that is not a
  property `SerialCommTransport` can offer. It is split into an assertion about
  the real recovery — close, reopen, and the next request gets its own token —
  and an assertion about what holds without one: every byte belongs to one
  identifiable exchange, intact, never mixed. Second, non-`None` handshakes are
  applied and read back but never opened and written through, because a
  handshake nobody asserts on the far end can block a write for the whole write
  timeout.
  **First runtime, 2026-08-11.** The real preflight proved both documented
  cross-connections. The first two-minute `net9.0` probe exited 4 after 127
  seconds with 156 passed and 26 failed checks: the same two scenario checks
  failed in all 12 cycles. `a-gap-beyond-the-quiet-period-ends-the-read` waited
  1500 ms for a 19-byte response sent in 3-byte chunks with six 300 ms gaps, so
  it reopened before the final `\r\n` arrived. `configuration-parity` asserted
  that RTS/CTS snapshots would be rejected, but runtime accepted them. A focused
  public-API probe confirmed all four `RequestToSend*` / `RtsEnable` combinations
  on both targets without opening a port, so this was not a product limitation.
  The slow-chunk check now derives a bounded settle from the frame length, chunk
  size and gap count; the configuration check asserts accepted values and exact
  `PortInfo` read-back. No `src/Devices` file changed.
  **Corrected standalone development probes pass on both targets:** `net9.0` and
  `net481` each completed 11 cycles in 124 seconds with 168/168 checks passing,
  zero failed, zero skipped and exit 0. Both peers closed normally, cleanup
  reopened and released COM19, COM20, COM9 and COM10, and no scenario or emulator
  process remained. These runs remain development probes and exercise no
  scheduled recovery fault.
  **The outcome-first gate closed on 2026-08-12, first attempt.** With
  NekoPcbEmulator confirmed stopped and all four ports verified free, the compact
  `net9.0` recovery sweep
  (`--recovery-rehearsal --rehearsal-duration 10m --seed 20260808`) passed
  **33/33 checks, zero failed, zero skipped, exit 0 in 467.2 seconds**, with the
  **recovery phase 5/5**. Preflight proved both cross-connections with a real
  exchange. Each peer fault reached its expected terminal and was followed by a
  clean request: the delay timed out at 403 ms rather than waiting for the peer;
  two restarts left the caller's port working without reopening; the disconnect
  produced no data and no exception in 804 ms; the malformed frame was rejected
  on CRC-16/CCITT-FALSE (`0xE52D` against the expected `0x1A2D`); and the silent
  peer produced the documented no-data result three times before the same
  transport served normally. PCB-A text framing and PCB-B binary framing/CRC both
  passed in each matrix pass. Counters recorded 99 operations, 75 successes, 20
  expected failures, 4 cancellations and **zero unexpected failures**. Cleanup
  closed both peers and **reopened and released COM19, COM20, COM9 and COM10**,
  with `cleanupProblems` and `setupGaps` empty and no scenario or emulator
  process remaining; the four ports were independently re-verified free
  afterwards. Artifact:
  `artifacts/validation/phase-e/e3dev-recovery-net9.0-s20260808-20260812T140422862Z`,
  schedule `fnv1a64:ca1bf7c85e9c5f48` persisted before the first exchange. The
  run records `belowSpecifiedWindow: true`, expected and preserved.
  **What this evidence is and is not.** It is real Windows serial API behaviour
  against a real com0com driver with the independent emulator stopped and the
  scenario owning both ends. It is **not** protocol parity — that claim belongs
  to the oracle pass alone — and **not** physical hardware: com0com is a virtual
  pair and emulates no baud, framing, line levels, wiring or electrical
  behaviour. Its `repository.dirty: true` comes solely from the then-uncommitted
  E3-PIPE scenario fix; nothing under `src/Devices` or this scenario changed.
  **Optional, not required:** a `net481` runtime repeat, full nominal windows,
  and a four-hour soak. Registration in `E3-ORCH`'s `campaign.json`
  remains deferred because a COM-pair prerequisite and adoption contract the
  orchestrator cannot validate would be a claim rather than a fact.
  **Design decision taken 2026-08-10, before any code.** The specification asks
  for faults of "emulator delay, silence, malformed frame, disconnect, restart",
  and the emulator cannot supply them: it is an independent oracle in another
  repository with no reference to NekoLib, and giving it a control channel would
  make it an accomplice rather than an oracle — the same objection that keeps a
  `TestControl` API out of every product module.
  The answer is E3-PIPE's raw peer applied to serial: **the scenario opens the
  other end of the com0com pair and acts as its own peer**, able to delay, fall
  silent, send a malformed frame and disconnect on demand. That makes the
  automated modes and the oracle pass **mutually exclusive**, because both want
  `COM9`/`COM10`, and that is the right trade: they prove different things. The
  oracle proves protocol parity against an independent implementation; the owned
  peer proves transport behaviour under faults nobody can ask the oracle to
  produce. Both remain recorded, neither replaces the other.
  **Prerequisite state on this machine, executed 2026-08-11:**
  `SerialPort.GetPortNames()` reports COM1, COM3, COM4, COM9, COM10, COM19 and
  COM20. The preflight opened both ends and proved `COM9 <-> COM19` with a real
  text PING and `COM10 <-> COM20` with a real binary PING. It still treats a
  missing, occupied or mispaired port as exit code `3` — an environment result,
  never a product finding.
- [x] **E4-SQL — Data against local SQL Server.** **Scenario source delivered
  2026-08-08** at [`runtime_tests/Data/SqlServer/`](runtime_tests/Data/SqlServer/README.md):
  a dual-target x64 console scenario with smoke, recovery-rehearsal and soak
  modes, `Microsoft.Data.SqlClient` confined to the scenario project, container
  adoption that restores the state it found, and the artifact and exit-code
  contracts. Both targets build with no warnings and the exit-code contract was
  exercised. Schedule determinism is verified: the same seed produces
  `fnv1a64:49a3ab65b5f249e9` on `net481` and `net9.0`.
  **Smoke passed on both targets with exit code 0 on 2026-08-08** against SQL
  Server 16.0.4265.3 Developer Edition — 28 checks on `net9.0`, 27 plus a
  correctly skipped streaming check on `net481`, zero unexpected failures, both
  producing the same data digest. This is the repository's first real-server
  provider evidence: mid-flight cancellation, pool reuse and the
  `DynamicMode.IL` schema cap are now executed rather than argued.
  **Recovery rehearsal passed inside the specified window on 2026-08-08:**
  82 minutes on `net9.0`, exit 0, 31 checks, 0 failed, all seven fault handlers
  proven with their provider error numbers recorded, 4871 operations and zero
  unexpected failures. It has to be `net9.0`: `net481` skips the streaming
  fault, so it covers six of seven, and it passed a separate 10-minute run.
  **The soak path is proven as of 2026-08-08:** `--soak 15m` exits 0 with 85439
  checks passed, 0 failed, and 335283 operations at zero unexpected failures,
  with all seven faults executing concurrently with the workload.
  Getting there took three runs and found three scenario defects, none of them
  in `NekoLib.Data`, and none findable by smoke or the rehearsal — only the soak
  overlaps assertions with faults. In order: assertion matrices ran concurrently
  with container-stopping faults and an out-of-check digest turned that into a
  dead process rather than a red check; cleanup restarted the container only
  when the run had found it running, so a run that ended with the server down
  leaked its database; and steady-state background traffic shared the workspace
  whose lifecycle counters several checks zero and assert on. All three are
  fixed and recorded in the scenario README.
  Under the 2026-08-11 outcome-first decision, this closes E4-SQL: both targets
  have real-provider smoke/cancellation evidence, every fault is covered on the
  representative target that supports it, target-specific streaming behavior
  is recorded, and the soak path already overlapped all faults with sustained
  work. A four-hour isolated SQL run remains optional duration confidence.
  **Container revalidated 2026-08-08 by querying Docker directly:** the pinned
  `mcr.microsoft.com/mssql/server:2022-CU26-ubuntu-22.04` image resolves to
  digest `sha256:ba4c8329f48fb8f02e1416be6a930ebfd71268caee78aa985f3af4315e457c89`,
  the container was found `exited`, and two setup gaps were recorded rather than
  corrected: the port publishes on **every host interface** rather than
  loopback only, and there is no volume, so the data lives in the container's
  writable layer and survives restart. `NEKOLIB_SQLSERVER_PASSWORD` is not set
  on the machine, which is what currently blocks execution.

**Failure intake:** record every initial failure in the owning scenario's
verification record and artifacts with repository commit and dirty state,
environment/version matrix, command, seed and schedule event where applicable,
expected versus actual result, and a minimal reproduction. Classify it first as
an environment/setup failure, a scenario/oracle defect, or a reproducible
product defect. Only a product defect confirmed against current source and given
an accepted implementation direction receives a stable item in this roadmap.
Unconfirmed observations remain scenario evidence; build failures and missing
prerequisites are not silently promoted into product work.

Required coverage:

- **Navigation:** thousands of page switches; forward/back and login/logout
  cycles; reset cycles; repeated `Start`/`Shutdown`; cache reuse; weak and
  strong page lifetimes; background loading; redirects; guard rejection;
  overlay repetition; idle-timeout cycles; memory growth; retained handlers;
  disposal.
- **Logging:** sustained writes at expected PDV volume; sink-failure isolation;
  rolling-file rotation; retained-file count; flush during an incident;
  shutdown/disposal; bounded recent snapshots.
  **First consumption — 2026-08-08, scenario `4186e48`.** The FarmDatabase simulation writes its
  measurements through `Logger` and `RollingFileLogSink`; this is the first use of
  the module anywhere in the repository. Only sustained writing and
  shutdown/disposal are exercised, and deliberately at a low rate: the sink opens
  and closes the file per entry, so the scenario accumulates in memory and emits
  one rolled-up line per ten-second window rather than one per tick.
  **Rotation, retained-file count, sink-failure isolation and flush-during-incident
  remain unexercised** — the log has never grown past a few kilobytes.
- **Telemetry:** bounded retention; operation completion; abandoned operations
  if relevant; checkpoint ordering; correlation; snapshot behavior under
  sustained activity.
  **First consumption — 2026-08-08, scenario `4186e48`**, from the same scenario: one completed
  operation with measurements per window, under sustained activity, against a
  bounded pipeline. **Checkpoints, correlation, abandoned operations and snapshot
  reads have no coverage yet.**
- **Inspection:** bounded operation retention; state-provider timeout; provider
  failure isolation; enable/dispose cycles; no action rollout.
- **Pipes:** sustained request/response; reconnect; subscriber churn; slow
  subscribers; frame-size failures; timeouts; dispose while active;
  memory/thread growth.
- **Watchdog:** repeated child exit/restart; clean shutdown; fast crash loops;
  attach/bootstrap; logging forwarding; crash-bundle finalization; Host restart
  behavior; no duplicate supervision.
- **Devices:** repeated timeout and recovery; reconnect; delayed responses;
  endpoint-switching rules; serialized operations; transport disposal;
  cancellation; late-response isolation.
- **Data:** repeated connection/session use; disposal; transaction cycles;
  streaming cleanup; provider failures; cancellation where supported.
  **Partially delivered — 2026-08-07** by the
  [`Data / FarmDatabase`](runtime_tests/Data/FarmDatabase/README.md) simulation,
  committed as `4186e48`,
  which commits one transaction per tick and has been run for tens of thousands of
  ticks unattended on both engines. Covered: repeated session use and disposal,
  transaction cycles, streaming, and provider failure through a forced
  mid-transaction rollback. Each run verifies state against the database, checks
  invariants every tick, and reconciles the audit trail by delta, reporting the
  outcome as an exit code. Cancellation is covered separately by an
  already-cancelled-token probe over every entry point including the insert; only
  mid-flight cancellation is unclaimed, because a local file database completes
  faster than a cancellation can be timed. No new runtime framework was
  introduced: the simulation drives the same gateway calls the UI does.

Measure success where practical: no unbounded memory or handler growth; no
leaked process, thread, or pipe handles; no deadlock; no unreleased semaphore
or gate; bounded queue behavior; deterministic cleanup; and expected terminal
outcomes. Do not invent hard performance thresholds before measurements exist.

### E4 — Real integration validation

- [ ] Close the gap between in-process/fake evidence and external-system
  behavior.

Devices:

- [x] validate real or emulated COM-port behavior while preserving an
  independent emulator/test oracle and real protocol readiness checks. The
  versioned [`Devices / com0com serial parity`](runtime_tests/Devices/Com0Com/README.md)
  scenario passed on `net481` and `net9.0` on 2026-08-01;
- do not infer physical serial correctness from TCP, named-pipe, stream, or
  fake transports;
- document prerequisites, cleanup, platform assumptions, and driver
  assumptions.

Data:

- [ ] use SQLite as the local relational baseline for connection, transaction,
  mapping, cancellation, streaming, and failure behavior with a deliberately
  created fixture. **Partially delivered — 2026-08-07** by the versioned
  [`Data / FarmDatabase`](runtime_tests/Data/FarmDatabase/README.md) scenario,
  committed as `4186e48`:
  connection, transaction, mapping, streaming, and failure behaviour are covered
  against a created SQLite fixture, including a forced mid-transaction rollback
  and a streaming read verified against `COUNT(*)`. Cancellation is covered by
  handing every entry point an already-cancelled token: session opening, raw,
  typed, dynamic, callback, streaming and insert all refused, on both engines and
  both target frameworks. Mid-flight cancellation against a local file database
  cannot be arranged deterministically and is not claimed. The measurement also
  shows `SynchronousFallbackMode` defaults to `Disabled` and that **neither
  provider reaches the blocking-fallback path** — no `NotSupportedException` is
  raised by either, so the fallback is unexercised rather than silently active;
- [x] verify positional OleDb/Access binding and DML on `net481`, recording the
  installed provider, architecture, fixture, and machine prerequisites. Delivered
  2026-08-07 against ACE OLEDB 12.0, x64, `net481`, with a created `.accdb`
  fixture; prerequisites and the bitness constraint are recorded in the scenario
  README. The same seed produced identical state on both engines and on both
  target frameworks, and positional binding was exercised through DML,
  transactions, and every `QueryBuilder` clause. Two defects were found and fixed
  as DATA-021 and DATA-022 below;
- [x] select at most one initial server provider from SQL Server, PostgreSQL, or
  MySQL based on an actual consumer. **Selected 2026-08-08:** SQL Server, hosted
  locally as an official Linux container on WSL 2 on the Windows AMD64 validation
  machine. This is a development-validation topology, not a production-hosting
  claim;
- [x] implement and execute the versioned SQL Server scenario specified by the
  [`Phase E scenario suite`](runtime_tests/PHASE_E_SCENARIO_SUITE.md), validating
  pooling/data-source ownership, network failure and recovery, mid-flight
  cancellation, transactions, mapping, streaming cleanup, and dynamic-result
  lifetime before claiming support. **Implemented and executed 2026-08-08** —
  both targets passed real-provider smoke, representative recovery proved all
  seven faults, and a 15-minute soak overlapped faults with sustained work; see
  the E4-SQL entry under Phase E runtime scenario delivery.
  The scenario required no change to `NekoLib.Data`: it reaches SQL Server
  through the existing `IDbConnectionFactory` seam and `SqlServerQueryTranslator`,
  which is the topology this roadmap asked to preserve;
- keep concrete provider packages outside the relational core and record exact
  package, target, architecture, and server versions in validation evidence;
- promote provider-native parameter hooks, data-source lifecycle adapters, or
  cancellable factory expansion only from a concrete gap observed in this
  provider evidence;
- distinguish translator/build tests, fake ADO.NET contract tests, and real
  command execution truthfully;
- do not cite tracked-but-unused fixtures as coverage and do not include MongoDB
  in the relational provider matrix.

Provider evidence and separate follow-ups:

- [x] **Validate dynamic-result lifetime against a real provider — done
  2026-08-08.** `DynamicMode.IL` was enabled against SQL Server with genuinely
  varying row shapes (rotating aliases *and* projected SQL types, not values
  under one shape) on both targets. Measured: eight distinct shapes emitted
  exactly eight types and repeating them added no cache misses; the process-wide
  cap was crossed deliberately at twelve; the thirteenth shape threw
  `InvalidOperationException("Dynamic IL schema limit reached (12)")` and emitted
  nothing; a context permitting fallback answered the same new shape with Expando
  and still emitted nothing; already-emitted shapes and ordinary typed queries
  kept working past the boundary. The cap **falls back or fails as designed and
  does not leak** — but the emitted count never fell, which is the honest
  reading: Reflection.Emit types live in a non-collectible assembly for the life
  of the process, and the cap bounds emission, not lifetime. The per-context
  `MaxDynamicSchemas` is locked by the first IL use and later contexts cannot
  reconfigure it, exactly as DATA-012 specified.
  The later 15-minute SQL soak exercised the sustained dynamic-shape workload;
  no four-hour lifetime claim is made or required by outcome-first acceptance.
- [x] **Cancel an operation in flight — done 2026-08-08.** A remote engine that
  can be told to wait is what made the question answerable. Each command is
  marked with a comment, the scenario polls `sys.dm_exec_requests` until SQL
  Server reports that exact batch executing, and only then cancels — a
  server-visible start signal rather than a wall-clock sleep. Raw, typed,
  dynamic, callback, streaming and transaction-bound paths were all interrupted
  after execution had begun, on both targets, each with one cancellation
  terminal, **no success terminal**, and a successful probe afterwards through
  the same gateway and through a freshly pooled connection. A cancelled
  transaction committed nothing and disposed without throwing.
  **Recorded consequence for callers:** the terminal is not one type. A command
  held open by `WAITFOR DELAY` cancels as `SqlException` number 0; one waiting on
  a row lock cancels as `TaskCanceledException`. Application code catching only
  `OperationCanceledException` would miss the first. This is provider behaviour,
  not a NekoLib defect, and it does not by itself justify a Data change — but it
  belongs in the Data documentation when cancellation is next described.
  The already-cancelled-token matrix remains a separate claim and still passes.
- [ ] The scenario's own UI has never been driven automatically; all of its
  evidence is headless. Interactive verification stays manual and is recorded as
  such in the scenario README.

**Promoted from E4 provider evidence — 2026-08-07.** Both are adaptations
demonstrated by real execution against ACE, which is the only basis E4 accepts for
promoting them. Neither adds a provider package to the relational core.

- [x] **DATA-021 — Order subquery predicates first so positional binding is
  correct.** The positional binder rewrites placeholders to `?` in the order they
  appear in the SQL text, which is right; ACE consumes them with the subquery's
  first regardless of where it sits in the clause. A predicate authored before an
  `EXISTS` therefore received the subquery's value. Measured: an integer predicate
  before an `EXISTS` carrying a string produced *Data type mismatch in criteria
  expression*, and with two compatible predicates Access **silently returned zero
  rows where the correct answer was six** — the same builder answering correctly on
  SQLite. `QueryBuilder` now emits subquery predicates before the others so textual
  order and consumption order agree. Predicates combine with `AND`, so meaning is
  preserved, and a query without a subquery keeps its original SQL. Verified for a
  single subquery; the relative order among two or more parameterized subqueries is
  authoring order and has not been measured. Implemented with a dual-target
  regression in `865d90f`.
- [x] **DATA-022 — Translate `COUNT(DISTINCT …)` for Access.** The builder emits it
  for every dialect, but Jet and ACE have never supported it and reject the query
  outright with *Syntax error (missing operator) in query expression*.
  `AccessQueryTranslator` now rewrites it as a count over an aliased distinct
  subselect, which Jet does accept. Both engines answer the same value. Anything
  that is not exactly that shape is left untouched. Implemented with dual-target
  positive and no-regression coverage in `865d90f`.
- Unaliased aggregates return under engine-invented column names — SQLite names the
  column after the expression, Access does not. This is engine behaviour with no
  library fix; callers reading an aggregate by name must alias it. Recorded as
  guidance in the scenario README, not as a defect.

Navigation native WinForms/WPF interactive scenarios belong to E2, not generic
unit coverage. Any Watchdog scenario that claims package behavior must use the
deployed sidecar layout.

### E5 — Pipes and IPC hardening review

- [x] Reverify the current IPC boundary before promoting hardening work.
  Completed 2026-08-08 by the commit-bound
  [`Pipes and Watchdog IPC hardening review`](docs/audit/pipes-ipc-hardening-review-2026-08-08.md)
  against `941e17e`. The review confirms that current Pipes is a local transport,
  not an authorization boundary; Watchdog places read-only, ingestion, and
  process-changing commands on the same unauthenticated endpoint. It also
  confirms the event single-writer gap, subscriber backpressure, and untracked
  in-flight server work during disposal. No product fix is authorized by the
  review itself.
- [x] Accept the E5 threat model and disposition. **Decision 2026-08-08:**
  generic Pipes supports local, same-machine, cooperative callers and is not an
  authorization boundary. Watchdog must restrict RPC and event access to the
  current Windows user on both targets, but resistance to a hostile process
  already running as that same user is explicitly outside the Phase E threat
  model. Do not claim privilege separation from the pipe name, its hash, the
  attach token, or the current-user restriction. The correctness and lifecycle
  items below are accepted independently of this security boundary.

Historical leads include pipe ACL/security, a per-subscriber bounded queue, an
explicit drop policy, and graceful drain of in-flight work during disposal.
The review reverified and promoted only the bounded work below.

#### E5.1 — Declare and enforce the local-user boundary

- [x] Document the generic Pipes trust contract and add an opt-in current-user
  server policy without changing the compatibility default for generic callers.
  Use `PipeOptions.CurrentUserOnly` on `net9.0-windows` and an explicit
  current-user ACL on `net481`. **Implemented 2026-08-08:**
  `PipeAccessPolicy.PlatformDefault` preserves the existing generic behavior;
  `CurrentUserOnly` is shared by RPC and event server creation. The root module
  map records that this policy is an OS-user boundary, not authorization against
  another process already running as that user.
- [x] Enable that policy for both Watchdog RPC and event endpoints. Add focused
  same-user success coverage on both targets and a Windows identity-boundary
  probe where the test environment can provide another account or elevation.
  Record an explicit manual disposition when that environment is unavailable;
  do not represent constructor inspection alone as an access-denial test.
  **Implemented 2026-08-08:** Watchdog selects `CurrentUserOnly`; focused RPC and
  event round trips pass on both targets. The `net481` test verifies a protected
  DACL granting the current SID, and the `net9.0-windows` test verifies the
  `CurrentUserOnly` creation flag. No alternate account or elevation token was
  available, so cross-identity denial remains an explicitly unexecuted manual
  probe rather than a claimed runtime result. The `net481` ACL is SID-based;
  unlike the documented `net9` flag, it does not add an elevation-level check.

#### E5.2 — Serialize and bound event delivery

- [x] Give each event subscriber one bounded single-writer queue so concurrent
  `PublishAsync` calls cannot interleave frames. Define observable queue-full
  behavior as best-effort drop or subscriber disconnect; never block Watchdog
  supervision indefinitely in pursuit of lossless telemetry. **Implemented
  2026-08-08:** each subscriber owns one bounded queue and one asynchronous
  writer. `PublishAsync` reports the enqueue attempt rather than waiting for
  pipe I/O. `DropNewest` is the compatibility default and records a failed
  delivery through `IPipeMetrics`; `DisconnectSubscriber` is the explicit
  alternative and fails queued deliveries before removing the subscriber.
- [x] Cover concurrent publishers, a non-reading subscriber, queue overflow,
  cancellation, removal, and unaffected delivery to healthy subscribers on
  both target families. **Implemented 2026-08-08:** dual-target tests exercise
  100 concurrent publishers, a non-reading 512 KiB subscriber, observable
  overflow, healthy-subscriber progress, cancelled publication, and the
  disconnect-on-overflow policy. The existing fan-out test now uses individual
  waits because `WaitHandle.WaitAll` is unsupported when the net481 runner uses
  an STA thread.

#### E5.3 — Own admitted work through shutdown

- [x] Track active RPC client tasks and connected streams. Disposal must stop
  admission, cancel and close active transports, perform a bounded drain, and
  avoid cleanup against already-disposed synchronization primitives. Apply the
  equivalent ownership rule to event accepts where required. **Implemented
  2026-08-08:** a shared internal operation registry admits each RPC/event
  connection before scheduling it, owns its current server stream, closes all
  transports on stop, and exposes a completion that reaches zero only after
  operation cleanup. Synchronous disposal waits up to two seconds; if a user
  handler ignores cancellation, semaphore/token cleanup is deferred until that
  handler actually returns instead of racing its late `Release()`.
- [x] Make the obsolete `WatchdogLogPipeServer` shutdown truthful while it is
  shipped. Its removal remains a breaking-release decision under F1; Phase E
  does not silently remove the public type. **Implemented 2026-08-08:** the
  pending accept is now owned by the instance and disposed before joining; all
  connected writers/streams are also closed before the bounded joins. The
  public obsolete type and its line-oriented protocol remain intact.
- [x] Cover disposal during a cooperative handler, a handler that ignores
  cancellation, a pending accept, and a connected event subscriber.
  **Implemented 2026-08-08:** dual-target Pipes tests cover all four states,
  including deferred completion after the ignoring handler is explicitly
  released. Watchdog tests cover both a pending legacy accept and a connected
  non-reading legacy client, and assert that both background threads terminate.

#### E5.4 — Bound protocol disclosure without inventing privileged IPC

- [x] Replace raw handler exception messages on the wire with a stable,
  sanitized error while retaining detailed local diagnostics. Add focused
  malformed/truncated-frame coverage and make serializer depth explicit only
  where the accepted payload contract requires it. **Implemented 2026-08-08:**
  handler failures retain code `exception` but return the stable message `The
  handler failed.`; the original exception remains available to
  `IPipeMetrics.OnError`. Framing now distinguishes clean EOF from a partial
  length/payload and throws `EndOfStreamException` for truncation. Dual-target
  tests cover clean EOF, truncated frames, malformed JSON, and absence of the
  handler's private exception text. No depth override was added because the
  accepted Watchdog DTOs do not require one beyond the existing 1 MiB byte cap.
- [x] Do not add session authentication, replay infrastructure, remote
  administration, sender-selected CLR types, Instrumentation, or TestControl.
  Revisit authentication and server-identity proof only if hostile same-user
  processes or automatic retries of non-idempotent commands enter an accepted
  threat model. **Disposition confirmed 2026-08-08:** none of those surfaces
  were added by E5.

**E5 closure — 2026-08-08:** complete. E5.1-E5.4 implement the accepted local
user boundary, bounded single-writer event delivery, owned shutdown, compatible
legacy shutdown, and sanitized framing/error behavior. Generic Pipes remains a
cooperative local transport rather than a security framework.

The review must determine:

- the current threat model and whether only same-user trusted processes are
  supported;
- whether Watchdog commands require stronger authorization, whether privileged
  commands exist, and whether an untrusted local process can reach endpoints;
- pipe-name predictability and squatting risk;
- message-size and depth limits, error sanitization, and replay or duplicate
  requests where relevant;
- subscriber backpressure and shutdown behavior;
- target-specific `net481`/`net9.0` differences.

The threat model and bounded implementation direction are now accepted.
Rejected alternatives and rationale stay in the audit. E5 closes only after
E5.1-E5.4 are implemented, validated on both target families, and reconciled
into current technical documentation.
Do not turn Pipes into a service bus or generic security framework. This phase
does not authorize TestControl or Instrumentation IPC.

### E6 — Complete the Diagnostics-sector review

- [x] Revalidate the three remaining findings in
  [`docs/audit/diagnostics-boundaries-review-2026-07-30.md`](docs/audit/diagnostics-boundaries-review-2026-07-30.md).
  Completed 2026-08-08 against committed `HEAD`
  `6e91aea58f08e6227dc26259d1aec1e2911aeb7e`; all three remain confirmed and no
  product code changed during the review.
- [x] Accept the bounded E6 disposition below. **Decision 2026-08-08:** CRASH-01
  closes without a product change; only CRASH-02 and WIN-01 are authorized for
  implementation. The audit is complete and historical.

**Original review baseline:** `1727a1cac3f66666b2df02bc618ad6ab45807a49`.

Phase D already implemented DGN-01, CORE-01, BND-01, LOG-01, CORE-02,
TEST-01, and the accepted DBG-01 rename/boundary direction. E6 resolved the
remaining CRASH-01, CRASH-02, and WIN-01 decisions:

- **CRASH-01 — accepted no-code disposition:** retain `CrashDumpLevel`, the
  `crash.dmp` convention, and the Windows implementation boundary during Phase
  E. Do not invent a neutral artifact API or Linux adapter without a concrete
  second-platform requirement and an explicit migration plan.
- **CRASH-02 — implemented:** remove active Watchdog-environment
  policy from Diagnostics. A configured `ExternalNotifier` runs after artifact
  creation regardless of `NEKO_UNDER_WATCHDOG`; the application owns whether to
  wire Watchdog notification. Keep callback failure isolated and preserve
  `NotifyWatchdog` only as an obsolete source-compatibility gate during Phase E.
  Removing it remains a separately approved breaking-release decision. Preserve
  the end-to-end Watchdog bundle test and add direct generic-callback coverage.
- **WIN-01 — implemented:** make `HookWinForms()` an idempotent,
  one-shot process-lifetime installation with a named handler. Do not add a
  reversible handle. Correct the physical filenames and add Windows-targeted
  coverage proving repeated calls do not multiply dispatch.

Completing the review does not authorize implementation. Accepted work is
promoted selectively to this roadmap; rejected alternatives remain in the
review. Mark the review historical only when every decision is resolved, and
append reconciliation rather than rewriting the original snapshot.

#### E6.1 — Make external crash notification generic

- [x] Remove `NEKO_UNDER_WATCHDOG` detection from Diagnostics. Invoke a
  configured `ExternalNotifier` after artifact creation while isolating callback
  failures. The application composition root owns whether Watchdog notification
  is wired. **Implemented 2026-08-08:** commit
  `68b2f3d42eca047ad70aa44d7c905dce090448d3`.
- [x] Preserve `NotifyWatchdog` only as an obsolete source-compatibility gate
  during Phase E. Its removal requires a separately approved breaking release.
- [x] Preserve the end-to-end Watchdog crash-bundle integration and add direct
  dual-target coverage for callback behavior without a Watchdog environment.
  Diagnostics tests pass 6/6 per target; the three Watchdog crash-bundle tests
  pass per target without setting `NEKO_UNDER_WATCHDOG`.

#### E6.2 — Make the WinForms hook one-shot

- [x] Make `WindowsCrash.HookWinForms()` idempotent with one named handler for
  the process lifetime. Do not add a reversible handle.
- [x] Rename `CrashSupressor.cs` to `CrashSuppressor.cs` and `DumpWritter.cs` to
  `MiniDumpWriter.cs`; public type names and package boundaries remain unchanged.
- [x] Add Windows-targeted dual-target coverage proving repeated hook calls do
  not multiply crash dispatch. **Implemented 2026-08-08:** commit
  `d93004c2f93898f49edd10dfe1518ecf80136382`; Diagnostics tests pass 7/7 per
  target and exercise `Application.OnThreadException` directly.

**E6 closure — 2026-08-08:** complete. CRASH-01 has an accepted no-code
disposition; CRASH-02 and WIN-01 are implemented and validated on both target
families. The full solution test also passed serially on Windows. Real minidump
and WER-dialog behavior remains outside this focused automated evidence and is
not required to close the boundary review.

### E7 — Confidence stabilization closure

- [ ] Close and archive Phase E after E1-E6 are complete or have an explicitly
  accepted disposition.

Closure work:

- reconcile current documentation and remove duplicate active-work lists;
- run documentation and solution-membership verification;
- rebuild both target families on Windows and run the full solution tests;
- run the relevant runtime scenarios and record the evidence type truthfully;
- run package-consumer probes with a new immutable package version when package
  inputs changed;
- compare normalized warning identities rather than warning counts;
- create a dated, commit-bound validation snapshot;
- archive the completed Phase E work log and retain only live freezes and
  remaining work in this file.

## Phase E exit criteria

Phase E may be marked complete only when:

- [x] Data has a current commit-bound review.
- [x] Every promoted Data finding was reverified against current code.
- [x] WinForms has a current adapter review.
- [x] WPF has a current adapter review.
- [x] A current versioned WinForms runtime scenario exists and has truthful
  status.
- [x] The WPF runtime scenario has truthful build and interactive status.
- [x] Long-running/recovery scenarios are reproducible.
- [x] Results distinguish automated, build-only, manual, and interactive
  evidence.
- [x] Data real-provider validation has a deliberate scope and outcome.
- [x] Devices COM-port validation has an executed or explicitly accepted
  disposition.
- [x] Pipes/IPC hardening leads have been reverified.
- [x] The Diagnostics-sector review is complete.
- [x] No confirmed high-impact correctness issue lacks an accepted
  disposition.
- [ ] Both target families build successfully on Windows.
- [ ] The full automated suite passes.
- [ ] No new normalized warning identity exists.
- [ ] Package-consumer probes pass when package inputs change.
- [ ] The final state is recorded in a dated, commit-bound snapshot.
- [ ] This roadmap contains no duplicate active-work list.
- [ ] All live freezes retain their complete resume context.

Completing these checkboxes does not automatically start Phase F. Phase F must
be explicitly promoted after Phase E is archived.

## Phase F — Scale preparation (gated)

> **Status: GATED — NOT ACTIVE.** Do not implement, investigate incidentally,
> or create files for these candidates. No Phase F work may start before Phase
> E is complete and archived. Explicit promotion is required after Phase E
> closure.

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

**GATED — DO NOT START.** After E2 establishes the current native-adapter
baseline, design an opt-in surface-region model whose first concrete consumer is
stacked toast notification. A region is a visual and lifetime scope—sometimes
described as a pseudo-page—but it must not participate in `Current`, navigation
history, guards, page reuse, or the canonical page lifecycle.

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

During Phase E and while Phase F remains gated, do not create a generic
application host, Neko-specific DI container, Microsoft DI wrapper, global
service registry, message bus, event bus, universal exception policy, HTTP
client abstraction, API gateway, ORM expansion, repository/unit-of-work
framework, scheduler, job engine, distributed cache, configuration framework,
secret manager, plugin platform, Instrumentation project family, TestControl
project, generic remote debugger, cloud backend, dashboard, updater inside
Watchdog, or fleet-control plane.

These candidates require a real use case and an explicit decision before they
may enter the roadmap.

## Completed phases and history

- Phases A, B, and D are complete and historical. See the
  [`architecture roadmap through Phase D`](docs/history/architecture-roadmap-through-phase-d-2026-08-01.md).
- Phase C repository hygiene is complete. Its commit-bound validation remains
  historical evidence in the
  [`Phase C completion snapshot`](docs/history/phase-c-repository-hygiene-2026-08-01.md).

Historical test, warning, project, and package counts remain in their dated,
commit-bound snapshots and are not repeated in this live roadmap.
