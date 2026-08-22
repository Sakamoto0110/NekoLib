# Navigation Module

**Kind:** guide

**Lifecycle:** historical

**Subject:** previously machine-local Claude Navigation guidance retained as
migration input

**Reference date:** not recorded

**Reference commit:** not recorded

**Current state:** pending the planned current-state audit; not authoritative

> **Documentation migration notice (2026-08-22):** This previously local file
> is now versioned as migration input and has not yet completed its planned
> current-state audit. Reverify every technical claim against current source,
> project files, tests, `TODO.md`, and the authoritative Navigation
> documentation before relying on it.

Guidance for the three projects under `src/Navigation/`: `NekoLib.Navigation` (core runtime), `NekoLib.Navigation.WinForms`, and `NekoLib.Navigation.Wpf` (platform adapters). Solution-wide rules (layering, compile-time constants, build commands) live in the root `CLAUDE.md`.

The Navigation module is the most complex in NekoLib. Read `src/Navigation/NekoLib.Navigation/README.md` before modifying it.

**Module conventions:** Nullable **enabled**, ImplicitUsings **disabled** in all three projects — match these, don't flip them. The code carries pre-existing nullable warnings (CS86xx); don't introduce new ones.

## Architecture: three moving parts

| Component | Role |
|---|---|
| `NavigationContext` (public, FROZEN) | Navigation-scoped **state bag**: Host, Services (locked `ServiceLocator`), Registry, History, Session, Platform, Diagnostics/Events. No navigation logic. Created by `PageNavBootstrap.Start()`. |
| `NavigationRuntime` (internal) | The actual **engine**. Owns `Current`, the strong/weak page caches, attached/visible page sets, and the navigation gate. Created by `NavigationService.UseContext()`, one per mounted context. |
| `NavigationService` (public static facade) | The application-facing API (`SwitchPage<T>`, `GoBackAsync`, `ShowDialogAsync`, `Session`, `Events`, static events). `UseContext` is `internal`; `Start()` auto-mounts. Double-mount without `Shutdown()` throws. |

## Execution model (why navigation never races)

Every public entry point runs through `ExecuteAsync` = **marshal to UI thread** (`IEventDispatcherAdapter.BeginInvoke`) **then serialize** on a `SemaphoreSlim(1,1)` nav gate. Consequences:

- All lifecycle methods run on the UI thread; never `ConfigureAwait(false)` inside the runtime's UI path.
- Guards run **inside** the gate, bounded by a 30s timeout (`GuardEvaluationTimeoutMs`) — a hung guard denies the navigation and releases the gate instead of deadlocking all future navigation.
- Dialog/Prompt/Popover calls marshal to UI but deliberately do **not** take the nav gate: a modal awaits user input, and holding the gate would freeze navigation.
- `DisposeAsync` uses `ExecuteSafeOnUiAsync`: if the message pump is already dead (app shutdown), teardown runs inline instead of hanging forever.

## Canonical lifecycle order (DO NOT CHANGE)

As implemented in `NavigationRuntime.SwitchInternalAsync`:

