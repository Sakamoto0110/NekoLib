
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

```
Resolve target page instance
↓
Navigating(from, toType, args)
↓
Reset timeout
↓
FROM:
  IPageView.OnNavigatedFromAsync()
  IPageLifecycle.OnExitAsync()
↓
Detach + Cleanup (cache policy driven)
↓
Attach + BringToFront + Visible=true
↓
TO:
  IPageView.OnNavigatedToAsync(args)
↓
Load strategy:
  ShowImmediately | LoadBeforeShow | LoadInBackground
↓
IPageLifecycle.OnEnterAsync(args)
↓
CurrentChanged + History.Record
↓
Navigated(from, to, args)
```

NavigationContext is the **only component allowed** to invoke lifecycle methods.

---

## 3. Folder responsibilities

### Core/Abstractions
Pure contracts. No logic. No platform assumptions.

### Core/Models
DTO-like structures and enums. No behavior.

### Core/Services
Navigation runtime, registry, cleanup, timeout, history.

### Core/Infrastructure
Low-level helpers (ServiceLocator, PlatformRegistry).

### Diagnostics
Leak detection, lifecycle tracking, navigation tracing.

---

## 4. What should be considered FROZEN

Do not casually modify:

- NavigationContext
- PageRegistry
- PageFactory
- PageLifecycleCleanupService

Extensions should live outside Core.

---

## 5. Typical initialization (example)

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

## 6. Next logical steps

1. WPF adapter parity with WinForms
2. Build DM extension layer (virtual keyboard, dialogs, kiosk rules)

This snapshot intentionally favors **clarity over cleverness**.
