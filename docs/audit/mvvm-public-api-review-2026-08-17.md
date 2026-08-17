# Mvvm Public API Review — 2026-08-17

**Kind:** audit

**Lifecycle:** current

**Subject:** F1-MVVM compiled public surface, command construction and parameter
coercion, `CanExecuteChanged` ownership, `ViewModelBase` equality and
notification semantics, nullability contract, target parity, and documentation
ownership

**Status:** review complete; dispositions proposed and awaiting the consolidated
F1 decision gate

**Reference date:** 2026-08-17

**Reference commit:** `c9c4321e9fe67c0aeadcb7afda36347368fce457`

**Last reconciliation:** none

**Current state:** [`TODO.md`](../../TODO.md) F1-MVVM

## Baseline and authority

This review covers committed `HEAD` on branch
`phase-e/sqlserver-and-orchestration`. The reviewed product source is unchanged
from `89f05b667be10104e8ef966ac9bebba7b7f13a23`; the three commits in between
added the F1-DIAG, F1-WIN, and F1-HTTP review artifacts and their index entries.
The worktree and index were clean before this artifact was added, the branch was
26 commits ahead of `origin/phase-e/sqlserver-and-orchestration`, and nothing was
pushed.

The reviewed authority is the `NekoLib.Mvvm` project, both of its source files,
its project file, the two assembly-derived manifests under
[`eng/public-api/NekoLib.Mvvm/`](../../eng/public-api/NekoLib.Mvvm), the
dual-target focused tests, and the
[public API and release policy](../public-api-release-policy.md).

This review changes no product source, test, API baseline, package, changelog,
migration guide, or roadmap item.

## Scope

Included:

- all three compiled public type declarations and their public and protected
  members, on both target frameworks;
- command constructor overloads, overload resolution, and null-delegate
  rejection;
- execute and can-execute null behavior;
- `RelayCommand<T>` parameter coercion across reference types, value types,
  nullable value types, enums, numeric widening, and null;
- exception propagation from delegates and from event subscribers;
- `CanExecuteChanged` ownership, notification, and threading assumptions;
- reentrancy and concurrent invocation;
- `ViewModelBase` `SetProperty` equality semantics, `PropertyChanged` ordering,
  and caller-member-name behavior;
- the compiled nullability contract and its interaction with `ICommand` and
  `INotifyPropertyChanged`;
- target parity, test-project topology, and documentation ownership.

Excluded:

- implementing any recommendation, editing product source or tests, or updating
  an accepted API manifest;
- a framework-wide MVVM architecture, service location, dependency injection,
  async command infrastructure, Navigation coupling, WPF- or WinForms-specific
  adapters, a forwarding facade over the three types, or any project
  dependency — all explicitly out of bounds;
- interactive UI verification. No WinForms or WPF binding pipeline was driven.

## Package and ownership boundary

`NekoLib.Mvvm` targets `net481;net9.0`, enables `Nullable`, disables
`ImplicitUsings`, defines `NEKOLIB` plus the conditional symbols, and has **no
project reference and no package reference at all**
([`NekoLib.Mvvm.csproj`](../../src/Mvvm/NekoLib.Mvvm/NekoLib.Mvvm.csproj)).
`ICommand` and `INotifyPropertyChanged` come from the platform on both targets.
There is no conditional compilation, so both targets compile the same text.

The ownership split is the whole point of the module and it holds: the module
owns delegate dispatch, parameter coercion, and change notification; the
consumer owns when `CanExecuteChanged` is raised, on which thread anything is
raised, what a command does, and every UI concern. There is no static state, no
registry, no service locator, and no UI-framework dependency.

## Compiled-surface inventory and recommended classification

| Type | Kind | Members | Recommended class |
|---|---|---|---|
| `RelayCommand` | sealed class, `ICommand` | 2 ctors, `CanExecuteChanged`, `CanExecute`, `Execute`, `RaiseCanExecuteChanged` | Stable candidate |
| `RelayCommand<T>` | sealed class, `ICommand` | 1 ctor, `CanExecuteChanged`, `CanExecute`, `Execute`, `RaiseCanExecuteChanged` | Stable candidate |
| `ViewModelBase` | abstract class, `INotifyPropertyChanged` | protected ctor, `PropertyChanged`, protected `OnPropertyChanged`, protected `SetProperty<T>` | Stable candidate |

