# NekoLib.Mvvm

**Document ID:** MVVM-INTRODUCTION

**Schema version:** 1

**Kind:** reference

**Lifecycle:** current

**Subject:** concise consumer introduction to NekoLib.Mvvm

**Surface:** introduction

**Boundary:** mvvm

**Authority role:** non-normative

**Mutation:** authored

**Indexing:** include

`NekoLib.Mvvm` is a deliberately small, dependency-free binding-helper package
for `net481` and `net9.0`. It provides `ViewModelBase`, `RelayCommand`, and
`RelayCommand<T>` for applications that use `INotifyPropertyChanged` and
`ICommand`, including WinForms and WPF consumers.

Start with the [technical reference](REFERENCE.md) for coercion, notification,
threading, exception, and nullability contracts. Use the
[module manifest](MANIFEST.md) to find API baselines, migrations, audits,
validation surfaces, source, and tests.
