# NekoLib.Mvvm

**Document ID:** MVVM-REFERENCE

**Schema version:** 1

**Kind:** reference

**Lifecycle:** current

**Subject:** binding helper and command contracts — parameter coercion, notification semantics, threading, and exception behavior

**Surface:** technical-reference

**Boundary:** mvvm

**Authority role:** normative

**Mutation:** authored

**Indexing:** include

Three types, deliberately. `NekoLib.Mvvm` is an opt-in `net481`/`net9.0` package
with **no project reference and no package reference at all**: `ICommand` and
`INotifyPropertyChanged` come from the platform on both targets.

It is a binding helper, not an architecture. There is no service locator, no
dependency injection, no messenger, no async command infrastructure, and no
coupling to Navigation. If you need more than this, write it — the point of the
module is that it stays small.

```csharp
public sealed class OrderViewModel : ViewModelBase
{
    private string? _customer;

    public string? Customer
    {
        get => _customer;
        set => SetProperty(ref _customer, value);
    }

    public RelayCommand Save { get; }

    public OrderViewModel()
    {
        Save = new RelayCommand(() => Persist(), () => !string.IsNullOrEmpty(_customer));
    }
}
```

## Commands

`RelayCommand` passes the binding's parameter through as `object?`.
`RelayCommand<T>` coerces it to `T` first.

### Parameter coercion

`RelayCommand<T>` never throws `InvalidCastException` through a binding pipeline.
It requires an **exact runtime type match**, plus null for reference and nullable
value types:

| Supplied parameter | `T` | Result |
|---|---|---|
| `"hello"` | `string` | executes |
| `null` | `string`, `int?`, `object` | executes with `default(T)` |
| `null` | `int` | `CanExecute` false, `Execute` is a no-op |
| boxed `5` | `int?` | executes |
| boxed `5` | `long` | **rejected** — no numeric widening |
| boxed `1` | an enum | **rejected** — no integer-to-enum conversion |
| `"1"` | `int` | **rejected** — no parsing |
| wrong type | anything | `CanExecute` false, `Execute` is a no-op |

The last four rows are the ones that surprise people, because they are exactly
what a XAML `CommandParameter="1"` (a **string**) and a WinForms `Tag` (often a
boxed `int`) supply. A rejected parameter means the button does nothing and
nothing is reported. Declare `T` to match what the binding actually provides.

There is no `Convert.ChangeType` and there will not be: it would reintroduce the
conversion failures this design removes.

### `Execute` does not consult `CanExecute`

That is standard `ICommand`, and it matters here because **WPF gates the call
through the bound control while WinForms does not gate it at all**. Calling
`Execute` directly runs the delegate regardless of the predicate.

### `CanExecuteChanged` is yours to raise

Neither command touches `CommandManager.RequerySuggested` — that would be a WPF
dependency and would break the `net9.0` target. Call
`RaiseCanExecuteChanged()` yourself when a dependency of the predicate changes.

It raises **synchronously on the calling thread**. WPF's `CanExecuteChanged`
subscribers assume dispatcher affinity, so raise it on the UI thread there.

## `ViewModelBase`

### `SetProperty` equality

Equality is `EqualityComparer<T>.Default`, which has three consequences worth
knowing:

- **`NaN` equals `NaN`** — `Equals`, not `==`. Assigning `NaN` over `NaN` raises
  nothing.
- **A reference type without value equality compares by reference.** Mutating a
  collection in place and reassigning the same instance raises nothing; this is
  the classic "my grid didn't refresh".
- **`null` to `null` raises nothing.**

`SetProperty` returns whether a change was applied, so it composes inside a
setter.

### `OnPropertyChanged(null)` means "everything"

Both WinForms and WPF binding treat a null or empty property name as "every
property changed". It is a supported way to refresh a whole view-model.

### Overriding the notification funnel

`OnPropertyChanged` is `virtual`, and `SetProperty` routes through it, so **one
override intercepts every notification**:

```csharp
public abstract class UiThreadViewModel : ViewModelBase
{
    private readonly ISynchronizeInvoke _target;

    protected override void OnPropertyChanged(string? propertyName)
    {
        if (_target.InvokeRequired)
            _target.Invoke(new Action(() => base.OnPropertyChanged(propertyName)), null);
        else
            base.OnPropertyChanged(propertyName);
    }
}
```

That is the seam for the case this framework actually has: a view-model updated
by background work, bound to WinForms controls that require the UI thread. The
module ships no such base class — a UI-framework adapter is out of scope, and the
override above is six lines.

## Exceptions and reentrancy

**Subscriber exceptions propagate and abort the remaining subscribers**, for both
`PropertyChanged` and `CanExecuteChanged`. A throwing `PropertyChanged`
subscriber escapes through the property setter, and a throwing command delegate
reaches the UI framework — where a WinForms application sees it as
`Application.ThreadException` if `WindowsCrash.HookWinForms()` is installed.

This is deliberately **unlike** Logging, Telemetry, Inspection, and Diagnostics,
which all isolate subscriber failures. A binding helper should surface a view
error, not swallow it.

Notification reentrancy is unguarded: a `CanExecuteChanged` subscriber may raise
it again, and a `PropertyChanged` subscriber may assign another property.
Cascading notifications are normal in view-models, so nothing prevents them —
including an unbounded cycle you write yourself.

Neither type synchronizes anything. Concurrent `Execute` from two threads runs
both delegates; the caller owns synchronization.

## Nullability

The public surface is annotated to match the interfaces it implements:
`CanExecute(object?)`, `Execute(object?)`, `EventHandler?`,
`PropertyChangedEventHandler?`, and nullable optional parameters throughout.

One consequence for a nullable-enabled consumer: `RelayCommand` takes
`Action<object?>`, because a binding with no command parameter really does supply
null. A lambda that dereferences the parameter without checking now warns:

```csharp
new RelayCommand(p => Console.WriteLine(p.ToString()));  // CS8602
new RelayCommand(p => Console.WriteLine(p?.ToString())); // fine
```

That warning is a true positive. Use `RelayCommand<T>` when you want a parameter
whose absence is handled for you.

## Verification

```powershell
dotnet test tests\NekoLib.Mvvm.Tests\Unit\NekoLib.Mvvm.Tests.Unit.csproj
```

The suite runs on `net481` and `net9.0-windows`. Note that the shipped `net9.0`
assembly is therefore exercised through a Windows-flavoured host rather than its
own target framework — compatible, but not identical. No WinForms or WPF binding
pipeline is driven by it, so the threading guidance above rests on documented
framework behaviour rather than on an interactive run.
