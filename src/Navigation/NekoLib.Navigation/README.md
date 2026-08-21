# NekoLib.Navigation — Technical Reference

A desktop navigation runtime for WinForms and WPF. It provides page
registration, a deterministic navigation lifecycle, history, guards,
idle/session behavior, overlays, and platform adapters, while keeping
application pages framework-native.

It is **not** a general UI framework, a dependency injection container, or a web
routing model. The runtime owns page attach/detach, lifecycle calls, caching,
overlay teardown, and navigation diagnostics — nothing else.

This file is the complete reference for the three projects under
`src/Navigation/`. For the framework overview and the other modules, see the
[root README](../../../README.md).

**Module conventions:** Nullable **enabled**, ImplicitUsings **disabled** in all
three projects — match these, do not flip them. The code carries pre-existing
nullable warnings (CS86xx); do not add new ones.

---

## Quick start

WinForms — host navigation inside a `Control` such as a `Panel`:

```csharp
PageNavBootstrap
    .Use<WinFormsPlatformAdapter>(mainPanel)
    .RegisterPagesFromAssembly(typeof(IdlePage).Assembly)
    .SetIdle<IdlePage>()
    .UseIdleTimeout(10_000)
    .Start();
```

WPF — use a `Panel` host such as a `Grid`:

```csharp
PageNavBootstrap
    .Use<WpfPlatformAdapter>(MainGrid)
    .RegisterPagesFromAssembly(typeof(IdlePage).Assembly)
    .ConfigurePages(cfg => cfg.Page<IdlePage>().AsIdle().StrongSingleton())
    .Start();
```

`Start()` builds a `NavigationContext`, registers platform and runtime services,
and mounts the static `NavigationService` facade:

```csharp
var result = await NavigationService.SwitchPage<DashboardPage>();
await NavigationService.GoBackAsync();
await NavigationService.GoIdleAsync();
```

`SwitchPage` accepts an optional immutable `NavigationArgs` request and returns a
call-scoped `NavigationResult`. Guard denial and redirect are normal results;
registration, page creation, load, lifecycle, and teardown failures throw.
`NavigationResult.FinalPage` is null for a terminal denial and identifies the
redirect destination after a successful redirect.

Call `NavigationService.Shutdown()` before a fresh `Start()` — double-mount
throws.

---

## Architecture: three moving parts

| Component | Role |
|---|---|
| `NavigationContext` (public, stability-sensitive) | Navigation-scoped **state bag**: Host, Services (locked `ServiceLocator`), Registry, History, Session, Platform, Diagnostics/Events. No navigation logic. Created by `PageNavBootstrap.Start()`. |
| `NavigationRuntime` (internal) | The actual **engine**. Owns `Current`, the strong/weak page caches, attached/visible page sets, and the navigation gate. One per mounted context. |
| `NavigationService` (public static facade) | The application-facing API. `UseContext` is internal; `Start()` auto-mounts. |

## Execution model

The runtime accepts and correlates a request first, then **marshals to the UI
thread** (`IEventDispatcherAdapter.BeginInvoke`) and **serializes** mutation on a
`SemaphoreSlim(1,1)` navigation gate. Consequences:

- `NavigationStarted` diagnostics are observable at API entry, including time
  spent waiting for UI dispatch and the navigation gate.
- All lifecycle methods run on the UI thread. Never `ConfigureAwait(false)`
  inside the runtime's UI path.
- Guards run **inside** the gate, bounded by a 30 s timeout
  (`GuardEvaluationTimeoutMs`) — a hung guard denies the navigation and releases
  the gate instead of deadlocking every future navigation.
- Navigation events and the internal diagnostics hub invoke subscribers inline.
  Exceptions are isolated per subscriber, but latency is not: subscribers must
  be fast and non-blocking. In particular, the 30 s guard timeout starts when
  `EvaluateAsync` is invoked, after `Navigating` subscribers return.
- Dialog/Prompt/Popover calls marshal to the UI thread but deliberately do
  **not** take the gate: a modal awaits user input, and holding the gate would
  freeze navigation.
- `DisposeAsync` uses `ExecuteSafeOnUiAsync`: if the message pump is already dead
  at app shutdown, teardown runs inline instead of hanging forever.

## Canonical lifecycle order (DO NOT CHANGE)

As implemented in `NavigationRuntime.SwitchInternalAsync`:

```
Internal NavigationStarted diagnostic (API request received)
→ UI dispatch
→ navigation-gate wait
→ Registry lookup (unregistered type ⇒ NavigationFailed + throw)
→ resolve descriptor-effective NavigationArgs
→ Navigating(from, toType, effectiveArgs) (processing signal; subscriber-safe)
→ Guard evaluation (30s cap; deny/redirect: depth ≤ 8, cycle detection)
→ Capture FROM state early (IPageStateful.CaptureState)
→ Resolve TO instance (reuse-policy caches)
→ [LoadBeforeShow only] load now (with loading mask)
→ FROM: IPageVisibility.HidePage() → IPageLifecycle.OnNavigatedFromAsync()
→ Detach FROM (unless KeepAttachedWhenHidden)
→ Attach TO + BringToFront + IPageVisibility.ShowPage()
→ Current = to; CurrentChanged
→ [ShowImmediately] load now / [LoadInBackground] fire-and-forget guarded load
→ [back-nav only] IPageStateful.RestoreState(state) — BEFORE the enter hook
→ TO: IPageLifecycle.OnNavigatedToAsync(args)
→ Cleanup detached FROM (Transient ⇒ dispose)
→ History.Record(from) + HistoryChanged (forward navigation only)
→ Navigated(from, to, args) + diagnostics EmitSuccess
→ correlated request terminal
```

Guard denial/timeout closes the attempt as `GuardDenied`; a redirect closes the
parent attempt and links a child attempt to the same request. Runtime failures
raise `NavigationFailed`, emit a `NavigationFailureKind`
(`PageNotRegistered` → `PageCreationFailed` → `LoadFailed` →
`LifecycleFailed`), close the request as failed, then rethrow. A
`LoadInBackground` failure has its own terminal and does not retroactively turn
a completed page navigation into a failure.

Once the old page starts leaving, a later attach/show/load/restore/enter failure
is rolled back best-effort: only that target's background work is discarded, a newly
attached target is removed (and disposed when transient), and the prior page is
reattached, brought forward, shown and restored as `Current`. The old transient
is therefore disposed only after the target enter hook succeeds. Page lifecycle
callbacks themselves are not replayed during rollback—their application-side
effects remain the page author's responsibility. If reattach, bring-to-front or
show itself fails, the runtime does not publish the prior page as current/visible;
the corresponding terminal blank-state event remains observable instead.

`NavigationRuntime`, driven through the static facade, is the **only component
allowed** to invoke lifecycle methods. `IPageLifecycle` has exactly two hooks.

---

## Page model

Every navigable view implements `IPageView`. Platform projects supply base
classes: `NekoLib.Navigation.WinForms.Hosting.PageView` and
`NekoLib.Navigation.Wpf.Hosting.PageView` (designer-safe; implement `IPageView` +
`IPageLifecycle` + `IPageVisibility`).

Page-side contracts are opt-in — a page implements only what it needs:

| Contract | Hooks |
|---|---|
| `IPageView` | `Name`, `NativeView`, `IsDisposed`. **Required.** |
| `IPageLifecycle` | `OnNavigatedToAsync(args)` / `OnNavigatedFromAsync()` |
| `IPageVisibility` | `ShowPage()` / `HidePage()` |
| `IPageStateful` | `CaptureState()` / `RestoreState(state)` |
| `IBackgroundLoadable` | `LoadInBackgroundAsync(args)` / `ApplyBackgroundResultAsync()` |
| `IHostAttachable` | `OnAttach(host)` / `OnDetach()` |
| `IUnfocusAware` | `OnUnfocusAsync()` for light-dismiss surfaces |

The `AllowBackNavigation` property retained by
the WinForms/WPF base pages is likewise inert; history policy must not depend on
it. `IPageOverlay` is consumed by the built-in runtime only for a registered
`IGlobalLoadingMask`; toast, dialog, prompt and popover use their dedicated
contracts.

## Registration and metadata precedence

Descriptors are built in three phases; later phases override earlier ones.

1. **Defaults** (`PageDescriptorBuilder`) — Name = type name, `Transient`,
   `ShowImmediately`, no guards.
2. **Attributes** — `[PageMetadata(Name/Role/Tags)]`,
   `[PageLoad(mode)]`, `[PageReuse(policy)]`, `[PageTimeout(seconds)]`,
   `[KeepAttached]`, `[AllowAnonymous]`, and the guard attributes
   (`[RequireAuthenticated]`, `[RequireRole]`, `[RequirePermission]`,
   `[RequireAllPermissions]`, `[RequireAnyPermissions]`).
