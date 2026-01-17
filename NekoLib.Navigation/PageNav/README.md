
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

## 4. What still needs *real* documentation

The following files have XML TODO headers and should be documented next:

- NavigationContext.cs (lifecycle + threading guarantees)
- PageRegistry.cs (attribute reference + cache rules)
- IPlatformAdapter.cs (implementation guide)
- IPageOverlayService.cs (modal vs non-modal rules)
- PageTimeoutController.cs (state machine + timing)

---

## 5. What should be considered FROZEN

Do not casually modify:

- NavigationContext
- PageRegistry
- PageFactory
- PageLifecycleCleanupService
- PlatformRegistry

Extensions should live outside Core.

---

## 6. Typical initialization (example)

```csharp
var ctx = PageNavBootstrap
    .Use(this, new WinFormsPlatformAdapter())
    .RegisterPagesFromAssembly(typeof(MainPage).Assembly)
    .ConfigurePages(cfg =>
    {
        cfg.Page<HomePage>().AsHome().StrongSingleton();
        cfg.Page<AdminPage>().AsModal().StrongSingleton();
    })
    .Timeout(10)
    .Start();
```

---

## 7. Next logical steps

1. Add Bootstrap layer permanently
2. Implement WinForms/WPF adapters
3. Build DM extension layer (virtual keyboard, dialogs, kiosk rules)

This snapshot intentionally favors **clarity over cleverness**.
