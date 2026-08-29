# NekoLib.Mvvm Validation Requirements

**Document ID:** MVVM-VALIDATION-REQUIREMENTS

**Schema version:** 1

**Kind:** reference

**Lifecycle:** current

**Subject:** evidence contract for qualifying the NekoLib.Mvvm boundary

**Surface:** validation-requirements

**Boundary:** mvvm

**Authority role:** normative

**Mutation:** authored

**Indexing:** include

The [module manifest](MANIFEST.md) owns the inherited profile list. The
requirements below specialize it for a dependency-free binding helper whose
entire risk surface is semantic: what equality suppresses, what coercion
rejects, what propagates, and who owns the thread.

Two facts shape this contract. The module has no lifecycle, no resources, and no
external boundary, so there is nothing to soak or recover. And its real consumers
are UI binding pipelines that no test in this repository drives, so several
requirements below describe evidence that does not exist yet and say so rather
than being satisfied by the tests that happen to exist.

## MVVM-VALREQ-001

**Classification:** REQUIRED

**Trigger:** every release candidate and every change to source, project, target, or package settings

**Category:** build

**Boundary:** in-process

**Targets:** `net481` and `net9.0`

**Acceptance criteria:** Both target assemblies build with zero errors and no new normalized warning identity, and the nullable-enabled project produces zero nullable warnings of its own.

**Required evidence level:** build-only

**Rationale:** The F1 work took this project from 20 nullable warnings to 0, and a zero-warning nullable build is what keeps the annotation contract honest rather than aspirational.

## MVVM-VALREQ-002

**Classification:** REQUIRED

**Trigger:** every release candidate and every change to the library or test target frameworks

**Category:** build

**Boundary:** in-process

**Targets:** the shipped `net9.0` assembly

**Acceptance criteria:** Evidence states which target framework actually executed the tests. While the focused project targets `net9.0-windows` and the library targets `net9.0`, no record may describe the shipped `net9.0` assembly as having been exercised on its own target framework.

**Required evidence level:** build-only

**Rationale:** `net9.0-windows` is a superset, so a passing suite is compatible evidence but not identical evidence. See [`MVVM-FINDING-001`](FINDINGS.md); this requirement exists to stop the gap being closed by assumption.

## MVVM-VALREQ-003

**Classification:** REQUIRED

**Trigger:** every release candidate and every public declaration, accessibility, virtual, nullability, or default-value change

**Category:** api-compatibility

**Boundary:** in-process

**Targets:** both accepted `NekoLib.Mvvm` manifests

**Acceptance criteria:** Release assemblies match both accepted manifests exactly, and both manifests remain identical apart from the per-target framework attribute. Any delta carries an explicit compatibility disposition — including whether it is binary-breaking — and a migration entry before acceptance.

**Required evidence level:** build-only

**Rationale:** This surface is three types and fifteen members, so a single accidental addition is a large fraction of it, and the one accepted change in its history was binary-breaking through a `virtual` keyword that source review would not have flagged.

## MVVM-VALREQ-004

**Classification:** REQUIRED

**Trigger:** every change to `SetProperty` or the equality it relies on

**Category:** focused-regression

**Boundary:** in-process

**Targets:** both focused test targets

**Acceptance criteria:** The suite proves that a changed value raises and returns `true`; that an equal value neither raises nor assigns and returns `false`; that `EqualityComparer<T>.Default` governs, so `NaN` over `NaN` is suppressed and `null` over `null` is suppressed; and that a reference type without value equality compares by reference, so an instance mutated in place and reassigned raises nothing.

**Required evidence level:** automated-runtime

**Rationale:** Every one of these is a silent outcome. The in-place-mutation case in particular is the classic "my grid did not refresh", and nothing about it is visible without a test that asserts the absence of a notification.

## MVVM-VALREQ-005

**Classification:** REQUIRED

**Trigger:** every change to the notification funnel or its virtuality

**Category:** focused-regression

**Boundary:** in-process

**Targets:** both focused test targets

**Acceptance criteria:** The suite proves that a supplied name reaches subscribers verbatim, that a null or empty name is forwarded unchanged as the "every property changed" signal, and that a single override of `OnPropertyChanged` intercepts both a direct call and every notification routed through `SetProperty`.

**Required evidence level:** automated-runtime

**Rationale:** The override is the module's only supported extension seam and the documented answer to UI-thread marshalling. If `SetProperty` ever stopped routing through it, the seam would silently cover half the notifications.

## MVVM-VALREQ-006

**Classification:** REQUIRED

**Trigger:** every change to `RelayCommand` construction, execution, or predicate evaluation

**Category:** focused-regression

**Boundary:** in-process

**Targets:** both focused test targets

**Acceptance criteria:** The suite proves that the binding parameter is passed through unchanged, that the parameterless constructor overload ignores any supplied parameter, that an omitted predicate means always executable, that a supplied predicate receives the parameter, that a null execute delegate is rejected at construction, and that `Execute` invokes the delegate without consulting `CanExecute`.

