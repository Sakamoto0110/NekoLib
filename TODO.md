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

- [ ] Complete a deep native-adapter review without automatically reopening
  the Navigation core.

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

**Confirmed runtime finding — 2026-08-02:** the PCB emulation scenario observed
the configured 30-second idle tick sign the session out without navigating to
Home. Current source confirms that `NavigationBootstrapLifetime` validates the
interaction generation, calls `Session.SignOut()`, and validates the same
generation again. A synchronous application UI update caused by sign-out can be
reported by the WinForms interaction observer and make that second validation
abort `GoIdleAsync()`.

- [ ] **NAV-001 — Preserve an admitted idle transition across sign-out UI
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

- [ ] **NAV-006 — Make WinForms UI dispatch truthful when the host handle does
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

- [ ] **NAV-007 — Define and document surface dismissal reachability per
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

- [ ] **NAV-008 — Correct the small confirmed adapter and bootstrap defects.**
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

- [ ] **NAV-009 — Resolve the surface-DPI and ergonomics dispositions.**
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

- [ ] **NAV-010 — Give native surfaces an anchored default position and wire the
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

- [ ] Create small, specific, reproducible scenarios for unattended execution
  over long periods without creating a new runtime framework.

Required coverage:

- **Navigation:** thousands of page switches; forward/back and login/logout
  cycles; reset cycles; repeated `Start`/`Shutdown`; cache reuse; weak and
  strong page lifetimes; background loading; redirects; guard rejection;
  overlay repetition; idle-timeout cycles; memory growth; retained handlers;
  disposal.
- **Logging:** sustained writes at expected PDV volume; sink-failure isolation;
  rolling-file rotation; retained-file count; flush during an incident;
  shutdown/disposal; bounded recent snapshots.
- **Telemetry:** bounded retention; operation completion; abandoned operations
  if relevant; checkpoint ordering; correlation; snapshot behavior under
  sustained activity.
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
  created fixture;
- [ ] verify positional OleDb/Access binding and DML on `net481`, recording the
  installed provider, architecture, fixture, and machine prerequisites;
- [ ] select at most one initial server provider from SQL Server, PostgreSQL, or
  MySQL based on an actual consumer, then validate pooling/data-source ownership,
  network failure, cancellation, transactions, and mapping before claiming
  support;
- keep concrete provider packages outside the relational core and record exact
  package, target, architecture, and server versions in validation evidence;
- promote provider-native parameter hooks, data-source lifecycle adapters, or
  cancellable factory expansion only from a concrete gap observed in this
  provider evidence;
- distinguish translator/build tests, fake ADO.NET contract tests, and real
  command execution truthfully;
- do not cite tracked-but-unused fixtures as coverage and do not include MongoDB
  in the relational provider matrix.

Navigation native WinForms/WPF interactive scenarios belong to E2, not generic
unit coverage. Any Watchdog scenario that claims package behavior must use the
deployed sidecar layout.

### E5 — Pipes and IPC hardening review

- [ ] Reverify the current IPC boundary before promoting hardening work.

Historical leads include pipe ACL/security, a per-subscriber bounded queue, an
explicit drop policy, and graceful drain of in-flight work during disposal.
They are not automatically accepted tasks.

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

Only after confirmation and an accepted decision may hardening be promoted as
implementation work. Rejected alternatives and rationale stay in the audit.
Do not turn Pipes into a service bus or generic security framework. This phase
does not authorize TestControl or Instrumentation IPC.

### E6 — Complete the Diagnostics-sector review

- [ ] Complete the remaining decisions in
  [`docs/audit/diagnostics-boundaries-review-2026-07-30.md`](docs/audit/diagnostics-boundaries-review-2026-07-30.md).

**Original review baseline:** `1727a1cac3f66666b2df02bc618ad6ab45807a49`.

Phase D already implemented DGN-01, CORE-01, BND-01, LOG-01, CORE-02,
TEST-01, and the accepted DBG-01 rename/boundary direction. The only remaining
review-only decisions are CRASH-01, CRASH-02, and WIN-01:

- **CRASH-01:** decide whether cross-platform Diagnostics should expose Windows
  minidump vocabulary. Preserve current Windows behavior unless a migration is
  accepted; do not build a Linux adapter merely to resolve vocabulary.
- **CRASH-02:** decide whether Watchdog-specific notification policy remains in
  `CrashHandler`; consider composition-root ownership while preserving the
  working IPC integration. Do not break crash notification while cleaning the
  architecture.
- **WIN-01:** decide hook lifecycle, including one-shot versus reversible
  installation, idempotence, duplicate handlers, physical filename cleanup,
  and Windows-only validation.

Completing the review does not authorize implementation. Accepted work is
promoted selectively to this roadmap; rejected alternatives remain in the
review. Mark the review historical only when every decision is resolved, and
append reconciliation rather than rewriting the original snapshot.

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
- [ ] WinForms has a current adapter review.
- [ ] WPF has a current adapter review.
- [ ] A current versioned WinForms runtime scenario exists and has truthful
  status.
- [ ] The WPF runtime scenario has truthful build and interactive status.
- [ ] Long-running/recovery scenarios are reproducible.
- [ ] Results distinguish automated, build-only, manual, and interactive
  evidence.
- [ ] Data real-provider validation has a deliberate scope and outcome.
- [x] Devices COM-port validation has an executed or explicitly accepted
  disposition.
- [ ] Pipes/IPC hardening leads have been reverified.
- [ ] The Diagnostics-sector review is complete.
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
