# Navigation Public API Review — 2026-08-20

**Kind:** audit

**Lifecycle:** historical

**Subject:** F1-NAV compiled public surface, facade, registration, lifecycle,
history, guards, session, page and platform contracts, surfaces, diagnostics,
nullability, target parity, and package boundary

**Status:** all twelve dispositions accepted and implemented

**Reference date:** 2026-08-20

**Reference commit:** `9706a2c165d3bc4bcfac810319a829f42845eb95`

**Last reconciliation:** 2026-08-20

**Current state:** the [Navigation technical reference](../../src/Navigation/NekoLib.Navigation/README.md)
owns the implemented core contract; [`TODO.md`](../../TODO.md) records F1-NAV
as complete while F1-NAV-WF and F1-NAV-WPF retain adapter finalization.

## Baseline and authority

This review covers committed `HEAD` on branch
`phase-e/sqlserver-and-orchestration`. At entry:

- `HEAD` was exactly `9706a2c165d3bc4bcfac810319a829f42845eb95`;
- the worktree and index were clean;
- the branch was 117 commits ahead of `origin/master`;
- the branch was 51 commits ahead of its upstream
  `origin/phase-e/sqlserver-and-orchestration`; and
- none of the reviewed work was pushed by this review.

Authority order:

1. tracked source under `src/Navigation/NekoLib.Navigation/`;
2. the core, WinForms, WPF, and Navigation test project files;
3. tracked tests under `tests/NekoLib.Navigation.Tests/Unit/`;
4. all six approved Navigation-family manifests under `eng/public-api/`;
5. the Navigation technical reference, release policy, changelog, repository
   documentation indexes, and test taxonomy;
6. `TODO.md` F1 and its active freezes; and
7. the historical adapter review only as a dated lead rechecked against current
   source and manifests.

The two adapter projects were read only where their implementation of a core
contract was necessary to classify that core contract. Their own public types
remain outside this block and belong to F1-NAV-WF and F1-NAV-WPF.

This artifact changes no product source, test, project, approved manifest,
package, changelog, migration guide, accepted decision, or implementation
roadmap item.

## Scope

Included:

- every type and member in the two compiled `NekoLib.Navigation` manifests;
- facade mounting, shutdown, reset, navigation, back, current-page, history,
  service, and event boundaries;
- attribute and fluent registration, precedence, validation, immutability, and
  lookup;
- page, platform, guard, user/session, history, overlay, surface, toolkit,
  diagnostic, telemetry, and passive Inspection contracts;
- ownership, threading, UI dispatch, navigation gating, lifecycle ordering,
  guard timeout/redirect behavior, rollback, and teardown consequences of the
  proposed surface;
- public nullability versus observed runtime behavior;
- `net481` and `net9.0` parity, dependencies, and expected package shape; and
- three already-landed adapter compatibility changes that must not be silently
  lost when the later adapter baselines are finalized.

Excluded:

- implementing any recommendation;
- editing `NavigationContext`, `NavigationRuntime`, `PageRegistry`, or
  `PageFactory`;
- changing WinForms or WPF product/API source;
- updating an approved API or warning baseline;
- creating changelog or migration entries before acceptance;
- adding Inspection actions, TestControl, reflection activation, IPC, debugger
  hooks, F7 actions, fault injection, or a new project reference;
- launching an interactive or long-running runtime scenario;
- packaging, publishing, committing, or pushing; and
- reading ignored `.local/` or `experiments/` contents.

## Project and future-package boundary

`NekoLib.Navigation` targets `net481;net9.0`, enables nullable analysis,
disables implicit usings, and references `NekoLib.Core`. Its `net481` target
also carries `Microsoft.Bcl.AsyncInterfaces 10.0.1` because the internal runtime
implements `IAsyncDisposable`. The core compiled surface is identical across
both targets except target-framework assembly metadata.

`NekoLib.Navigation.WinForms` and `NekoLib.Navigation.Wpf` target
`net481;net9.0-windows` and each references the core Navigation project. Their
approved public surfaces are target-parallel except normal modern Windows
assembly metadata. The Navigation test project targets
`net481;net9.0-windows` and references the three Navigation projects plus Core,
Inspection, and Telemetry for integration coverage.

The expected future package graph therefore remains shallow:

| Package | Assets | Direct project/package boundary |
|---|---|---|
| `NekoLib.Navigation` | `lib/net481`, `lib/net9.0` | `NekoLib.Core`; `Microsoft.Bcl.AsyncInterfaces` on `net481` |
| `NekoLib.Navigation.WinForms` | `lib/net481`, `lib/net9.0-windows` | `NekoLib.Navigation` |
| `NekoLib.Navigation.Wpf` | `lib/net481`, `lib/net9.0-windows` | `NekoLib.Navigation` |

No package was built, so this table records evaluated project inputs rather
than package-content or PackageReference evidence.

## Compiled public-surface inventory

The two core approved manifests are identical apart from the target-framework
attribute. Each records 89 public types and 381 direct public/protected member,
enum-value, and type declarations. The count is a physical manifest inventory,
not a claim that every declaration should become stable.