```
Registry lookup (unregistered type ⇒ throw)
→ Guard evaluation (30s cap; deny/redirect: depth ≤ 8, cycle detection via visited set)
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

On failure: `NavigationFailed` event + `EmitFailure` with the stage recorded as `NavigationFailureKind` (`PageNotRegistered` → `PageCreationFailed` → `LoadFailed` → `LifecycleFailed`), then rethrow.

`NavigationRuntime` (driven through the static `NavigationService` facade) is the **only component allowed** to invoke lifecycle methods. `IPageLifecycle` has exactly two hooks: `OnNavigatedToAsync(NavigationArgs)` and `OnNavigatedFromAsync()`.

### Known gaps

`PageDescriptor.AllowAnonymous` (set by `[AllowAnonymous]`) is stored but **never consulted by the runtime** — guards on the descriptor always run.

## Page registration & metadata precedence

Descriptors are built in three phases; later phases override earlier ones:

1. **Defaults** (`PageDescriptorBuilder`): Name = type name, `Transient`, `ShowImmediately`, `Replace`, no guards.
2. **Attributes**: `[PageMetadata(Name/Role/Presentation/Tags)]`, `[PageLoad(mode)]`, `[PageReuse(policy)]`, `[PageTimeout(seconds)]`, guard attributes (`[RequireAuthenticated]`, `[RequireRole]`, `[RequirePermission]`, `[RequireAllPermissions]`, `[RequireAnyPermissions]`), `[AllowAnonymous]`, `[KeepAttached]`.
3. **Manual DSL** (`ConfigurePages(cfg => cfg.Page<T>()...)`): `.Named()`, `.AsIdle()`, `.StrongSingleton()/.WeakSingleton()/.Transient()`, `.LoadMode()`, `.IdleTimeout(seconds)`, `.Tag()`.

Multiple guard attributes on one page compose into an `AndGuard`. `OrGuard` exists for manual composition only.

`PageRegistry` is immutable after `Create` (FrozenDictionary on net9). Duplicate page **type** or duplicate page **name** (case-insensitive) throws at bootstrap. `PageFactory` creates via registered factory or default-ctor fallback (`AllowUnregisteredPages` defaults to `true`; the fallback raises the internal `Warn` event).

## Reuse policies (runtime caches)

| Policy | Behavior |
|---|---|
| `Transient` (default) | New instance per navigation; **disposed** when navigated away from. |
| `StrongSingleton` | One instance in a strong cache; lives until `ResetAsync`/`Shutdown` (both dispose cached pages). |
| `WeakSingleton` | `WeakReference` cache; reused while alive & undisposed, recreated after GC. Dead slots compacted on miss (L-5). |

`KeepAttachedWhenHidden` (`[KeepAttached]`) keeps the page in the visual tree when hidden — only honored when the policy is **not** Transient and the page isn't disposed.

## Load modes & loading mask

`NavigationLoadMode` decides *when* `IBackgroundLoadable` work runs relative to attach:

- `ShowImmediately` (default): attach first, then await load.
- `LoadBeforeShow`: await load **before** the old page is hidden/detached.
- `LoadInBackground`: attach, then fire-and-forget. The result is applied via `ApplyBackgroundResultAsync` **only if** the page is still `Current` and not disposed (A-5); failures are logged to diagnostics, never thrown.

`IBackgroundLoadable.LoadInBackgroundAsync` runs via `Task.Run` (must not touch UI); `ApplyBackgroundResultAsync` runs on the UI thread. During load, the runtime shows the registered `IGlobalLoadingMask` page (driven directly through `IViewHost`, not the overlay services). The platform's default mask is auto-registered at bootstrap **unless** the scanned assemblies contain a custom `IGlobalLoadingMask` implementation.

## History & page state

- `NavigationHistory` = back stack + forward stack. Forward navigation records the **from** page (`Record` pushes back + clears forward). `GoBackAsync` pops back, pushes the current page onto the forward stack, navigates with `NavigationArgs.Back(state)`.
- Back-navigation **skips** `History.Record` inside the switch (the back path manages both stacks itself — history-double-push fix) and fires `HistoryChanged` once from `GoBackInternalAsync`.
- `IPageStateful`: `CaptureState()` is called when leaving a page (blob stored in the history entry); `RestoreState(blob)` is called **only on back-navigation, before `OnNavigatedToAsync`**. The same blob also arrives as `NavigationArgs.Payload` with `IsBackNavigation == true`, but `RestoreState` is the preferred channel.
- WinForms `PageView.AllowBackNavigation` exists on the base class but back-stack participation is currently driven by the runtime, not that flag.

## Guards & session

- `IGuard.EvaluateAsync(GuardContext{TargetPage, User})` → `GuardResult.Allow() / Deny(reason) / Redirect(pageType, reason)`.
- Redirect chains: max depth 8, cycle detection via a visited set; violations emit `GuardDenied` and stop (no throw).
- A guard that throws ⇒ navigation denied with the exception message as reason (never crashes navigation).
- `NavigationSession` (framework-owned, per context) implements `IUserContext`: `SignIn(roles)`, `SignIn(roles, permissions)`, `SignOut()`. Registered in the locator as both `NavigationSession` and `IUserContext` — the **same instance** guards read, so `NavigationService.Session.SignIn("admin")` is visible to guards on the next navigation. Consumers never implement an auth contract for the built-in guards to work.

## Idle system

- `IdlePageRules` is the single source of truth for "which page is idle". Priority: `PageRole.Idle` (via `SetIdle<T>()` or `.AsIdle()`) → `idle` tag → name containing "idle". No fallback to "MainPage".
- More than one page tagged `Role=Idle` ⇒ bootstrap throws. `SetIdle<T>()` may be called at most once.
- Idle timeout: `[PageTimeout(seconds)]` / `.IdleTimeout(seconds)` is only valid **on the idle page** (bootstrap throws otherwise) and overrides the global `UseIdleTimeout(milliseconds)`. Either alone is enough to enable the timer.
- Wiring (all automatic in `Start()`): platform `IInteractionObserverService` resets the timer on any input; on tick the timer stops, `Session.SignOut()` runs, then `GoIdleAsync()`. The timer starts immediately, so an unattended boot also lands on the idle page.

## Bootstrap (`PageNavBootstrap.Start()`) sequence

1. Build registry: assembly scan (`GetLoadableTypes` tolerates `ReflectionTypeLoadException`; loads what it can), explicit registrations, `SetIdle` lands before `ConfigurePages`, default loading mask auto-registration. `UseRegistry(...)` is mutually exclusive with all page-configuration methods.
2. Validate: single idle page; `[PageTimeout]` placement; platform `CanHandle(nativeHost)`.
3. `ServiceLocator` open phase — registered services: `IPageHost`, `IEventDispatcherAdapter`, `IInteractionBlocker`, `ITimerAdapter`, `PageFactory`, optional `IInteractionObserverService`/`IEventSubscriptionAdapter`/`IFocusObserverAdapter`, and the four overlay services. Then `ConfigureServices(...)` callback for app extensions.
4. Create `NavigationContext`; attach `DebugUtilsNavigationObserver` if `UseDebugUtils` was called; register the context, `NavigationSession`, and `IUserContext`.
5. `services.Lock()` — registration after this throws.
6. Wire idle timer (if configured), then `NavigationService.UseContext(context)` (throws if already mounted — tests must call `Shutdown()` in teardown).

## FROZEN components

Do not modify without strong justification; extensions must live outside `Core/`:
- `NavigationContext.cs`
- `NavigationRuntime.cs`
- `PageRegistry.cs`
- `PageFactory.cs`

(The old FROZEN list mentioned `PageLifecycleCleanupService.cs` — that class no longer exists; its cleanup responsibilities live inside `NavigationRuntime`.)

## Key contracts

**Page-side (opt-in capabilities — a page implements what it needs):**
- `IPageView` — minimal contract (Name, NativeView, IsDisposed). Required.
- `IPageLifecycle` — `OnNavigatedToAsync(args)` / `OnNavigatedFromAsync()`.
- `IPageVisibility` — `ShowPage()` / `HidePage()`.
- `IPageStateful` — `CaptureState()` / `RestoreState(state)` for history.
- `IBackgroundLoadable` — `LoadInBackgroundAsync(args)` / `ApplyBackgroundResultAsync()`.
- `IHostAttachable` — `OnAttach(host)` / `OnDetach()` callbacks from the host.
- `IUnfocusAware` — `OnUnfocusAsync()` for light-dismiss surfaces.

**Platform-side (`IPlatformAdapter` is a pure factory):** `CreateHost` (→ `IPageHost`), `CreateEventDispatcher`, `CreateInteractionBlocker`, `CreateTimerAdapter`, `CreateInteractionObserverAdapter` (nullable — idle timeout silently unavailable), `CreateFocusObserver` (nullable — popovers just won't auto-dismiss), `GetDefaultLoadingMaskType` (nullable).

**Host split:** `IPageHost` (Attach/Detach/BringToFront of pages) vs `IViewHost` (AddView/RemoveView/BringToFront/Focus of raw views — used by overlay services and the loading mask). `WinFormsLayeredPageHostBase` implements both over a single Panel: pages are pinned to the bottom of the z-order (`SendToBack`), surfaces stay above.

**Guards:** `IGuard` / `GuardContext` / `GuardResult`; compose via `AndGuard`, `OrGuard`.

## Overlay primitives

4 services, strictly partitioned by intent:

| Primitive | Blocking | Auto-dismiss | Return |
|---|---|---|---|
| `IToastView` / `IToastService` | no | timer (+ tap via `BindDismiss`) | `void` |
| `IDialogView` / `IDialogService` | **yes** | — | `Task<bool>` |
| `IPromptView<TResult>` / `IPromptService` | **yes** | — | `Task<TResult>` |
| `IPopoverView` / `IPopoverService` | no | on focus loss (via `IUnfocusAware`) | `Task<bool>` |

Rules: Dialog is always `bool` — anything tri-state uses `Prompt<TEnum>`. Popover is always non-blocking — blocking variants go through Dialog.

Service semantics worth knowing:
- Dialogs/prompts **stack**: the first open engages the `IInteractionBlocker`, the last close releases it.
- View pattern: the service creates the view via `PageFactory`, adds it through `IViewHost`, calls `OnShownAsync(payload)`, and completion flows through `BindCompletion(callback)` — the view calls the callback, the service tears down and resolves the awaiter.
- `PopoverService` wires the focus observer **after** focusing the view (so the focus switch itself doesn't insta-dismiss) and only for views implementing `IUnfocusAware`; the view's `OnUnfocusAsync` decides whether to dismiss.
- `CloseAll()` on every service (invoked by `ResetAsync`/`DisposeAsync`) resolves pending awaiters with `false`/default — callers are never left hanging.
- WinForms/WPF ship base classes (`ToastViewBase`, `DialogViewBase`, `PromptViewBase`, `PopoverViewBase`, `AutoDismissPopoverBase`) so subclasses skip the wiring; `PageView` is the base for regular pages (designer-safe, implements `IPageView` + `IPageLifecycle`).

## Diagnostics & observability

- `NavigationDiagnostics` emits `PageLogEntry` (success/failure, presentation, load mode, reuse policy, failure kind, error) and `GuardDeniedEvent` into the context's `NavigationEventHub` (`NavigationLogged` / `GuardDenied` events). Subscriber exceptions are swallowed — diagnostics never break navigation.
- The runtime shares the **context's** hub (D-3 fix) — subscribe via `NavigationService.Events` or `context.Events`; both see all activity.
- `UseDiagnostics(IDiagnosticsContext)` bridges entries into NekoLib.Diagnostics via `DiagnosticsNavigationSink`; local hub events keep flowing regardless.
- `UseDebugUtils(IDebugUtils)` attaches `DebugUtilsNavigationObserver` — a *pure subscriber* that forwards events to `IDebugUtils.Record(...)` and exposes pull-based state, **without touching the frozen `NavigationContext`/`NavigationRuntime`**. Disabled sink (`NullDebugUtils`) ⇒ no subscription, returns `Disposable.Empty`. This is the template for hooking other modules: subscribe to an existing event seam, never instrument frozen core.

  **The observer has two fidelity levels** — the hub only has 2 events and both fire *after* a navigation resolves:

  | Overload | Records | State keys |
  |---|---|---|
  | `Attach(NavigationEventHub, debug)` | `Navigated`, `NavigationFailed`, `GuardDenied` | `Navigation::current`, `Navigation::stats` |
  | `Attach(NavigationContext, debug)` (bootstrap path) | the above **+** `NavigationStarted`, `FirstPageAttached`, `NoPageAttached`, `NoPageVisible` | the above **+** `Navigation::history`, `Navigation::session` |

  The extra signals come from the **static `NavigationService` events**, the only public seam carrying them. `NavigationStarted` is the navigation *intent*: when navigation hangs (a guard that never returns, a deadlocked `OnNavigatedToAsync`) the hub stays silent, so a `NavigationStarted` with no matching outcome is the fingerprint of that freeze. `NoPageAttached`/`NoPageVisible` mean the shell went blank — the classic page-leak symptom.

  `Navigation::stats` carries aggregate counters that **outlive the ring buffer**: once it wraps, the totals are the only surviving evidence. `started > navigated + failed` ⇒ a navigation was entered and never resolved.

  Two consequences of the static subscription: `Shutdown()` nulls those events, silently dropping the handlers (harmless — the next bootstrap attaches a fresh observer); and `Navigation::history` is best-effort, because `NavigationHistory` is UI-thread-affine with no internal locking, so capturing from another thread during a navigation can throw (the sink isolates per provider and yields a placeholder).

- **The observability module is FROZEN** as of 2026-07-26 — see the freeze section in the root `TODO.md` for what is deliberately incomplete (no hooks outside Navigation, dead command channel, no reusable consumer surface). Don't extend it without an explicit decision.
- `NavigationService` also exposes static events: `Navigating`, `Navigated`, `NavigationFailed`, `CurrentChanged`, `HistoryChanged`, `OnFirstPageAttached`, `OnNoPageAttached`, `OnNoPageVisible`. Static events root their subscribers — `Shutdown()` nulls all of them (L-4).

## Reset vs Shutdown

- `NavigationService.ResetAsync()` — tears down current page, cached pages, and all overlays (resolving awaiters), clears history. Context, session, and adapter stay alive; navigation can continue.
- `NavigationService.Shutdown()` — full teardown: disposes the runtime, unmounts the facade, clears all static event subscribers. Required before a fresh `Start()`.

## Typical bootstrap

```csharp
PageNavBootstrap
    .Use<WinFormsPlatformAdapter>(this)
    .RegisterPagesFromAssembly(typeof(IdlePage).Assembly)
    .ConfigurePages(cfg =>
    {
        cfg.Page<IdlePage>().AsIdle().StrongSingleton();
        cfg.Page<AdminPage>().StrongSingleton();
    })
    .UseIdleTimeout(10_000)
    .Start();