3. **Manual DSL** (`ConfigurePages`) — `.Named()`, `.AsIdle()`,
   `.StrongSingleton()`/`.WeakSingleton()`/`.Transient()`, `.LoadMode()`,
   `.IdleTimeout(seconds)`, `.Tag()`.

The DSL configures reuse, naming, tags, load mode and idle role; it does **not**
declare guards — those come from attributes only. Multiple guard attributes on
one page compose into an `AndGuard`; `OrGuard` exists for manual composition.
Repeated DSL rules for one page compose in declaration order. Built descriptors
own defensive copies of their tag collections, and invalid names, tags, enum
values, page types, callbacks, or timeout bounds fail during configuration or
registry creation.

`PageRegistry` is immutable after `Create` (FrozenDictionary on net9). A
duplicate page **type** or **name** (case-insensitive) throws at bootstrap.
`PageFactory` creates via a registered factory or a default-ctor fallback
(`AllowUnregisteredPages` defaults to `true`; the fallback raises the internal
`Warn` event). Bootstrap registers no factories, so **every** page and surface
takes that fallback today; `Start()` subscribes `Warn` to the configured
`ILogger` — or to `Debug` output when logging is not configured — so the
fallback is at least visible instead of silent.

`IPageView.Name` is seeded by both platform base classes from `GetType().Name`.
It is a display/diagnostic fallback: the descriptor name stays authoritative for
registration and history.

`PageDescriptor.AllowAnonymous` (set by `[AllowAnonymous]`) bypasses the
descriptor guard. This is evaluated before calling the guard, so an anonymous
page does not accidentally invoke authentication/authorization work.

## Reuse policies

| Policy | Behavior |
|---|---|
| `Transient` (default) | New instance per navigation; **disposed** when navigated away from. |
| `StrongSingleton` | One instance in a strong cache; lives until `ResetAsync`/`Shutdown`, both of which dispose cached pages. |
| `WeakSingleton` | `WeakReference` cache; reused while alive and undisposed, recreated after GC. Dead slots are compacted on miss. |

`KeepAttachedWhenHidden` (`[KeepAttached]`) keeps the page in the visual tree
when hidden — honored only when the policy is **not** `Transient` and the page is
not disposed. The page must also implement `IPageVisibility`; otherwise the
runtime falls back to detaching it because it cannot truthfully make the native
view hidden.

## Load modes and the loading mask

`NavigationLoadMode` decides *when* `IBackgroundLoadable` work runs relative to
attach:

- `ShowImmediately` (default) — attach first, then await the load.
- `LoadBeforeShow` — await the load **before** the old page is hidden/detached.
- `LoadInBackground` — attach, then fire-and-forget. The result is applied via
  `ApplyBackgroundResultAsync` **only if** the page is still `Current` and not
  disposed; failures are logged to diagnostics, never thrown.

The registered descriptor is the sole public owner of load mode and reuse
policy. A caller creates `NavigationArgs.Default(payload)` and may attach a
`NavigationTimingContext`; after registry lookup, `Navigating`, `Navigated` and
page lifecycle hooks receive an effective copy whose `LoadMode` matches the
descriptor. There are no request-side preload, background, transient, or back
factories.

`LoadInBackgroundAsync` runs via `Task.Run` and must not touch the UI;
`ApplyBackgroundResultAsync` runs on the UI thread. During a load the runtime
shows the registered `IGlobalLoadingMask` page, driven directly through
`IViewHost` rather than the overlay services. The platform default mask is
auto-registered at bootstrap **unless** the scanned assemblies contain a custom
`IGlobalLoadingMask`.

## History and page state

- `NavigationHistory` is a framework-owned back stack plus a forward stack.
  Consumers receive top-first `IReadOnlyList<PageHistoryEntry>` snapshots through
  `HistoryBack` and `HistoryForward`; construction and mutation are internal.
  Forward navigation records the **from** page and clears forward. History
  entries use the descriptor's logical name.
- `GoBackAsync` first inspects the entry and navigates with
  an internal back request. It commits the pop/push only after the requested
  target succeeds; denial, redirect and pre-show failure preserve both stacks.
- Back-navigation **skips** `History.Record` inside the switch — the back path
  manages both stacks itself — and fires `HistoryChanged` once from
  `GoBackInternalAsync`.
