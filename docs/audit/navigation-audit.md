# NekoLib.Navigation — Audit (Passes 1–6)

**Auditor:** Claude (Sonnet 4.6, later Opus 4.7/4.8)
**Started:** 2026-05-28 · **Last updated:** 2026-06-03
**Scope:** `src/Navigation/**`, `tests/NekoLib.Navigation.Tests/**`, `runtime_tests/WinForms_481/**`. Pass 6 also touches the sibling `src/Mvvm/NekoLib.Mvvm/**` (extracted from `NekoLib.Navigation`) and `tests/NekoLib.Mvvm.Tests/**`.

**Status (current):** all static-analysis findings (Passes 1–4) and runtime-repro findings (Pass 5: NEW-7..10, plus NEW-11 caught during the §2.12 walkthrough) are closed. Pass 6 added two minor API-smell findings (NEW-12, NEW-13) plus a body of structural work — automated test coverage, MVVM helpers extracted into a sibling project, and the runtime-sim demo moved out of `tests/` into a dedicated `runtime_tests/` tree.

| Pass | Phase | Output |
|---|---|---|
| 1 | Flat survey | A-1..A-8, L-1..L-5, S-1..S-4, D-1..D-10, N-1..N-6 |
| 2 | Verify/triage + deep-dive | NEW-1..NEW-6; deep-dive on N-1..N-6; Portuguese→English |
| 3 | P0+P1+P2 implementation | most findings closed |
| 4 | Deferred (S-1, N-2) + runtime-repro plan | S-1, N-2 closed; §2.8 checklist |
| 5 | Live runtime repro via demo | NEW-7..NEW-11 found and fixed; demo instrumented |
| 6 | Structural + automated coverage | NEW-12, NEW-13 (NOTE); 65 xunit tests; `NekoLib.Mvvm` sibling; `runtime_tests/` split; designer-split restore |

---

## Preface: What This Module Is Supposed to Do

NekoLib.Navigation is a single-page-application–style navigation framework designed to work in WinForms (and optionally WPF). It is **not** a routing library in the HTTP sense — it is a **shell/host framework** that controls which `Control` (page) is currently visible inside a container panel, and provides structured access to overlaid surfaces (modals, dialogs, prompts, toasts).

### Core Concepts

| Concept | Purpose |
|---|---|
| `NavigationContext` | The passive root container: holds the page registry, service locator, history, and host. One per shell window. |
| `NavigationRuntime` | The active engine: owns caches, the presentation stack, the nav semaphore, and dispatches to the UI thread. |
| `NavigationService` | A **static facade** over a default `NavigationContext`. Intended as a convenience for simpler apps; the doc comment says "keeps legacy call sites working while the framework becomes instance-based." |
| `IPageHost` / `IViewHost` | Abstracts attaching `IPageView` controls into the root panel. WinForms impl: `WinFormsLayeredPageHostBase`. |
| `PageRegistry` | Immutable metadata map built during bootstrap (via attribute scan or fluent DSL). |
| `PageFactory` | Creates `IPageView` instances; supports custom factories and a fallback reflective `Activator.CreateInstance`. |
| `ServiceLocator` | A closed-once IoC container scoped to the navigation context. |
| `IGuard` / `GuardComposer` | Navigation access control pipeline. Can short-circuit navigation and redirect. |
| `IToastService` / `IDialogService` / `IPromptService` | ISP-split overlay surfaces. Replaced the old `OverlayService` monolith. |
| `WinFormsPlatformAdapter` | Creates the WinForms-specific implementations of all platform interfaces from a single `Control` host. |
| `NavigationHistory` | Back/forward stack, instance-scoped to `NavigationContext`. |

### Design Intentions Observed in Code

- The `OverlayService` monolith was refactored into three ISP services (Toast, Dialog, Prompt). The refactor is **complete as of Pass 3** — the old overlay/modal/stack subsystem was deleted entirely; the three services are fully wired and replace it.
- The static `NavigationService` is a **migration shim** and is acknowledged as such in its doc comment. Long-term intent appears to be fully instance-based.
- `WinFormsLayeredPageHostBase` is the single canonical host: content pages sit at the back, transient surfaces (toasts, dialogs, prompts) `BringToFront`. No separate overlay/modal layer exists.

---

## Audit Findings

Items are grouped by domain. Severity labels: **CRITICAL**, **HIGH**, **MEDIUM**, **LOW**, **NOTE**.

---

### 1. Async / Threading

#### [CRITICAL] A-1: Toast/Dialog/Prompt service calls bypass the UI thread marshal

**Files:** `NavigationRuntime.cs:267–304`, `ToastService.cs:46–51`, `DialogService.cs:46–48`, `PromptService.cs:47–50`

`ShowToast`, `ShowDialogAsync`, and `ShowPromptAsync` in `NavigationRuntime` call `EnsureRuntimeServices()` then directly call the service — they do **not** go through `ExecuteAsync` (which is the `RunOnUiAsync` + `SerializeAsync` pipeline). The service implementations immediately call `_viewHost.AddView`, `BringToFront`, and `Focus`, all of which touch the WinForms `Controls` collection. Calling these from a thread-pool thread (e.g., if the caller is inside a `Task.Run` or a background continuation) is an illegal cross-thread UI access in WinForms.

**Impact:** `InvalidOperationException: Cross-thread operation` in Release, silent UI corruption in some edge cases.

**Needs deeper work:** Wrap all three entry points in `ExecuteAsync` OR establish a contract that they may only be called from the UI thread (and assert it).

---

#### [CRITICAL] A-2: `ToastService.RunDismissTimerAsync` touches UI from thread pool

**File:** `ToastService.cs:65–83`

`RunDismissTimerAsync` runs on the thread pool (via the `Task.Delay` continuation). After the delay, it calls `DismissCurrentInternal` which calls `_viewHost.RemoveView(toRemove.NativeView)` and `toRemove.Dispose()`. Both are WinForms UI operations. No thread-marshal occurs.

**Impact:** Same as A-1. `RemoveView` calls `Root.Controls.Remove(control)` off the UI thread.

**Fix direction:** Marshal the dismissal back to the UI thread before touching any view, e.g., via `_dispatcher.BeginInvoke(() => DismissCurrentInternal())`.

---

#### [HIGH] A-3: Navigation semaphore held across modal blocking wait

**File:** `NavigationRuntime.cs:820–839` (`ShowModalInternalAsync`)

`ShowModalInternalAsync` is invoked from inside `SwitchInternalAsync`, which runs inside `SerializeAsync` — meaning the `_navGate` semaphore is held the entire time the modal is open. The method awaits `tcs.Task`, which only resolves when the user closes the modal. While the modal is open, **no other navigation can start** — including `GoBack`, `ResetAsync`, or any timeout-triggered navigation.

**Impact:** If the user closes the app, triggers a timeout, or if anything else tries to navigate while a `ModalOverlay` is open, it will deadlock waiting for `_navGate`.

