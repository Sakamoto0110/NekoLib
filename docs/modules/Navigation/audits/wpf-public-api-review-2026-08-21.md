# Navigation.Wpf Public API Review

**Document ID:** NAV-AUDIT-WPF-PUBLIC-API-20260821

**Schema version:** 1

**Kind:** audit

**Lifecycle:** historical

**Subject:** F1-NAV-WPF code-first public API finalization review

**Surface:** audit

**Boundary:** navigation

**Authority role:** evidence

**Mutation:** snapshot

**Indexing:** include

**Reference date:** 2026-08-21

**Reference commit:** `aefd2b8985f626abe1a02e78094bf48cfdf6494e`

**Original path:** docs/audit/navigation-wpf-public-api-review-2026-08-21.md

**Last reconciliation:** 2026-08-21

**Current state:** all six dispositions explicitly accepted and implemented

## Outcome

`NekoLib.Navigation.Wpf` has a coherent adapter boundary and should retain its
current factory, native host, view-base, interaction, surface, and toolkit type
families. The two dead duplicate adapter types removed in NAV-008 and virtual
disposal added to the four surface bases in NAV-009 are correct pre-stable
changes and should be accepted.

The package is not ready for a no-change stable baseline. As in WinForms, explicit
interface forwarding preserved the existing manifest after core F1, but several
public events and consumer override hooks still declare runtime-null values as
non-nullable. That public/protected correction requires approval. This review
therefore stops at the disposition gate and changes no product code, approved
manifest, migration guide, changelog, roadmap, or current technical reference.

## Baseline and scope

- Branch: `phase-e/sqlserver-and-orchestration`.
- Reviewed state: clean `HEAD` at the reference commit, immediately after the
  isolated F1-NAV core checkpoint.
- Project: `src/Navigation/NekoLib.Navigation.Wpf/`, targeting `net481` and
  `net9.0-windows`, with nullable analysis enabled and one project reference to
  `NekoLib.Navigation`.
- API oracle: both approved manifests under
  `eng/public-api/NekoLib.Navigation.Wpf/`.
- Evidence: every adapter source file, both manifests, the Navigation technical
  reference, the F1 release policy, focused Navigation tests, the 2026-08-03
  adapter review, and the 2026-08-06 design-time review.
- Excluded: core Navigation API changes, frozen core implementation, runtime
  scenarios, package production, PackageReference consumers, interactive UI,
  publish, and push.

The two target manifests contain the same declared API after removing normal
target-framework and Windows-platform assembly attributes. The `net481` oracle
contains 17 public types and 98 declared public/protected members: 83 public and
15 protected. Inherited WPF members are outside the generated declaration count,
but the `FrameworkElement.Name` fallback is reconciled below.

## Complete declared-surface inventory

The classification applies to each member named in the row. A proposed correction
is not accepted merely because it appears here.