- `IPageStateful.CaptureState()` runs when leaving a page and the blob is stored
  in the history entry. `RestoreState(blob)` runs **only on back-navigation,
  before `OnNavigatedToAsync`**. The same blob also arrives as
  `NavigationArgs.Payload` with `IsBackNavigation == true`, but `RestoreState` is
  the preferred channel.

## Guards and session

- `IGuard.EvaluateAsync(GuardContext{TargetPage, User})` returns
  `GuardResult.Allow()`, `.Deny(reason)` or `.Redirect(pageType, reason)`.
- Every built-in guard attribute honors its optional `RedirectTo` property.
  `RequirePermissionAttribute(string)` is the deny-only form; the two-argument
  constructor and the named property configure a redirect.
- Redirect chains are capped at depth 8 with cycle detection via a visited set;
  violations emit `GuardDenied` and stop without throwing.
- A guard that throws denies the navigation with the exception message as the
  reason — it never crashes navigation.
- `NavigationSession` (framework-owned, one per context) implements
  `IUserContext`: `SignIn(roles)`, `SignIn(roles, permissions)`, `SignOut()`. It
  is registered in the locator as both `NavigationSession` and `IUserContext` —
  the **same instance** guards read, so `NavigationService.Session.SignIn("admin")`
  is visible to guards on the next navigation. Consumers never implement an auth
  contract for the built-in guards to work.

## Idle system

`IdlePageRules` is the single source of truth for which page is idle. Priority:
`PageRole.Idle` (via `SetIdle<T>()` or `.AsIdle()`) → an `idle` tag → a name
containing "idle". There is no fallback to "MainPage".

More than one page with `Role=Idle` throws at bootstrap, and `SetIdle<T>()` may
be called at most once.

Idle timeout precedence, highest first:

1. DSL `.IdleTimeout(seconds)` on the idle page
2. `[PageTimeout(seconds)]` on the idle page
3. Global `UseIdleTimeout(milliseconds)`

A timeout declared on the idle page enables the timer on its own, so the global
call is optional. Declaring `[PageTimeout]` or `.IdleTimeout()` on any **non-idle**
page throws at bootstrap.

Wiring is automatic in `Start()`: the platform `IInteractionObserverService`
resets the timer on any input; on tick the timer stops, `Session.SignOut()` runs,
then `GoIdleAsync()`. The timer starts immediately, so an unattended boot also
lands on the idle page. The bootstrap lifetime stops and detaches the idle
callbacks before runtime teardown, preventing a timer tick from enqueueing new
work during shutdown. An interaction invalidates a stale tick before it can
start navigation; a denied/failed idle navigation rearms a fresh interval, while
`StopIdle()` prevents both rearming and new requests. If the platform has no
interaction observer, navigation still starts and the Inspection idle snapshot
reports `Unavailable`.

## Overlays

Four services, strictly partitioned by intent:

| Primitive | Blocking | Auto-dismiss | Return |
|---|---|---|---|
| `IToastView` / `IToastService` | no | timer (+ tap via `BindDismiss` — see reachability below) | `void` |
| `IDialogView` / `IDialogService` | **yes** | — | `Task<bool>` |
| `IPromptView<TResult>` / `IPromptService` | **yes** | — | `Task<TResult>` |
| `IPopoverView` / `IPopoverService` | no | **keyboard-focus** loss (via `IUnfocusAware`), not hit testing | `Task<bool>` |

Dialog is always `bool` — anything tri-state uses `Prompt<TEnum>`. Popover is
always non-blocking — blocking variants go through Dialog.

Semantics worth knowing:

- Dialogs and prompts **stack**: the first open engages the
  `IInteractionBlocker`, the last close releases it. Bootstrap wraps custom
  platform blockers with reference counting, so overlapping dialog and prompt
  stacks cannot unblock each other prematurely. The optional
  `IPageAwareInteractionBlocker` extension also tracks pages and overlays added
  while blocked: background views remain disabled and only the top modal stays
  interactive. Ownership is published before calling a platform blocker, so a
  synchronous/reentrant UI callback cannot corrupt the modal depth.
- View pattern: the service creates the view via `PageFactory`, adds it through
  `IViewHost`, calls `OnShownAsync(payload)`, and completion flows back through
  `BindCompletion(callback)`.
- `PopoverService` wires the focus observer **after** focusing the view, so the
  focus switch itself does not insta-dismiss, and only for views implementing
  `IUnfocusAware`.