**Required evidence level:** automated-runtime

**Rationale:** `Execute` not gating on `CanExecute` is standard `ICommand` but repeatedly surprises consumers, and it matters more here because WPF gates through the bound control while WinForms does not gate at all.

## MVVM-VALREQ-007

**Classification:** REQUIRED

**Trigger:** every change to `RelayCommand<T>` coercion

**Category:** focused-regression

**Boundary:** in-process

**Targets:** both focused test targets

**Acceptance criteria:** The suite proves that an exact runtime type match executes; that null passes `default(T)` for a reference type and for a nullable value type but is rejected for a non-nullable value type; that a boxed value coerces to its nullable form; and that a wrong runtime type is rejected without numeric widening, integer-to-enum conversion, or string parsing. Rejection must make `CanExecute` return `false` and `Execute` a no-op, never an `InvalidCastException`.

**Required evidence level:** automated-runtime

**Rationale:** The rejected cases are exactly what a XAML `CommandParameter` string and a WinForms boxed `Tag` supply, and rejection is silent, so only a test distinguishes "correctly rejected" from "quietly broken". See [`MVVM-FINDING-002`](FINDINGS.md).

## MVVM-VALREQ-008

**Classification:** REQUIRED

**Trigger:** every change to event raising or delegate invocation

**Category:** focused-regression

**Boundary:** in-process

**Targets:** both focused test targets

**Acceptance criteria:** The suite proves that a throwing `PropertyChanged` subscriber escapes through the property setter, that a throwing `CanExecuteChanged` subscriber propagates and skips later subscribers, that a throwing command delegate reaches the caller, and that `RaiseCanExecuteChanged` tolerates a subscriber that raises it again.

**Required evidence level:** automated-runtime

**Rationale:** This module deliberately does the opposite of Logging, Telemetry, Inspection, and Diagnostics, which all isolate subscriber failures. A binding helper should surface a view error rather than swallow it, and that inversion has to be pinned or it will be "fixed" by someone assuming the family-wide rule applies.

## MVVM-VALREQ-009

**Classification:** RECOMMENDED

**Trigger:** a change to event raising, or an accepted requirement for concurrent command use

**Category:** focused-regression

**Boundary:** in-process

**Targets:** both focused test targets

**Acceptance criteria:** A bounded characterization of concurrent `Execute` and concurrent `RaiseCanExecuteChanged` from multiple threads, recording that neither type synchronizes anything and that the caller owns synchronization.

**Required evidence level:** automated-runtime

**Rationale:** The absence of synchronization is documented but has never been measured; the F1 review recorded that limit explicitly. It stays below REQUIRED because thread ownership is deliberately the consumer's, not this module's.

## MVVM-VALREQ-010

**Classification:** CONDITIONAL

**Trigger:** a change to notification semantics, command behavior, or the guidance about UI-thread marshalling

**Category:** interactive-ui

**Boundary:** native-ui

**Targets:** a real WinForms or WPF binding pipeline

**Acceptance criteria:** Observed behavior against a live binding surface: a bound control updating from `PropertyChanged`, a null-name notification refreshing a whole view, a command's enabled state following `CanExecuteChanged`, and a background-thread notification marshalled through an `OnPropertyChanged` override.

**Required evidence level:** interactive

**Rationale:** Every threading and binding claim in the reference rests on documented framework behavior and on source. No test in this repository drives a binding pipeline; the only such evidence is cross-boundary and belongs to Data.

## MVVM-VALREQ-011

**Classification:** REQUIRED

**Trigger:** every publishable release candidate and every package, dependency, target, or XML-delivery change

**Category:** package

**Boundary:** package-feed

**Targets:** the `NekoLib.Mvvm` package and both target assets

**Acceptance criteria:** The immutable package contains both target assemblies, a package-owned matching XML file per assembly, correct target groups, **empty dependency groups on both targets**, provenance metadata, and a recorded hash, with no unexpected repository assets.

**Required evidence level:** build-only

**Rationale:** The empty dependency groups are the packaged proof of this module's central claim. A dependency appearing there would contradict the reference without any source change being visible.

## MVVM-VALREQ-012

**Classification:** REQUIRED

**Trigger:** every publishable release candidate and every public XML comment or documentation-generation change

**Category:** build

**Boundary:** in-process

**Targets:** both target assemblies

**Acceptance criteria:** Documentation-enabled Release builds generate matching XML assets with zero missing-member, malformed, unresolved, or ambiguous XML-comment diagnostics. Because `CS1591` is suppressed on `net481` by the project's `NoWarn`, a qualifying run either measures the `net9.0` target where it is live or unsuppresses it explicitly.

**Required evidence level:** build-only

**Rationale:** The accepted manifests prove compiled shape but say nothing about IDE guidance, and an ordinary `net481` build cannot detect a missing member at all. Inherited `<inheritdoc>` needs the same scrutiny: it can be present, well-formed, and still describe a different member's behavior.
