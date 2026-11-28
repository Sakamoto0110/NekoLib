# NekoLib.Diagnostics

**Document ID:** DIAG-INTRODUCTION

**Schema version:** 1

**Kind:** reference

**Lifecycle:** current

**Subject:** concise consumer introduction to the NekoLib.Diagnostics family

**Surface:** introduction

**Boundary:** diagnostics

**Authority role:** non-normative

**Mutation:** authored

**Indexing:** include

NekoLib.Diagnostics is the opt-in incident-evidence family for unattended and
desktop applications. The cross-platform package coordinates unhandled-fault
reporting, bounded contributors, redacted text bundles, optional dump and tail
artifacts, and observable success or failure. The Windows adapter adds explicit
WinForms and WPF exception hooks, `dbghelp.dll` minidumps, and process-wide error-dialog
suppression without moving Windows dependencies into the core package.

Applications construct `CrashHandler` with application-owned Core contracts and
budgets, subscribe to outcome events, and call `Install()`. Folder writing
defaults to enabled and requires a nonblank `CrashRootDirectory` for the text
report. `DumpWriter` defaults to null. To request a native Windows dump, call
`UseMiniDump()` on the options before constructing the handler.

WinForms consumers call `WindowsCrash.HookWinForms()` before creating a
window. WPF consumers call `WindowsCrash.HookWpf(dispatcher)` on the owning
thread and dispose the returned lease there. By default the hook reports the
exception and leaves it unhandled.

Diagnostics owns incident sequencing and crash-bundle formatting. It does not
own logging, telemetry, inspection, notification transport, access control,
retention policy, or the lifetime of supplied dependencies. Start with the
[technical reference](REFERENCE.md) for the lifecycle and security boundaries.
Use the [module manifest](MANIFEST.md) for package, API, evidence, audit, and
migration routes.