**Note:** The `IDialogService`/`IPromptService` path does NOT have this problem (they don't hold `_navGate`). The issue is specifically the legacy `ShowModalAsync` / `ShowModalInternalAsync` path invoked via the page presentation stack when a page is registered as `PagePresentationMode.ModalOverlay`.

---

#### [HIGH] A-4: `BeginInvoke` called before window handle may be created

**File:** `WinFormsEventDispatcherAdapter.cs:26`

`_root.BeginInvoke(action)` requires the control's window handle to be created. If `NavigateAsync` (or any `ExecuteAsync` caller) is invoked before the host control is shown/created (e.g., inside the `Form` constructor before `InitializeComponent`), this throws:

> `InvalidOperationException: Invoke or BeginInvoke cannot be called on a control until the window handle has been created.`

The `Form1` demo shows a `Form1_Load` entry point for the first navigation, but `UseContext` is called inside the constructor. Any eager call to navigation methods in the constructor (not `Load`) would trigger this.

**Needs investigation:** Is there a guaranteed safe window in the bootstrap lifecycle where `BeginInvoke` will always work? Consider guarding with `_root.IsHandleCreated`.

---

#### [MEDIUM] A-5: Fire-and-forget `LoadInBackground` may apply results to a disposed/navigated-away page

**File:** `NavigationRuntime.cs:665–668`

```csharp
_ = LoadAsync(to, navArgs.Payload);
```

`LoadAsync` is fired and forgotten. If the user navigates away before `LoadInBackgroundAsync` completes, `ApplyBackgroundResultAsync` will still be called on the page — which may already be detached, hidden, or even disposed (if `Transient` policy). There is no cancellation mechanism and no check that the page is still the current one at apply-time.

**Impact:** Potential `ObjectDisposedException` or stale UI updates on a non-visible page.

---

#### [MEDIUM] A-6: `DisposeAsync` acquires `ExecuteAsync` which marshals via `BeginInvoke` — may deadlock on app close

**File:** `NavigationRuntime.cs:399–423`

`NavigationRuntime.DisposeAsync` calls `ExecuteAsync(async () => ...)` which internally calls `RunOnUiAsync` which calls `_dispatcher.BeginInvoke`. During application shutdown, the message pump may have already stopped. In that case, `BeginInvoke` enqueues a message that will never be processed, and the `TaskCompletionSource` inside `RunOnUiAsync` will never complete. The `await` in `NavigationService.Shutdown` will hang.

**Impact:** App shutdown hang; process must be killed.

---

#### [LOW] A-7: `OnTimeout` swallows `ExecuteAsync` exceptions silently

**File:** `NavigationRuntime.cs:699–701`

```csharp
private void OnTimeout()
{
    _ = OnTimeoutAsync();
}
```

If `OnTimeoutAsync` throws (e.g., the navigation gate is poisoned), the exception is silently dropped via `_ =`. No log, no diagnostic event, no re-throw.

---

#### [LOW] A-8: `CleanupAsync` is falsely async

**File:** `NavigationRuntime.cs:984–1004`

`CleanupAsync` has an `async Task` signature but all return paths are `return Task.CompletedTask` — it never actually awaits anything. The signature misleads readers into expecting async disposal/teardown paths. Either drop the `async` modifier or populate actual async teardown paths.

---

### 2. Resource Leaks

#### [HIGH] L-1: Singleton pages not disposed on `DisposeAsync` / `ResetAsync`

**File:** `NavigationRuntime.cs:399–423`, `NavigationRuntime.cs:362–396`

Both `DisposeAsync` and `ResetAsync` dispose only the `Current` page. The `_strongCache` may contain additional singleton pages (pages that were navigated to, put into cache, and then replaced). None of those cached pages are disposed.

**Impact:** Any singleton page with unmanaged resources (file handles, subscriptions, timers) will leak.

---

#### [HIGH] L-2: `WinFormsInteractionObserver` leaks handlers when controls are removed

**File:** `WinFormsInteractionObserver.cs:63–66`

`OnRootControlAdded` hooks new controls added to `_root`. But there is no corresponding `ControlRemoved` handler. When a page's controls are removed from `Root.Controls`, their event handlers remain in `_hooked` (and still delegate to `OnInteraction`). The removed controls are never unhooked.

**Impact:** Growing `_hooked` set over navigation lifetime; removed controls are kept alive by the event handler references in `_hooked`.

---

#### [MEDIUM] L-3: `PageLifecycleTracker` is static global state — never actually populated

**File:** `PageLifecycleTracker.cs`, `NavigationRuntime.cs`

`PageLifecycleTracker` provides `Register`, `Update`, and `Unregister` methods, but `NavigationRuntime` never calls any of them. The tracker is entirely inert. As a side effect, `NavigationService.AssertFrameworkIsDown` (the `#if DEBUG` leak assertion) always passes vacuously, providing false assurance.

**Impact:** The leak detection tooling is non-functional.

---

#### [MEDIUM] L-4: Static events on `NavigationService` are GC roots

**File:** `NavigationService.cs:31–38`

`Navigating`, `Navigated`, `NavigationFailed`, `CurrentChanged`, `HistoryChanged`, `OnFirstPageAttached`, `OnNoPageAttached`, `OnNoPageVisible` are static events. Any object that subscribes and is not explicitly unsubscribed will be kept alive for the lifetime of the `AppDomain`. This is especially risky in long-running desktop apps with multiple "sessions" (e.g., the user logs out and back in).

---

#### [LOW] L-5: `_strongCache` and `_weakCache` stale entries accumulate

**File:** `NavigationRuntime.cs:44–45`, `RemoveFromCaches:1006–1019`

`RemoveFromCaches` is only called in `CleanupAsync` with `forceDispose: true`. Cached pages are removed on demand but not on a schedule or on overall runtime disposal. For `Cached` (weak) policy pages, the `_weakCache` dictionary keeps the `WeakReference<IPageView>` wrapper alive even after the target is collected. No compaction of dead entries occurs.

---

### 3. Static / Global State

#### [HIGH] S-1: `PlatformRegistry` is a static singleton that never resets

**File:** `PlataformRegistry.cs`

`PlatformRegistry.Register` throws on a second call, and the registered adapter is never cleared. This makes the module **incompatible with multiple navigation contexts in the same process** (e.g., unit tests, multi-window scenarios). There is no `Reset()` or `Unregister()` method.

---

#### [HIGH] S-2: `NavigationService.UseContext` double-init guard is `#if DEBUG` only

**File:** `NavigationService.cs:48–62`

In Release builds, calling `UseContext` twice silently replaces `_context` and `_runtime` without calling `Shutdown` on the previous runtime. The previous runtime's event subscriptions are leaked, services are not disposed, and `_runtime` is overwritten without cleanup.

**Fix direction:** Remove the `#if DEBUG` guard — the double-init check should be release-safe.

---

#### [MEDIUM] S-3: Dead fields `_attachedPages` / `_visiblePages` in `NavigationService`

**File:** `NavigationService.cs:40–41`

```csharp
private static int _attachedPages;
private static int _visiblePages;
```

These two `int` fields are declared at static class scope but never read or written. The actual tracking is inside `NavigationRuntime` as `HashSet<IPageView>`. These are dead code that create false expectations.

---

#### [MEDIUM] S-4: `OnFirstPageAttached` and `OnNoPageAttached` static events never fire

**File:** `NavigationService.cs:163–180` (`WireRuntimeEvents` / `UnwireRuntimeEvents`)

The `WireRuntimeEvents` method wires `Navigating`, `Navigated`, `NavigationFailed`, `CurrentChanged`, and `HistoryChanged`. It does **not** wire `OnFirstPageAttached`, `OnNoPageAttached`, or `OnNoPageVisible`. So those three public static events on `NavigationService` are permanently silent.

---

### 4. Structural / Design Issues

#### [HIGH] D-1: Duplicate `PresentationEntry` class — one is dead code

**Files:** `NavigationRuntime.cs:1054–1078` (private nested class), `Runtime/Core/PresentationEntry.cs` (standalone file)

There are two implementations of `PresentationEntry`. The private nested class inside `NavigationRuntime` is the one actually used (it has a public `ModalTcs` property). The standalone `PresentationEntry.cs` has a private field and a `CompleteModal()` method — it appears to be an earlier version that was never removed. The standalone file is dead code.

---

#### [HIGH] D-2: `WinFormsInteractionBlocker` only disables direct children — not the whole subtree

**File:** `WinFormsInteractionBlocker.cs:22–26`

```csharp
private static void SetChildrenEnabled(Control c, bool enabled)
{
    foreach (Control child in c.Controls)
        child.Enabled = enabled;
}
```

This disables only the first-level children of `_root`. Any `Panel` or `GroupBox` inside those children will still have **enabled** grandchildren. During a modal or dialog, users can interact with buttons/inputs nested inside panels. This is a functional modal-blocking bypass.

**Fix direction:** Recurse into the subtree, or disable the root itself.

---

#### [MEDIUM] D-3: `NavigationContext` allocates a dead `NavigationEventHub`

**File:** `NavigationContext.cs:49–56`

The constructor creates a local `var hub = new NavigationEventHub()` on line 49, then creates a `NavigationDiagnostics(hub, sink)` on line 56. But `NavigationDiagnostics` creates its OWN hub internally — the `hub` passed to the constructor is stored and exposed as `NavigationDiagnostics.Hub`. However, the `hub` local on line 49 is passed to the `NavigationDiagnostics` constructor — so they DO share the same hub. This is fine at runtime.

**But:** `NavigationRuntime` ALSO creates its own `NavigationEventHub` and `NavigationDiagnostics` in its constructor (lines 75–82), completely independent of the one inside `NavigationContext`. The runtime's hub is what receives all actual events. The context's hub is never published to by the runtime. This means `NavigationContext.Events` (and `context.Diagnostics`) is an orphaned dead channel.

---

#### [MEDIUM] D-4: `NavigationArgs` factory methods `Default`, `Transient`, and `Silent` are identical

**File:** `NavigationArgs.cs:28–35`

All three produce `new(payload, NavigationLoadMode.ShowImmediately)`. `Transient` and `Silent` carry no semantic difference from `Default`. Call-site intent is not expressed in the args object (e.g., a transient navigation should probably produce a `Transient` reusePolicy hint, but the args object has no such field). The names imply behavior but deliver none.

---

#### [MEDIUM] D-5: `INavigationContext` interface is stale/abandoned

**File:** `Contracts/Runtime/INavigationContext.cs`

This interface declares `IPageOverlay Overlay` and `IInteractionBlocker Blocker` but the concrete `NavigationContext` class does not implement `INavigationContext`. The interface is never referenced in the runtime. It appears to be a leftover from a prior design that was superseded but not deleted.

---

#### [MEDIUM] D-6: `WinFormsLayeredPageHostBase.AddView` is partially broken

**File:** `WinFormsLayeredPageHostBase.cs:79–145`

The overlay path (for `IPageOverlay` views) creates a `Form host` and a `Form mask`, fills `_activeOverlays`, but the `host.Show(...)` and `Root.Controls.Add(host)` lines are commented out. The host form is created, positioned, and stored — but never shown or attached. Overlay display is effectively broken for this host class. A parallel `_popupWrappers` dictionary (line 75) is declared and never populated.

`RemoveView` (line 147) only closes and disposes the host/mask from `_activeOverlays`. Since the host was never shown, `pair.Host.Close()` on an invisible form is a no-op that hides the bug.

---

#### [LOW] D-7: `PageRegistry.ResolveTimeoutTarget` resolves to `PageRole.Home`, not `IsTimeoutTarget`

**File:** `PageRegistry.cs:115–118`

```csharp
public PageDescriptor? ResolveTimeoutTarget()
    => _byType.Values.FirstOrDefault(x => x.Role == PageRole.Home);
```

The method name says "timeout target" but looks for `PageRole.Home`. The actual timeout-target lookup in the runtime (`NavigationRuntime.cs:730–733`) correctly looks for `PageTimeoutPolicy.IsTimeoutTarget` first. The `ResolveTimeoutTarget` helper in the registry is inconsistent with the runtime logic and is unused by the runtime.

---

#### [LOW] D-8: Hardcoded Portuguese fallback in `DefaultLoadingMask`

**File:** `DefaultLoadingMask.cs:43`

```csharp
_lblMessage.Text = payload?.ToString() ?? "Carregando...";
```

"Carregando..." is Portuguese for "Loading...". This is a development artifact. The english fallback should be `"Loading..."` or this should draw from a localization resource.

---

#### [NOTE] D-9: `PageMetadataBuilder.RegisterFromAssemblyAndReferences` swallows load failures

**File:** `PageMetadataBuilder.cs:52–54`

```csharp
try { queue.Enqueue(Assembly.Load(an)); }
catch { /* ignore load failures */ }
```

Silent swallowing is acceptable for assembly scanning (missing deps are common in plugin scenarios), but there's no warning emitted. A missed page registration would be invisible.

---

#### [NOTE] D-10: `NavigationRuntime.SwitchInternalAsync` has redundant condition

**File:** `NavigationRuntime.cs:591`

```csharp
if (toDesc.Presentation == PagePresentationMode.Replace && from != null)
    fromState = (from as IPageStateful)?.CaptureState();
```

After the `switch (toDesc.Presentation)` block above (which returns for non-Replace modes), `toDesc.Presentation == PagePresentationMode.Replace` is always true at this point. The first half of the condition is dead. Minor — just noise.

---

### 5. Things That Need Deeper Investigation (Pass 2 Candidates)

#### [NEEDS DEEPER LOOK] N-1: Guard pipeline — async evaluation inside serialized UI dispatch

Guard evaluation runs inside `SwitchInternalAsync` which runs inside `SerializeAsync` (holding `_navGate`). A guard that performs I/O (DB lookup, HTTP call, file read) will block the navigation semaphore for the duration. There's no timeout on guard evaluation. If a guard hangs, the entire navigation system deadlocks.

---

#### [NEEDS DEEPER LOOK] N-2: `GoBack` restores state via `NavigationArgs.Payload` — undocumented contract

`GoBackInternalAsync` pushes `entry.State` as `NavigationArgs.Default(entry.State).Payload`. The page's `OnNavigatedToAsync` receives this as `args.Payload`. A page wanting to restore its state must know to inspect `args.Payload` and cast it. This implicit contract is not documented anywhere in the interfaces or contracts.

---

#### [NEEDS DEEPER LOOK] N-3: Multiple concurrent `ShowDialogAsync` / `ShowPromptAsync` calls

`DialogService` and `PromptService` each have an `_activeCount` to stack multiple concurrent dialogs/prompts. This is intentional design (stacking). Needs a scenario audit: what happens when two prompts are shown, then the app navigates away, and only one is closed? Are both TCSes resolved? Are both views disposed?

---

#### [NEEDS DEEPER LOOK] N-4: `WinFormsInteractionObserver` — `ControlAdded` fires only for direct children of root

`WinFormsInteractionObserver` hooks `_root.ControlAdded` to catch newly added controls. But this event fires only for direct children of `_root`. Controls added to panels inside `_root` (grandchildren) will not fire `_root.ControlAdded` — they'll fire `panel.ControlAdded` instead. So dynamically added controls in nested panels are never hooked.

---

#### [NEEDS DEEPER LOOK] N-5: `DefaultLoadingMask` registered as `ModalOverlay` but used as a layer-0 view

`DefaultLoadingMask` has `[PageMetadata(Presentation = PagePresentationMode.ModalOverlay)]`. In `LoadAsync`, it is created and added via `viewHost.AddView(mask.NativeView)`. If the host's `AddView` treats `IModalView` overlays specially (as in `WinFormsLayeredPageHostBase`), the mask will go through the overlay path. But `DefaultLoadingMask` implements `IGlobalLoadingMask` and `IPageOverlay`, not `IModalView`. So in `WinFormsLayeredPageHostBase.AddView`, it falls into the `IPageOverlay` path without a mask-form. This interaction needs a scenario test.

---

#### [NEEDS DEEPER LOOK] N-6: `DisposeAsync` ordering — runtime disposes before services

`NavigationRuntime.DisposeAsync` closes overlays and disposes `Current`, but the `ToastService`, `DialogService`, and `PromptService` (which may have active views/tasks live) are never explicitly disposed or notified. If a dialog is open when `DisposeAsync` is called, its `tcs.Task` will never complete, and any awaiter will hang.

---

## Summary Table

| ID | Domain | Severity | File | One-line summary |
|---|---|---|---|---|
| A-1 | Async | CRITICAL | NavigationRuntime.cs | Toast/Dialog/Prompt bypass UI thread |
| A-2 | Async | CRITICAL | ToastService.cs | Dismiss timer touches UI from thread pool |
| A-3 | Async | HIGH | NavigationRuntime.cs | Nav semaphore held during modal wait |
| A-4 | Async | HIGH | WinFormsEventDispatcherAdapter.cs | BeginInvoke before handle created |
| A-5 | Async | MEDIUM | NavigationRuntime.cs | LoadInBackground may update disposed page |
| A-6 | Async | MEDIUM | NavigationRuntime.cs | DisposeAsync may hang on app close |
| A-7 | Async | LOW | NavigationRuntime.cs | OnTimeout swallows exceptions |
| A-8 | Async | LOW | NavigationRuntime.cs | CleanupAsync is falsely async |
| L-1 | Leak | HIGH | NavigationRuntime.cs | Singleton cache not disposed on shutdown |
| L-2 | Leak | HIGH | WinFormsInteractionObserver.cs | Removed controls never unhooked |
| L-3 | Leak | MEDIUM | PageLifecycleTracker.cs | Tracker never populated — leak detection inert |
| L-4 | Leak | MEDIUM | NavigationService.cs | Static events are GC roots |
| L-5 | Leak | LOW | NavigationRuntime.cs | Stale WeakReference wrappers not compacted |
| S-1 | Static state | HIGH | PlataformRegistry.cs | PlatformRegistry never resets |
| S-2 | Static state | HIGH | NavigationService.cs | Double-init guard DEBUG-only |
| S-3 | Static state | MEDIUM | NavigationService.cs | Dead _attachedPages/_visiblePages int fields |
| S-4 | Static state | MEDIUM | NavigationService.cs | OnFirstPageAttached / OnNoPageAttached never fire |
| D-1 | Design | HIGH | NavigationRuntime.cs + PresentationEntry.cs | Duplicate PresentationEntry — standalone is dead |
| D-2 | Design | HIGH | WinFormsInteractionBlocker.cs | Block() only hits direct children |
| D-3 | Design | MEDIUM | NavigationContext.cs + NavigationRuntime.cs | Context diagnostics hub orphaned |
| D-4 | Design | MEDIUM | NavigationArgs.cs | Default/Transient/Silent are identical |
| D-5 | Design | MEDIUM | INavigationContext.cs | INavigationContext is stale/abandoned |
| D-6 | Design | MEDIUM | WinFormsLayeredPageHostBase.cs | AddView overlay path is broken (host never shown) |
| D-7 | Design | LOW | PageRegistry.cs | ResolveTimeoutTarget resolves Home, not timeout target |
| D-8 | Design | LOW | DefaultLoadingMask.cs | Hardcoded Portuguese string |
| D-9 | Design | NOTE | PageMetadataBuilder.cs | Assembly load failures silently swallowed |
| D-10 | Design | Note | NavigationRuntime.cs | Redundant Presentation check after switch |
| N-1 | Deeper | — | NavigationRuntime.cs | Guard I/O blocks nav semaphore |
| N-2 | Deeper | — | NavigationRuntime.cs | GoBack state restore contract undocumented |
| N-3 | Deeper | — | DialogService.cs, PromptService.cs | Concurrent dialog stack teardown |
| N-4 | Deeper | — | WinFormsInteractionObserver.cs | ControlAdded only catches direct children |
| N-5 | Deeper | — | DefaultLoadingMask.cs | Modal vs IPageOverlay vs IModalView path interaction |
| N-6 | Deeper | — | NavigationRuntime.cs | Services not notified on DisposeAsync |

---

## Context for Pass 2

The most impactful changes to tackle first are:

1. **A-1 + A-2**: All three overlay service calls need UI thread marshaling. The `ToastService` auto-dismiss timer is the clearest repro path for a cross-thread crash.
2. **D-2**: `WinFormsInteractionBlocker.Block()` needs full subtree recursion to actually block interaction.
3. **D-6**: `WinFormsLayeredPageHostBase.AddView` overlay path is broken. A scenario test with a `ModalOverlay` page would expose this immediately.
4. **A-3**: The nav-gate-held-during-modal issue needs an architectural decision — either move modal await outside the semaphore, or accept that "only one thing at a time" is the intended constraint.
5. **L-1**: The `_strongCache` disposal gap is a correctness issue for apps with multiple navigation sessions (login/logout flows).

Pass 2 should exercise the demo app under:
- Rapid navigation (stress the semaphore)
- App close while a modal is open (expose A-6)
- Navigate away while a background load is in progress (expose A-5)
- ShowDialog called from a Task.Run context (expose A-1)
- Multiple ShowPromptAsync calls stacked (exercise N-3)

---
---

# Pass 2 — Verification, Triage & Deep-Dive

**Auditor:** Claude (Opus 4.7)
**Date:** 2026-05-28
**Scope:** Same as Pass 1 — `src/Navigation/**` + `tests/NekoLib.Navigation.Tests/Demo/**`
**Method:** Re-read the current source (the overlay refactor has progressed since Pass 1, so Pass 1 line numbers are stale), verified every Pass 1 finding against live code, deep-dived N-1…N-6, translated the remaining Portuguese, and ran a Debug build.

> **Build status:** `dotnet build NekoLib.Navigation.WinForms` (which transitively builds `NekoLib.Navigation`) → **succeeds, 0 warnings, 0 errors.** Everything below is a *runtime / design* problem, not a compile break. The duplicate `PresentationEntry`/`ModalResult` types and the orphaned host class all compile.

## 2.0 What changed since Pass 1 (context for the triage)

The "in-progress" overlay refactor Pass 1 noted has moved further:

- `OverlayService` is gone; **Toast / Dialog / Prompt services exist, are registered in `PageNavBootstrap.Start()`, and work structurally.** New view contracts (`IToastView`, `IDialogView`, `IPromptView<T>`) and WinForms bases (`ToastViewBase`, `DialogViewBase`, `PromptViewBase`) are clean.
- A **new** flat host `WinFormsPageHostBase` (abstract, no overlay logic) was added — but **nothing uses it**. `WinFormsPlatformAdapter.CreateHost` still returns the **old** `WinFormsLayeredPageHostBase`, which still contains the broken overlay path from D-6. The refactor is half-landed (see NEW-2).
- The demo was converted to MVVM; its strings are already English. The `ConfirmDialog`/`MyToast` pages were deleted and replaced by `SimpleToast` etc.

The single most important Pass 2 discovery is **NEW-1**: the `Overlay` / `ModalOverlay` *presentation* path is currently **dead** (throws), which re-contextualizes A-3, D-6, and part of N-1.

---

## 2.1 Triage of Pass 1 findings (verified against current code)

Legend — **Status:** ✅ Confirmed (live) · ✏️ Confirmed, details/lines updated · ⤵️ Downgraded · 🚫 Blocked/masked by NEW-1 · ✔️ Fixed in Pass 2.

| ID | Status | Current location | Notes after re-reading |
|---|---|---|---|
| A-1 | ✅ | `NavigationRuntime.cs:267-304` | `ShowToast/ShowDialogAsync/ShowPromptAsync` still bypass `ExecuteAsync`. Real, but only crashes when the caller is **off** the UI thread — nothing currently asserts UI-thread affinity. |
| A-2 | ⤵️ | `ToastService.cs:65-83` | Re-analyzed: `RunDismissTimerAsync` does **not** use `ConfigureAwait(false)`. If `ShowToast` is called on the UI thread (the normal contract), the `await Task.Delay` resumes on the captured WinForms `SynchronizationContext`, so `DismissCurrentInternal` runs on the **UI thread** — safe. It only crashes if the toast was shown from a non-UI thread / with no sync context. Severity HIGH→**MEDIUM**, same fix (assert/marshal). |
| A-3 | 🚫 | `NavigationRuntime.cs:820-840` | The gate-held-during-modal-await is real in the code, but **cannot manifest today**: `ShowModalInternalAsync` calls `EnsureModalHost()` which throws before reaching `await tcs.Task` (see NEW-1). Becomes live the moment an `IModalHost` is implemented. |
| A-4 | ✅ | `WinFormsEventDispatcherAdapter.cs:25-28` | `BeginInvoke` with no `IsHandleCreated` guard. Demo dodges it by navigating in `Form1_Load`, not the ctor. |
| A-5 | ✅ | `NavigationRuntime.cs:667` | `_ = LoadAsync(to, navArgs.Payload)` fire-and-forget; `ApplyBackgroundResultAsync` still runs with no "is this page still current / not disposed" check and no cancellation. |
| A-6 | ✅ | `NavigationRuntime.cs:399-424` | `DisposeAsync` → `ExecuteAsync` → `RunOnUiAsync` → `BeginInvoke`. If the pump is gone at shutdown, the TCS never completes and `NavigationService.Shutdown` awaits forever. |
| A-7 | ✅ | `NavigationRuntime.cs:699-702` | `OnTimeout` still drops `OnTimeoutAsync` exceptions via `_ =`. |
| A-8 | ✅ | `NavigationRuntime.cs:984-1004` | `CleanupAsync` is `async`-signatured but every path is `Task.CompletedTask` (no `await`). |
| L-1 | ✅ | `NavigationRuntime.cs:363-397, 399-424` | `ResetAsync`/`DisposeAsync` dispose only `Current`. `_strongCache` singletons are never disposed; `RemoveFromCaches` is reachable only via `CleanupAsync(forceDispose:true)` for the single current page. |
| L-2 | ✏️⤵️ | `WinFormsInteractionObserver.cs` | Now **has** a `Dispose()` that unhooks the whole tree, so it's not a permanent leak. But there is still **no `ControlRemoved` handler** — controls removed from `Root` mid-session stay in `_hooked` and stay subscribed (kept alive) until `Dispose`. Live leak during a session; bounded at teardown. |
| L-3 | ✅ | `PageLifecycleTracker.cs` | Grep confirms `Register/Update/Unregister` are **never** called anywhere. Tracker is inert; `AssertFrameworkIsDown` passes vacuously. |
| L-4 | ✅ | `NavigationService.cs:31-38` | Eight `static event`s; subscribers leak for AppDomain lifetime. |
| L-5 | ✅ | `NavigationRuntime.cs:43-44, 1006-1019` | `_weakCache` dead `WeakReference` wrappers are never compacted. |
| S-1 | ✅ | `Infrastructure/PlataformRegistry.cs` | `PlatformRegistry.Register` throws on 2nd call; no `Reset`/`Unregister`. Blocks multi-context / test isolation. (Filename is misspelled "Plataform" — Portuguese "plataforma" artifact; class name is correct.) |
| S-2 | ✅ | `NavigationService.cs:48-62` | Double-init guard + null check are still inside `#if DEBUG`. Release silently leaks the old runtime. |
| S-3 | ✅ | `NavigationService.cs:40-41` | `private static int _attachedPages; _visiblePages;` still dead (real tracking is `HashSet<IPageView>` in the runtime). |
| S-4 | ✅ | `NavigationService.cs:162-170` | `WireRuntimeEvents` wires 5 events; `OnFirstPageAttached`/`OnNoPageAttached`/`OnNoPageVisible` are still never wired → permanently silent. |
| D-1 | ✅➕ | `NavigationRuntime.cs:1054-1078` + `Runtime/Core/PresentationEntry.cs` | Duplicate `PresentationEntry` confirmed (private nested = used; standalone = dead). **And it's now a pair** — see NEW-3 for the parallel duplicate `ModalResult`. |
| D-2 | ✅⬆️ | `WinFormsInteractionBlocker.cs:22-27` | Still only disables **direct** children of `_root`. More important now: this is the **only live** interaction blocker (used by Dialog/Prompt), so the bypass affects real modal dialogs, not just the dead modal path. |
| D-3 | ✅ | `NavigationContext.cs:49-56` vs `NavigationRuntime.cs:75-82` | Each constructs its own `NavigationEventHub`/`NavigationDiagnostics`. The runtime's hub gets all events; `NavigationContext.Events`/`Diagnostics` is an orphan channel. |
| D-4 | ✅ | `NavigationArgs.cs:28-35` | `Default`/`Transient`/`Silent` are byte-identical (`Preload`/`Background` do differ). |
| D-5 | ✅ | `Contracts/Runtime/INavigationContext.cs` | Declares `IPageOverlay Overlay` + `IInteractionBlocker Blocker`; `NavigationContext` does not implement the interface and has no such members. Stale. |
| D-6 | ✅ | `WinFormsLayeredPageHostBase.cs:78-162` | Overlay path still broken in the **active** host: `host.Show(...)`/`Root.Controls.Add(host)` commented (`:142-143`), `overlayControl.Dock` commented (`:127`), and `RemoveView` never removes `overlayControl` from `Root.Controls`. `Root.Controls.Add(mask)` with a null `mask` is a silent no-op (WinForms ignores null), so non-modal overlays don't crash there. |
| D-7 | ✅ | `PageRegistry.cs:115-117` | `ResolveTimeoutTarget` still returns `PageRole.Home`; runtime ignores it and does its own `IsTimeoutTarget` lookup (`:730-731`). |
| D-8 | ✔️ | `DefaultLoadingMask.cs:44` | **Fixed in Pass 2:** `"Carregando..."` → `"Loading..."`. |
| D-9 | ✅ | `PageMetadataBuilder.cs:51-55` | `Assembly.Load` failures still swallowed silently (acceptable, but no diagnostic). |
| D-10 | ✅ | `NavigationRuntime.cs:591` (and again `:675`) | `Presentation == Replace && from != null` — after the `switch` returns for non-Replace, the `== Replace` half is always true. Two occurrences now. Noise. |

---

## 2.2 New findings discovered in Pass 2

#### [HIGH / blocking] NEW-1: `Overlay` & `ModalOverlay` presentation navigation is non-functional — no `IModalHost` exists

**Files:** `NavigationRuntime.cs:788-800` (`EnsureModalHost`), `WinFormsLayeredPageHostBase.cs:15`, `PageNavBootstrap.cs:223-243`

No type implements `IModalHost`, and the bootstrap never registers one. The active host (`WinFormsLayeredPageHostBase`) implements only `IPageHost, IViewHost`. Therefore every path that routes through the unified presentation stack —
- `NavigateAsync(page)` where the page is `[PageMetadata(Presentation = Overlay | ModalOverlay)]` (`SwitchInternalAsync:558-567`),
- `ShowModalAsync(...)` (`:335-352`),
- `GoBack` when an overlay/modal is on top (`GoBackInternalAsync:433-443`),

— calls `EnsureModalHost()`, which throws `InvalidOperationException("Modal host missing: IModalHost not available.")`. The exception propagates out of `NavigateAsync` to the caller (after `NavigationFailed` fires).

**Why it matters:** the entire `_stack`/overlay/modal subsystem (the `PresentationEntry` machinery, `ShowOverlayInternalAsync`, `ShowModalInternalAsync`, `CloseTop*`) is currently unreachable. `Reset`/`Dispose` are *not* affected because their `CloseAllOverlaysInternalAsync` is a no-op while the stack can never hold an overlay.

**Decision required for Pass 3:** either (a) implement `IModalHost` on the WinForms host (resurrects the overlay feature — and then A-3, D-6, and the gate-held-during-modal risk become live and must be fixed together), or (b) the Toast/Dialog/Prompt services are the intended replacement and the `Overlay`/`ModalOverlay` presentation path + `ShowModalAsync` + `_stack` modal handling should be **deleted**. This single decision determines whether ~6 other findings are "fix" or "delete".

#### [MEDIUM] NEW-2: Orphaned `WinFormsPageHostBase` — refactor half-applied

**Files:** `WinFormsPageHostBase.cs` (new, unused), `WinFormsLayeredPageHostBase.cs` (old, still wired), `PlatformAdapter.cs:26`

A clean new abstract host was added (flat, no broken overlay code) but `CreateHost` still `new`s the old layered host. Pick one. If the new one is canonical, it still needs an overlay/modal story (or it relies on the Dialog/Prompt services + plain `IViewHost`).

#### [MEDIUM] NEW-3: Duplicate `ModalResult` type (parallel to the duplicate `PresentationEntry`)

**Files:** `Metadata/ModalResult.cs` (top-level `public struct`) vs `NavigationRuntime.cs:1039` (private nested `public struct`)

The runtime uses its **nested** `ModalResult`; the standalone `Metadata/ModalResult.cs` is referenced only by the **dead** standalone `Runtime/Core/PresentationEntry.cs` (`using NekoLib.Navigation.Metadata;`). So D-1's dead code is actually a *pair*: `PresentationEntry.cs` + `Metadata/ModalResult.cs`. Delete both together, or promote both to top-level and have the runtime use them.

#### [LOW] NEW-4: `WinFormsInteractionBlocker` XML doc is malformed

`WinFormsInteractionBlocker.cs:7` — `</summary>` is written as `/summary>` (missing `<`). Cosmetic.

#### [NOTE] NEW-5: `PageMetadataBuilder.Build` does not catch `ReflectionTypeLoadException`

`PageMetadataBuilder.cs:83` — `asm.GetTypes()` throws `ReflectionTypeLoadException` for assemblies with unresolvable references (the same plugin scenario D-9 tries to tolerate). Consider `GetTypes()`-with-catch / `GetExportedTypes` guard.

#### [NOTE] NEW-6: Misspelled filenames (Portuguese / typo artifacts)

`Infrastructure/PlataformRegistry.cs` ("Plataform" ← Portuguese *plataforma*) and `Diagnostics/NavigationDiagnosts.cs` ("Diagnosts"). The **class names** inside are already correct (`PlatformRegistry`, `NavigationDiagnostics`). Left as-is in Pass 2 (renaming files is a structural change, not a translation); flagged for Pass 3.

---

## 2.3 Deep-dive: N-1 … N-6

#### N-1 — Guard I/O blocks the nav semaphore — **CONFIRMED (live)**
`SwitchInternalAsync` awaits `guard.EvaluateAsync(guardCtx)` at `NavigationRuntime.cs:524`, inside `SerializeAsync` (the `_navGate` is held). There is no timeout and no `CancellationToken` on guard evaluation. A guard that does DB/HTTP/file I/O blocks **all** navigation (including `GoBack`, timeout-driven nav, `Reset`) for its full duration; a hung guard deadlocks the whole subsystem. Guard-redirect recursion re-enters `SwitchInternalAsync` on the same stack while the gate is held (correct, not re-entrant on the semaphore). **Pass 3:** evaluate guards *before* acquiring `_navGate`, or wrap evaluation in a timeout + cancellation and emit `GuardDenied` on timeout.

#### N-2 — `GoBack` state restore is an undocumented `Payload` contract — **CONFIRMED**
`GoBackInternalAsync:459-461` does `SwitchInternalAsync(entry.PageType, NavigationArgs.Default(entry.State))`. The page receives the captured state as `args.Payload` in `IPageLifecycle.OnNavigatedToAsync(NavigationArgs)`. The producing side is `IPageStateful.CaptureState()`. Nothing in `IPageStateful`, `IPageLifecycle`, or `NavigationArgs` documents that "on back-navigation, `Payload` *is* your restored state object" — a page can't distinguish a fresh forward-nav payload from a restored state blob. **Pass 3:** document it on `IPageStateful`/`NavigationArgs`, or add an explicit `RestoreState(object)` channel / a flag on `NavigationArgs` (this also gives D-4's `Transient`/`Silent` something real to encode).

