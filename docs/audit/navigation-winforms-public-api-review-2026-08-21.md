# Navigation.WinForms Public API Review

**Kind:** audit

**Lifecycle:** historical

**Subject:** F1-NAV-WF code-first public API finalization review

**Reference date:** 2026-08-21

**Reference commit:** `aefd2b8985f626abe1a02e78094bf48cfdf6494e`

**Last reconciliation:** 2026-08-21

**Current state:** all six dispositions explicitly accepted and implemented

## Outcome

`NekoLib.Navigation.WinForms` is structurally coherent and its host, dispatcher,
timer, interaction, surface-base, layered-host, and toolkit ownership boundaries
should remain. It is not ready for a no-change stable baseline, however. The
compiled surface still exposes nullable runtime values as non-nullable in public
events and consumer override hooks, the timer constructor publishes a misspelled
named-argument contract, and the interaction blocker exposes an unused native-host
conversion that is unrelated to its abstraction.

Those are public API decisions. This review therefore stops at the disposition
gate: no product code, approved manifest, migration guide, changelog, roadmap, or
current technical documentation was changed.

## Baseline and scope

- Branch: `phase-e/sqlserver-and-orchestration`.
- Reviewed state: clean `HEAD` at the reference commit, immediately after the
  isolated F1-NAV core checkpoint.
- Project: `src/Navigation/NekoLib.Navigation.WinForms/`, targeting `net481` and
  `net9.0-windows`, with nullable analysis enabled and one project reference to
  `NekoLib.Navigation`.
- API oracle: both approved manifests under
  `eng/public-api/NekoLib.Navigation.WinForms/`.
- Evidence: every adapter source file, both manifests, the Navigation technical
  reference, the F1 release policy, focused Navigation tests, the 2026-08-03
  adapter review, and the 2026-08-06 design-time review.
- Excluded: core Navigation API changes, frozen core implementation, runtime
  scenarios, package production, PackageReference consumers, interactive UI,
  publish, and push.

The two target manifests contain the same declared API after removing normal
target-framework and Windows-platform assembly attributes. The `net481` oracle
contains 17 public types and 104 declared public/protected members: 78 public and
26 protected. Inherited `System.Windows.Forms` members are outside the generated
declaration count, but behavior attached to inherited `Control.Name` is reconciled
separately below.

## Complete declared-surface inventory

The classification applies to each member named in the row. A proposed correction
is not accepted merely because it appears here.

| Type | Declared public/protected surface | Classification |
|---|---|---|
| `WinFormsEventDispatcherAdapter` | `WinFormsEventDispatcherAdapter(Control)`, `Invoke(Action)`, `BeginInvoke(Action)` | Retain as stable platform dispatch implementation. The handle-less owner-thread rule is intentional. |
| `WinFormsEventSubscriptionAdapter` | public constructor, `Attach<THandler>`, `Detach<THandler>` | Retain as stable optional event-subscription implementation. |
| `WinFormsFocusObserverAdapter` | public constructor, `Track(object, Action)` | Retain as stable focus-loss adapter; an unsupported native view returns an inert subscription. |
| `WinFormsInteractionBlocker` | constructor, `Block`, `Unblock`, `OnViewAdded`, `OnViewRemoved`, explicit conversion to `Control` | Retain the blocker lifecycle; propose removing only the native-host conversion. |
| `WinFormsInteractionObserver` | constructor, `InteractionDetected`, `Dispose` | Retain the type and lifecycle; correct the event annotation to nullable. |
| `WinFormsPlatformAdapter` | public constructor, `CanHandle`, `CreateHost`, `CreateEventDispatcher`, `CreateEventSubscriber`, `CreateInteractionBlocker`, `CreateTimerAdapter`, `CreateInteractionObserverAdapter`, `CreateFocusObserver`, `GetDefaultLoadingMaskType` | Retain as the stable `PageNavBootstrap.Use<TPlatform>()` entry adapter. Its concrete factories always return implementations even where the core interface permits null. |
| `WinFormsTimerAdapter` | `WinFormsTimerAdapter(int intervalMilis = 15000)`, `IntervalMilliseconds`, `Tick`, `Start`, `Stop`, `Dispose` | Retain the timer behavior; correct the parameter spelling and nullable event annotation before baseline. |
| `DefaultLoadingMask` | constructor, `NativeView`, `IsDisposed`, `OnOverlayOpenedAsync(object)`, `OnOverlayClosingAsync()` | Retain the default mask; correct the public opened-payload annotation. |
| `AutoDismissPopoverBase` | public constructor, virtual `OnUnfocusAsync()` | Retain as the opt-in light-dismiss base. |
| `DialogViewBase` | protected constructor; `NativeView`, `IsDisposed`, `DesignMode`; protected `Confirm`, `Cancel`, `OnShownAsync(object)`, `OnHandleCreated`, `Dispose(bool)` | Retain the designer and completion extension seams; correct the protected payload annotation. |
| `PageView` | protected constructor; `NativeView`, `IsDisposed`, `DesignMode`, virtual `AllowBackNavigation`; virtual `OnNavigatedToAsync`, `OnNavigatedFromAsync`, `ShowPage`, `HidePage`; protected `Dispose(bool)` | Retain. `AllowBackNavigation` is deliberately inert compatibility surface and must not be interpreted as history policy. |
| `PopoverViewBase` | protected constructor; `NativeView`, `IsDisposed`, `DesignMode`; protected `Complete`, `OnShownAsync(object)`, `OnHandleCreated`, `Dispose(bool)` | Retain placement, completion, designer, and disposal seams; correct the protected payload annotation. |
| `PromptViewBase<TResult>` | protected constructor; `NativeView`, `IsDisposed`, `DesignMode`; protected `CompletePrompt(TResult)`, `OnShownAsync(object)`, `OnHandleCreated`, `Dispose(bool)` | Retain the typed prompt base; correct result and payload annotations. |
| `ToastViewBase` | protected constructor; `NativeView`, `IsDisposed`, `DesignMode`; protected virtual `AnchorInset`, `ApplyDefaultAnchor`, `OnShown(object)`; protected `Dismiss`, `OnParentChanged`, `Dispose(bool)` | Retain the anchored toast extension surface; correct the protected payload annotation. |
| `WinFormsLayeredPageHostBase` | constructor, protected `Root`, `Surface`, `FocusSurface`, and virtual `Attach`, `Detach`, both `BringToFront` overloads, `AddView`, `RemoveView`, `Focus` | Retain as the advanced host/customization seam. Page and overlay z-order ownership remains separate. |
| `WinFormsNavigationSurface` | constructor, `ClientBounds`, `Scale`, `IsActive`, `ResolveAnchor` | Retain as a read-only surface implementation. The `DeviceDpi` read is side-effect-free. |
| `WinFormsNavigationToolkit` | constructor, `Surface`, `FocusSurface` | Retain as the concrete toolkit implementation. |

