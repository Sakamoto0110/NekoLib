# NekoLib Navigation

NekoLib Navigation is a desktop navigation runtime for WinForms and WPF applications. It provides page registration, deterministic navigation lifecycle, history, guards, idle/session behavior, overlays, and platform adapters while keeping application pages framework-native.

It is not a general UI framework, a dependency injection container, or a web navigation model. The runtime owns page attach/detach, lifecycle calls, caching, overlay teardown, and navigation diagnostics.

## Quick Start

For WinForms, host navigation inside a `Control` such as a `Panel`:

```csharp
PageNavBootstrap
    .Use<WinFormsPlatformAdapter>(mainPanel)
    .RegisterPagesFromAssembly(typeof(IdlePage).Assembly)
    .SetIdle<IdlePage>()
    .UseIdleTimeout(10_000)
    .Start();
```

For WPF, use a `Panel` host such as a `Grid`:

```csharp
PageNavBootstrap
    .Use<WpfPlatformAdapter>(MainGrid)
    .RegisterPagesFromAssembly(typeof(IdlePage).Assembly)
    .ConfigurePages(cfg =>
    {
        cfg.Page<IdlePage>().AsIdle().StrongSingleton();
    })
    .Start();
```

`Start()` builds a `NavigationContext`, registers platform/runtime services, and mounts the static `NavigationService` facade. Application code can then navigate with:

```csharp
await NavigationService.SwitchPage<DashboardPage>();
await NavigationService.GoBackAsync();
await NavigationService.GoIdleAsync();
```

## Page Model

Every navigable view implements `IPageView`. Platform projects provide base classes:

- WinForms: derive from `NekoLib.Navigation.WinForms.Hosting.PageView`.
- WPF: derive from `NekoLib.Navigation.Wpf.Hosting.PageView`.

The runtime calls optional contracts when implemented:

- `IPageLifecycle`: `OnNavigatedFromAsync` before leaving, `OnNavigatedToAsync` after attach.
- `IBackgroundLoadable`: load off the UI thread, then apply results on the UI thread.
- `IPageStateful`: capture state when leaving and restore it on back-navigation.
- `IPageVisibility`: explicit show/hide hooks for platform-specific visual state.
- `IHostAttachable`: receive attach/detach notifications from the host.

The canonical forward-navigation flow is:

1. Resolve page descriptor and evaluate guards.
2. Resolve or create the target page according to reuse policy.
3. Optionally load before show.
4. Hide, leave, detach, and clean up the current page.
5. Attach and show the target page.
6. Optionally load immediately or in the background.
7. Restore back-navigation state when applicable.
8. Run target lifecycle, update current page, record history, and emit diagnostics.

## Registration And Metadata

Pages are registered through assembly scanning and/or manual configuration.
Authentication and role/permission rules are declared with attributes on the
page class — the assembly scanner turns each guard attribute into a runtime
guard. Instance reuse, naming, tags, and load mode are tuned through the fluent
DSL in `ConfigurePages`:

```csharp
[AllowAnonymous]
public sealed class LoginPage : PageView { /* ... */ }

[RequireRole("admin")]
public sealed class AdminPage : PageView { /* ... */ }

PageNavBootstrap
    .Use<WinFormsPlatformAdapter>(host)
    .RegisterPagesFromAssembly(typeof(LoginPage).Assembly)
    .ConfigurePages(cfg =>
    {
        cfg.Page<LoginPage>().StrongSingleton();
        cfg.Page<AdminPage>().WeakSingleton();
    })
    .Start();
```

Metadata controls:

- Role: normal page, idle page, or timeout target.
- Reuse: `[PageReuse(...)]` or the DSL `Transient()` / `StrongSingleton()` / `WeakSingleton()` (weak-cached). Strong = held for the context lifetime; weak = reused while alive, recreated after GC.
- Load mode: `[PageLoad(...)]` or `.LoadMode(...)` — show immediately, load before show, or load in background.
- Guards: `[RequireAuthenticated]`, `[RequireRole]`, `[RequirePermission]`, or a custom `[Guard]`-derived attribute.
- Tags/name: `Tag(...)` / `Named(...)` for lookup and convention-based resolution.

The DSL (`cfg.Page<T>()`) configures reuse, naming, tags, load mode, and idle
role; it does not declare guards. Guards/anonymous/authorization come from page
attributes. Attribute metadata is applied first; manual DSL configuration runs
afterward and wins on the properties it sets.