| Namespace area | Types | Direct declarations | Review coverage |
|---|---:|---:|---|
| Bootstrap | 4 | 33 | NAV-01, NAV-04 |
| Contracts.Guards | 5 | 20 | NAV-06, NAV-11 |
| Contracts.Pages | 18 | 35 | NAV-07, NAV-11 |
| Contracts.Platform | 7 | 22 | NAV-07, NAV-11 |
| Contracts.Runtime | 5 | 9 | NAV-09, NAV-11 |
| Diagnostics | 7 | 55 | NAV-10, NAV-11 |
| Metadata.Attributes | 12 | 28 | NAV-05, NAV-06, NAV-11 |
| Metadata | 8 | 50 | NAV-03, NAV-04, NAV-05, NAV-08, NAV-11 |
| Root facade | 1 | 26 | NAV-01, NAV-03, NAV-11 |
| Runtime context/factory/registry | 3 | 24 | NAV-02, NAV-04, NAV-12 |
| Runtime guards | 7 | 14 | NAV-06 |
| Runtime history | 1 | 14 | NAV-08, NAV-11 |
| Runtime outcome enum | 1 | 8 | NAV-10 |
| Runtime services/session | 6 | 26 | NAV-06, NAV-09, NAV-11 |
| Telemetry | 1 | 2 | NAV-03, NAV-10 |
| Toolkit abstractions/models | 3 | 15 | NAV-09 |

The classification below covers every manifest type. Members not called out for
removal, restriction, annotation, or behavioral correction are proposed stable
with their current role and ownership.

| Public area and types | Proposed classification |
|---|---|
| `NavigationService` | Intentional static application facade; stable after NAV-01, NAV-03, and NAV-11 |
| `PageNavBootstrap`, `PageBuilderConfigurator`, `PageMetadataBuilder`, `PageRuleBuilder<T>` | Intentional registration surface; stable after removing the nonfunctional entry point and correcting composition/validation |
| `NavigationContext` | Intentional runtime state view; public construction is accidental and separately gated by NAV-02 |
| `PageRegistry`, `PageFactory` | Deliberate immutable registry and factory extension seams; frozen implementation remains unchanged |
| `NavigationArgs`, `NavigationTimingContext` | Deliberate request/correlation contracts after NAV-03 removes forged/internal and ineffective factories |
| `PageDescriptor`, `PageDescriptorBuilder`, metadata attributes and enums | Deliberate descriptor configuration after NAV-04/NAV-05 remove accidental construction and inert vocabulary |
| `IGuard`, `GuardContext`, `GuardResult`, built-in guard types and attributes, `IUserContext`, `NavigationSession` | Deliberate authorization extension and built-ins after NAV-06 |
| Page lifecycle/host/visibility/state/background/overlay contracts | Deliberate framework-agnostic page extension surface after NAV-07 nullability cleanup |
| `IPageResources`, `IPageInteraction`, `DefaultUserContext` | Dead or misleading candidate surface; remove before stabilization |
| Platform adapter contracts | Deliberate third-party adapter seam; retain, annotate optional factories, and coordinate concrete adapter impact later |
| `NavigationHistory`, `PageHistoryEntry` | Deliberate read-only consumer observation; mutation/diagnostic helpers are accidental and should close under NAV-08 |
| Dialog, prompt, popover, toast service interfaces and implementations | Deliberate application services; stable with current asymmetric teardown rules under NAV-09 |
| `INavigationSurface`, `INavigationToolkit`, `SurfaceAnchor` | Deliberate opt-in region/toolkit extension; stable without expanding page presentation vocabulary |
| `NavigationEventHub`, event DTOs, `NavigationDiagnostics` | Deliberate read-only observation; framework emission/construction is accidental and should close under NAV-10 |
| `INavigationDiagnosticsSink`, `LoggingNavigationSink` | Superseded public extension seam; application configuration already owns logging integration, so internalize/remove under NAV-10 |
| `InspectionNavigationObserver` | Deliberate passive integration; retain attach/module/disposal surface and add no actions |

## Observed contract and invariants

The following architecture is coherent and should remain stable:

- `PageNavBootstrap.Start()` is the single mount operation and
  `await NavigationService.Shutdown()` is the required completion boundary
  before another mount.
- The static facade is appropriate for the product's unattended, single-window
  application class. A repository-wide DI replacement would not improve the
  ownership model.
- Request admission, reset, and back navigation use the navigation gate.
  Dialog, prompt, and popover operations deliberately marshal to the UI thread
  without taking that gate.
- Guard evaluation remains bounded at 30 seconds. Redirect attempts remain
  children of one request and retain the redirect-cycle/depth protection.
- Lifecycle order, rollback, history commit, background-load ownership, current
  page transitions, and concurrent shutdown behavior are stability-sensitive.
- Toast teardown uses `DismissCurrentToast()`; dialog, prompt, and popover
  teardown use `CloseAll()`. This asymmetry is intentional.
- Logging, Telemetry, and passive Inspection are optional integrations. None is
  an authorization boundary and Navigation must not gain privileged actions.