- Runtime teardown calls `DismissCurrentToast()` for Toast and `CloseAll()` for
  Dialog, Prompt and Popover. Pending awaiters resolve with `false`/default, so
  callers are never left hanging. Teardown then runs best-effort across every
  owned view and blocker; its first cleanup failure is traced and rethrown to
  the reset/shutdown caller after the remaining resources have been reclaimed.
- Setup is transactional: a synchronous completion, `Bind*`, host, focus or
  `OnShown*` failure reclaims the partially opened view and releases its blocker
  or focus subscription. Normal dialog/prompt/popover completion faults its
  awaiter if native cleanup fails, but the UI callback itself contains that
  exception. Toast dismiss callbacks also contain cleanup failures and are bound
  to their own instance, so a stale callback cannot dismiss its replacement.

### Dismissal reachability

"Tap anywhere to dismiss" and "closes when you click away" are both wrong as
written. What actually dismisses differs per primitive and per platform.

**Toast — the click binding does not reach child controls.**

| Platform | Bound event | Dismisses | Does not dismiss |
|---|---|---|---|
| WinForms | `Control.Click` on the toast container | a click on the toast's own background | a click on any child control: WinForms click events do not bubble, so a child's `Click` never reaches the container |
| WPF | `MouseLeftButtonDown` on the toast container | a click on the background and on most children, because the input system re-raises the event along the bubble route of `Mouse.MouseDownEvent` | a click on a child that marks the event handled — `Button` does so outside `ClickMode.Hover` |

The bindings are kept as they are, for compatibility. **A toast that contains
child controls must offer an explicit close affordance**; tap-to-dismiss is not
reachable across it. `Dismiss()` on both bases is the programmatic equivalent and
is what such an affordance calls.

**Popover — light dismissal follows focus, not hit testing.**

Both `IFocusObserverAdapter` implementations are focus observers: WinForms tracks
`Control.Leave` (subtree-scoped) plus `Form.Deactivate`; WPF tracks
`LostKeyboardFocus`, filtered to focus actually leaving the element's subtree,
plus `Window.Deactivated`. Neither performs a hit test. On both platforms:

- clicking a control **outside** the popover that can take focus dismisses it;
- moving focus **within** the popover — tabbing between its own fields — does
  not;
- clicking inert area (labels, panels, a page built only from static content)
  moves no focus and therefore **does not dismiss**; the popover stays open;
- switching away from the application dismisses it, through the form/window
  deactivation subscription.

Recorded on WinForms on 2026-08-03 while validating NAV-004: clicking the
Dashboard counter (inside the host) and left-panel buttons such as "Limpar log"
(outside it) both dismissed the popover, while clicking the Idle page — labels
only — dismissed nothing.

A real click-outside model would need mouse capture or a hit-test scrim. It is
deliberately **not** implemented.

### Surface placement

`WinFormsLayeredPageHostBase.AddView` docks every added view to `Fill`. Each
surface base then places itself: dialog and prompt centre themselves, popover
keeps its designer placement, and `ToastViewBase` parks itself at the host's
`BottomRight` anchor, inset by `AnchorInset` (default 20, scaled by
`INavigationSurface.Scale`) and anchored bottom-right so it stays parked when the
host resizes. Override `AnchorInset` to change the gap, or `ApplyDefaultAnchor()`
to place the toast somewhere else entirely. The WPF `ToastViewBase` reaches the
same result declaratively, through alignment and a 20px margin set in its
constructor.

Both platform projects ship `ToastViewBase`, `DialogViewBase`,
`PromptViewBase<TResult>`, `PopoverViewBase` and `AutoDismissPopoverBase` so
subclasses skip the wiring.

## Platform adapters

`IPlatformAdapter` is a pure factory:

| Method | Produces |
|---|---|
| `CreateHost` | `IPageHost` |
| `CreateEventDispatcher` | UI-thread marshaling |
| `CreateInteractionBlocker` | modal input blocking; built-in adapters also implement the page-aware extension |
| `CreateTimerAdapter` | idle timer |
| `CreateInteractionObserverAdapter` | nullable — idle timeout remains inactive and diagnostics report it unavailable |
| `CreateFocusObserver` | nullable — popovers just will not auto-dismiss |
| `GetDefaultLoadingMaskType` | nullable |

The concrete adapter events `InteractionDetected` and `Tick` are nullable: an
adapter with no subscriber is a normal state. Overlay payloads are likewise
nullable all the way through the default loading masks and the protected
`OnShownAsync(object?)` / `OnShown(object?)` consumer hooks. A typed prompt base
accepts `CompletePrompt(TResult?)`, matching the default result used when a
prompt is closed during teardown.