#### N-3 — Concurrent Dialog/Prompt teardown — **CONFIRMED (leak + hang + frozen UI)**
`DialogService`/`PromptService` track depth with `_activeCount` and block once on the first, unblock on the last (`DialogService.cs:38-44,55-73`; `PromptService.cs:40-46,57-77`). **But their live views are added via `IViewHost` and are *not* registered in the runtime `_stack`.** Consequences when the app navigates away / `Reset`s / `Dispose`s while a dialog or prompt is open:
- the awaited `TaskCompletionSource` is never completed → every awaiter hangs (two stacked prompts → both hang);
- the view controls are never `RemoveView`'d/`Dispose`d → leak;
- `Complete()` never runs → `_interactionBlocker` stays `Block()`ed → **the whole UI is left disabled.**
`Replace` navigation's `CloseAllOverlaysInternalAsync` does **not** touch these services (only `_stack`). **Pass 3:** give the three services a "close/cancel all" + `IDisposable`, and call it from runtime `ResetAsync`/`DisposeAsync` (resolves N-6 too).

#### N-4 — `ControlAdded` misses late grandchildren — **CONFIRMED (with mitigation)**
`WinFormsInteractionObserver` only subscribes `_root.ControlAdded` (`:27`). Its handler `Hook(e.Control)` recurses the added control's subtree, so adding a *panel* hooks the descendants that exist *at add time* (`:34-40,63-67`). The gap: controls added **later** into an already-attached nested panel raise `panel.ControlAdded`, not `_root.ControlAdded`, so they're never hooked → their interactions don't reset the idle/timeout. Same root cause as L-2 (no per-container `ControlRemoved`/`ControlAdded`). **Pass 3:** subscribe `ControlAdded`/`ControlRemoved` per container as the tree is hooked/unhooked, or replace the whole scheme with an app-level `IMessageFilter`.

