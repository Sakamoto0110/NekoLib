# Module Documentation Index

**Document ID:** GLOBAL-MODULE-INDEX

**Schema version:** 1

**Kind:** reference

**Lifecycle:** current

**Subject:** bounded-context routing for the module-first documentation tree

**Surface:** index

**Boundary:** global

**Authority role:** normative

**Mutation:** authored

**Indexing:** include

This directory is the module-first entry point. A boundary becomes canonical
here only after its structural migration is reviewed. During the transition,
unmigrated boundaries continue to use the technical references registered in
the [global documentation index](../README.md).

| Boundary | Status | Entry point |
|---|---|---|
| Mvvm | Structural pilot | [`Mvvm/MANIFEST.md`](Mvvm/MANIFEST.md) |
| Core | Not migrated | Current source-adjacent reference |
| Data | Not migrated | Current source-adjacent reference |
| Devices | Populated module-first boundary | [`Devices/MANIFEST.md`](Devices/MANIFEST.md) |
| Diagnostics / Windows | Not migrated | Current source-adjacent reference |
| Hosting | Not migrated; unshipped | Current project/source evidence |
| Http | Not migrated | Current source-adjacent reference |
| Inspection | Not migrated | Current source-adjacent reference |
| Logging | Not migrated | Current source-adjacent reference |
| Navigation / WinForms / Wpf | Populated module-first boundary | [`Navigation/MANIFEST.md`](Navigation/MANIFEST.md) |
| Pipes | Populated module-first boundary | [`Pipes/MANIFEST.md`](Pipes/MANIFEST.md) |
| Telemetry | Not migrated | Current source-adjacent reference |
| Watchdog / Host | Not migrated | Current source-adjacent references |

Payments remains a proposal, not an active module. Logging, Telemetry, and
Inspection remain separate capabilities; this index does not create an
Observability module.
