# NekoLib.Mvvm Changelog

**Document ID:** MVVM-CHANGELOG

**Schema version:** 1

**Kind:** reference

**Lifecycle:** current

**Subject:** consumer-visible evolution of NekoLib.Mvvm

**Surface:** changelog

**Boundary:** mvvm

**Authority role:** non-normative

**Mutation:** authored

**Indexing:** include

The [coordinated family changelog](../../../CHANGELOG.md) remains the release
summary. This file records Mvvm-specific consumer impact without duplicating
package hashes or release provenance.

## 1.0.0

**Packages:** `NekoLib.Mvvm`

**Compatibility class:** mixed

**Consumer impact:** No type, member, signature, default value, namespace, target, or dependency was added or removed, and the three types keep the same fifteen members. Both manifests changed, in nullability annotations and one `virtual` keyword. Source compatibility is unaffected; one narrow binary case and one new true-positive warning are described below.

**Migration:** `docs/modules/Mvvm/migrations/f1.md`

- `OnPropertyChanged` became `protected virtual`, so a single override now
  intercepts every notification `SetProperty` raises. That is the seam a
  WinForms view-model uses to marshal notifications to the UI thread, and the
  module deliberately ships no such base class of its own.
- The `virtual` addition is classified **binary-breaking** by the repository's
  own rule: an external assembly compiled against the non-virtual signature must
  be recompiled. Measured on both targets, an un-recompiled consumer loads and
  runs unchanged. One silent divergence is possible and is named in the migration
  guide — a non-recompiled assembly that calls `OnPropertyChanged` directly emits
  `call` rather than `callvirt` and therefore skips a derived override.
- The nullability contract now matches `ICommand`, `INotifyPropertyChanged`, and
  the module's own behavior: `CanExecute(object?)`, `Execute(object?)`,
  `EventHandler?`, `PropertyChangedEventHandler?`, and nullable optional
  parameters throughout. This is binary-compatible.
- A nullable-enabled consumer stops getting `CS8625` from the module's own
  defaults — `OnPropertyChanged(null)`, `Execute(null)`, `CanExecute(null)`, and
  an explicit null predicate are all clean.
- One new warning is a true positive and is not a defect. `RelayCommand` takes
  `Action<object?>`, because a binding with no command parameter really does
  supply null, so a lambda that dereferences the parameter without checking now
  emits `CS8602`. Use `RelayCommand<T>` when you want a parameter whose absence
  is handled for you.

## Unreleased

**Packages:** `NekoLib.Mvvm`

**Compatibility class:** documentation-only

**Consumer impact:** Two XML comments now describe existing behavior more precisely. Compiled signatures, the accepted API baselines, and runtime behavior are unchanged.

**Migration:** none

- `RelayCommand<T>.Execute` no longer inherits its documentation from
  `RelayCommand.Execute`. The inherited text said the delegate is invoked, which
  is not what the generic overload does: it coerces first and is a silent no-op
  when coercion fails. The member now documents that, and still records that
  `Execute` does not consult `CanExecute`.
- `ViewModelBase.SetProperty`'s `propertyName` parameter now states that the
  compiler supplies it from the calling member and that a null or empty value is
  forwarded unchanged, meaning "every property changed".
- These source comments have not been qualified in a new package candidate.
  Immutable `1.1.0-local.8` proves delivery of the prior XML bytes only.