## Confirmed findings and proposed dispositions

### NAV-01 — Facade and bootstrap entry points

**Observed fact.** `NavigationService` is the only mounted runtime facade and
has focused process-wide lifecycle tests. `PageNavBootstrap.Use<TPlatform>()`
captures an explicit adapter. By contrast, `UseRegistered(object)` creates a
bootstrap with no platform adapter and no registry can supply one; `Start()`
then deterministically throws "No platform adapter registered. Call
Use<TPlatform>(nativeHost)."
([`PageNavBootstrap.cs:83`](../../src/Navigation/NekoLib.Navigation/Bootstrap/PageNavBootstrap.cs#L83),
[`PageNavBootstrap.cs:327`](../../src/Navigation/NekoLib.Navigation/Bootstrap/PageNavBootstrap.cs#L327)).

**Recommended disposition.** Keep the static facade, `Use<TPlatform>`, fluent
configuration, `Start`, `Shutdown`, `ResetAsync`, service access, current page,
history, and event boundaries. Remove `UseRegistered` before the first stable
baseline. Preserve the requirement to await shutdown before remounting.

**Compatibility and migration.** Removing `UseRegistered` is a source and
binary break. Replace it with `PageNavBootstrap.Use<TPlatform>(nativeHost)`.
There is no successful behavior to preserve.

**Rejected alternatives.** A global platform-adapter registry adds process-wide
state solely to make a dead entry point functional. Replacing the facade with a
DI-only host conflicts with the intentional product architecture.

### NAV-02 — `NavigationContext` construction boundary

**Observed fact.** The public `NavigationContext` constructor exposes assembly
composition details (`PageRegistry`, `PageFactory`, host, dispatcher, services,
history, diagnostics, and session). Consumers cannot mount such a context
because the facade context-install operation is internal. Normal contexts are
created only by bootstrap. Public getters remain useful to advanced observers
and services
([`NavigationContext.cs:20`](../../src/Navigation/NekoLib.Navigation/Runtime/Core/NavigationContext.cs#L20)).

**Recommended disposition.** Retain `NavigationContext` and its read-only
properties, but make its constructor internal. This requires an explicit,
small unfreeze: **only change the accessibility of the
`NavigationContext(...)` constructor**. Do not change its fields, ownership,
state transitions, or behavior.

**Compatibility and migration.** This is a source and binary break for direct
construction. Consumers mount through `PageNavBootstrap`; tests use the
assembly's existing internal-access path where necessary.

**Rejected alternatives.** Keeping an unusable public composition root implies
unsupported manual mounting. Making facade context installation public would
open a much larger lifecycle and ownership contract.

### NAV-03 — Request arguments, result, and cancellation

**Observed fact.** The public facade accepts `object` and always wraps it with
`NavigationArgs.Default`. Passing a `NavigationArgs.WithTiming(...)` instance
therefore makes it the payload instead of the request, so the documented timing
correlation is not reachable through the supported facade. `Transient` and
`SwitchTransient` are aliases with no transient behavior. `Preload` and
`Background` request modes are overwritten by descriptor metadata. Public
`Back` lets a caller forge `IsBackNavigation`, which skips normal history
recording and attempts state restoration without a history transition
([`NavigationService.cs:309`](../../src/Navigation/NekoLib.Navigation/NavigationService.cs#L309),
[`NavigationArgs.cs:75`](../../src/Navigation/NekoLib.Navigation/Metadata/NavigationArgs.cs#L75),
[`NavigationArgs.cs:98`](../../src/Navigation/NekoLib.Navigation/Metadata/NavigationArgs.cs#L98)).

The current `Task` return completes normally for success, guard denial, and
redirect, while operational/lifecycle failures throw. A caller cannot
deterministically distinguish the normal outcomes or correlate a global event
under concurrency. `GoBackAsync` already demonstrates that a normal navigation
outcome belongs in the return contract.

**Recommended disposition.** Change the two facade entry points to accept
`NavigationArgs?` directly, with generic and `Type` forms, and return an
immutable `NavigationResult`. The result should report the requested page,
final page when any, success/denial, whether a redirect occurred, and an
optional denial reason; failures continue to throw. Keep `Default`, `Empty`,
`WithTiming`, payload, effective load mode, timing, and the runtime-delivered
back marker. Remove public `Transient`, `Preload`, `Background`, `Back`, and both
`SwitchTransient` aliases. Descriptor metadata remains the sole public owner of
load/reuse behavior.

Do **not** add caller cancellation in F1. Once admitted, a navigation request
crosses UI lifecycle and rollback boundaries where arbitrary cancellation
would create ambiguous partial state. Keep bounded guard evaluation, terminal
shutdown, and exception behavior instead.

Returning a result requires an explicit, narrow unfreeze of
`NavigationRuntime`: **propagate the already-computed internal attempt outcome,
final target, redirect flag, and denial reason through its request completion**.
Do not change gate acquisition, dispatch, guard timing, redirect recursion,
lifecycle order, rollback, history, cache, or teardown.

**Compatibility and migration.** The parameter and return changes are source
and binary breaks, appropriate only before the first stable baseline. Replace
`SwitchPage<T>(payload)` with
`SwitchPage<T>(NavigationArgs.Default(payload))`, inspect the returned result
when outcome matters, configure load/reuse on the descriptor, and call
`GoBackAsync` for back navigation.

**Rejected alternatives.** An overload beside `object?` makes explicit `null`
calls ambiguous and keeps two competing request models. Global events do not
provide call-scoped completion. A `CancellationToken` limited to only one
undocumented stage would be misleading; full cancellation requires a separate
lifecycle design.

### NAV-04 — Registration composition, immutability, and construction

**Observed fact.** `PageMetadataBuilder` stores one configuration delegate per
type and overwrites it on every fluent rule. The documented chain
`.AsIdle().StrongSingleton()` therefore retains only its last action. No current
test covers composition. `PageDescriptorBuilder.Build()` wraps the live tag
list with `AsReadOnly`; a consumer that captures the builder can mutate an
already-built descriptor. Public constructors on `PageMetadataBuilder`,
`PageBuilderConfigurator`, and `PageDescriptorBuilder` create detached builders
that cannot register into a mounted runtime
([`PageMetadataBuilder.cs:77`](../../src/Navigation/NekoLib.Navigation/Bootstrap/PageMetadataBuilder.cs#L77),
[`PageDescriptorBuilder.cs:76`](../../src/Navigation/NekoLib.Navigation/Metadata/PageDescriptorBuilder.cs#L76)).

**Recommended disposition.** Compose repeated rules in declaration order,
defensively copy descriptor collections at build, validate null types/callbacks,
blank names/tags, enum values, and positive timeout bounds, and add regression
tests for fluent composition and post-build mutation. Make the three detached
builder constructors internal while keeping the types and callback members
public. Retain attribute defaults followed by manual override and retain
`PageRegistry.Create(...)` as the immutable registry factory.

**Compatibility and migration.** Correct composition and immutability change
broken behavior. Constructor restriction is a source/binary break for detached
construction; migrate to `PageRegistry.Create(builder => ...)` or bootstrap
configuration callbacks.

**Frozen boundary.** No `PageRegistry` or `PageFactory` source change is needed.

### NAV-05 — Remove inert metadata vocabulary

**Observed fact.** `PagePresentationMode` has only `Replace`, and the runtime
never branches on it; it is copied only into metadata/diagnostics. The active F7
direction explicitly keeps surface regions out of page presentation enums.
`PageRole.TimeoutTarget` is unused; idle timeout behavior is owned by the
`Idle` role and descriptor timeout
([`PagePresentationMode.cs:6`](../../src/Navigation/NekoLib.Navigation/Metadata/PagePresentationMode.cs#L6),
[`PageRole.cs:32`](../../src/Navigation/NekoLib.Navigation/Metadata/PageRole.cs#L32)).

**Recommended disposition.** Remove `PagePresentationMode` and all associated
`Presentation` properties/parameters/diagnostic fields. Remove
`PageRole.TimeoutTarget`. Retain `Normal` and `Idle`, `NavigationLoadMode`,
`PageReusePolicy`, `AllowAnonymous`, tags, guards, timeout, and
`KeepAttachedWhenHidden`.

**Compatibility and migration.** These are source and binary removals. Delete
`Presentation = Replace`; there is no alternative behavior to select. Replace
`TimeoutTarget` declarations with the supported idle-page role and timeout
configuration.

**Rejected alternatives.** Stabilizing a single-value enum reserves a contract
without behavior. Adding modal/overlay/region values would conflate page
navigation with the existing service and surface abstractions.

### NAV-06 — Guards and session

**Observed fact.** `GuardAttribute.RedirectTo` is public on every guard
attribute, but only the role attribute honors it. Authentication and permission
combinators silently ignore a legal named argument; `RequirePermissionAttribute`
instead requires a separate redirect constructor. Parameter arrays and inputs
are not consistently validated or copied. `DefaultUserContext` is unused,
always unauthenticated, and exposes a `UserId` member absent from `IUserContext`
([`GuardAttribute.cs:9`](../../src/Navigation/NekoLib.Navigation/Metadata/Attributes/GuardAttribute.cs#L9),
[`RequireRoleAttribute.cs:18`](../../src/Navigation/NekoLib.Navigation/Metadata/Attributes/RequireRoleAttribute.cs#L18),
[`DefaultUserContext.cs:5`](../../src/Navigation/NekoLib.Navigation/Contracts/Guards/DefaultUserContext.cs#L5)).

**Recommended disposition.** Retain `IGuard`, `IUserContext`, `GuardContext`,
`GuardResult`, `NavigationSession`, guard composition, all built-in guard types,
and all attributes. Make `RedirectTo` consistently effective for every built-in
attribute, add a deny-only one-argument permission attribute constructor,
validate redirect/page/role/permission inputs, and defensively copy role and
permission collections. Remove `DefaultUserContext`. Keep the 30-second guard
bound, exception-to-denial behavior, redirect correlation, cycle/depth limit,
and `AllowAnonymous` bypass.

**Compatibility and migration.** Removing `DefaultUserContext` is a source and
binary break; use `NavigationSession` or an application `IUserContext`.
Previously ignored redirects begin working, which is an intentional behavioral
correction. The permission constructor addition is additive. Invalid values
fail earlier.

**Rejected alternatives.** Removing `RedirectTo` would discard a useful
declarative capability already implemented by role guards. Keeping silently
ignored named arguments is unsafe. Authentication APIs do not belong in
Navigation; the application-owned `IUserContext` boundary remains sufficient.

### NAV-07 — Page and platform extension contracts

**Observed fact.** Page lifecycle, host, visibility, state, background load,
overlay, attach, and unfocus contracts are exercised by the runtime and both
adapters. `IPageResources` and `IPageInteraction` are documented as legacy and
have no runtime or tracked consumer. Platform event subscription is registered
as an optional service and implemented by both adapters even though the core
runtime does not directly consume it. The platform interface documents several
nullable returns but exposes them as non-nullable
([`IPageResources.cs:11`](../../src/Navigation/NekoLib.Navigation/Contracts/Pages/IPageResources.cs#L11),
[`IPageInteraction.cs:8`](../../src/Navigation/NekoLib.Navigation/Contracts/Pages/IPageInteraction.cs#L8),
[`IPlatformAdapter.cs:24`](../../src/Navigation/NekoLib.Navigation/Contracts/Platform/IPlatformAdapter.cs#L24)).

**Recommended disposition.** Retain all exercised page contracts and all
platform adapter contracts. Remove `IPageResources` and `IPageInteraction`.
Retain `IEventSubscriptionAdapter` and `CreateEventSubscriber` as an optional
platform service so the core decision does not preempt the adapter reviews.
Annotate optional interaction blocker, event subscriber, interaction observer,
focus observer, and default loading-mask type returns as nullable; keep host,
dispatcher, and timer factories non-null. Preserve UI-thread lifecycle and
host ownership.

**Compatibility and migration.** Removing the two dead interfaces is a source
and binary break only for direct implementations; remove those interface
declarations from consumer pages. Nullable annotations are binary-neutral but
may produce source warnings in implementations. Concrete adapter signature and
manifest reconciliation belongs to F1-NAV-WF/WPF.

**Rejected alternatives.** Removing event subscription from core now forces a
decision on both adapters out of order. Generalizing `NativeView` or exposing
native UI types in core would break framework independence.

### NAV-08 — History ownership and observation

**Observed fact.** `NavigationService.History` exposes the mutable runtime-owned
`NavigationHistory`. Public `Record`, pop, push, and `Clear` members let a
consumer or page lifecycle callback invalidate the runtime's back/forward
assumptions. The runtime already needs a special expected-entry pop to defend
its own transition. `BackStackSnapshot` and `ForwardStackSnapshot` reverse the
stack while documenting index zero as the top; the existing `HistoryBack` and
`HistoryForward` properties already expose snapshots. `Dump()` produces little
useful information because entries do not override `ToString()`
([`NavigationHistory.cs:24`](../../src/Navigation/NekoLib.Navigation/Runtime/History/NavigationHistory.cs#L24),
[`NavigationHistory.Debug.cs:13`](../../src/Navigation/NekoLib.Navigation/Runtime/History/NavigationHistory.Debug.cs#L13),
[`NavigationHistory.Dump.cs:8`](../../src/Navigation/NekoLib.Navigation/Runtime/History/NavigationHistory.Dump.cs#L8)).

**Recommended disposition.** Keep `NavigationHistory` public as a read-only
view with `CanGoBack`, `CanGoForward`, `HasHistory`, `HistoryBack`, and
`HistoryForward`, returning explicit top-first `IReadOnlyList` snapshots.
Internalize its constructor and all mutation methods. Remove the redundant
snapshot methods and `Dump`. Keep `PageHistoryEntry` as an immutable observation
DTO, but internalize its framework-owned constructor, validate its type/name,
and annotate its captured state as nullable. Navigation mutations remain on
`SwitchPage`, `GoBackAsync`, and `ResetAsync`.

**Compatibility and migration.** Direct history mutation and redundant helper
calls are source/binary breaks. Read the two snapshot properties and invoke the
facade for mutations. This makes runtime ownership enforceable without changing
runtime history order or rollback.

**Frozen boundary.** No lifecycle/history algorithm change in
`NavigationRuntime` is recommended.

### NAV-09 — Overlay services, surfaces, toolkit, and service location

**Observed fact.** Dialog, prompt, popover, toast, surface, and toolkit
contracts are exercised through real runtime/adapters and encode intentional
differences. `ServiceLocator` is locked after bootstrap and provides the
advanced extension/read path. Surface regions are opt-in and separate from page
presentation
([`NavigationRuntime.cs:805`](../../src/Navigation/NekoLib.Navigation/Runtime/Core/NavigationRuntime.cs#L805),
[`NavigationService.cs:366`](../../src/Navigation/NekoLib.Navigation/NavigationService.cs#L366)).

**Recommended disposition.** Retain all five service interfaces, their four
concrete implementations, `ServiceLocator`, `INavigationSurface`,
`INavigationToolkit`, and `SurfaceAnchor`. Retain current dispatcher behavior,
modal blocking where available, prompt default-result shutdown, popover focus
behavior, toast replacement/dismissal, anchored placement, subscriber
isolation, and asymmetric teardown. Correct optional payload/result/service
nullability but add no new service, overlay enum, action, or navigation-gate
coupling.

**Compatibility and migration.** No structural API removal is proposed here.
Nullable annotation changes are binary-neutral and may surface correct compile
warnings. Behavior remains unchanged.

### NAV-10 — Diagnostics, Telemetry, Logging, and passive Inspection

**Observed fact.** Consumers need read-only navigation events, scalar trace
events, timing correlation, and optional Logging/Telemetry/Inspection wiring.
However `NavigationEventHub.Publish`, public diagnostic DTO constructors,
`NavigationDiagnostics` construction and `Emit*`, and the public
`INavigationDiagnosticsSink`/`LoggingNavigationSink` let consumers fabricate
framework-owned navigation evidence. Bootstrap already provides `UseLogging`,
`UseTelemetry`, and passive Inspection configuration
([`NavigationEventHub.cs:49`](../../src/Navigation/NekoLib.Navigation/Diagnostics/NavigationEventHub.cs#L49),
[`NavigationDiagnostics.cs:126`](../../src/Navigation/NekoLib.Navigation/Diagnostics/NavigationDiagnostics.cs#L126),
[`INavigationDiagnosticsSink.cs:8`](../../src/Navigation/NekoLib.Navigation/Diagnostics/INavigationDiagnosticsSink.cs#L8)).

**Recommended disposition.** Retain the event hub and its subscription events,
read-only DTO properties, `NavigationDiagnostics.Hub`,
`NavigationTimingContext`, `InspectionNavigationObserver.Attach`, its passive
module factory, and deterministic disposal. Internalize hub publication,
diagnostic/DTO construction and emission. Internalize or remove
`INavigationDiagnosticsSink` and `LoggingNavigationSink`; keep `UseLogging` as
the supported application integration. Preserve subscriber isolation, bounded
passive snapshots, correlation, and the distinction between `page_ready` and
first paint. Add no action registration or privileged diagnostic control.

**Compatibility and migration.** Directly fabricated events/sinks are
source/binary breaks; subscribe to `NavigationService.Events` and configure
integrations at bootstrap. Custom logging can subscribe to read-only events or
use the Core logging contract through the supported configuration path.

**Rejected alternatives.** A public emitter makes framework evidence
untrustworthy. Adding Inspection actions would violate the active passive-only
boundary and is unrelated to public observation.

### NAV-11 — Public nullability must match runtime truth

**Observed fact.** The project enables nullable analysis, but much of the
candidate surface predates annotations. Examples include a non-nullable
`NavigationService.Current` before first navigation and after reset/shutdown;
nullable `from` and current-page event arguments; optional payload, state,
reason, redirect, metadata name/tags, service resolution, and platform factory
results; and page state capture/restore that explicitly documents null. A clean
dual-target rebuild currently succeeds with 198 warning occurrences and no
errors, dominated by `CS8618` and `CS8625`. These are baseline warnings, not
introduced by this review
([`NavigationService.cs:36`](../../src/Navigation/NekoLib.Navigation/NavigationService.cs#L36),
[`IPageStateful.cs:20`](../../src/Navigation/NekoLib.Navigation/Contracts/Pages/IPageStateful.cs#L20)).

**Recommended disposition.** Correct public annotations to observed runtime
behavior across the accepted surface. Mark actual optional values nullable,
validate required constructor/configuration inputs, use nullable `out` values on
failed `Try*` operations, and keep required host/dispatcher/timer/page/type/name
values non-null. This is a contract correction, not a request to suppress or
bulk-fix every internal warning. Add compile-time contract assertions or
targeted tests where behavior is not executable.

**Compatibility and migration.** Annotation-only changes are binary-neutral but
source-visible to nullable-enabled consumers. Consumers add null handling where
the runtime has always allowed absence. Tightened required inputs fail earlier
instead of failing later or producing corrupt metadata.

**Rejected alternatives.** Disabling nullable or adding suppression preserves
false contracts. Treating all references as nullable would discard useful
requirements and hide real mistakes.

### NAV-12 — Target parity, adapter sequence, and recorded compatibility

**Observed fact.** Core target manifests are surface-identical and the shallow
project graph is intentional. The historical adapter work already landed three
consumer-visible changes: removal of dead WPF `InteractionObserver` and
`EventSubscriptionAdapter` types, WinForms `IPageView.Name` fallback changing
from full to simple type name, and virtual `Dispose` on four WPF surface bases.
Those changes are visible in current source/manifests but have not yet passed
the F1 adapter decision/migration gate
([`navigation-adapter-review-2026-08-03.md:216`](navigation-adapter-review-2026-08-03.md#L216)).

**Recommended disposition.** Keep the core targets, dependencies, constants,
and target-parallel public surface. Record the three historical adapter changes
as inputs to F1-NAV-WF/WPF, where their concrete types, migration guidance,
changelog, and manifests must be explicitly accepted. Do not finalize or alter
adapter API in F1-NAV. After accepted core implementation, build both adapters
against the core contract before starting their reviews.

**Compatibility and migration.** No core API change follows from this item.
The later adapter blocks must document that direct WPF consumers recompile away
from the removed dead types, WinForms fallback names are simple type names, and
WPF surface subclasses recompile against virtual disposal.

**Rejected alternatives.** Folding adapter finalization into this review would
violate the requested order and obscure whether a change belongs to the core
extension contract or a concrete UI implementation.

## Exact frozen-type impact

Nothing in this review unfreezes a type. If the corresponding dispositions are
approved, the smallest necessary unfreezes are:

| Frozen type | Exact permitted change | Explicitly not permitted |
|---|---|---|
| `NavigationContext` | NAV-02: constructor accessibility from public to internal | fields, property meaning, ownership, lifecycle, mounting, or state behavior |
| `NavigationRuntime` | NAV-03: carry the already-computed request result/reason to facade completion | gate, dispatch, guard timeout, redirect recursion, lifecycle order, rollback, history, caching, surfaces, background work, reset, or shutdown behavior |
| `PageRegistry` | none | any source change |
| `PageFactory` | none | any source change |

Approval of another item does not imply approval of either unfreeze.

## Consolidated decision gate

Nothing below is accepted or scheduled by this review.

| # | Decision requested | Recommended disposition |
|---:|---|---|
| 1 | Facade/bootstrap | Keep the static facade and lifecycle; remove nonfunctional `UseRegistered` |
| 2 | Context construction | Internalize only the `NavigationContext` constructor under a separate narrow unfreeze |
| 3 | Requests/outcomes | Accept `NavigationArgs` directly, return `NavigationResult`, remove misleading/internal factories and transient aliases, reject caller cancellation; narrowly unfreeze result propagation only |
| 4 | Registration | Compose fluent rules, copy collections, validate deterministically, and internalize detached builder constructors |
| 5 | Metadata vocabulary | Remove single-value presentation metadata and unused `TimeoutTarget` |
| 6 | Guards/session | Retain extension model, make redirects consistent, validate/copy inputs, and remove `DefaultUserContext` |
| 7 | Page/platform contracts | Retain exercised contracts and optional event subscription, remove two dead page interfaces, and annotate optional factories |
| 8 | History | Expose read-only snapshots only; internalize mutation and remove redundant/debug helpers |
| 9 | Services/surfaces | Retain existing services, locator, surfaces, toolkit, UI rules, and asymmetric teardown |
| 10 | Observability | Retain read-only events/timing/passive Inspection; close public emission and superseded sink construction |
| 11 | Nullability | Align public annotations and validation with observed runtime truth without suppressing warnings |
| 12 | Targets/adapters | Keep targets/dependencies; defer adapter finalization while carrying forward the three recorded compatibility changes |

The recommended direction is to accept all twelve as one coherent core
baseline, while treating items 2 and 3 as separately visible frozen-type
unfreezes. Items 3, 4, and 8 correct the highest consumer risks: unobservable
navigation outcomes, lost registration rules, and externally mutable runtime
history.

## Implementation sequence if approved

Approval should promote only the accepted numbered items to `TODO.md`. A small,
reviewable implementation order would be:

1. request/result contract plus the two explicitly bounded frozen-type edits;
2. registration, descriptor, metadata, guard, and history corrections;
3. dead/accidental surface removal and nullability alignment;
4. diagnostics emission boundary and documentation;
5. focused dual-target tests, current Navigation technical reference,
   changelog, migration guide, and reviewed core manifests; and
6. clean core/adapter/test verification before opening F1-NAV-WF.

The API baseline must be updated only after accepted implementation, tests,
consumer migration guidance, and changelog are present. F1-NAV-WF and
F1-NAV-WPF remain separate later gates.

## Validation

Executed on Windows without `-UpdateBaseline` and without launching runtime
applications:

| Command | Result |
|---|---|
| baseline Git checks | exact expected branch/HEAD; clean worktree/index; 117 ahead of `origin/master`; 51 ahead of upstream |
| core `Release` rebuild for both target frameworks | succeeded; 198 pre-existing warning occurrences, 0 errors |
| compiled manifest comparison | core manifests identical except target metadata; all six approved manifests read |
| `dotnet build src/Navigation/NekoLib.Navigation/NekoLib.Navigation.csproj -c Release -m:1` | both targets succeeded; incremental build emitted 0 warnings and 0 errors |
| `dotnet build src/Navigation/NekoLib.Navigation.WinForms/NekoLib.Navigation.WinForms.csproj -c Release -m:1` | both targets succeeded; 80 pre-existing nullable warning occurrences, 0 errors |
| `dotnet build src/Navigation/NekoLib.Navigation.Wpf/NekoLib.Navigation.Wpf.csproj -c Release -m:1` | both targets succeeded; 40 pre-existing nullable warning occurrences, 0 errors |
| focused Navigation tests, `net481` | **278 passed**, 0 failed, 0 skipped |
| focused Navigation tests, `net9.0-windows` | **278 passed**, 0 failed, 0 skipped |
| `verify-public-api.ps1` scoped separately to core, WinForms, and WPF | all six target baselines verified; no baseline updated |
| `verify-docs.ps1` | passed; the new audit used a temporary intent-to-add index entry because the verifier rejects links to wholly untracked files; the entry was removed afterward |
| `git diff --check` | passed |

No package or runtime evidence is part of this review.

## Residual limitations and validation gaps

- Focused automated tests exercise the current contract, not the proposed API
  changes; accepted fixes would require new regressions and migrated tests.
- No interactive WinForms/WPF prompt-close, focus, designer, or native-host
  scenario ran.
- No long-running, recovery, memory-growth, or shutdown-under-native-message-
  pump scenario ran.
- No package or PackageReference consumer was produced, so future asset and
  dependency expectations are not package evidence.
- No external consumer inventory exists beyond tracked repository consumers.
- The core nullability correction will require adapter compilation and may
  reveal adapter-specific signature warnings that belong to the later reviews.
- The three historical adapter changes are not accepted by this core review;
  they remain explicit inputs to F1-NAV-WF/WPF.

## Review-only declaration

F1-NAV remains open. This review produced only this audit and minimal current
index/TODO references. It implemented no product or test change, accepted no
proposal, changed no approved API baseline, created no migration or changelog
entry, built no package, launched no runtime scenario, committed nothing,
published nothing, and pushed nothing.

## Implementation reconciliation — 2026-08-20

After explicit acceptance of all twelve dispositions, the implementation was
completed in the working tree based on reference commit
`9706a2c165d3bc4bcfac810319a829f42845eb95`. The review-only declaration above
remains the truth for the original review turn; this section records the later
accepted implementation.

### Implemented dispositions

1. The intentional static facade remains. `SwitchPage` accepts
   `NavigationArgs?` and returns a call-scoped `NavigationResult` for success,
   denial, and redirect; ineffective request factories, transient aliases, and
   forged public back requests were removed.
2. Only the accepted frozen-type edits were made: the `NavigationContext`
   constructor is framework-owned, and `NavigationRuntime` propagates the
   result without changing lifecycle order, gate ownership, rollback, caching,
   or teardown. `PageRegistry` and `PageFactory` have no source diff.
3. Registration rules compose in declaration order; detached builder
   construction is internal; descriptor/session/guard collections are copied
   and their public inputs are validated.
4. Inert presentation and timeout-target metadata, `UseRegistered`,
   `DefaultUserContext`, `IPageResources`, and `IPageInteraction` were removed.
5. Every built-in guard attribute honors `RedirectTo`; deny-only permission
   construction is available. Public `GuardAttribute.CreateGuard()` remains so
   tracked and external consumers can define custom guard attributes.
6. History exposes top-first read-only snapshots and framework-owned mutation.
   Diagnostics and passive Inspection remain observable, while framework event
   construction, publication, emitters, and sinks are internal.
7. Optional payload, state, result, current-page/event, and platform-factory
   nullability now matches runtime truth. Concrete adapter surfaces retain
   their reviewed public manifests through explicit interface forwarding.
8. The current technical reference, changelog, migration guide, roadmap, audit
   index, and compiled core manifests were reconciled to the accepted contract.

### Implementation validation

Executed serially on Windows from the implementation working tree:

| Command | Result |
|---|---|
| focused Navigation tests | **290 passed** on `net481` and **290 passed** on `net9.0-windows`; 0 failed, 0 skipped, 0 emitted warning lines |
| `dotnet test NekoLib.sln -c Release -m:1 --no-build` | **1,666 passed**, 0 failed, 0 skipped |
| `dotnet build NekoLib.sln -c Release -t:Rebuild -m:1 --no-restore` | succeeded; 210 existing warning occurrences, 0 errors; no new normalized warning identity |
| all six tracked Navigation runtime-scenario projects | built successfully; build-only, never launched |
| core `verify-public-api.ps1` update and verification | accepted `net481` and `net9.0` core baselines updated and verified |
| WinForms and WPF `verify-public-api.ps1` | all four adapter baselines verified unchanged |
| `verify-docs.ps1 -BuildLogPath artifacts\f1-nav-solution-rebuild.log` | passed; 100 baseline warning identities were not emitted |
| `git diff --check` | passed |

### Residual validation limits

- No WinForms/WPF application, interactive prompt-close/focus/designer probe,
  long-running campaign, or recovery scenario was launched.
- No package, PackageReference consumer, publish, or immutable package evidence
  was produced.
- F1-NAV-WF and F1-NAV-WPF remain separate public-surface reviews; this core
  closure verified that their existing concrete manifests did not change.
- No commit or push was created by this implementation reconciliation.