## Ownership, lifecycle, and extension conclusions

- `WinFormsPlatformAdapter` owns factory composition, while the created host,
  timer, observers, blocker, and default loading mask retain their existing
  independent lifetimes. No new global or static adapter state is justified.
- The layered host remains the owner of native attachment and z-order. Page views
  stay below dialog, prompt, popover, toast, and loading-mask surfaces.
- The dispatcher captures the constructing UI thread because
  `Control.InvokeRequired` is not authoritative before handle creation. A worker
  call without a reachable handle fails instead of running UI lifecycle work on
  the worker.
- The four surface bases and `PageView` remain concrete, designer-loadable base
  classes with protected constructors. WinForms disposal remains extensible through
  `Dispose(bool)`.
- The concrete adapter types remain useful for direct host composition, focused
  testing, and custom bootstrap integrations. Internalizing the whole implementation
  family would remove legitimate adapter-package value.

## Confirmed findings

### F1-NAV-WF-01 - remaining public/protected nullability is not runtime-truthful

The core F1 contract now accepts nullable overlay payloads, nullable prompt
results, and nullable event subscription state. The WinForms implementation uses
explicit interface forwarders so those corrected core contracts compile without
changing this package's manifest. The forwarders then pass nullable payloads into
non-nullable public/protected members with the null-forgiving operator. A derived
surface can therefore receive `null` at runtime through an override whose declared
contract says it cannot.

The affected declared surface is:

- `WinFormsInteractionObserver.InteractionDetected` and
  `WinFormsTimerAdapter.Tick`;
- `DefaultLoadingMask.OnOverlayOpenedAsync(object)`;
- protected `OnShownAsync(object)` on dialog, prompt, and popover;
- protected `ToastViewBase.OnShown(object)`;
- protected `PromptViewBase<TResult>.CompletePrompt(TResult)`.

The event declarations also account for existing `CS8618` warnings. Correcting
these annotations is binary-neutral but source-visible to nullable-enabled
consumers. No runtime behavior should change.

### F1-NAV-WF-02 - timer named-argument contract contains a spelling error

`WinFormsTimerAdapter(int intervalMilis = 15000)` publishes `intervalMilis`, while
the WPF counterpart and the property use `intervalMillis`/`IntervalMilliseconds`.
Parameter names are source API for named arguments. Stabilizing the typo would
make a later correction unnecessarily breaking.

Rename only the parameter to `intervalMillis`. This is binary-neutral and source
compatible for positional calls, but source-breaking for a consumer that used the
misspelled named argument.