| Type | Declared public/protected surface | Classification |
|---|---|---|
| `WpfEventDispatcherAdapter` | `WpfEventDispatcherAdapter(Dispatcher)`, `Invoke(Action)`, `BeginInvoke(Action)` | Retain. Record the dispatcher-shutdown inline fallback as an intentional teardown policy. |
| `WpfEventSubscriptionAdapter` | public constructor, `Attach<THandler>`, `Detach<THandler>` | Retain as the one stable WPF event-subscription implementation. |
| `WpfFocusObserverAdapter` | public constructor, `Track(object, Action)` | Retain as the stable keyboard-focus/window-deactivation observer. |
| `WpfInteractionBlocker` | constructor, `Block`, `Unblock`, `OnViewAdded`, `OnViewRemoved` | Retain. `SetCurrentValue` and binding re-evaluation preserve application bindings. |
| `WpfInteractionObserver` | constructor, `InteractionDetected`, `Dispose` | Retain the type and lifecycle; correct the event annotation to nullable. |
| `WpfPlatformAdapter` | public constructor, `CanHandle`, `CreateHost`, `CreateEventDispatcher`, `CreateEventSubscriber`, `CreateInteractionBlocker`, `CreateTimerAdapter`, `CreateInteractionObserverAdapter`, `CreateFocusObserver`, `GetDefaultLoadingMaskType` | Retain as the stable `PageNavBootstrap.Use<TPlatform>()` entry adapter. Its concrete factories always return implementations even where the core interface permits null. |
| `WpfTimerAdapter` | `WpfTimerAdapter(int intervalMillis = 15000)`, `IntervalMilliseconds`, `Tick`, `Start`, `Stop`, `Dispose` | Retain behavior and spelling; correct only the nullable event annotation. |
| `DefaultLoadingMask` | constructor, `NativeView`, `IsDisposed`, `Dispose`, `OnOverlayOpenedAsync(object)`, `OnOverlayClosingAsync()` | Retain the default mask; correct the public opened-payload annotation. |
| `AutoDismissPopoverBase` | public constructor, virtual `OnUnfocusAsync()` | Retain as the opt-in light-dismiss base. |
| `DialogViewBase` | protected constructor; `NativeView`, `IsDisposed`, `DesignMode`; protected `Confirm`, `Cancel`, `OnShownAsync(object)`; public virtual `Dispose()` | Retain the designer, completion, and virtual disposal seams; correct the protected payload annotation. |
| `PageView` | protected constructor; `NativeView`, `IsDisposed`, `DesignMode`, virtual `AllowBackNavigation`; virtual `OnNavigatedToAsync`, `OnNavigatedFromAsync`, `ShowPage`, `HidePage`, `Dispose` | Retain. `AllowBackNavigation` is deliberately inert compatibility surface and must not be interpreted as history policy. |
| `PopoverViewBase` | protected constructor; `NativeView`, `IsDisposed`, `DesignMode`; protected `Complete`, `OnShownAsync(object)`; public virtual `Dispose()` | Retain placement, completion, designer, and disposal seams; correct the protected payload annotation. |
| `PromptViewBase<TResult>` | protected constructor; `NativeView`, `IsDisposed`, `DesignMode`; protected `CompletePrompt(TResult)`, `OnShownAsync(object)`; public virtual `Dispose()` | Retain the typed prompt base; correct result and payload annotations. |
| `ToastViewBase` | protected constructor; `NativeView`, `IsDisposed`, `DesignMode`; protected `Dismiss`, `OnShown(object)`; public virtual `Dispose()` | Retain the anchored toast and disposal extension seams; correct the protected payload annotation. |
| `WpfLayeredPageHostBase` | constructor, protected `Root`, `Surface`, `FocusSurface`, and virtual `Attach`, `Detach`, both `BringToFront` overloads, `AddView`, `RemoveView`, `Focus` | Retain as the advanced host/customization seam. Page ordering remains below the overlay band. |
| `WpfNavigationSurface` | constructor, `ClientBounds`, `Scale`, `IsActive`, `ResolveAnchor` | Retain as a read-only, side-effect-free surface implementation. |
| `WpfNavigationToolkit` | constructor, `Surface`, `FocusSurface` | Retain as the concrete toolkit implementation. |

## Ownership, lifecycle, and extension conclusions

- `WpfPlatformAdapter` owns factory composition, while the created host, timer,
  observers, blocker, and default loading mask retain their existing independent
  lifetimes. No global adapter registry or new static state is justified.
- The layered host owns native attachment, focus admission, and page/overlay
  z-order. Modal blocking preserves `IsEnabled` bindings and restores the newest
  source value.
- Dialog, prompt, popover, and toast remain concrete designer-loadable bases with
  protected constructors. Their public virtual `Dispose()` methods are intentional
  extension seams; overrides must call `base.Dispose()`.
- The concrete implementation types remain useful for direct host composition,
  focused testing, and custom bootstrap integrations. Internalizing the complete
  adapter family is not recommended.

## Confirmed findings

### F1-NAV-WPF-01 - remaining public/protected nullability is not runtime-truthful

The core F1 contract accepts nullable overlay payloads, nullable prompt results,
and nullable event subscription state. The WPF adapter preserves its old manifest
through explicit interface forwarders and passes nullable payloads into
non-nullable public/protected members with the null-forgiving operator. A derived
surface can therefore receive `null` through an override whose annotation says it
cannot.

The affected declared surface is:

- `WpfInteractionObserver.InteractionDetected` and `WpfTimerAdapter.Tick`;
- `DefaultLoadingMask.OnOverlayOpenedAsync(object)`;
- protected `OnShownAsync(object)` on dialog, prompt, and popover;
- protected `ToastViewBase.OnShown(object)`;
- protected `PromptViewBase<TResult>.CompletePrompt(TResult)`.

The event declarations also account for existing `CS8618` warnings. Correcting
these annotations is binary-neutral but source-visible to nullable-enabled
consumers. No runtime behavior should change.

### F1-NAV-WPF-02 - shutdown dispatch has a deliberate platform-specific fallback

Once the WPF `Dispatcher` reports shutdown, both `Invoke` and `BeginInvoke` run
the action inline from the caller. The corrected WinForms adapter instead throws
when it cannot reach a handle and the caller is not its owning UI thread. The core
runtime already treats unreachable-UI teardown as a best-effort inline cleanup
boundary, and the facade cutoff prevents a new normal navigation request after
shutdown has taken ownership.

Retain the WPF fallback rather than introduce a behavioral correction in F1, but
document the platform difference explicitly. It is teardown evidence, not a claim
that normal page lifecycle may run on arbitrary threads.

## Historical compatibility reconciliation

### Removed duplicate WPF adapter types