The WinForms timer constructor parameter is named `intervalMillis`. The
`WinFormsInteractionBlocker` owns its blocking state but does not expose or
convert back to the native root; a consumer that needs the host must retain the
`Control` it supplied. On WPF, `Dispose()` remains virtual on the page and four
surface bases. Consumer overrides must call `base.Dispose()` to release callbacks
and input subscriptions and to publish `IsDisposed`.

**UI-thread rule for `CreateEventDispatcher`:** an adapter must decide UI-thread
identity rather than infer it from a helper that only works once the host is
realized. WinForms captures the constructing thread because
`Control.InvokeRequired` answers `false` on every thread while the host has no
window handle. If a WinForms action cannot reach a handle and the caller is not
that owner thread, dispatch throws instead of running normal Navigation lifecycle
on a worker. WPF uses `Dispatcher.CheckAccess` while the dispatcher is alive; once
dispatcher shutdown has started or finished, it runs the action inline only as a
best-effort teardown fallback. That WPF fallback does not authorize normal page
lifecycle on arbitrary threads. `DisposeAsync` remains bounded on either platform
because `ExecuteSafeOnUiAsync` can reclaim resources inline when UI dispatch is no
longer available.

**Navigation toolkit:** both shipped layered hosts also implement
`INavigationToolkit`, and `Start()` registers the host under that contract with
the same `host as INavigationToolkit` probe it uses for `IViewHost`. An adapter
whose host does not implement it simply leaves the toolkit unregistered — no
`IPlatformAdapter` member was added, so third-party adapters keep compiling.
Resolve it from `context.Services` to read `INavigationSurface.ClientBounds`,
`Scale` and `ResolveAnchor(SurfaceAnchor)`, or to call `FocusSurface()`.

**Host split:** `IPageHost` handles Attach/Detach/BringToFront of *pages*;
`IViewHost` handles AddView/RemoveView/BringToFront/Focus of *raw views* and is
what overlay services and the loading mask use. `WinFormsLayeredPageHostBase`
implements both over a single Panel: pages are pinned to the bottom of the
z-order (`SendToBack`) and surfaces stay above.

## Bootstrap sequence

`PageNavBootstrap.Start()`:

1. **Build the registry** — assembly scan (`GetLoadableTypes` tolerates
   `ReflectionTypeLoadException` and loads what it can), explicit registrations,
   `SetIdle` before `ConfigurePages`, default loading-mask auto-registration.
   `UseRegistry(...)` is mutually exclusive with all page-configuration methods.
2. **Validate** — single idle page, `[PageTimeout]` placement, platform
   `CanHandle(nativeHost)`.
3. **`ServiceLocator` open phase** — registers `IPageHost`,
   `IEventDispatcherAdapter`, `IInteractionBlocker`, `ITimerAdapter`,
   `PageFactory`, the optional observers, and the four overlay services; then
   runs the `ConfigureServices(...)` callback.
4. **Create the `NavigationContext`** — attach the independently configured
   Logging, Telemetry, and Inspection bridges; register the context,
   `NavigationSession` and `IUserContext`.
5. **`services.Lock()`** — registration after this throws.
6. **Wire the idle timer**, then mount the context (throws if already mounted).

## Reset vs Shutdown

- `ResetAsync()` — tears down the current page, cached pages and all overlays
  (resolving awaiters), and clears history. The active page receives
  `HidePage → OnNavigatedFromAsync → Detach → Dispose`; hidden keep-attached
  pages are detached/disposed without receiving their exit hooks twice.
  Context, session and adapter stay alive; navigation can continue.
- `Shutdown()` — first stops idle callbacks, then performs the same page/surface
  teardown, emits teardown diagnostics while the observer is still attached,
  unregisters its state providers, disposes native bootstrap resources,
  unmounts the facade, and clears all static event subscribers. Concurrent
  callers share the same shutdown operation, and a new context cannot mount
  until it completes. Work accepted before the shutdown cutoff holds an
  operation lease through its UI admission. Modal leases end after the owning
  service has registered the surface—not after user completion—so teardown can
  close the modal without deadlocking and no queued surface can appear after the
  runtime is gone. Required before a fresh `Start()`.

## Logging, telemetry, Inspection, and local diagnostics