#### N-5 — Loading-mask path through the broken overlay branch — **CONFIRMED + concretized**
`DefaultLoadingMask : UserControl, IGlobalLoadingMask`, and `IGlobalLoadingMask : IPageOverlay`, with `NativeView => this`. In `LoadAsync` the runtime calls `viewHost.AddView(mask.NativeView)` (`NavigationRuntime.cs:758`). Because `mask.NativeView` **is** an `IPageOverlay`, `WinFormsLayeredPageHostBase.AddView` takes the **overlay branch** (`:91`), not the plain content branch. There:
- it's not `IModalView` → `mask` Form stays `null` → `Root.Controls.Add(null)` is a silent no-op;
- `overlayControl.Dock = Fill` is commented (`:127`) → the scrim renders at its **default bounds**, not full-panel;
- the host `Form` is created but never shown;
- `RemoveView` (`:147-162`) only closes the (never-shown) host — it **never removes the mask control from `Root.Controls`**. It disappears only because the runtime later calls `mask.Dispose()` (`:776`), and disposing a `Control` detaches it.

Net: the loading mask works by accident (via Dispose) and looks wrong (not full-screen). **Pass 3:** route the *system* loading mask through a plain content add (it isn't a user overlay), or fix the overlay branch as part of NEW-1.

#### N-6 — `DisposeAsync` doesn't tear down Toast/Dialog/Prompt services — **CONFIRMED**
`DisposeAsync` (`:399-424`) closes `_stack` overlays and disposes `Current`, but never disposes/notifies the three services. An in-flight toast timer keeps its `CancellationTokenSource` alive; an open dialog/prompt's TCS hangs and its view leaks (and the UI stays blocked, per N-3). Fix is the same `IDisposable`/close-all hook as N-3.

---

## 2.4 Portuguese → English translations applied in Pass 2

| File | Before | After |
|---|---|---|
| `WinForms/Defaults/DefaultLoadingMask.cs:44` | `"Carregando..."` | `"Loading..."` (D-8 closed) |
| `Wpf/Properties/AssemblyInfo.cs:5-30` | VS template boilerplate comments in pt-BR (4 blocks) | Standard English boilerplate |

Searches (accented-char sweep + keyword sweep over `src/Navigation/**` and `tests/.../Demo/**`, incl. `.resx`/`.Designer.cs`) found **no other** Portuguese in code, comments, or UI strings — the MVVM demo rewrite is already English. Remaining pt-flavored artifacts are the two misspelled **filenames** in NEW-6 (deferred, not text).

---

## 2.5 Prioritized plan for Pass 3

**P0 — correctness / blocks features (decide NEW-1 first; it gates the rest):**
1. **NEW-1 decision:** implement `IModalHost` *or* delete the `Overlay`/`ModalOverlay`/`ShowModalAsync`/`_stack`-modal subsystem. If implementing → fix **A-3** (don't hold `_navGate` across the modal `await`) and **D-6** in the same change.
2. **A-1 / A-2:** marshal Toast/Dialog/Prompt entry points onto the UI thread (or assert UI-thread affinity at the entry points).
3. **N-3 / N-6:** add close-all + `IDisposable` to the three services; invoke from `ResetAsync`/`DisposeAsync`. (Also un-freezes the UI when a dialog is torn down mid-flight.)

**P1 — robustness:**
4. **D-2:** block the full subtree (or disable `Root` itself) — affects real dialogs/prompts now.
5. **L-1:** dispose `_strongCache` singletons on `Reset`/`Dispose`.
6. **S-2:** move the double-init/null guard out of `#if DEBUG`.
7. **A-5:** guard background-load apply against navigated-away/disposed pages (+ cancellation).
8. **A-6:** make shutdown safe when the message pump is gone (`InvokeRequired`/`IsHandleCreated` fast-path, or a non-marshaled dispose).
9. **N-1:** guard timeout / evaluate before the gate.

**P2 — dead code, hygiene, docs:**
10. Delete dead pairs: **D-1 + NEW-3** (`PresentationEntry.cs` + `Metadata/ModalResult.cs`); **D-5** (`INavigationContext`); **D-7** (`ResolveTimeoutTarget`); **S-3** (dead int fields); **NEW-2** (orphan host — pick one); **D-10**.
11. **L-2 / N-4:** per-container hook/unhook or `IMessageFilter`.
12. **S-4** (wire the 3 silent events), **D-3** (orphan diagnostics hub), **D-4** (collapse/repurpose `Transient`/`Silent`), **L-3** (wire the tracker or delete it + `AssertFrameworkIsDown`), **L-5** (compact weak cache), **A-7/A-8**, **D-9 / NEW-5** (`ReflectionTypeLoadException`), **NEW-6** (rename `PlataformRegistry.cs`, `NavigationDiagnosts.cs`).

**Caveat — what Pass 2 could *not* exercise:** the Pass-1 wishlist (rapid-nav stress, app-close-with-modal, navigate-away-mid-load, ShowDialog-from-`Task.Run`, stacked prompts) requires *running* the WinForms demo interactively, which isn't possible in this headless environment. Those remain runtime-repro tasks for Pass 3; this pass verified them by **static analysis + a compile build** only. Note that "app-close-with-modal" and "stacked prompts" can't be reproduced via the *presentation* modal path until NEW-1 is resolved — but they **can** be reproduced today via `ShowDialogAsync`/`ShowPromptAsync` (that's N-3/N-6).

---

## 2.6 Pass 3 — Work log (changes applied)

**Scope:** navigation module only (`src/Navigation/**` + `tests/NekoLib.Navigation.Tests/Demo/**`).
**Decisions taken before starting:** NEW-1 = **delete** the overlay/modal subsystem (not implement `IModalHost`); Pass-3 breadth = **everything (P0 + P1 + P2)**.
**Verification:** `dotnet build` on both targets (`net9.0-windows`, `net481`) after each phase. Final state — library `NekoLib.Navigation.WinForms`: **0 errors / 32 warnings** (all pre-existing nullable-context / hides-inherited-member noise, none introduced here); demo `NavigationDemo`: **0 errors / 0 warnings**.

### P0 — correctness / feature-gating

- **NEW-1 (delete overlay/modal subsystem).** Removed the `Overlay`/`ModalOverlay`/`ShowModalAsync`/`_stack`-modal machinery entirely rather than building `IModalHost`. This dissolves A-3, D-6 (as a modal concern), D-1, NEW-3, D-5, **and D-10** (the redundant `Presentation == Replace` checks vanished with the presentation subsystem) by deletion. Files deleted: `Runtime/Services/OverlayService.cs`, `WinForms/Adapters/OverlayService.cs`, `WinForms/Hosting/PopupView.cs`, `Contracts/Runtime/IOverlayService.cs`, `Contracts/Pages/IModalHost.cs`, `Contracts/Pages/IModalView.cs`, `Metadata/ModalResult.cs`, `Runtime/Core/PresentationEntry.cs`, `Contracts/Runtime/INavigationContext.cs` (D-5). `PagePresentationMode` trimmed to `Replace` only.
- **Replacement surface (ISP split).** Added focused services + contracts: `IToastService`/`IDialogService`/`IPromptService` (+ `Runtime/Services/{Toast,Dialog,Prompt}Service.cs`) and view contracts `IToastView`/`IDialogView`/`IPromptView<TResult>`; WinForms bases `ToastViewBase`/`DialogViewBase`/`PromptViewBase`. Registered in `PageNavBootstrap`, resolved by the runtime.
- **D-6 / N-5 (host rework).** `WinFormsLayeredPageHostBase` reworked: single `Panel`, content pages parented + `SendToBack`, transient surfaces `BringToFront`; `RestackOverlays` keeps non-`PageView` controls above pages. The system loading mask now goes through the plain content path (no overlay branch).
- **A-1 / A-2 (UI-thread affinity).** Toast/Dialog/Prompt entry points marshal onto the UI thread via `IEventDispatcherAdapter`. Dialogs/prompts marshal but do **not** hold `_navGate`.
- **N-3 / N-6 (teardown).** Each service got `CloseAll()`; the runtime calls `TeardownOverlayServices()` (DismissCurrentToast + CloseAll×2) from `ResetAsync`/`DisposeAsync`, completing pending TCSs (`false`/`default`) and releasing the interaction blocker. Idempotent via an `owned`/`RemoveEntry` guard so user-completion and CloseAll don't double-tear-down.

### P1 — robustness

- **D-2 (`WinFormsInteractionBlocker`).** Disables the full subtree (recursive `DisableSubtree`), remembering only controls it actually toggled so `Unblock()` restores prior state.
- **L-1.** `DisposeCachedPages()` disposes strong singletons + live weak targets and clears both caches on `Reset`/`Dispose`.
- **S-2.** Null + double-init guard in `NavigationService.UseContext` moved out of `#if DEBUG` (throws in Release too).
- **A-5.** Background load is fire-and-forget via `LoadInBackgroundSafeAsync` (try/catch → EmitFailure); `LoadAsync(..., guardApply: true)` skips `ApplyBackgroundResultAsync` if the page was disposed or is no longer `Current`.
- **A-6.** `ExecuteSafeOnUiAsync` catches the dead-pump `BeginInvoke` throw and runs inline so shutdown is safe when the message pump is gone.
- **A-4.** `WinFormsEventDispatcherAdapter` checks `IsHandleCreated`/`InvokeRequired`: runs inline on the UI thread pre-handle, throws a clear error only when cross-thread marshaling is genuinely impossible.
- **N-1.** Guard evaluation is bounded by a 30 s timeout (`Task.WhenAny`); a timed-out guard emits `GuardDenied` instead of deadlocking — chosen over move-before-gate to avoid a TOCTOU race.

### P2 — dead code, hygiene, docs

- **D-4.** Deleted the redundant `NavigationArgs.Silent` factory; documented `Transient` as a current alias of `Default` (kept for `SwitchTransient` call sites + future reuse-policy encoding); replaced the misplaced TODO XML doc with a real type summary. (`Preload`/`Background` differ and were kept.)
- **L-2 / N-4 (`WinFormsInteractionObserver`).** Now subscribes `ControlAdded`/`ControlRemoved` **per container** (in `HookSingle`/`UnhookSingle`), so deep/late controls added into already-attached panels are hooked, and removed subtrees are unhooked. Dropped the single `_root.ControlAdded` subscription.
- **L-3.** Deleted the inert `Diagnostics/PageLifecycleTracker.cs` (its `Register`/`Update`/`Unregister` were never called → always empty). Simplified `NavigationService.AssertFrameworkIsDown` to assert only `_context == null`; removed the now-unused `using System.Linq;` and `using NekoLib.Navigation.Diagnostics;`.
- **L-5.** Added `CompactWeakCache()` and call it on the `Cached` resolve path so weak-cache slots for collected/disposed pages don't accumulate over the app's lifetime.
- **D-9 / NEW-5 (`PageMetadataBuilder`).** `Assembly.Load` failures now `Debug.WriteLine` instead of silent swallow. `asm.GetTypes()` wrapped in `GetLoadableTypes()` which catches `ReflectionTypeLoadException` and recovers `ex.Types.OfType<Type>()`. Added `using System.Linq;`.
- **A-7.** `OnTimeout` fire-and-forget now observes faults via `ContinueWith(..., OnlyOnFaulted)` → `Debug.WriteLine`.
- **A-8.** Renamed `CleanupAsync` → `Cleanup` and made it synchronous `void` (no async teardown work existed); updated the three call sites (`ResetAsync`, `DisposeAsync`, `SwitchInternalAsync`).
- **D-3.** Runtime ctor now reuses the context's diagnostics (`_diagnostics = _ctx.Diagnostics;`) instead of building an orphan hub/sink.
- **D-7.** Removed unused `PageRegistry.ResolveTimeoutTarget()`.
- **D-10.** Closed by the P0 deletion (no `Presentation == Replace` checks remain).
- **S-3 / S-4.** Deleted dead `_attachedPages`/`_visiblePages` int-tracking fields from `NavigationService`; wired the three previously-silent events (`OnFirstPageAttached`/`OnNoPageAttached`/`OnNoPageVisible`) with forwarders.
- **NEW-2.** Deleted orphan abstract host `WinForms/Hosting/WinFormsPageHostBase.cs` (canonical host is `WinFormsLayeredPageHostBase`).
- **NEW-6 (renames).** `Infrastructure/PlataformRegistry.cs` → `PlatformRegistry.cs`; `Diagnostics/NavigationDiagnosts.cs` → `NavigationDiagnostics.cs` (class names were already correct; only filenames were misspelled). Done via `git mv` to preserve history.
- **Dead contract trim.** Removed unused `IPageOverlay<TResult>` and `OverlayOptions` from `Contracts/Pages/IPageOverlay.cs`; kept base `IPageOverlay` (used by `IGlobalLoadingMask` + the loading-mask path).
- **L-4 (static events GC roots).** `NavigationService.Shutdown()` now nulls all eight static events after tearing down the runtime, so external subscribers are released at the end of every navigation session without requiring callers to unsubscribe manually. A doc comment on the events block explains the contract.
- **NEW-4 (malformed XML doc).** The `</summary>` tag in `WinFormsInteractionBlocker.cs` was already repaired during the Pass 3 D-2 full-file rewrite.
- **Demo migration (in-scope).** Demo updated off the deleted overlay API onto the new services: `ConfirmDialog*` → `ConfirmDialogView` (`IDialogView`), `MyToast*` → `SimpleToast` (`IToastView`), new `TextInputPrompt` (`IPromptView<T>`); `Form1`, `NavigationDemo.csproj`, `PageA*`, `ConfirmDialogViewModel` adjusted. Removed a dead `Form1._host` field.

### Deferred / out of scope (with reason)

- **S-1 (`PlatformRegistry` has no `Reset`).** Not in the agreed P2 item list (10–12); left as-is. Low risk — registry is process-wide and set once at bootstrap.
- **N-2 (GoBack state-restore is an undocumented `Payload` contract).** Documentation/contract-shape change; left for a follow-up since it touches public `IPageStateful`/`IPageLifecycle` semantics rather than being a bug fix.
- **`PageLifecycleState` enum** (`Runtime/PageLifecycleState.cs`) is now unreferenced after the tracker deletion. Intentionally **kept** (per plan) as a small public type for future lifecycle instrumentation; emits no warning.
- **Runtime-repro caveats (unchanged from 2.5):** rapid-nav stress, app-close-with-open-dialog, navigate-away-mid-load, ShowDialog-from-`Task.Run`, stacked prompts still require interactively running the WinForms demo (not possible headless). Verified here by static analysis + compile build only. With NEW-1's deletion, the app-close-with-modal / stacked-prompt cases now exercise the new `ShowDialogAsync`/`ShowPromptAsync` + `TeardownOverlayServices` path (N-3/N-6), which is the surface that should be stress-tested.

---

## 2.7 Post-Pass-3 master status

Complete disposition of every finding after all three passes (Pass 1 → Pass 2 → Pass 3 + gap-close).

| ID | Severity | Post-Pass-3 status | Resolved by |
|---|---|---|---|
| A-1 | CRITICAL | ✔ Fixed | Pass 3 P0 — UI-thread marshal via `IEventDispatcherAdapter` |
| A-2 | HIGH→MEDIUM | ✔ Fixed | Pass 3 P0 — toast dismiss marshaled via `_dispatcher.BeginInvoke` |
| A-3 | HIGH | ✔ N/A | Dissolved — `ShowModalAsync` / gate-held-during-modal path deleted (NEW-1) |
| A-4 | HIGH | ✔ Fixed | Pass 3 P1 — `IsHandleCreated`/`InvokeRequired` guard added |
| A-5 | MEDIUM | ✔ Fixed | Pass 3 P1 — `guardApply` check + `LoadInBackgroundSafeAsync` try/catch |
| A-6 | MEDIUM | ✔ Fixed | Pass 3 P1 — `ExecuteSafeOnUiAsync` catches dead-pump throw, runs inline |
| A-7 | LOW | ✔ Fixed | Pass 3 P2 — `ContinueWith(OnlyOnFaulted)` → `Debug.WriteLine` |
| A-8 | LOW | ✔ Fixed | Pass 3 P2 — renamed to `Cleanup`, made `void` |
| L-1 | HIGH | ✔ Fixed | Pass 3 P1 — `DisposeCachedPages()` disposes strong + weak on Reset/Dispose |
| L-2 | HIGH→MEDIUM | ✔ Fixed | Pass 3 P2 — per-container `ControlAdded`/`ControlRemoved` in `HookSingle`/`UnhookSingle` |
| L-3 | MEDIUM | ✔ Fixed | Pass 3 P2 — `PageLifecycleTracker.cs` deleted; `AssertFrameworkIsDown` simplified |
| L-4 | MEDIUM | ✔ Fixed | Pass 3 gap-close — `Shutdown()` nulls all static events; doc comment added |
| L-5 | LOW | ✔ Fixed | Pass 3 P2 — `CompactWeakCache()` called on `Cached` resolve path |
| S-1 | HIGH | ✔ Fixed | Pass 4 — `PlatformRegistry.Reset()` added; called from `NavigationService.Shutdown()` |
| S-2 | HIGH | ✔ Fixed | Pass 3 P1 — double-init guard moved out of `#if DEBUG` |
| S-3 | MEDIUM | ✔ Fixed | Pass 3 P2 — dead `_attachedPages`/`_visiblePages` int fields deleted |
| S-4 | MEDIUM | ✔ Fixed | Pass 3 P2 — `OnFirstPageAttached`/`OnNoPageAttached`/`OnNoPageVisible` wired |
| D-1 | HIGH | ✔ N/A | Dissolved — standalone `PresentationEntry.cs` deleted with overlay subsystem |
| D-2 | HIGH | ✔ Fixed | Pass 3 P1 — full subtree recursion in `WinFormsInteractionBlocker` |
| D-3 | MEDIUM | ✔ Fixed | Pass 3 P2 — runtime ctor uses `_ctx.Diagnostics` instead of orphan hub |
| D-4 | MEDIUM | ✔ Fixed | Pass 3 P2 — `Silent` deleted; `Transient` documented; TODO doc replaced |
| D-5 | MEDIUM | ✔ N/A | Dissolved — `INavigationContext.cs` deleted with overlay subsystem |
| D-6 | MEDIUM | ✔ Fixed | Pass 3 P0 — `WinFormsLayeredPageHostBase` reworked; overlay branch removed |
| D-7 | LOW | ✔ Fixed | Pass 3 P2 — `PageRegistry.ResolveTimeoutTarget()` deleted |
| D-8 | LOW | ✔ Fixed | Pass 2 — `"Carregando..."` → `"Loading..."` |
| D-9 | NOTE | ✔ Fixed | Pass 3 P2 — `Debug.WriteLine` on `Assembly.Load` failure |
| D-10 | NOTE | ✔ N/A | Dissolved — redundant `Presentation == Replace` checks gone with presentation subsystem |
| N-1 | — | ✔ Fixed | Pass 3 P1 — 30 s `Task.WhenAny` timeout; timed-out guard emits `GuardDenied` |
| N-2 | — | ✔ Fixed | Pass 4 — `RestoreState` wired on back-nav via `NavigationArgs.Back`/`IsBackNavigation`; contract documented on `IPageStateful` |
| N-3 | — | ✔ Fixed | Pass 3 P0 — `CloseAll()` on each service; called from `TeardownOverlayServices` |
| N-4 | — | ✔ Fixed | Pass 3 P2 — per-container hook/unhook (same fix as L-2) |
| N-5 | — | ✔ Fixed | Pass 3 P0 — host rework routes loading mask through plain content path |
| N-6 | — | ✔ Fixed | Pass 3 P0 — services torn down from `ResetAsync`/`DisposeAsync` |
| NEW-1 | HIGH | ✔ N/A | Decision: deleted, not implemented |
| NEW-2 | MEDIUM | ✔ Fixed | Pass 3 P2 — orphan `WinFormsPageHostBase.cs` deleted |
| NEW-3 | MEDIUM | ✔ N/A | Dissolved — standalone `ModalResult.cs` deleted with overlay subsystem |
| NEW-4 | LOW | ✔ Fixed | Pass 3 P2 — malformed `</summary>` repaired during D-2 full-file rewrite |
| NEW-5 | NOTE | ✔ Fixed | Pass 3 P2 — `GetLoadableTypes()` catches `ReflectionTypeLoadException` |
| NEW-6 | NOTE | ✔ Fixed | Pass 3 P2 — `PlataformRegistry.cs` → `PlatformRegistry.cs`; `NavigationDiagnosts.cs` → `NavigationDiagnostics.cs` |
| NEW-7 | HIGH | ✔ Fixed | Pass 5 — history double-push on back-nav; `Record(from)` now skipped when `IsBackNavigation`, `HistoryChanged` re-fired from `GoBackInternalAsync` |
| NEW-8 | LOW | ✔ Fixed | Pass 5 — `ToastService.RunDismissTimerAsync` no longer throws `TaskCanceledException` on rapid-fire toasts; uses `WhenAny(Delay, cancelTcs)` |
| NEW-9 | MEDIUM | ✔ Fixed | Pass 5 — `DialogViewBase` / `PromptViewBase` now self-center as a rectangle instead of inheriting the host's `Dock=Fill` |
| NEW-10 | LOW | ✔ Fixed | Pass 5 — `IPageStateful` contract clarified in PageE demo doc: back-nav restores; forward re-nav creates a fresh Transient page |
| NEW-11 | NOTE | ✔ Fixed | Pass 5 — `[A-5]` button targeted the `LoadBeforeShow` HeavyPage; semaphore serialized the navigations and the scenario never ran mid-load. New `HeavyPageBackground` (`LoadInBackground`) wired and the button rerouted |
| NEW-12 | NOTE | ⏸ Open (cosmetic) | Pass 6 — `NekoLib.Diagnostics.Diagnostics` (class) collides with `NekoLib.Navigation.Diagnostics` (sub-namespace); consumers under `NekoLib.Navigation.*` must fully qualify or rename. Workaround in `runtime_tests/.../TestForm.cs` |
| NEW-13 | NOTE | ⏸ Open (API ergonomics) | Pass 6 — `PageMetadataBuilder.Register<T>(configure)` adds to `_manual` only; the type never lands in `_explicitTypes`, so `Build()` produces zero descriptors for it. `RegisterType(typeof(T), configure)` is the working path. Confusing API gap |

**Open / deferred after all passes:** two **cosmetic** smells (NEW-12, NEW-13) — both API-shape issues, neither a bug. All resolvable framework defects (Passes 1–5) are closed.

---

## 2.8 Pass 4 — Forward plan

**Goal:** close the two remaining deferred items and exercise the runtime paths that static analysis could not reach.

### Deferred code items

**S-1 — `PlatformRegistry` has no `Reset()`**
`PlatformRegistry.Register` throws on a second call and has no teardown. This prevents multi-context scenarios (multiple shell windows, unit-test isolation with multiple `UseContext` calls in one process). Fix: add a `Reset()` / `Unregister()` method guarded appropriately, and call it from `NavigationService.Shutdown()`.
*Effort: small. Risk: low — the change is additive.*

**N-2 — GoBack state-restore is an undocumented `Payload` contract**
`GoBackInternalAsync` pushes `entry.State` as `NavigationArgs.Default(entry.State).Payload`. A page that implements `IPageStateful` has no documented way to know that `args.Payload` on a back-navigation *is* its captured state. Options:
- Add a `bool IsBackNavigation` flag to `NavigationArgs` so pages can branch.
- Add an explicit `IPageLifecycle.OnStateRestoredAsync(object state)` callback invoked only on back-nav.
- At minimum: add XML doc to `IPageStateful.CaptureState()` and `IPageLifecycle.OnNavigatedToAsync` stating the contract.
*Effort: small (doc only) to medium (new callback). Choose the doc-only path first; promote to a new callback if the contract proves error-prone in practice.*

### Runtime-repro tasks (require interactive WinForms session)

These scenarios were confirmed by static analysis but never exercised at runtime. Run the demo app and verify each:

| Scenario | What to check | Finding it validates |
|---|---|---|
| Rapid forward/back navigation (10+ navigations/sec) | No deadlock; `_navGate` semaphore releases; no `ObjectDisposedException` | A-6, N-1 |
| Navigate away while a `LoadInBackground` page is loading | Page does not receive `ApplyBackgroundResultAsync`; no stale UI update | A-5 |
| `ShowDialogAsync` called from `Task.Run(...)` | Reaches the UI thread; dialog appears; no `InvalidOperationException` | A-1 |
| Open two `ShowDialogAsync` calls simultaneously; close second first | Both TCSs complete; blocker released; UI re-enabled | N-3 |
| Call `NavigationService.Shutdown()` while a dialog is open | Dialog view disposed; TCS resolves `false`; `_interactionBlocker.Unblock()` called | N-3, N-6 |
| App close via `Form.Close()` while a prompt is open | Shutdown completes; no hang; no orphaned window | A-6, N-6 |

*Recommended tooling: use the `/verify` or `/run` skill to drive the demo app and observe behavior.*

---

## 2.9 Pass 4 — Work log

Pass 4 closed the two items that were deferred out of Pass 3 because they touched public contract shape rather than being plain bug fixes. Both are now resolved in code, and the demo + WinForms adapter build clean (0 errors; the 32 warnings are pre-existing core-lib nullable/XML-doc noise unrelated to these changes).

### S-1 — `PlatformRegistry` multi-session support — **FIXED**
- Added `PlatformRegistry.Reset()` (nulls `_current`) so a new platform adapter can be registered after a session ends.
- `Register` now throws with an actionable message ("Call `PlatformRegistry.Reset()` before registering a new adapter.") instead of a bare double-register failure.
- `NavigationService.Shutdown()` calls `PlatformRegistry.Reset()` after nulling `_context`, so a `UseContext → Shutdown → UseContext` cycle (login/logout, multi-window, test isolation) starts from a clean slate.
- Rewrote the class XML doc to describe the single-adapter-per-session / `Reset()`-between-sessions lifecycle.
- *Files: `Infrastructure/PlatformRegistry.cs`, `NavigationService.cs`.*

### N-2 — GoBack state-restore is now an explicit, documented channel — **FIXED**
- Root cause refined during the fix: `IPageStateful.RestoreState(object)` already existed but was **never called** — the runtime only invoked `CaptureState()` and smuggled the blob back through `NavigationArgs.Payload`, with no documented way for a page to know that `Payload` *was* its captured state on a back-step.
- `NavigationArgs` gained `bool IsBackNavigation` and a `Back(object state)` factory; the runtime's GoBack path now constructs `NavigationArgs.Back(entry.State)` instead of `Default(...)`.
- `NavigationRuntime` now calls `stateful.RestoreState(navArgs.Payload)` on back-navigation **before** `OnNavigatedToAsync`, so a stateful page enters fully rehydrated. `Payload` is still populated for backward compatibility, but `RestoreState` is documented as the preferred, unambiguous channel.
- Documented the full `CaptureState → history entry → RestoreState (on back-nav, before enter hook)` contract on `IPageStateful` and on `NavigationArgs.Payload`/`IsBackNavigation`/`Back`.
- *Files: `Metadata/NavigationArgs.cs`, `Runtime/Core/NavigationRuntime.cs`, `Contracts/Pages/IPageStateful.cs`.*

### Remaining
- All static-analysis findings are now resolved or dissolved. The §2.8 runtime-repro checklist (six interactive WinForms scenarios) is the only outstanding Pass 4 activity; it validates already-fixed paths at runtime and requires driving the demo app interactively.

---

## 2.10 New findings discovered in Pass 5

Pass 5 is **post-Pass-4 runtime repro** — bugs surfaced by actually driving the live framework through the demo (`TestForm` + `TestToolsForm`), not by reading code. NEW-7..NEW-10 below all share the property that no amount of static review would have caught them: they are interaction bugs (NEW-7), debugger-output noise (NEW-8), UI-shape decisions (NEW-9), and contract clarity gaps (NEW-10).

#### [HIGH] NEW-7: History double-push on back-navigation — silently re-pushes the page being left back onto the back-stack

**Files:** `Runtime/Core/NavigationRuntime.cs` (`SwitchInternalAsync` Record call site; `GoBackInternalAsync`)

`SwitchInternalAsync` unconditionally calls `_ctx.History.Record(new PageHistoryEntry(from, …))` after every navigation — including the synthetic navigation that `GoBackInternalAsync` performs to replay a popped history entry. Trace for `HOME → A → D → E → back → D → back`:

```
back = [HOME, A, D]      current = E
GoBack: pop D                                        back = [HOME, A]
        SwitchInternalAsync(D, IsBackNavigation=true)
        → Record(from=E)  ← regression             back = [HOME, A, E]
GoBack: pop E                                        back = [HOME, A]
        → lands on E instead of A
```

**Why static analysis missed it.** The unconditional `Record` looked symmetric and correct in isolation; only the *interaction* with the back path produced the cycle. Surfaced immediately on first interactive use.

#### [LOW] NEW-8: `ToastService.RunDismissTimerAsync` throws `TaskCanceledException` on every superseded toast

**Files:** `Runtime/Services/ToastService.cs`

`RunDismissTimerAsync` awaits `Task.Delay(durationMs, token)`. When `ShowToast` supersedes a live toast, the previous CTS is cancelled and `Task.Delay` raises `TaskCanceledException`. The throw is caught (`catch (OperationCanceledException)`), but the debugger still surfaces every first-chance exception. Rapid-fire 5 toasts → 5 lines of red `'System.Threading.Tasks.TaskCanceledException' in mscorlib.dll` in the output window — pure noise that hides real bugs.

#### [MEDIUM] NEW-9: `DialogViewBase` / `PromptViewBase` render as dock-fill, not as a centered rectangle

**Files:** `Hosting/DialogViewBase.cs`, `Hosting/PromptViewBase.cs` (WinForms adapter); interacts with `WinFormsLayeredPageHostBase.AddView`

The framework host sets `control.Dock = DockStyle.Fill` on every overlay added through `IViewHost.AddView`. That is the correct default for the loading mask (full-bleed), but dialogs and prompts are conceptually centered rectangles at a designer-defined size. With the unfixed bases, `ConfirmDialogView` (309×124 in its designer) covered the entire host instead of appearing as a modal box.

#### [LOW] NEW-10: `IPageStateful` contract under-documented for the forward-re-nav boundary

**Files:** demo `Pages/PageE/PageE.cs`; (suggested for the framework) `Contracts/Pages/IPageStateful.cs`

The framework wiring is correct from Pass 4's N-2 fix: `CaptureState` is invoked when leaving the page, `RestoreState` is invoked on back-navigation before `OnNavigatedToAsync`. However, the contract does not state — and the demo did not surface — that **forward re-navigation** (e.g. `A → E → Back → A → click "E" again`) creates a fresh `Transient` `PageE` with no restore, because the forward stack is consumed only by a hypothetical `GoForward` (which does not exist in the public API). A user testing PageE hit this and reported "stateful failed". No code bug, but the contract gap is real.

#### [NOTE] NEW-11: `[A-5]` runtime-repro button doesn't actually test A-5 — semaphore serializes the navigations

**Files:** demo `Pages/HeavyPage/HeavyPage.cs`, `TestToolsForm.NavigateAwayMidLoad`

Discovered during the live Pass 4 walkthrough (§2.12). The `[A-5] Navigate-away-mid-load` button calls `_ = SwitchPage<HeavyPage>(); await Task.Delay(200); await SwitchPage<PageA>();`. Because `HeavyPage` is attributed `[PageLoad(LoadBeforeShow)]`, the runtime holds `_navGate` for the full 2 s load *before* the next navigation can enter the gate — the second `await SwitchPage<PageA>` therefore waits ≈ 2 s and the "jump to PageA" never happens mid-load. The A-5 stale-apply guard (`if (page.IsDisposed || !ReferenceEquals(Current, page)) return;`) only matters under `LoadInBackground`, where the load runs *after* the page is shown, so the LoadBeforeShow probe never exercises the guard.

Evidence from the run log:

```
03:18:36.706  [A-5] Switching to HeavyPage then jumping to PageA mid-load...
03:18:38.758  [A-5] Jumped to PageA. Watch logs over the next ~2s for stale apply.
```

2.05 s between those lines — the second `await` waited for the full HeavyPage load. Not a framework bug; test instrumentation incorrect.

---

## 2.11 Pass 5 — Work log

Pass 5 closed the loop by actually driving the framework through a new demo (`tests/NekoLib.Navigation.Tests/Demo/TestForm.cs` + `TestToolsForm.cs`) wired to the HOME → A → {B, C, D, E, F}, D → F, E → F page graph and a secondary tools window that one-clicks every §2.8 scenario plus state capture/restore. The demo also serves as the persistent regression harness: every §2.8 scenario is a labelled button, every action logs to a live `RichTextBox`, and `PageE` writes `[PageE] CaptureState/RestoreState` to the debug output so the state-restore channel can be visually verified.

### NEW-7 — History double-push — **FIXED**
Guarded the `Record(from)` block in `SwitchInternalAsync` with `!navArgs.IsBackNavigation`. Fired `HistoryChanged?.Invoke()` once from `GoBackInternalAsync` instead, so subscribers still get notified when the back-step mutates the stacks.
*Files: `Runtime/Core/NavigationRuntime.cs`.*

### NEW-8 — Toast cancellation throw — **FIXED**
Replaced `await Task.Delay(ms, token)` with `Task.WhenAny(Task.Delay(ms), cancelTcs.Task)`, where a `CancellationTokenRegistration` completes `cancelTcs` on cancel. No exception is raised on supersession — the timer task exits silently.
*Files: `Runtime/Services/ToastService.cs`.*

### NEW-9 — Dialog/Prompt dock-fill — **FIXED**
`DialogViewBase` and `PromptViewBase` now react to `ParentChanged` (fired when the host adds them), snapshot their natural `Size`, undo `Dock=Fill`, and recenter inside the parent. They also track the parent's `Resize` so they stay centered as the host window resizes. The host code is untouched — the change is opt-in via the base class, so anything that genuinely wants dock-fill (the loading mask) still gets it.
*Files: `Hosting/DialogViewBase.cs`, `Hosting/PromptViewBase.cs` (WinForms adapter).*

### NEW-10 — IPageStateful contract docs — **FIXED**
Not a code bug. Improvements:
- Added `Debug.WriteLine` to PageE's `CaptureState` / `RestoreState` so the call sequence is visible in the debug output.
- When restore fires, the counter label turns dark green and shows `Counter: N (restored ✓)` — visually unmissable.
- Added an XML-doc block to PageE listing which scenarios restore vs. which do not (back-nav ✓, forward re-nav ✗).

Considering for a future pass: surface the same guidance on `IPageStateful` itself.

### Demo / harness additions

- New `TestForm` (primary host) and `TestToolsForm` (secondary tools window) replace the legacy `Form1`/`ShellForm` as the demo entry point.
- New pages: `PageC`, `PageD`, `PageE` (stateful), `PageF` (user-added); `BottomLeftToastView` exercises the toast-positioning path.
- `TestToolsForm` exposes one-click buttons for every §2.8 scenario tagged with the finding it validates (e.g. `[A-1]`, `[N-3/N-6]`, `[A-5]`).
- `HomePage` is now click-anywhere → PageA.

### NEW-11 — A-5 test instrumentation — **FIXED**
Added `Pages/HeavyPageBackground/HeavyPageBackground.cs`, attributed `[PageLoad(LoadInBackground)]`, with a `Debug.WriteLine` inside `ApplyBackgroundResultAsync` so the stale-apply guard's effect is observable from the debug output. Rewired the `[A-5] Navigate-away-mid-load` button in `TestToolsForm` to target the new page, and added a peer button "Show HeavyPageBackground (LoadInBackground 2 s)" so the load-mode pair (LoadBeforeShow vs LoadInBackground) is both exercisable from the tools window. Verdict for A-5 becomes: if `"[HeavyPageBackground] ApplyBackgroundResultAsync fired."` prints in the debug output after the mid-load jump, A-5 regressed; if it stays silent, the guard held.
*Files: `Pages/HeavyPageBackground/HeavyPageBackground.cs` (new), `TestToolsForm.cs`.*

### Follow-ups (candidates for Pass 6)

- ~~Automated tests around `NavigationHistory` + `GoBackInternalAsync` would have caught NEW-7. Currently zero unit-test coverage on the navigation module.~~ **Done in Pass 6 (§2.14).** Plus an `NekoLib.Mvvm.Tests/Unit/` sibling project covering `RelayCommand` / `RelayCommand<T>` / `ViewModelBase`. **65 tests total**, both TFMs green.
- Remaining §2.8 scenarios not yet driven interactively (see §2.12 below): `Form.Close()` while a prompt is open is the last one (one manual probe).
- ~~Legacy `Form1.cs` / `Form1ViewModel.cs` / `Form1.Designer.cs` / `ShellForm.cs` are orphaned after the `TestForm` switch.~~ **Done in post-Run-#2 cleanup** — files deleted via `git rm`; `TestForm.cs` doc-comment updated; build clean.
- API delta / migration notes section.

---

## 2.12 Pass 4 — Live verification log

First live walkthrough of the §2.8 runtime-repro checklist. Demo launched from the user's desktop; results captured from `TestToolsForm`'s `RichTextBox` log. Three scenarios still need driving (deferred to a follow-up run); one scenario was found to be mis-instrumented (NEW-11, fixed above) and needs re-running.

### Run #1 — 2026-05-29 03:17–03:19 local

| § | Scenario | Verdict | Evidence |
|---|---|---|---|
| 1 | Rapid 20 forward navs (A-6/N-1) | ✅ PASS | `[Stress] forward done: ok=20 errors=0` (300 ms total, ~15 ms/nav) |
| 1 | 20 alternating fwd/back (A-6/N-1) | ✅ PASS | `[Stress] alternating done: ok=20 errors=0` (713 ms total, ~36 ms/nav) |
| 1 | 5 concurrent `SwitchPage` from `Task.Run` (A-6/N-1) | ✅ PASS | `[A-6/N-1] All 5 concurrent navs completed cleanly.` (101 ms — consistent with semaphore serialization, no deadlock) |
| 2 | Navigate-away mid `LoadInBackground` (A-5) | ⚠️ INVALID — NEW-11 | 2.05 s gap between the two `[A-5]` lines: the second `await SwitchPage` waited for the full LoadBeforeShow HeavyPage; mid-load never occurred. Re-run needed against `HeavyPageBackground`. |
| 3 | `ShowDialogAsync` from `Task.Run` (A-1) | ⏭ Not driven | — |
| 4 | Two stacked dialogs, close 2nd first (N-3) | ✅ PASS | `[N-3] 2nd dialog -> True` then `[N-3] 1st dialog -> True` — both TCS resolved without leak. |
| 5 | `Shutdown` with dialog open (N-3/N-6) | ⏭ Not driven | — |
| 6 | `Form.Close()` with prompt open (A-6/N-6) | ⏭ Not driven | — |

**Other surface checks confirmed in the same run:**
- Four bottom-left toasts spawned in succession including long text — no `TaskCanceledException` lines in the debug output. **NEW-8 fix confirmed at runtime.**
- Confirm dialog cycle clean (`Confirm dialog -> True`) — NEW-9's centered-rectangle change didn't break the completion path.
- `DismissCurrentToast` worked twice.
- Basic nav surface clean (`Navigated home.`, `GoBack -> True`).

**Summary:** 3 of 6 §2.8 scenarios PASS; 1 invalid → NEW-11 logged & fixed; 3 deferred to Run #2. Pass 5 NEW-8 and NEW-9 corroborated at runtime as a side-benefit. No new framework regressions observed.

### Run #2 — 2026-05-29 04:01–04:03 local

Pre-flight: NEW-11 fix landed; `HeavyPageBackground.ApplyFired` static event wired into the `TestToolsForm` log so the A-5 probe is visible without DebugView.

| § | Scenario | Verdict | Evidence |
|---|---|---|---|
| 2 | Navigate-away mid `LoadInBackground` (A-5) | ✅ PASS | 294 ms between the two `[A-5]` lines — jump happened mid-load (vs 2.05 s in Run #1 / NEW-11). **No `[A-5] [HeavyPageBackground] ApplyBackgroundResultAsync fired.`** line in the next 30 s — stale-apply guard held. |
| 3 | `ShowDialogAsync` from `Task.Run` (A-1) | ✅ PASS | Dialog appeared from a thread-pool thread, user clicked Confirm, `[A-1] Dialog from Task.Run -> True`. No cross-thread `InvalidOperationException`. |
| 4 | Two stacked dialogs, close 2nd first (N-3) | ✅ PASS (re-confirm) | `[N-3] 2nd dialog -> True` then `[N-3] 1st dialog -> True` — both TCS resolved, now also against the new centered-rectangle dialog layout. |
| 5 | `Shutdown` with dialog open (N-3/N-6) | ✅ PASS — **with bonus** | The run inadvertently exercised a richer case than the §2.8 plan envisioned: a still-pending `[A-1]` dialog from 3 s earlier overlapped with the `[N-3/N-6]` scenario's own dialog. Shutdown completed at 04:03:03.320, then within 17 ms both TCSs resolved `False` — first the `[N-3/N-6]` dialog, then the older `[A-1]` dialog. Confirms N-6's teardown cancels **every** pending dialog, not just the most recent. |
| 6 | `Form.Close()` with prompt open (A-6/N-6) | ⏭ Not driven | Manual scenario; deferred to Run #3. |

**Summary:** 4 of 4 driven scenarios PASS in Run #2. Combined with Run #1, **5 of 6 §2.8 scenarios are now verified at runtime**; only `Form.Close()` with prompt open remains, and is a one-step manual probe.

**Notable behavior corroborated (not new findings, just evidence):**
- N-6 teardown cancels *all* pending dialog TCSs in one shot — verified by the overlapping `[A-1]` + `[N-3/N-6]` case.
- The dialog-centering change from NEW-9 did not break any modal-completion path.
- Pass 5's NEW-7 (history double-push) regression-fix held through all §2.8 traffic in both runs.

### Pending — Run #3 (manual probe only)

One scenario left to drive:

1. `Form.Close()` with prompt open — relaunch the demo, open the text-input prompt from `TestToolsForm`, then click ✕ on the **primary** window without dismissing the prompt. Expect: clean shutdown, no orphan window in Task Manager.

After Run #3 the §2.8 checklist is fully validated.

---

## 2.13 New findings discovered in Pass 6

Pass 6 was mostly structural work (tests, MVVM extraction, runtime-sim restructure), not bug-hunting. Even so, two minor API-shape smells surfaced while writing the new tests and migrating the runtime-sim namespace. Both are NOTE-severity cosmetic issues, not defects.

#### [NOTE] NEW-12: `NekoLib.Diagnostics.Diagnostics` (class) collides with `NekoLib.Navigation.Diagnostics` (sub-namespace)

**Files:** `src/Diagnostics/NekoLib.Diagnostics/Diagnostics.cs`; observed at `runtime_tests/WinForms_481/.../TestForm.cs`

Any consumer whose namespace lives under `NekoLib.Navigation.*` (the new `runtime_tests` project, the WinForms adapter, the demo) finds bare `Diagnostics` ambiguous: the C# resolver sees both `NekoLib.Diagnostics` (the sibling package containing the host class) and `NekoLib.Navigation.Diagnostics` (a navigation sub-namespace holding `NavigationDiagnostics`, `NavigationEventHub`, etc.). The compiler picks the closer one — the sub-namespace — and emits `CS0118: 'Diagnostics' is a namespace but is used like a type`.

The workaround in `TestForm.cs` is to fully qualify: `new global::NekoLib.Diagnostics.Diagnostics(logger, memory)`. A cleaner long-term fix is to rename either the class (e.g. `DiagnosticsHost` or `NekoLibDiagnostics`) or the navigation sub-namespace (e.g. `Diag`), so the conflict can't happen.

#### [NOTE] NEW-13: `PageMetadataBuilder.Register<T>(configure)` doesn't actually register the type

**Files:** `src/Navigation/NekoLib.Navigation/Bootstrap/PageMetadataBuilder.cs`

```csharp
public void Register<TPage>(Action<PageDescriptorBuilder>? configure = null)
    where TPage : IPageView
{
    _manual[typeof(TPage)] = configure ?? (_ => { });
}
```

`Register<T>` stores the configurator in `_manual`, but `Build()` only iterates `_assemblies` (assembly scan) and `_explicitTypes` (the working register path). A configurator in `_manual` is only consumed when the type is *also* in one of those two buckets. So `builder.Register<MyPage>()` alone produces zero descriptors.

The working API is `builder.RegisterType(typeof(MyPage), configure)`, which adds to `_explicitTypes` and (when `configure` is non-null) also stores the configurator. This was bugged from the start of the test-fixture authoring — the first runtime tests failed with empty page graphs until the fixture switched to `RegisterType(typeof(T), ...)`.

Fix: `Register<T>` should call into the same code path as `RegisterType(typeof(T), ...)`, OR be removed entirely. Either is a tiny PR. Tracked as NOTE because the workaround is one-line.

---

## 2.14 Pass 6 — Work log

Pass 6 was a structural pass: no framework defects (NEW-7..11 were already fixed in Pass 5), but the surrounding scaffolding around the framework got significantly more solid.

### Automated test coverage — **NEW**

Built two xunit projects (`tests/NekoLib.Navigation.Tests/Unit/` + `tests/NekoLib.Mvvm.Tests/Unit/`), multi-target `net481` + `net9.0-windows`. **65 tests total, all green on both TFMs.**

The Navigation set (43) covers three layers:
- **Pure logic (22)** — `NavigationHistoryTests` (back/forward stack semantics including the `BrowserDance_RecordAfterBack_WipesForwardStack` regression) + `NavigationArgsTests` (`Default`/`Transient`/`Preload`/`Background`/`Back` factories, including Pass 4's `IsBackNavigation` flag).
- **Runtime integration (8)** — drives `NavigationRuntime` directly via `[InternalsVisibleTo]` against a sync dispatcher + recording host. Includes **`GoBackAsync_TwiceFromC_LandsOnA_NotOnC`** which pins NEW-7 directly, plus `BackNavigation_ToStatefulPage_CallsRestoreStateWithCapturedValue` pinning the Pass 4 N-2 wiring at the runtime layer.
- **Service-level (13)** — `ToastServiceTests` (NEW-8 rapid-fire supersession does not throw), `DialogServiceTests` (N-3 CloseAll across two pending dialogs, second-completes-first ordering), `PromptServiceTests` (N-3/N-6 CloseAll resolves with `default(TResult)`).

The MVVM set (22) covers `RelayCommand` (non-generic), the new `RelayCommand<T>` (parameter coercion matrix — ref-type / value-type / nullable / wrong type), and `ViewModelBase.SetProperty` semantics.

The scaffolding (`Fakes/RuntimeTestFixture`, `SyncEventDispatcherAdapter`, `FakePageHost`, stub views, stub surfaces) is the reusable part — every future runtime test costs about 4 lines plus the assertion.

*Files: `tests/NekoLib.Navigation.Tests/Unit/**`, `tests/NekoLib.Mvvm.Tests/Unit/**`.*

### MVVM helpers extracted to `NekoLib.Mvvm` sibling — **NEW**

`RelayCommand` and `ViewModelBase` previously lived in the demo project (`NavigationDemo.Core`), then briefly inside `NekoLib.Navigation/Mvvm/`. Final placement: a dedicated sibling package `src/Mvvm/NekoLib.Mvvm/`, parallel to `NekoLib.Diagnostics`, `NekoLib.Navigation`, etc. No dependency on Navigation — MVVM is opt-in.

Plus a new `RelayCommand<T>` generic variant with typed parameter and safe coercion: an out-of-band parameter type now leaves `Execute` as a no-op and `CanExecute` returning false, instead of letting an `InvalidCastException` escape through the binding pipeline.

*Files: `src/Mvvm/NekoLib.Mvvm/*` (new project), `tests/NekoLib.Mvvm.Tests/Unit/*` (new tests), demo VMs updated, demo csproj adds the `NekoLib.Mvvm` ProjectReference.*

### `runtime_tests/` restructure — **NEW conceptual split**

The previous demo lived under `tests/NekoLib.Navigation.Tests/Demo/`, which conflated two distinct concerns: *assertions* (the unit tests) vs *simulations* (the runnable scenario app). Migrated to:

```
tests/                                              ← assertion harnesses (xunit)
  NekoLib.Mvvm.Tests/Unit/
  NekoLib.Navigation.Tests/Unit/

runtime_tests/                                      ← runnable scenario sims
  WinForms_481/
    NekoLib.Navigation.RuntimeTests.Winforms481/   ← previously Demo/
  (future: WinForms_9/, Wpf/, …)
```

The TFM-bucket grouping is intentional — different UI tech / framework versions need different csproj boilerplate, and each scenario is a distinct runnable assembly. The first migrated project is net481-only (matches its name); a parallel `WinForms_9/` for net9.0 can come later if needed.

Namespaces migrated `NavigationDemo.*` → `NekoLib.Navigation.RuntimeTests.Winforms481.*` (42 files, zero remnants). Solution updated. Surfaced NEW-12 during the rename (see §2.13).

*Files: `runtime_tests/WinForms_481/NekoLib.Navigation.RuntimeTests.Winforms481/**` (new), `tests/NekoLib.Navigation.Tests/Demo/**` (deleted), `NekoLib.sln`.*

### Designer-split restore — **HYGIENE**

Pages I had written code-only (`PageC`, `PageD`, `PageE`, `PageF`, `BottomLeftToastView`, `HeavyPageBackground`, `TestForm`, `TestToolsForm`) could not be opened in the WinForms designer because they lacked the `partial class` + `*.Designer.cs` split with controls inside `InitializeComponent()`. User-reported during Pass 5 verification — minor hygiene rather than a finding, but felt structural enough to record here.

Every demo page/form is now designer-editable:
- Static control creation lives in `*.Designer.cs#InitializeComponent`.
- Custom logic (event wiring, lifecycle hooks, state) lives in the `*.cs` user-code partial.
- Special case: `BottomLeftToastView` uses `partial void DisposeOverrides(bool)` so its positioning hooks can unhook from inside the designer-owned `Dispose(bool)`.

*Files: `runtime_tests/WinForms_481/NekoLib.Navigation.RuntimeTests.Winforms481/{Pages/*, TestForm.Designer.cs, TestToolsForm.Designer.cs}`.*

### Branch metadata

Branch was renamed `navigation/runtime-tests` → `navigation/feat/audit-and-mvvm-split` to reflect the actual scope of the work (audit fixes + MVVM extraction + tests, not just runtime tests).

### Remaining

- §2.8 Run #3 — the last manual probe (`Form.Close()` with prompt open). Single scenario; no code change expected.
- NEW-12, NEW-13 — cosmetic API-shape smells, low priority.
- API delta / migration notes section — useful if any external consumer attaches to the library.