NAV-008(c) removed public `InteractionObserver` and
`EventSubscriptionAdapter`. They duplicated `WpfInteractionObserver` and
`WpfEventSubscriptionAdapter`, while `WpfPlatformAdapter` produced only the
prefixed implementations. The removed types remain absent from current source and
both approved manifests.

Accept the removal as a pre-stable source/binary break. Direct consumers migrate
to `WpfInteractionObserver` and `WpfEventSubscriptionAdapter`; normal bootstrap
consumers require no code change.

### Virtual surface disposal

NAV-009(b) made `Dispose()` virtual on `DialogViewBase`,
`PromptViewBase<TResult>`, `PopoverViewBase`, and `ToastViewBase`. `PageView`
already had virtual disposal. The change is source-compatible and
binary-breaking for an already compiled derived assembly under the repository's
public API policy.

Accept the virtual methods as stable extension seams. Derived consumer assemblies
must be recompiled, and overrides must call `base.Dispose()` so callbacks and
input subscriptions are released and `IsDisposed` becomes true.

The simple `GetType().Name` fallback is retained across `PageView` and the four
surface bases. WPF names cannot use namespace-qualified type names. The registered
descriptor name remains authoritative.

The design-time changes recorded on 2026-08-06 are also retained: the surface
bases stay non-abstract with protected constructors. The generic prompt base may
still require a non-generic consumer shim for designer tooling; no public API
change is proposed for that limitation.

## Numbered dispositions requiring acceptance

1. **Retain the adapter family and extension structure.** Stabilize all listed
   types and members except the nullability correction in disposition 4. Preserve
   host ownership, focus behavior, z-order, modal blocking, timer ownership,
   designer loadability, and teardown.
2. **Accept the historical duplicate-type removals.** Keep `InteractionObserver`
   and `EventSubscriptionAdapter` removed and publish the two prefixed replacements
   in migration guidance.
3. **Accept virtual surface disposal.** Stabilize virtual `Dispose()` on dialog,
   prompt, popover, and toast; require recompilation of existing derived binaries.
4. **Correct nullable adapter annotations.** Make the two events nullable, make
   the default-mask and protected shown-payload parameters nullable, and allow a
   nullable typed prompt completion. Do not change behavior.
5. **Retain and document shutdown dispatch.** Keep the WPF inline fallback after
   dispatcher shutdown as teardown policy; document its difference from the
   WinForms handle-less rule.
6. **Retain the inert compatibility property.** Keep
   `PageView.AllowBackNavigation` but continue documenting that the runtime does
   not consult it and history policy must not depend on it.

Disposition 4 alters the public/protected manifest and requires explicit approval
before implementation. Consequently the F1-NAV-WPF baseline,
changelog/migration closure, roadmap completion, and release gate remain open.

## Review-stage validation and limits

- The shared Navigation suite passed 290/290 on `net481` and 290/290 on
  `net9.0-windows` immediately before the reference commit.
- Both WPF API manifests verified unchanged during the F1-NAV checkpoint.
- This review did not update a manifest and did not run an adapter closure build,
  runtime scenario, package flow, PackageReference consumer, or interactive UI
  procedure.
- The historical virtual-disposal tests cover representative dialog and toast
  overrides. The other two methods are verified by the assembly manifests and
  compilation, not separate behavioral tests.

## 2026-08-21 acceptance and implementation reconciliation

The user explicitly accepted all six dispositions, with the requirement that
contract changes be recorded. Implementation commit
`c5974fd93e8ca0b02292a6b34743293722430bc3` made the approved annotation
corrections without changing core Navigation or its frozen runtime components.

The assembly-derived manifests for both targets record the complete new delta:

- `InteractionDetected` and `Tick` are nullable events;
- `DefaultLoadingMask.OnOverlayOpenedAsync`, all protected shown hooks, and
  `CompletePrompt` declare their nullable runtime values.

The prefixed replacements for the removed duplicate types, virtual surface
disposal, simple type-name fallback, inert `PageView.AllowBackNavigation`,
adapter family, native ownership, focus model, z-order, timer behavior, and
dispatcher-shutdown teardown policy were retained. The migration guide records
the two replacement types, recompilation and `base.Dispose()` requirements, and
the intentional dispatcher difference. The changelog classifies these changes
for the coordinated first `1.0.0` family candidate.

Implementation validation completed before this reconciliation:

- explicit Release rebuilds passed for `net481` and `net9.0-windows` with 53
  and 57 warning occurrences respectively, and no errors;
- Navigation tests passed 292/292 on each target family;
- both WPF manifests and both unchanged core manifests verified;
- the tracked LongRunning WPF consumer built on `net9.0-windows` but was not
  launched;
- documentation verification passed against the adapter rebuild log with no new
  normalized warning identity; 114 repository-baseline identities were not
  emitted by this focused build, and `git diff --check` passed;
- no package or PackageReference campaign ran.