Totals: **3 public types and 15 public or protected member declarations**,
identical on both targets apart from the `TargetFramework` assembly attribute.

**The recommendation is that this entire surface is intentionally stable.**
Nothing is proposed for removal, addition, rename, namespace move,
internalization, deprecation, or the experimental class. The module is
deliberately small and it is the right size; the review's substance is the
nullability contract (MVVM-01), one optional virtuality change (MVVM-06), and a
set of contracts that currently have no documentation owner.

`ViewModelBase` is the one intended inheritance seam and is correctly `abstract`
with `protected` helpers. The two commands are correctly `sealed`: they are
delegate holders, and extension belongs in the delegates.

## Downstream usage

- `tests/NekoLib.Mvvm.Tests/Unit/` — 22 tests per target.
- `runtime_tests/Data/FarmDatabase/` — exercises Mvvm binding as part of the
  Data scenario, per the scenario README. It was not built or run for this
  review.

Per the release policy, this proves nothing about external use, and no removal
is proposed on consumer-count grounds.

## Observed facts, risks, and recommended dispositions

Findings marked *probe-confirmed* were reproduced with a disposable
**nullable-enabled** dual-target console consumer built against the
`NekoLib.Mvvm` project reference and run on **both** `net481` and `net9.0`. Every
runtime result was identical on the two targets.

### MVVM-01 — The compiled nullability contract contradicts both implemented interfaces and the module's own defaults

**Confirmed, probe-confirmed on both targets.** A clean rebuild of the module
emits nullable warnings whose subjects are the public surface itself:

```text
net9.0:
  CS8767 x4  RelayCommand/RelayCommand<T>.CanExecute(object parameter) and
             .Execute(object parameter) do not match ICommand.CanExecute(object?)
             / ICommand.Execute(object?)
  CS8612 x3  RelayCommand.CanExecuteChanged, RelayCommand<T>.CanExecuteChanged and
             ViewModelBase.PropertyChanged do not match EventHandler? /
             PropertyChangedEventHandler?
  CS8625 x5  '= null' defaults on non-nullable parameters
  CS8618 x5, CS8601 x3 (internal state)
net481:
  CS8625 x5 only — the net481 reference assemblies carry no nullable annotations
             for ICommand or INotifyPropertyChanged, so the mismatch is invisible
```

Both accepted manifests record the incorrect contract, for example
`public bool CanExecute(object parameter)` and
`public RelayCommand(System.Action<object> execute, System.Predicate<object> canExecute = null)`.

The practical consequence reaches consumers. A nullable-enabled consumer
compiling against the module:

```text
net481 and net9.0:
  Program.cs(19,53): warning CS8625: Cannot convert null literal to
  non-nullable reference type.      // from OnPropertyChanged(null)
```

`null` is the documented, default, and correct value in every one of these
positions: `canExecute` is optional, `propertyName` is optional and `null`
legitimately means "all properties", and `ICommand.Execute(null)` is exactly what
a WPF binding with no `CommandParameter` and a WinForms call site both produce.
The module declares those inputs non-nullable and then handles null correctly
everywhere — the annotations are simply wrong.

**Recommended disposition:** annotate the surface to match the interfaces and
the behavior — `object?` on `CanExecute`/`Execute`, `EventHandler?` and
`PropertyChangedEventHandler?` on the events, `Predicate<object>?`,
`Func<bool>?`, `Predicate<T>?`, and `string?` on the optional parameters, and the
corresponding internal field annotations.

Compatibility: **binary-compatible** — nullable attributes do not change runtime
signatures. **Source-compatible in the safe direction** — every change widens
what a caller may pass, so an existing consumer gains no new error and loses
warnings. Both manifests change, so this requires an accepted decision, a
changelog entry, migration guidance, and a scoped `-UpdateBaseline`. It also
removes roughly twenty-five warning occurrences from the repository baseline,
which the warning-identity comparison reports as omitted identities rather than
new ones.

This is the single highest-value F1-MVVM change and the only reason this module
needs a migration guide.

### MVVM-02 — Typed coercion rejects exactly the parameter shapes a designer is most likely to supply