`NavigationDiagnostics` emits `PageLogEntry` (success/failure, load
mode, reuse policy, failure kind, error, correlation and duration) and
`GuardDeniedEvent` into the context's `NavigationEventHub`, surfaced as
`NavigationLogged` and `GuardDenied`. Subscriber exceptions are isolated
individually — diagnostics never break navigation or suppress later
subscribers. The runtime shares the **context's** hub, so
`NavigationService.Events` and `context.Events` see the same outcomes.

These are synchronous observation points, not a worker queue. A custom
`IInspectionRecorder` implementation and public event handlers must return
promptly; blocking one delays dispatch/lifecycle work. The built-in
`InspectionRuntime` only captures a bounded in-memory entry under a short lock.

`NavigationService` also exposes static events: `Navigating`, `Navigated`,
`NavigationFailed`, `CurrentChanged`, `HistoryChanged`, `OnFirstPageAttached`,
`OnNoPageAttached`, `OnNoPageVisible`. Static events root their subscribers, so
`Shutdown()` nulls all of them.

The hub is read-only to consumers: event publication, event/DTO construction,
and diagnostic emission are framework-owned. Configure logging through
`UseLogging(ILogger)` or subscribe to the read-only events; consumers cannot
fabricate Navigation evidence through a public emitter or sink.

Four independent observation layers coexist:

- **`UseLogging(ILogger)`** — writes Navigation category entries through the
  Core logging writer contract. Local hub events keep flowing regardless.
- **`UseTelemetry(ITelemetry)`** — creates one correlated
  `Navigation/page_switch` operation per request and records raw timing
  checkpoints and terminal measurements.
- The runtime's internal scalar trace describes requests, attempts, stages,
  pages, background loads, surfaces, idle and runtime teardown. It never carries
  a page instance, user payload, captured state, roles or permissions.
- **`UseInspection(IInspectionRecorder)`** projects that trace into
  `IInspectionRecorder.Record(...)` plus pull-based state. A disabled recorder
  (`NullInspection`) subscribes to nothing and allocates no request/surface trace
  scopes.
- **`UseInspection()`** — resolves `InspectionProvider.Current` at `Start()`.
  This keeps the integration fully opt-in while allowing one explicitly enabled
  `InspectionRuntime` to serve the whole process. If the slot still contains
  `NullInspection.Instance`, no observer is allocated or attached.

One API call owns a `RuntimeId` + `RequestId`. Guard redirects create linked
attempts with `AttemptId`, `ParentAttemptId` and `RedirectDepth`; a background
load receives its own `BackgroundOperationId`. Durations use a monotonic
`Stopwatch`, while UTC timestamps are only wall-clock labels. The order is:

```
RequestStarted (before UI dispatch)
→ Dispatch → GateWait → Processing
→ AttemptStarted → RegistryLookup → Navigating → GuardEvaluation
→ page/load/lifecycle stages
→ AttemptCompleted
→ RequestCompleted (exactly once)
```

A redirect closes its parent attempt and continues under the same request.
`Navigated` means the full synchronous page lifecycle completed, not merely that
the guard allowed it. `NoHistory` is a normal back-navigation terminal, not a
failure. Background completion/discard/failure is independent of the already
completed request.

### Initial page-switch timing

`UseTelemetry(...)` emits these checkpoints and raw measurements without
changing the canonical lifecycle order:

| Boundary | Owner | Meaning |
|---|---|---|
| `page_switch_started` | Navigation | API request started, before UI dispatch and gate wait |
| `authentication_completed` | application | the caller-defined authentication milestone was reached |
| `page_ready` | Navigation | the synchronous Navigation lifecycle completed successfully |

The completed operation contains `page_switch.total_ms`. When the application
reports authentication completion it also contains
`page_switch.time_to_authenticated_ms` and
`page_switch.post_auth_to_ready_ms`. Those are milestone intervals, not pure
authentication or page-load duration: the first interval includes dispatch and
gate waiting, and `page_ready` does not claim first paint or completion of work
started in the background.

Create one `NavigationTimingContext`, pass it through
`NavigationArgs.Default().WithTiming(timing)`, and call
`context.Timing?.AuthenticationCompleted()` from the application guard after
authentication succeeds. API POST/GET boundaries and catalog behavior remain
application concerns. The telemetry operation ID is the Navigation request ID,
so later Data or Devices operations can use it as a parent without those modules
depending on Navigation.

The observer registers these state providers:

| Key | Snapshot |
|---|---|
| `runtime` | runtime id/status, current type, attached/visible totals, last runtime decision |
| `inFlight`, `activeAttempts`, `queue` | request/attempt correlation, current stage, duration and gate depth |
| `current`, `currentPage` | aliases for the actual current page and descriptor name |
| `lastNavigation` | last request terminal, correlation, outcome, failure stage and duration |
| `registry` | scalar descriptor metadata and registered-page count |
| `pages`, `cache` | logical page decisions, attached/visible totals, strong/weak cache totals |
| `backgroundLoads` | active correlated loads and aggregate terminals |
| `overlays` | opening/open surfaces and last terminal, with kind/depth/reason/duration |
| `idle` | armed/unavailable/elapsed/disposed state, interval and last interaction |
| `history` | immutable mirrored names/counts for both stacks |
| `session` | authenticated flag plus role/permission **counts only** |
| `stats` | request/attempt/redirect/guard/background/blank-shell aggregate counters |

The hub-only overload registers the 13 common keys; the bootstrap/context
overload adds `registry`, `history` and `session` for 16 total. Providers read
only observer-owned copies under a lock. `CaptureState()` therefore never walks
the UI, runtime caches or live `NavigationHistory` from a consumer thread.

The operation ring records meaningful transitions and terminals:
`NavigationStarted`, `Navigating`, `Navigated`, `NavigationFailed`,
`GuardDenied`, `GuardRedirected`, `NavigationNoHistory`, slow/failing stages,
background terminals, surface open/close/failure, idle configuration/elapsed/
failure/disposal and terminal blank-shell detection. High-frequency idle input
updates state but deliberately does not flood the ring.

`OnFirstPageAttached` fires once per runtime. `OnNoPageAttached` and
`OnNoPageVisible` describe an explicit terminal blank state (for example reset
or a failed switch that lost its former page); the transient zero between detach
and attach during a successful replacement is not published as a blank shell.

`NavigationService.Shutdown()` keeps the observation bridges alive through
runtime teardown, then disposes them and unregisters all Inspection providers,
including delegates that capture the context. Navigation deliberately registers
**no Inspection actions** yet:
an async/cancellation/timeout/UI-marshalling command contract must be decided
before exposing state-changing runtime actions.

> Phase D explicitly unfroze the bounded Navigation timing producer and the
> Diagnostics read-only snapshot bridge. Broad B4 Inspection instrumentation in
> Data, Pipes, Watchdog, Devices, and other feature modules remains frozen; see
> [`TODO.md`](../../../TODO.md).

---

## Stability-sensitive components

The lifecycle/trace correction and bounded timing addition above are complete.
Treat these types as
frozen again unless a future task explicitly reopens them:

- `NavigationContext.cs`
- `NavigationRuntime.cs`
- `PageRegistry.cs`
- `PageFactory.cs`

## Folder responsibilities

| Folder | Contents |
|---|---|
| `Contracts/` | Pure contracts. No logic, no platform assumptions. |
| `Metadata/` | DTO-like structures, attributes and enums. No behavior. |
| `Runtime/` | Navigation runtime, registry, factories, services, history, session. |
| `Diagnostics/` | Navigation tracing and the optional bridges. |
| `Toolkit/` | Optional surface positioning (anchors). Not required by Core. |

## Tests

```bash
dotnet test tests/NekoLib.Navigation.Tests/Unit/NekoLib.Navigation.Tests.Unit.csproj
```

Tests use the fakes in `tests/NekoLib.Navigation.Tests/Unit/Fakes/`:
`RuntimeTestFixture` wires a full in-memory runtime with `FakePlatformAdapter`,
`FakePageHost`, `StubPageViews` and a `SyncEventDispatcherAdapter` that runs "UI"
work inline. `DeferredEventDispatcherAdapter` covers the pre-dispatch boundary.
Canonical-order tests cover every load mode, keep-attached reuse, reset/shutdown,
surface rollback, idle ownership and correlated request terminals. Naming follows
`MethodName_Condition_ExpectedResult`.

**Tests that mount the static facade** touch process-wide state: they must carry
`[Collection("NavigationServiceFacade")]` so xunit serializes them, and
`await NavigationService.Shutdown()` in a `finally`.
`InspectionNavigationObserverFacadeTests` and
`NavigationServiceLifecycleTests` are references. Tests that do not mount the
facade stay parallel.

`InternalsVisibleTo("NekoLib.Navigation.Tests.Unit")` is set in the Navigation
project.

Runtime scenario apps under `runtime_tests/` are interactive WinForms/WPF
executables — launch them directly, never via `dotnet test`.
