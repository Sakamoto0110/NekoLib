# Desktop integration readiness for NekoLib 1.2.0

**Kind:** reference

**Lifecycle:** current

**Subject:** accepted desktop integration corrections, compatibility and qualification boundaries

**Decision date:** 2026-11-24

## Accepted scope

The owner authorized source corrections, regression tests, four-way desktop
qualification, integration and coordinated immutable package publication. The
demonstration will use one main window. A separate Watchdog process restarts
the application; no companion window or console fault-control protocol is part
of this change. Application requirements remain a later input.

Confirmed dispatch and focus defects reopen only the affected Navigation
adapter methods, bootstrap native-resource disposal and the runtime teardown
dispatch fallback. Registry, factory,
navigation lifecycle ordering and application synchronization rules are outside
that unfreeze. The release is a compatible minor because it adds stable APIs.

The owner explicitly requested unattended completion and publication while
unavailable. For this release, automated native UI behavior on all four
combinations is the functional qualification boundary. The human-driven visual
procedure in `NAV-VALREQ-004` is not claimed as executed or fully satisfied;
manual visual/accessibility sign-off remains an explicit release exception.
This does not convert automation into human evidence or erase older gaps.

## Compatibility decisions

- `WindowsCrash.HookWpf(Dispatcher, bool markHandled = false)` is additive. It
  must be installed and disposed on the dispatcher thread. Identical leases
  share one forwarding subscription; conflicting policies fail. The final lease
  uninstalls it, and dispatcher shutdown also detaches it. A separately installed
  `CrashHandler` receives reports. Default propagation reports a terminating
  incident without setting `Handled`; the handler's fatal latch prevents a
  second AppDomain report. Explicit `markHandled: true` reports a recoverable
  incident and sets `Handled`. Reporting never calls Exit or kills a process.
  Applications own fatal exit codes and notification to Watchdog. Later native
  subscribers must not reverse the chosen policy. Existing `HookWinForms()`
  keeps its process-wide, nonterminating reporting contract.
- WPF dispatch after shutdown runs inline only on its owning thread. A worker
  receives `InvalidOperationException`, matching the unreachable WinForms
  owner contract. Runtime shutdown propagates this failure instead of disposing
  native controls on a worker. Await `NavigationService.Shutdown()` before
  closing the host or stopping its UI loop. Accepted native posts can still be
  abandoned during native shutdown: this void dispatcher interface does not
  offer delivery acknowledgment. No claim of cancellation-safe dispatch is made.
  Bootstrap also marshals idle stop and native observer/timer disposal through
  the owning dispatcher, including after asynchronous runtime teardown.
- WPF toolkit focus requests now marshal from workers; requests after shutdown
  are ignored. WinForms ignores requests without a live handle. Geometry reads
  remain UI-thread operations. WPF coordinates are DIPs, WinForms coordinates
  are native client units; `Scale` is DPI/96, not a mandate to rescale WPF DIPs.
- `InteractionObservationMode` and observer constructor overloads are additive.
  Choose `InputOnly` on both frameworks to renew idle for native mouse/key/text
  input while excluding programmatic button clicks and text changes. Choose
  `InputAndControlChanges` on both to include those changes. Synthetic input
  raised by application code is indistinguishable from native input here; this
  is an event policy, not a security boundary or an exact gesture counter.
  Existing constructors retain WinForms' broader policy and WPF's input policy.
  Existing platform adapters retain those defaults for compatibility. Additive
  `PageNavBootstrap.UseInteractionObserver(() => new ...Observer(host, mode))`
  selects the mode without replacing an already registered service. The factory
  runs on the UI thread per Start; bootstrap owns and disposes its returned
  observer on failed startup or shutdown. Return a fresh instance per Start.
- Watchdog's first attached restart now excludes the Host's own descendant
  branch from forced termination. The initial application launched that Host;
  applying `taskkill /T` to it killed the supervisor before replacement. Other
  target descendants remain eligible for best-effort leaf-first termination.
  Later Host-launched replacements retain tree termination. A failed process
  snapshot falls back to terminating only the initial root, preserving the Host.
  This restores the bootstrap supervision contract without changing its API,
  pipe protocol or deployment layout.

## Native semantics retained

### Crash composition without implicit process exit

Install `CrashHandler` separately. An application can choose a fatal exit after
the incident collection attempt by setting this option before construction:

```csharp
ExternalNotifier = incident =>
{
    if (incident.IsTerminating)
        Environment.Exit(71); // Application-owned fatal policy, not library behavior.
}
```

For WPF, install `WindowsCrash.HookWpf(Dispatcher.CurrentDispatcher)` on the UI
thread and retain its returned lease. The default leaves native propagation
unchanged. The explicit `markHandled: true` policy is suitable only when the
application knows continuing is safe. A caught recoverable operation can also
call `CrashHandler.ReportExternalCrash(source, exception, false)` directly.

For a fatal WinForms policy, install one application-owned forwarding boundary
before creating windows, and remove it at shutdown:

```csharp
Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
ThreadExceptionEventHandler forward = (_, eventArgs) =>
    CrashHandler.ReportExternalCrash("Application.ThreadException", eventArgs.Exception, true);
Application.ThreadException += forward;
// Run the UI loop; unsubscribe with Application.ThreadException -= forward afterwards.
```

The existing `WindowsCrash.HookWinForms()` provides the legacy nonterminating
policy. Choose one WinForms forwarder at startup; installing it alongside the
fatal forwarder reports the same native event twice. Reporting, optional Host
notification and actual process exit are separate application decisions.

### Corresponding native surfaces

| Surface | Decision and qualification boundary |
|---|---|
| Page and layered host | Shared core lifecycle; native visibility, focus traversal and z-order are intentional. |
| Dispatcher | Owner-thread execution; no worker fallback during teardown. |
| Interaction observer | Explicit portable mode, legacy defaults retained. |
| Interaction blocker | Nested modal ownership disables background; WPF bindings survive and refresh from their current source on unblock. |
| Focus observer and toolkit | Native focus events/traversal; toolkit requests marshal while the owner exists. |
| Timer and event subscription | Native UI scheduling; stop/dispose before loop shutdown. |
| Dialog and prompt bases | Native sizing/layout; shared result, close and modal ownership contracts. |
| Popover and auto-dismiss base | Native focus transitions; common close/disposal contract. |
| Toast base | Background click dismisses on both. WPF events can bubble from children, whereas WinForms clicks do not. Interactive children require an explicit close affordance calling protected `Dismiss()`. Do not promise tap-anywhere dismissal. |
| Loading mask | Native presentation, common load blocking and cleanup contract. |
| Navigation surface | Native bounds and DPI units; headless Scale reads do not realize a window. No claim of identical pixels. |

The 17 corresponding public adapter classes are not a requirement to make the
two native event systems identical. Existing shared-core surface tests cover
replacement, auto-dismiss, pending results, nesting and failure cleanup; native
tests cover focus, binding restoration, geometry and disposal.

| Corresponding class (framework prefix where applicable) | Closure and evidence owner |
|---|---|
| `PageView` | Retain native control ownership; `PlatformPageLifecycleTests` and package page navigation/shutdown. |
| `LayeredPageHostBase` | Retain native layout/z-order; `PlatformPageLifecycleTests`, package modal and detach checks. |
| `EventDispatcherAdapter` | Correct WPF worker fallback and reject unreachable owner; `AdapterParityTests` and `WinFormsEventDispatcherAdapterTests`. |
| `EventSubscriptionAdapter` | Retain native event attachment; bootstrap lifetime and native lifecycle tests. |
| `FocusObserverAdapter` | Retain native focus transitions; `PlatformPageLifecycleTests` and `PopoverServiceTests`. |
| `InteractionBlocker` | Retain modal ownership/binding restoration; native lifecycle and four package modal probes. |
| `InteractionObserver` | Add explicit equivalent modes without changing defaults; parity, bootstrap ownership and package background-update probes. |
| `PlatformAdapter` | Retain host validation and composition; bootstrap lifetime and four package starts. |
| `TimerAdapter` | Retain native UI timer; idle tests, owner-thread native-resource teardown and toast timer integration. |
| `NavigationToolkit` | Correct WPF worker focus; parity and four package focus probes. |
| `NavigationSurface` | Clarify units/visible-enabled semantics; anchored toast and toolkit tests. |
| `DialogViewBase` | Retain native geometry; `DialogServiceTests`, native modal tests and package result/cleanup probes. |
| `PromptViewBase<T>` | Retain typed result and native layout; `PromptServiceTests` and package prompt result. |
| `PopoverViewBase` | Retain result/cleanup; `PopoverServiceTests` and package result. |
| `AutoDismissPopoverBase` | Retain native focus-based dismissal; focus/auto-dismiss regression tests and common popover lifetime. |
| `ToastViewBase` | Retain documented native click routing and explicit close affordance; common toast tests plus package anchor/focus/auto-dismiss. |
| `DefaultLoadingMask` | Retain native visuals and shared background-load ownership; runtime/page lifecycle tests. |

These are behavior and contract checks, not pixel comparison or a new prolonged
soak campaign. Multi-monitor DPI transitions and visual accessibility review
remain manual layers; the package probes verify native geometry at the desktop
DPI actually used by the run.

## Data and external dependencies

Data provides translation, named/positional binding, sessions, transactions and
failure reporting. It does not own pending operations, replay, idempotency,
conflict resolution or recovery journals. Qualification uses a deterministic
ADO.NET test provider to verify those generic boundaries through NuGet, without
pretending it is a live SQL Server or ACE run. The future application can share
its synchronization logic across native UIs without a new NekoLib sync engine.

TheCatAPI credentials, live SQL endpoint/schema, Access database and ACE bitness
are separate prerequisites. A NuGet restore cannot install ACE. Final evidence
must distinguish detection of prerequisites from live provider execution.
Watchdog heartbeat reports process existence while monitoring WaitForExit; it
does not measure UI responsiveness or automatically prove hang detection.

## Evidence

Results, commands, package provenance and remaining limitations are recorded in
the release record after qualification. No pending validation is considered a
pass merely because its scenario compiles.
