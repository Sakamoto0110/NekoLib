# NekoLib.Inspection

**Document ID:** INSP-INTRODUCTION

**Schema version:** 1

**Kind:** reference

**Lifecycle:** current

**Subject:** concise consumer introduction to the NekoLib.Inspection boundary

**Surface:** introduction

**Boundary:** inspection

**Authority role:** non-normative

**Mutation:** authored

**Indexing:** include

NekoLib.Inspection answers "what was this process doing just now" without a
debugger attached. A module records bounded operation evidence and registers
pull-based state providers; a reader captures both through the read-only
`IInspectionSnapshotSource`. `NekoLib.Diagnostics` is the shipped reader — it
folds a budgeted snapshot into a crash bundle.

It is **opt-in and off by default**. A composition root either constructs an
`InspectionRuntime` it owns, or installs one process-wide runtime through
`EnableGlobal()` so modules can push through the Core provider slot without a
separate enabled branch. With nothing installed, the Core slot stays a non-null
null object and every call is inert.

The stable product is **passive**. Inspection records, retains, and hands back
what it was given. It does not persist, transmit, redact, truncate, authorize,
or remotely expose anything, and a consumer that persists or transmits a
snapshot owns all of that.

Two boundaries matter before adopting it. **Concrete action registration and
invocation are explicitly experimental** under `NEKOEXP0001` and are not an
authorization boundary — the compiler marker is release signaling, not access
control, and no NekoLib module registers an action. And **broad module
instrumentation remains frozen**: Navigation is the only feature module that
records, so evidence you see from another module is your own application calling
`Record`, not the library emitting. The freeze and its unfreeze conditions live
in [`ROADMAP.md`](../../../ROADMAP.md).

Inspection is a capability separate from Logging and Telemetry. They share Core
contracts and a composition root, not an implementation or an authority.

Start with the [technical reference](REFERENCE.md) for identity, ordering,
budget, lifecycle, and the experimental boundary. Use the
[module manifest](MANIFEST.md) for package, API, evidence, audit, and migration
routes.