```

`Start()` auto-mounts the resulting `NavigationContext` onto the static `NavigationService` facade — view-models can call `NavigationService.SwitchPage<T>()` right after. Shut down with `NavigationService.Shutdown()` before a fresh `Start()`.

`InternalsVisibleTo("NekoLib.Navigation.Tests.Unit")` is set in the Navigation project.

## Tests

Run the unit tests:
```bash
dotnet test tests/NekoLib.Navigation.Tests/Unit/NekoLib.Navigation.Tests.Unit.csproj
```

Navigation tests use fakes in `tests/NekoLib.Navigation.Tests/Unit/Fakes/`: `RuntimeTestFixture` wires a full in-memory runtime with `FakePlatformAdapter`, `FakePageHost`, `StubPageViews`, and a `SyncEventDispatcherAdapter` (runs "UI" work inline). Test naming follows `MethodName_Condition_ExpectedResult`.

**Tests that mount the static facade** (`NavigationService.UseContext` — internal, visible via `InternalsVisibleTo`) touch process-wide state: they must carry `[Collection("NavigationServiceFacade")]` so xunit serializes them, and `await NavigationService.Shutdown()` in a `finally`. `DebugUtilsNavigationObserverFacadeTests` is the reference. Everything else stays parallel — the bootstrap tests never complete `Start()` (the `FakePlatformAdapter` throws on `CreateHost`), so they never mount the facade.

Runtime tests (`runtime_tests/WinForms_481`, `runtime_tests/LoginFlow_481`, `runtime_tests/Wpf_Smoke`) are WinForms/WPF `.exe` apps — launch them directly, not via `dotnet test`.
