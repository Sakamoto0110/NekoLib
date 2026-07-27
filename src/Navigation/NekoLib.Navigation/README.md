
# PageNav – Drop-in Core (v2)

This is a **cleaned, compressed, documented-ready snapshot** of PageNav Core.
It is designed to be dropped into an existing solution and compiled immediately.

---

## 1. What PageNav is

PageNav is an **instance-based navigation runtime** for desktop applications
(WinForms / WPF) with:

- Deterministic page lifecycle
- Explicit cache policies
- Platform abstraction (host, timer, dispatcher, overlays)
- Centralized timeout handling
- Strong diagnostics for leaks and navigation flow

It is **not** a UI framework, **not** a DI container, and **not** tied to ASP.NET.

---

## 2. Canonical lifecycle order (DO NOT CHANGE)

As implemented in `NavigationRuntime.SwitchInternalAsync`:

```
Registry lookup (unregistered type ⇒ throw)
↓
Guard evaluation (30s cap; deny/redirect: depth ≤ 8, cycle detection)
↓
Capture FROM state (IPageStateful.CaptureState)
↓
Navigating(from, toType, args)
↓
Resolve TO instance (reuse-policy caches)
↓
[LoadBeforeShow only] load now (with loading mask)
↓
FROM:
  IPageVisibility.HidePage()
  IPageLifecycle.OnNavigatedFromAsync()
↓
Detach FROM + Cleanup (unless KeepAttachedWhenHidden; Transient ⇒ dispose)
↓
Attach TO + BringToFront + IPageVisibility.ShowPage()
↓
Current = to; CurrentChanged
↓
Load strategy:
  [ShowImmediately] load now | [LoadInBackground] fire-and-forget guarded load
↓
[back-nav only] IPageStateful.RestoreState(state)
↓
TO:
  IPageLifecycle.OnNavigatedToAsync(args)
↓
History.Record(from) + HistoryChanged (forward navigation only)
↓
Navigated(from, to, args)
```

`IPageLifecycle` has exactly two hooks: `OnNavigatedToAsync(NavigationArgs)` and
`OnNavigatedFromAsync()`.

NavigationRuntime (driven through the static `NavigationService` facade) is the
**only component allowed** to invoke lifecycle methods.

---

## 3. Folder responsibilities

### Contracts/
Pure contracts. No logic. No platform assumptions.

### Metadata/
DTO-like structures, attributes, and enums. No behavior.

### Runtime/
Navigation runtime, registry, factories, services, history, session.

### Diagnostics/
Navigation tracing + optional bridge to NekoLib.Diagnostics.

### Toolkit/
Optional surface positioning abstractions (anchors). Not required by Core.

---

## 4. Overlay primitives (Toast / Dialog / Prompt / Popover)

| Primitive | Blocks input? | Auto-dismiss | Return |
|-----------|---------------|--------------|--------|
| `IToastView` / `IToastService` | no | timer (+ tap) | `void` |
| `IDialogView` / `IDialogService` | **yes** | — | `Task<bool>` |
| `IPromptView<TResult>` / `IPromptService` | **yes** | — | `Task<TResult>` |
| `IPopoverView` / `IPopoverService` | no | on focus loss (via `IUnfocusAware`) | `Task<bool>` |

Dialog is binary by definition; for 3+ outcomes use `IPromptView<TEnum>`.
Popover stays open until the view calls its completion, OR — if it implements
`IUnfocusAware` — the platform's `IFocusObserverAdapter` fires unfocus and the
view's `OnUnfocusAsync` resolves the awaiter. Both platform projects (WinForms
and WPF) ship `PopoverViewBase` (manual close only) and `AutoDismissPopoverBase`
(auto-close on unfocus) so subclasses don't repeat the wiring.

---

## 5. What should be considered FROZEN

Do not casually modify:

- NavigationContext
- NavigationRuntime
- PageRegistry
- PageFactory

Extensions should live outside Core.

---

## 6. Typical initialization (example)

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

`Start()` auto-mounts the context onto the static `NavigationService` facade,
so view-models can call `NavigationService.SwitchPage<T>()` immediately after.
The returned `NavigationContext` is optional — useful only for tests or for
subscribers that want `context.Events` / `context.History`.

---

## 7. Next logical steps

1. Build DM extension layer (virtual keyboard, dialogs, kiosk rules)
2. Anchor-positioned popover convenience (e.g. attach to a clicked control)

This snapshot intentionally favors **clarity over cleverness**.