**Confirmed, probe-confirmed on both targets.** `TryCoerce` accepts only an
exact runtime type match plus null for reference and nullable value types
([`RelayCommand.cs:98`](../../src/Mvvm/NekoLib.Mvvm/RelayCommand.cs#L98)):

```text
RelayCommand<int?>  boxed int 5 -> executes;  null -> executes with null
RelayCommand<long>  boxed int 5 -> CanExecute False   (no numeric widening)
RelayCommand<Colour> boxed int 1 -> CanExecute False; Colour.Red -> True
RelayCommand<object> null       -> CanExecute True
RelayCommand<string> boxed int  -> Execute is a silent no-op, CanExecute False
```

The design intent — never throw `InvalidCastException` through a binding
pipeline — is correct and worth keeping. The trap is which shapes fail: a XAML
`CommandParameter="1"` supplies a **string**, a WinForms `Tag` commonly holds a
boxed `int`, and an enum-typed command rejects the boxed integer that equals its
underlying value. In every one of those cases the button does nothing and
nothing is reported.

**Recommended disposition:** document the coercion table exactly as measured,
naming the enum, numeric-widening, and string cases explicitly. Do **not** add
`Convert.ChangeType` or any widening: it would reintroduce the conversion
failures the design removed, and silently coercing `"1"` to `1` is its own trap.

### MVVM-03 — `Execute` does not consult `CanExecute`

**Confirmed, probe-confirmed on both targets.** A command whose predicate
returns `false` still runs its action when `Execute` is called directly.

```text
CanExecute=false then Execute -> Execute ran even though CanExecute is false
```

This is standard `ICommand` semantics and matches every mainstream
implementation. It matters here because WPF gates the call through the bound
control while **WinForms does not gate at all** — and WinForms kiosks are this
framework's primary UI.

**Recommended disposition:** document it, prominently, in the WinForms context.
No code change: adding an internal `CanExecute` gate would diverge from
`ICommand` and silently swallow deliberate direct invocations.

### MVVM-04 — Subscriber exceptions propagate and abort later subscribers

**Confirmed, probe-confirmed on both targets.** Neither `RaiseCanExecuteChanged`
nor `OnPropertyChanged` isolates subscribers
([`RelayCommand.cs:43`](../../src/Mvvm/NekoLib.Mvvm/RelayCommand.cs#L43),
[`ViewModelBase.cs:17`](../../src/Mvvm/NekoLib.Mvvm/ViewModelBase.cs#L17)).

```text
throwing CanExecuteChanged subscriber -> propagates to caller;
                                         later subscribers skipped: True
throwing PropertyChanged subscriber   -> propagates out of the property setter
Execute delegate exception            -> propagates to the UI caller
```

This is the normal .NET event convention and is the right choice for a binding
helper: a view error should surface, not vanish. But it is the **opposite** of
Logging, Telemetry, Inspection, and Diagnostics, all of which deliberately
isolate subscriber failures, so the difference must be stated rather than left
for a reader to infer from family habit.

**Recommended disposition:** document the deliberate difference and its
consequence — a throwing `PropertyChanged` subscriber escapes through the
property setter, and a throwing command delegate reaches the UI framework, where
a WinForms application sees it as `Application.ThreadException` if
`WindowsCrash.HookWinForms()` is installed. No code change.

### MVVM-05 — `SetProperty` equality has three surprising, correct consequences

**Confirmed, probe-confirmed on both targets.** `SetProperty` uses
`EqualityComparer<T>.Default.Equals`
([`ViewModelBase.cs:30`](../../src/Mvvm/NekoLib.Mvvm/ViewModelBase.cs#L30)):

```text
NaN -> NaN                                   raised once, then suppressed
in-place mutation, then same reference        not raised
null -> null                                  not raised
OnPropertyChanged(null) property name        '<null>'
```

Each is right and each will surprise someone. `EqualityComparer<double>.Default`
follows `Equals`, not `==`, so `NaN` equals `NaN` and a second assignment is
suppressed. A collection mutated in place and reassigned is reference-equal, so
no notification is raised — the classic "my grid didn't refresh" case. And
`OnPropertyChanged(null)` is a real capability: WPF and WinForms binding both
treat a null or empty property name as "every property changed", which this type
supports and never mentions.

**Recommended disposition:** document all four. No code change: `SetProperty`
should not special-case floating point, and forcing notification on reference
equality would break the helper's purpose.

### MVVM-06 — `OnPropertyChanged` is not virtual, so there is no single funnel for UI-thread marshalling

**Confirmed.** `OnPropertyChanged` is `protected` and non-virtual
([`ViewModelBase.cs:17`](../../src/Mvvm/NekoLib.Mvvm/ViewModelBase.cs#L17)), and
`SetProperty` routes every notification through it. A derived type therefore
cannot intercept notifications in one place.

For this framework's stated product class — unattended WinForms shells where
background work updates a view-model — that is the one concrete extensibility
gap. WinForms data binding requires `PropertyChanged` on the UI thread, and
today every consumer must marshal at each subscriber or in each setter instead of
overriding one method on their own base class.

**Recommended disposition:** make `OnPropertyChanged` `protected virtual`. It is
source-compatible, and the repository's own recorded rule classifies
non-virtual → virtual as binary-breaking, requiring recompilation of an external
assembly that derives from `ViewModelBase` — the same classification applied to
NAV-009(b) and recorded in `TODO.md`. The manifests change, so this needs an
explicit yes at the decision gate.

**Rejected alternative:** shipping a `DispatcherViewModelBase` or a WinForms
marshalling base class. That is a UI-framework adapter, explicitly out of bounds,
and one virtual method lets every consumer write their own in six lines.

### MVVM-07 — `CanExecuteChanged` is entirely caller-driven, with no requery and no thread affinity

**Confirmed.** Neither command touches `CommandManager.RequerySuggested`, and
`RaiseCanExecuteChanged` raises synchronously on the calling thread
([`RelayCommand.cs:43`](../../src/Mvvm/NekoLib.Mvvm/RelayCommand.cs#L43)).

That is the correct consequence of having no WPF dependency, and it is what lets
the same type serve WinForms. The cost is that a WPF consumer gets no automatic
requery and must call `RaiseCanExecuteChanged` themselves, on the UI thread,
because WPF's `CanExecuteChanged` subscribers assume dispatcher affinity.

**Recommended disposition:** document the ownership and the thread requirement.
No code change: `CommandManager` would add a WPF dependency and break the
`net9.0` target.

### MVVM-08 — Notification reentrancy is unguarded

**Confirmed, probe-confirmed on both targets.** A `CanExecuteChanged` subscriber
that calls `RaiseCanExecuteChanged` re-enters immediately; the probe reached
depth 3 only because it self-limited.

```text
RaiseCanExecuteChanged reentrancy -> unguarded, nested depth reached 3
```

The same applies to a `PropertyChanged` subscriber that assigns another property.

**Recommended disposition:** document it, matching how the equivalent Telemetry
finding was handled. No code change: a reentrancy guard would silently drop
legitimate cascading notifications, which is normal in view-models.

### MVVM-09 — `new RelayCommand(null)` is ambiguous

**Confirmed by compilation.**

```text
error CS0121: The call is ambiguous between
  RelayCommand(System.Action<object>, System.Predicate<object>) and
  RelayCommand(System.Action, System.Func<bool>)
```

A degenerate call — every real call site passes a lambda or method group that
resolves cleanly — and the alternative would be removing one of two useful
overloads.

**Recommended disposition:** note it and move on. No code change.

### MVVM-10 — Target parity is clean, but the tests never run against the shipped TFM

**Confirmed.** The two manifests differ only by the `TargetFramework` assembly
attribute, and every probe result was identical on `net481` and `net9.0`.

The library targets `net481;net9.0`; the test project targets
`net481;net9.0-windows`
([`NekoLib.Mvvm.Tests.Unit.csproj`](../../tests/NekoLib.Mvvm.Tests/Unit/NekoLib.Mvvm.Tests.Unit.csproj))
without `UseWindowsForms`, `UseWPF`, or any Windows-specific reference. The
`net9.0` assembly is therefore always exercised through a `net9.0-windows` host —
compatible, but not the TFM that ships.

The one target-visible difference in the module is that `net481` reference
assemblies carry no nullable annotations for `ICommand` or
`INotifyPropertyChanged`, so the MVVM-01 mismatch is invisible there and loud on
`net9.0`.

**Recommended disposition:** record the observation; **leave the test targets
unchanged for this campaign**, because the F1 validation contract for this module
explicitly names `net481` and `net9.0-windows`. Retargeting the test project to
plain `net9.0` is a reasonable later hygiene decision and is deliberately not
proposed here.

### MVVM-11 — Mvvm has no documentation owner

**Confirmed.** There is no `src/Mvvm/NekoLib.Mvvm/README.md`, and
[`docs/README.md`](../README.md) registers no owner. The module's coverage today
is two rows: a one-line module-map entry and a one-line entry-point row in the
root README.

Every contract in MVVM-02 through MVVM-09 — the coercion table, the
`Execute`/`CanExecute` relationship, exception propagation, equality semantics,
`OnPropertyChanged(null)`, `CanExecuteChanged` ownership and threading, and
reentrancy — currently has no owner anywhere.

Logging and Telemetry set the precedent: both received a dedicated reference at
a comparable surface size because their contracts, not their size, needed an
owner. The Diagnostics.Windows counter-precedent does not apply, because Mvvm is
a standalone package with no owning module to host a section.

**Recommended disposition:** add `src/Mvvm/NekoLib.Mvvm/README.md` and register
it in the documentation index and the `AGENTS.md` navigation table. Keep it
short — this module's virtue is its size.

### MVVM-12 — Coverage is good on the shapes it tests and absent on every finding above

**Confirmed.** 22 tests per target cover reference/value/nullable coercion,
direct matches, wrong types, null parameters, typed predicates, the no-predicate
default, `RaiseCanExecuteChanged`, null-execute rejection, and `SetProperty`
change, no-change, null, and value-type behavior.

Nothing covers enum or numeric-widening rejection, `Execute` ignoring
`CanExecute`, subscriber exception propagation for either event, `NaN` equality,
in-place mutation with an unchanged reference, `OnPropertyChanged(null)`,
reentrancy, or the nullability contract.

**Recommended disposition:** add focused dual-target regressions for every
behavior the accepted dispositions document or change.

## Positive findings to preserve

1. `RelayCommand<T>` cannot throw `InvalidCastException` through a binding
   pipeline, on either target, for any parameter shape tested.
2. Both commands reject a null execute delegate at construction.
3. `SetProperty` returns whether a change was applied, which is what makes it
   composable in a setter.
4. There is no static state, no service location, no dependency injection, no
   Navigation coupling, and no UI-framework dependency anywhere in the module.
5. The surface has stayed small and has not accreted an async command, a
   `DelegateCommand` variant, a messenger, or a locator.

## Likely migration cost

| Disposition | Compiled surface | Behavior | Consumer action |
|---|---|---|---|
| MVVM-01 nullability | **both manifests change**; binary-compatible | none | none; nullable-enabled consumers lose warnings |
| MVVM-06 virtual `OnPropertyChanged` *(optional)* | **both manifests change**; binary-breaking per repository rule | none | recompile an external assembly deriving from `ViewModelBase` |
| MVVM-02 to MVVM-05, MVVM-07 to MVVM-12 | none | none | none |

`docs/migrations/f1-mvvm.md` is required: MVVM-01 changes the compiled
manifests, and MVVM-06 if accepted is binary-breaking.

## Core-contract conflict

None, and none is possible: the module references no NekoLib project and no
NuGet package. No recommendation adds a dependency, a Core reference, an async
command surface, a facade, or any Navigation coupling.

## Rejected alternatives

- **A framework-wide MVVM architecture, messenger, locator, or DI container.**
  Out of bounds and contrary to the module's stated purpose.
- **Async command infrastructure.** Out of bounds without a demonstrated
  accepted need; none was found in the repository or the roadmap.
- **A forwarding facade over the three types.** Out of bounds and pointless at
  this size.
- **WPF or WinForms adapter types**, including a dispatcher-marshalling base
  class. Rejected in favour of one virtual method (MVVM-06).
- **`CommandManager.RequerySuggested` integration.** Rejected: a WPF dependency
  that would break the `net9.0` target.
- **`Convert.ChangeType` or numeric widening in `TryCoerce`.** Rejected: it
  reintroduces the conversion failures the current design eliminates.
- **Gating `Execute` on `CanExecute`.** Rejected: it diverges from `ICommand` and
  swallows deliberate direct invocations.
- **Isolating event-subscriber exceptions.** Rejected: a binding helper should
  surface view errors, unlike the observability modules.
- **A reentrancy guard on either event.** Rejected: cascading notifications are
  normal in view-models.
- **Removing either `RelayCommand` constructor to resolve MVVM-09.** Rejected:
  it would break real call sites to fix a degenerate one.
- **Retargeting the test project to plain `net9.0` in this campaign.** Deferred:
  the F1 validation contract for this module names `net9.0-windows`.

## Proposed implementation block after acceptance

If the dispositions are accepted, one narrow commit should:

1. record the accepted decisions in `TODO.md` F1-MVVM with package-pending
   evidence and leave the checkbox unchecked;
2. implement MVVM-01, and MVVM-06 if the gate accepts it, in
   `src/Mvvm/NekoLib.Mvvm/`;
3. add the focused dual-target regressions described in MVVM-12;
4. add `src/Mvvm/NekoLib.Mvvm/README.md` covering MVVM-02 through MVVM-09 and
   the preserved positives;
5. add `docs/migrations/f1-mvvm.md`;
6. update `CHANGELOG.md`, `docs/README.md`, and the `AGENTS.md` navigation
   table;
7. update both `NekoLib.Mvvm` manifests through a scoped
   `verify-public-api.ps1 -UpdateBaseline -PackageId NekoLib.Mvvm`, and confirm
   the warning-identity comparison reports the removed nullable identities as
   omitted rather than new;
8. append a reconciliation section here without rewriting the snapshot above.

## Review validation

Commands run on Windows at the reference commit:

```text
dotnet test tests/NekoLib.Mvvm.Tests/Unit/NekoLib.Mvvm.Tests.Unit.csproj
  net481:          22 passed, 0 failed, 0 skipped
  net9.0-windows:  22 passed, 0 failed, 0 skipped

dotnet build src/Mvvm/NekoLib.Mvvm/NekoLib.Mvvm.csproj -t:Rebuild
  net9.0: CS8767 x4, CS8612 x3, CS8625 x5, CS8618 x5, CS8601 x3
  net481: CS8625 x5

diff eng/public-api/NekoLib.Mvvm/net481.approved.txt
     eng/public-api/NekoLib.Mvvm/net9.0.approved.txt
  TargetFramework assembly attribute only

git grep '#if|#else|#endif' -- src/Mvvm
  no match (no conditional compilation on either target)
```

A disposable **nullable-enabled** dual-target console consumer was built against
the `NekoLib.Mvvm` project reference and run on both `net481` and `net9.0`
outside the repository, then deleted. It measured the full coercion table,
`SetProperty` equality behavior, `OnPropertyChanged(null)`, subscriber and
delegate exception propagation, `Execute` versus `CanExecute`, and reentrancy,
and it reproduced the consumer-visible `CS8625` from MVVM-01. A separate
compilation confirmed `CS0121` for MVVM-09. No repository file changed.

## Residual validation limits

- **No WinForms or WPF binding pipeline was driven.** The threading and
  requery claims in MVVM-06 and MVVM-07 rest on documented framework behavior
  and on source, not on an interactive UI run.
- Concurrent `Execute` from multiple threads was not measured; the absence of
  synchronization is a source fact, and the recommendation is documentation
  only.
- The FarmDatabase runtime scenario, which exercises Mvvm binding, was neither
  built nor run.
- No package was produced and no package-consumer probe was run.
- The full solution was not rebuilt or tested for this review, so the exact
  effect of MVVM-01 on the 515-warning baseline is stated as an estimate from
  the module rebuild, not a measured full-solution delta.

## Decision gate

MVVM-01 is recommended as accepted work. MVVM-06 is recommended but needs an
explicit yes or no because it is binary-breaking by the repository's own rule.
MVVM-02 through MVVM-05 and MVVM-07 through MVVM-11 are recommended as
documentation-only, with a new module reference. MVVM-12 is recommended as
test-only. The entire compiled surface is otherwise recommended as intentionally
stable. Nothing here may be implemented until the consolidated F1 decision gate
accepts or modifies these dispositions.
