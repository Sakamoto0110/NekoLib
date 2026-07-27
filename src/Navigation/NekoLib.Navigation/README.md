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
await NavigationService.SwitchPage<DashboardPage>();
await NavigationService.GoBackAsync();
await NavigationService.GoIdleAsync();
```

Call `NavigationService.Shutdown()` before a fresh `Start()` — double-mount
throws.

---

## Architecture: three moving parts

| Component | Role |
|---|---|
| `NavigationContext` (public, FROZEN) | Navigation-scoped **state bag**: Host, Services (locked `ServiceLocator`), Registry, History, Session, Platform, Diagnostics/Events. No navigation logic. Created by `PageNavBootstrap.Start()`. |
| `NavigationRuntime` (internal) | The actual **engine**. Owns `Current`, the strong/weak page caches, attached/visible page sets, and the navigation gate. One per mounted context. |
| `NavigationService` (public static facade) | The application-facing API. `UseContext` is internal; `Start()` auto-mounts. |

## Execution model

Every public entry point runs through `ExecuteAsync` = **marshal to the UI
thread** (`IEventDispatcherAdapter.BeginInvoke`) **then serialize** on a
`SemaphoreSlim(1,1)` navigation gate. Consequences:

- All lifecycle methods run on the UI thread. Never `ConfigureAwait(false)`
  inside the runtime's UI path.
- Guards run **inside** the gate, bounded by a 30 s timeout
  (`GuardEvaluationTimeoutMs`) — a hung guard denies the navigation and releases
  the gate instead of deadlocking every future navigation.
- Dialog/Prompt/Popover calls marshal to the UI thread but deliberately do
  **not** take the gate: a modal awaits user input, and holding the gate would
  freeze navigation.
- `DisposeAsync` uses `ExecuteSafeOnUiAsync`: if the message pump is already dead
  at app shutdown, teardown runs inline instead of hanging forever.

## Canonical lifecycle order (DO NOT CHANGE)

As implemented in `NavigationRuntime.SwitchInternalAsync`:

```
Registry lookup (unregistered type ⇒ throw)
→ Guard evaluation (30s cap; deny/redirect: depth ≤ 8, cycle detection)
→ Capture FROM state early (IPageStateful.CaptureState)
→ Navigating(from, toType, args)
→ Resolve TO instance (reuse-policy caches)
→ [LoadBeforeShow only] load now (with loading mask)
→ FROM: IPageVisibility.HidePage() → IPageLifecycle.OnNavigatedFromAsync()
→ Detach FROM + Cleanup (unless KeepAttachedWhenHidden; Transient ⇒ dispose)
→ Attach TO + BringToFront + IPageVisibility.ShowPage()
→ Current = to; CurrentChanged
→ [ShowImmediately] load now / [LoadInBackground] fire-and-forget guarded load
→ [back-nav only] IPageStateful.RestoreState(state) — BEFORE the enter hook
→ TO: IPageLifecycle.OnNavigatedToAsync(args)
→ History.Record(from) + HistoryChanged (forward navigation only)
→ Navigated(from, to, args) + diagnostics EmitSuccess
```

On failure: the `NavigationFailed` event plus `EmitFailure` with the stage
recorded as a `NavigationFailureKind` (`PageNotRegistered` →
`PageCreationFailed` → `LoadFailed` → `LifecycleFailed`), then rethrow.

`NavigationRuntime`, driven through the static facade, is the **only component
allowed** to invoke lifecycle methods. `IPageLifecycle` has exactly two hooks.

---

## Page model

Every navigable view implements `IPageView`. Platform projects supply base
classes: `NekoLib.Navigation.WinForms.Hosting.PageView` and
`NekoLib.Navigation.Wpf.Hosting.PageView` (designer-safe; implement `IPageView` +
`IPageLifecycle`).

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

## Registration and metadata precedence

Descriptors are built in three phases; later phases override earlier ones.

1. **Defaults** (`PageDescriptorBuilder`) — Name = type name, `Transient`,
   `ShowImmediately`, `Replace`, no guards.
2. **Attributes** — `[PageMetadata(Name/Role/Presentation/Tags)]`,
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

`PageRegistry` is immutable after `Create` (FrozenDictionary on net9). A
duplicate page **type** or **name** (case-insensitive) throws at bootstrap.
`PageFactory` creates via a registered factory or a default-ctor fallback
(`AllowUnregisteredPages` defaults to `true`; the fallback raises the internal
`Warn` event).

> **Known gap:** `PageDescriptor.AllowAnonymous` (set by `[AllowAnonymous]`) is
> stored but **never consulted by the runtime** — guards on the descriptor always
> run.

## Reuse policies

| Policy | Behavior |
|---|---|
| `Transient` (default) | New instance per navigation; **disposed** when navigated away from. |
| `StrongSingleton` | One instance in a strong cache; lives until `ResetAsync`/`Shutdown`, both of which dispose cached pages. |
| `WeakSingleton` | `WeakReference` cache; reused while alive and undisposed, recreated after GC. Dead slots are compacted on miss. |

`KeepAttachedWhenHidden` (`[KeepAttached]`) keeps the page in the visual tree
when hidden — honored only when the policy is **not** `Transient` and the page is
not disposed.

## Load modes and the loading mask

`NavigationLoadMode` decides *when* `IBackgroundLoadable` work runs relative to
attach:

- `ShowImmediately` (default) — attach first, then await the load.
- `LoadBeforeShow` — await the load **before** the old page is hidden/detached.
- `LoadInBackground` — attach, then fire-and-forget. The result is applied via
  `ApplyBackgroundResultAsync` **only if** the page is still `Current` and not
  disposed; failures are logged to diagnostics, never thrown.

`LoadInBackgroundAsync` runs via `Task.Run` and must not touch the UI;
`ApplyBackgroundResultAsync` runs on the UI thread. During a load the runtime
shows the registered `IGlobalLoadingMask` page, driven directly through
`IViewHost` rather than the overlay services. The platform default mask is
auto-registered at bootstrap **unless** the scanned assemblies contain a custom
`IGlobalLoadingMask`.

## History and page state

- `NavigationHistory` is a back stack plus a forward stack. Forward navigation
  records the **from** page (`Record` pushes back and clears forward).
  `GoBackAsync` pops back, pushes the current page onto forward, and navigates
  with `NavigationArgs.Back(state)`.
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
lands on the idle page.

## Overlays

Four services, strictly partitioned by intent:

| Primitive | Blocking | Auto-dismiss | Return |
|---|---|---|---|
| `IToastView` / `IToastService` | no | timer (+ tap via `BindDismiss`) | `void` |
| `IDialogView` / `IDialogService` | **yes** | — | `Task<bool>` |
| `IPromptView<TResult>` / `IPromptService` | **yes** | — | `Task<TResult>` |
| `IPopoverView` / `IPopoverService` | no | on focus loss (via `IUnfocusAware`) | `Task<bool>` |

Dialog is always `bool` — anything tri-state uses `Prompt<TEnum>`. Popover is
always non-blocking — blocking variants go through Dialog.

Semantics worth knowing:

- Dialogs and prompts **stack**: the first open engages the
  `IInteractionBlocker`, the last close releases it.
- View pattern: the service creates the view via `PageFactory`, adds it through
  `IViewHost`, calls `OnShownAsync(payload)`, and completion flows back through
  `BindCompletion(callback)`.
- `PopoverService` wires the focus observer **after** focusing the view, so the
  focus switch itself does not insta-dismiss, and only for views implementing
  `IUnfocusAware`.
- `CloseAll()` on every service — invoked by `ResetAsync`/`DisposeAsync` —
  resolves pending awaiters with `false`/default, so callers are never left
  hanging.

Both platform projects ship `ToastViewBase`, `DialogViewBase`,
`PromptViewBase<TResult>`, `PopoverViewBase` and `AutoDismissPopoverBase` so
subclasses skip the wiring.

## Platform adapters

`IPlatformAdapter` is a pure factory:

| Method | Produces |
|---|---|
| `CreateHost` | `IPageHost` |
| `CreateEventDispatcher` | UI-thread marshaling |
| `CreateInteractionBlocker` | modal input blocking |
| `CreateTimerAdapter` | idle timer |
| `CreateInteractionObserverAdapter` | nullable — idle timeout silently unavailable |
| `CreateFocusObserver` | nullable — popovers just will not auto-dismiss |
| `GetDefaultLoadingMaskType` | nullable |

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
4. **Create the `NavigationContext`** — attach `DebugUtilsNavigationObserver` if
   `UseDebugUtils` was called; register the context, `NavigationSession` and
   `IUserContext`.
5. **`services.Lock()`** — registration after this throws.
6. **Wire the idle timer**, then mount the context (throws if already mounted).

## Reset vs Shutdown

- `ResetAsync()` — tears down the current page, cached pages and all overlays
  (resolving awaiters), and clears history. Context, session and adapter stay
  alive; navigation can continue.
- `Shutdown()` — full teardown: disposes the runtime, unmounts the facade, and
  clears all static event subscribers. Required before a fresh `Start()`.

## Diagnostics and observability

`NavigationDiagnostics` emits `PageLogEntry` (success/failure, presentation, load
mode, reuse policy, failure kind, error) and `GuardDeniedEvent` into the
context's `NavigationEventHub`, surfaced as the `NavigationLogged` and
`GuardDenied` events. Subscriber exceptions are swallowed — diagnostics never
break navigation. The runtime shares the **context's** hub, so
`NavigationService.Events` and `context.Events` both see all activity.

`NavigationService` also exposes static events: `Navigating`, `Navigated`,
`NavigationFailed`, `CurrentChanged`, `HistoryChanged`, `OnFirstPageAttached`,
`OnNoPageAttached`, `OnNoPageVisible`. Static events root their subscribers, so
`Shutdown()` nulls all of them.

Two optional bridges:

- **`UseDiagnostics(IDiagnosticsContext)`** — bridges entries into
  `NekoLib.Core.Diagnostics` via `DiagnosticsNavigationSink`. Local hub events
  keep flowing regardless.
- **`UseDebugUtils(IDebugUtils)`** — attaches `DebugUtilsNavigationObserver`, a
  *pure subscriber* that forwards events to `IDebugUtils.Record(...)` and exposes
  pull-based state **without touching the frozen core**. A disabled sink
  (`NullDebugUtils`) subscribes to nothing and returns `Disposable.Empty`. This
  is the template for hooking other modules: subscribe to an existing event seam,
  never instrument frozen core.

The observer has **two fidelity levels**, because the hub has only two events and
both fire *after* a navigation resolves:

| Overload | Records | State keys |
|---|---|---|
| `Attach(NavigationEventHub, debug)` | `Navigated`, `NavigationFailed`, `GuardDenied` | `Navigation::current`, `Navigation::stats` |
| `Attach(NavigationContext, debug)` (bootstrap path) | the above **+** `NavigationStarted`, `FirstPageAttached`, `NoPageAttached`, `NoPageVisible` | the above **+** `Navigation::history`, `Navigation::session` |

The extra signals come from the static `NavigationService` events, the only
public seam carrying them. `NavigationStarted` is the navigation *intent*: when a
navigation hangs — a guard that never returns, a deadlocked
`OnNavigatedToAsync` — the hub stays silent, so a `NavigationStarted` with no
matching outcome is the fingerprint of that freeze. `NoPageAttached` and
`NoPageVisible` mean the shell went blank, the classic page-leak symptom.

`Navigation::stats` carries aggregate counters that **outlive the ring buffer**:
once it wraps, the totals are the only surviving evidence.
`started > navigated + failed` means a navigation was entered and never resolved.

Two consequences of the static subscription: `Shutdown()` nulls those events and
silently drops the handlers (harmless — the next bootstrap attaches a fresh
observer), and `Navigation::history` is best-effort, because `NavigationHistory`
is UI-thread-affine with no internal locking, so capturing from another thread
during a navigation can throw (the sink isolates per provider and yields a
placeholder).

> **The observability module is frozen.** See the freeze section in
> [`TODO.md`](../../../TODO.md) for what is deliberately incomplete. Do not
> extend it without an explicit decision.

---

## FROZEN components

Do not modify without strong justification; extensions must live outside
`Core/`:

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
work inline. Naming follows `MethodName_Condition_ExpectedResult`.

**Tests that mount the static facade** touch process-wide state: they must carry
`[Collection("NavigationServiceFacade")]` so xunit serializes them, and
`await NavigationService.Shutdown()` in a `finally`.
`DebugUtilsNavigationObserverFacadeTests` is the reference. Everything else stays
parallel — the bootstrap tests never complete `Start()`, because
`FakePlatformAdapter` throws on `CreateHost`, so they never mount the facade.

`InternalsVisibleTo("NekoLib.Navigation.Tests.Unit")` is set in the Navigation
project.

Runtime scenario apps under `runtime_tests/` are interactive WinForms/WPF
executables — launch them directly, never via `dotnet test`.