## Session, Guards, And Idle

Every `NavigationContext` owns a `NavigationSession`. It implements `IUserContext`, so built-in guards read live session state:

```csharp
NavigationService.Session.SignIn(
    roles: new[] { "admin" },
    permissions: new[] { "orders.write" });
```

`SignOut()` clears authentication, roles, and permissions. When an idle timeout is configured, the bootstrap wires platform interaction observation and a timer. After the configured interval without input, the runtime signs the session out and navigates to the idle page.

The idle page is resolved in this order (any one condition qualifies a page):

1. A descriptor with `PageRole.Idle`, usually via `SetIdle<TPage>()` or `.AsIdle()`.
2. A page tagged `idle`.
3. A registered page whose name contains `Idle` (e.g. `IdlePage`).

### Idle timeout duration

The inactivity duration is the *idle* timeout, so it is declared on the idle page itself, in seconds:

```csharp
[PageTimeout(30)]                      // attribute form
public sealed class IdlePage : PageView { }

// or via the DSL:
cfg.Page<IdlePage>().AsIdle().IdleTimeout(30);
```

Precedence, highest first:

1. DSL `.IdleTimeout(seconds)` on the idle page.
2. `[PageTimeout(seconds)]` on the idle page.
3. Global `UseIdleTimeout(milliseconds)` fallback.

A timeout declared on the idle page also enables the timer on its own, so `UseIdleTimeout(ms)` is optional when the idle page sets one. Declaring `[PageTimeout]` / `.IdleTimeout()` on any non-idle page throws at bootstrap — only the idle page's value is read.

Guards may deny navigation or redirect to another page. Redirect chains are bounded to prevent loops.

## Overlays

Navigation provides four overlay primitives:

| Primitive | Blocks background input | Completion |
| --- | --- | --- |
| Toast | No | Fire-and-forget, auto-dismiss timer or user dismiss |
| Dialog | Yes | `Task<bool>` confirm/cancel |
| Prompt | Yes | `Task<TResult>` typed result |
| Popover | No | `Task<bool>`, optional auto-dismiss via `IUnfocusAware` |

Platform base classes are available for WinForms and WPF:

- `ToastViewBase`
- `DialogViewBase`
- `PromptViewBase<TResult>`
- `PopoverViewBase`
- `AutoDismissPopoverBase`

Runtime reset and shutdown close all live overlays so awaiting callers do not hang.

## Platform Adapter Responsibilities

An `IPlatformAdapter` provides the runtime boundary to WinForms, WPF, or another desktop platform. It must create:

- An `IPageHost` for page attach/detach.
- An `IEventDispatcherAdapter` for UI-thread marshaling.
- An `IInteractionBlocker` for modal overlays.
- An `ITimerAdapter` for idle timeout.
- Optional interaction and focus observers.
- Optional default loading mask type.

The core runtime stays platform-agnostic. Platform projects own native layout, focus, timers, and host behavior.

## Diagnostics And Events

`NavigationService` exposes runtime events such as `Navigating`, `Navigated`, `NavigationFailed`, `CurrentChanged`, and `HistoryChanged`. `NavigationService.Events` exposes the diagnostics hub for navigation log and guard-denied events.

If an `IDiagnosticsContext` is supplied through `UseDiagnostics`, navigation diagnostics are bridged into `NekoLib.Diagnostics`. Without it, the in-memory navigation event hub still works.

## Tests And Runtime Demos

Unit tests live under:

```powershell
dotnet test tests/NekoLib.Navigation.Tests/Unit/NekoLib.Navigation.Tests.Unit.csproj
```

Build the Navigation projects with:

```powershell
dotnet build src/Navigation/NekoLib.Navigation/NekoLib.Navigation.csproj
dotnet build src/Navigation/NekoLib.Navigation.WinForms/NekoLib.Navigation.WinForms.csproj
dotnet build src/Navigation/NekoLib.Navigation.Wpf/NekoLib.Navigation.Wpf.csproj
```

Runtime scenario apps under `runtime_tests/` are interactive WinForms applications. Launch them directly when validating visual behavior; they are not xUnit test projects.

## Documentation Notes

This file is based on the current `navigation` branch implementation. Existing source comments that referenced older home/timeout terminology should be treated as stale unless they match the current idle/session APIs. The root `README.md` can use this file as the Navigation-focused source of truth if the project later promotes it.