### F1-NAV-WF-03 - the interaction-blocker conversion leaks unrelated ownership

The explicit conversion from `WinFormsInteractionBlocker` to `Control` is not
used by the blocker, platform adapter, core runtime, tests, or tracked consumers.
More importantly, it is not part of `IInteractionBlocker`, has no WPF equivalent,
and exposes the private root solely as an escape hatch. A consumer that needs its
host already owns the `Control` supplied to the constructor.

Remove the conversion before the stable baseline. This is a source and binary
break for direct casts; migration is to retain the original host reference.

## Historical compatibility reconciliation

The 2026-08-03 NAV-008(g) correction changed the inherited `Control.Name`
fallback on `PageView` and all four surface bases from a full type name to the
simple type name. The descriptor name remains authoritative for registration,
history, and normal diagnostics. Accept the corrected simple-name fallback as
stable. Consumers that treated the fallback as a fully qualified key must use
their descriptor name or their own explicit identifier instead.

The design-time changes recorded on 2026-08-06 are retained: the surface bases
remain non-abstract, their constructors remain protected, and handle-dependent
WinForms layout remains deferred until a handle exists. The generic prompt base
still requires a non-generic consumer shim for the WinForms visual designer; this
is a documented tooling limitation, not a public API change proposed here.

## Numbered dispositions requiring acceptance

1. **Retain the adapter family and extension structure.** Stabilize all listed
   types and members except the narrow corrections in dispositions 3-5. Preserve
   host ownership, dispatcher behavior, UI threading, z-order, modal blocking,
   focus observation, timer ownership, designer loadability, and disposal.
2. **Accept the historical simple-name fallback.** Keep `GetType().Name` for
   `PageView` and the four surface bases; document the consumer migration.
3. **Correct nullable adapter annotations.** Make the two events nullable, make
   the default-mask and protected shown-payload parameters nullable, and allow a
   nullable typed prompt completion. Do not change behavior.
4. **Correct the timer parameter spelling.** Rename `intervalMilis` to
   `intervalMillis`; record the named-argument source break.
5. **Remove the native-host conversion.** Delete only the explicit
   `WinFormsInteractionBlocker -> Control` operator; retain the blocker type and
   all lifecycle members.
6. **Retain the inert compatibility property.** Keep
   `PageView.AllowBackNavigation` but continue documenting that the runtime does
   not consult it and that history policy must not depend on it.

Dispositions 3-5 alter the public/protected manifest or compatibility contract.
They require explicit approval before implementation. Consequently the
F1-NAV-WF baseline, changelog/migration closure, roadmap completion, and release
gate remain open.

## Review-stage validation and limits

- The shared Navigation suite passed 290/290 on `net481` and 290/290 on
  `net9.0-windows` immediately before the reference commit.
- Both WinForms API manifests verified unchanged during the F1-NAV checkpoint.
- This review did not update a manifest and did not run an adapter closure build,
  runtime scenario, package flow, PackageReference consumer, or interactive UI
  procedure.
- Repository-only usage is evidence about current composition, not proof that an
  external consumer does not use a public member. The proposed breaks are based on
  contract ownership and correctness, not only on absence of tracked call sites.

## 2026-08-21 acceptance and implementation reconciliation

The user explicitly accepted all six dispositions, with the requirement that
contract changes be recorded. Implementation commit
`c5974fd93e8ca0b02292a6b34743293722430bc3` made the approved adapter corrections
without changing core Navigation or its frozen runtime components.

The assembly-derived manifests for both targets record the complete contract
delta:

- `InteractionDetected` and `Tick` are nullable events;
- `DefaultLoadingMask.OnOverlayOpenedAsync`, all protected shown hooks, and
  `CompletePrompt` declare their nullable runtime values;
- the timer constructor parameter is `intervalMillis`;
- the `WinFormsInteractionBlocker -> Control` conversion is absent.

The adapter family, simple type-name fallback, inert
`PageView.AllowBackNavigation`, designer seams, host ownership, dispatcher,
timer behavior, modal blocking, focus observation, z-order, and disposal model
were retained. The migration guide records the named-argument and conversion
breaks, and the changelog classifies them for the coordinated first `1.0.0`
family candidate.

Implementation validation completed before this reconciliation:

- explicit Release rebuilds passed for `net481` and `net9.0-windows` with 59
  and 91 warning occurrences respectively, and no errors;
- Navigation tests passed 292/292 on each target family;
- both WinForms manifests and both unchanged core manifests verified;
- the tracked LongRunning WinForms consumer built on both targets but was not
  launched;
- documentation verification passed against the adapter rebuild log with no new
  normalized warning identity; 114 repository-baseline identities were not
  emitted by this focused build, and `git diff --check` passed;
- no package or PackageReference campaign ran.
