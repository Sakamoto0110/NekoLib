# NekoLib.Mvvm Findings

**Document ID:** MVVM-FINDINGS

**Schema version:** 1

**Kind:** reference

**Lifecycle:** current

**Subject:** unconfirmed and non-normative observations about NekoLib.Mvvm

**Surface:** findings

**Boundary:** mvvm

**Authority role:** non-normative

**Mutation:** authored

**Indexing:** include

Everything here is non-normative. A finding becomes an issue only after it is
verified, and scheduled work only after explicit promotion to
[`TODO.md`](../../../TODO.md). None of the entries below is promoted, and none
proposes a behavior, API, or dependency change.

## MVVM-FINDING-001

**Status:** open

**Confidence:** high

**Observation:** The focused test project targets `net481` and `net9.0-windows`, while the shipped library targets `net481` and plain `net9.0`. The `net9.0` assembly that consumers actually receive is therefore never exercised on its own target framework by any test in this repository.

**Evidence:** `NekoLib.Mvvm.csproj` declares `net481;net9.0`; `NekoLib.Mvvm.Tests.Unit.csproj` declares `net481;net9.0-windows`. The F1 review raised this as `MVVM-10` and it was deliberately not acted on: the F1 validation contract names `net9.0-windows`, and retargeting the test project was recorded as a later hygiene decision.

**Hypothesis:** The risk is small and well understood. `net9.0-windows` is a superset of `net9.0`, the module has no conditional compilation and no Windows-specific code, and the two accepted API manifests are identical apart from the framework attribute. A divergence would have to come from the host rather than from the library. The observation is that "compatible" is being inferred rather than measured, on the one assembly most consumers will load.

**Disposition:** Keep as a finding. Retargeting the test project is a repository hygiene decision that affects the F1 validation contract, so it belongs to an owner decision rather than to a documentation review. The reference and [`MVVM-VALREQ-002`](VALIDATION_REQUIREMENTS.md) both state the gap explicitly instead of letting a passing suite imply coverage it does not have.

**Outcome link:** [`VALIDATION_REQUIREMENTS.md`](VALIDATION_REQUIREMENTS.md)

## MVVM-FINDING-002

**Status:** open

**Confidence:** high

**Observation:** `RelayCommand<T>` rejects an uncoercible parameter silently. `CanExecute` returns `false` and `Execute` returns without invoking anything — no exception, no log, no diagnostic of any kind. The cases most likely to hit it are exactly the two most common binding sources: a XAML `CommandParameter="1"`, which supplies a `string`, and a WinForms `Tag`, which often supplies a boxed `int`.

**Evidence:** `TryCoerce` returns `false` for any non-null parameter that is not already a `T`, and `Execute` returns immediately on that result. Three focused tests pin the rejection of a boxed integer for an enum, a boxed integer for a wider numeric type, and a string for an `int`. Nothing anywhere reports the rejection.

**Hypothesis:** The design is deliberate and the alternative was considered and rejected in the F1 review: `Convert.ChangeType` would reintroduce the conversion failures the exact-match rule removes, and throwing `InvalidCastException` through a binding pipeline is worse than doing nothing. The residual cost is that the failure mode is a dead button with no signal, and the consumer's only defence is declaring `T` correctly in the first place.

**Disposition:** Keep as a finding, not a defect. Any diagnostic channel — an event, a callback, a debug trace — would be new public surface and a product decision, and this module's whole premise is that it stays small. The reference already calls the surprising rows out explicitly and says a rejected parameter reports nothing. No change is scheduled.

**Outcome link:** [`REFERENCE.md`](REFERENCE.md)

## MVVM-FINDING-003

**Status:** open

**Confidence:** medium

**Observation:** `RaiseCanExecuteChanged` is declared independently on `RelayCommand` and on `RelayCommand<T>`, and neither `ICommand` nor any shared base or interface exposes it. A view-model that publishes its commands as `ICommand` cannot raise the event without casting back to the concrete type, and code written against both command types generically cannot raise it at all.

**Evidence:** Both accepted API manifests show `RelayCommand` and `RelayCommand<T>` implementing only `System.Windows.Input.ICommand`, each declaring its own `RaiseCanExecuteChanged()`. There is no `IRaiseCanExecuteChanged`-style contract and no common base class.

**Hypothesis:** In practice a view-model owns its own commands as concrete fields and raises through them, which is the shape the reference's example uses, so this rarely bites. It becomes visible when commands are exposed as `ICommand` for binding purity, or when a helper tries to refresh several commands at once.

**Disposition:** Keep as a finding. Introducing a shared interface or base type would change the public surface of a stable package and is a product decision, not a documentation one. The reference records that raising the event is the consumer's responsibility; recording the ergonomic cost here keeps the observation available without proposing an API. No change is scheduled.

**Outcome link:** [`REFERENCE.md`](REFERENCE.md)

### Residual evidence limitations

These bound every conclusion above and in [`ISSUES.md`](ISSUES.md):

- no WinForms or WPF binding pipeline has been driven by any test in this
  repository, so the threading and binding guidance rests on documented framework
  behavior and on source rather than on an interactive run;
- concurrent `Execute` from multiple threads has never been measured;
- the cross-boundary FarmDatabase evidence is owned by Data, covers only the Mvvm
  behavior that scenario happens to exercise, and was last driven on 2026-08-06.
