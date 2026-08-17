# Diagnostics Public API Review — 2026-08-17

**Kind:** audit

**Lifecycle:** current

**Subject:** F1-DIAG compiled public surface, crash-handler installation and
ownership, process-wide hook lifetime, incident collection budgets, crash-bundle
composition, partial-evidence contracts, redaction boundary, external
notification, and compatibility boundaries

**Status:** review complete; dispositions proposed and awaiting the consolidated
F1 decision gate

**Reference date:** 2026-08-17

**Reference commit:** `89f05b667be10104e8ef966ac9bebba7b7f13a23`

**Last reconciliation:** none

**Current state:** [`TODO.md`](../../TODO.md) F1-DIAG

## Baseline and authority

This review covers committed `HEAD` on branch
`phase-e/sqlserver-and-orchestration`. Before this artifact was added the
worktree and index were clean and the branch was 23 commits ahead of
`origin/phase-e/sqlserver-and-orchestration`. Nothing was pushed.

The reviewed authority is the `NekoLib.Diagnostics` project, all of its source,
its project file, the two assembly-derived manifests under
[`eng/public-api/NekoLib.Diagnostics/`](../../eng/public-api/NekoLib.Diagnostics),
the dual-target focused tests, the Core Logging/Telemetry/Inspection contracts
it consumes, the [public API and release policy](../public-api-release-policy.md),
and current repository consumer source. The historical
[Diagnostics boundaries review](diagnostics-boundaries-review-2026-07-30.md)
supplied leads only; every finding cited from it was reverified against current
code.

This review changes no product source, test, API baseline, package, changelog,
migration guide, or roadmap item.

The latest immutable package family is `1.0.0-local.19`. Its Diagnostics
assemblies were produced before this review and correspond to the reviewed
surface. They are prior evidence and are not evidence for anything proposed
here.

## Scope

Included:

- the six compiled public Diagnostics type declarations and all their public
  members, on both target frameworks;
- `CrashHandler` construction, validation, installation, the process-wide
  registry, and disposal;
- `AppDomain.UnhandledException` and `TaskScheduler.UnobservedTaskException`
  subscription lifetime and process-level side effects;
- crash dispatch, reentrancy latching, concurrent reports, and terminating
  versus non-terminating reports;
- event ordering, subscriber failure isolation, and subscriber budgeting;
- incident recording, log flush, and the bounded collection of Logging,
  Telemetry, and Inspection evidence;
- shared versus per-contributor budgets and the meaning of a timeout;
- crash-bundle directory and file ownership, tails, temporary versus final
  paths, and partial-bundle behavior;
- redaction, truncation, formatting, and the boundary between persisted
  evidence and in-process notification;
- dump-writer delegate ownership and failure behavior;
- option defaults, validation, and mutation after construction;
- target parity, package boundary, and documentation ownership.

Excluded:

- implementing any recommendation, editing product source or tests, or updating
  an accepted API manifest;
- moving Windows APIs, `dbghelp`, WER, or WinForms hooks into
  `NekoLib.Diagnostics` — explicitly out of bounds;
- the `NekoLib.Diagnostics.Windows` surface, reviewed separately in the
  companion F1-WIN artifact `diagnostics-windows-public-api-review-2026-08-17.md`;
- producing a package, launching a real crash, terminating a process, or
  exercising WER or minidump generation;
- Core contract changes, new project references, and any Inspection unfreeze.

## Package, ownership, and lifecycle boundary

`NekoLib.Diagnostics` targets `net481;net9.0`, enables `Nullable`, disables
`ImplicitUsings`, defines `NEKOLIB` plus the conditional `NETFRAMEWORK`/`NET_9`
symbols, and references only `NekoLib.Core`
([`NekoLib.Diagnostics.csproj`](../../src/Diagnostics/NekoLib.Diagnostics/NekoLib.Diagnostics.csproj)).
It contains no conditional compilation at all, so both targets compile the same
text. It does not reference the concrete Logging, Telemetry, or Inspection
packages, and must not.

The intended ownership split is:

- the **composition root** owns the crash root directory, the logger, the
  optional flusher and snapshot sources, the dump writer, the redactor, the
  external notifier, and every budget value;
- **Diagnostics** owns incident sequencing, contributor isolation, bundle
  layout, evidence formatting, truncation, and the guarantee that the crash path
  never throws;
- **`NekoLib.Diagnostics.Windows`** owns every Windows-specific facility.

Two facts qualify that split materially, and neither is documented anywhere
today:

1. `CrashHandler` is not only an instance. `Install()` enters a process-wide
   static registry and installs process-wide CLR hooks that are **never
   removed** (see DIAG-04).
2. `CrashHandlerOptions` is retained by reference and every value is re-read
   during the crash, so the options object is effectively shared mutable state
   between the caller and the handler (see DIAG-09).

## Compiled-surface inventory and recommended classification

Both manifests are byte-identical, so the surface below is the surface on
`net481` and on `net9.0`.

| Type | Kind | Public members | Recommended class |
|---|---|---|---|
| `CrashHandler` | sealed class, `IDisposable` | 1 ctor, 2 events, `Install`, `Dispose`, static `ReportExternalCrash` | Stable candidate |
| `CrashHandlerOptions` | sealed class | 1 ctor, 20 settable properties | Stable candidate except `NotifyWatchdog` |
| `CrashDetectedEventArgs` | sealed class, `EventArgs` | 1 ctor, 3 get-only properties | Stable candidate |
| `CrashBundleWrittenEventArgs` | sealed class, `EventArgs` | 1 ctor, 4 get-only properties | Stable candidate |
| `CrashDumpLevel` | enum | 6 values | Stable candidate |
| `CrashDumpWriter` | delegate | — | Stable candidate |

Totals: **6 public types, 42 public member declarations** (6 + 21 + 4 + 5 + 6
enum values), identical on both targets. One member,
`CrashHandlerOptions.NotifyWatchdog`, already carries an ordinary
`[Obsolete]` deprecation — not the `NEKOEXP0001` experimental marker — and is
the only proposed removal.

Nothing here is recommended for the experimental class. The extension seams
that matter (`CrashDumpWriter`, `ExternalNotifier`, `Redact`, `ExtraLines`, and
the four Core-contract properties) are delegates and interfaces, not
inheritance, so the sealed concrete types cost nothing.

## Downstream usage

Repository consumers, by `git grep` at the reference commit:

- `tests/NekoLib.Diagnostics.Tests/Unit/` — six `CrashHandler` tests plus one
  Windows adapter test, all reaching `HandleCrash` by reflection;
- `tests/NekoLib.Watchdog.Tests/Unit/WatchdogCrashBundleTests.cs` — the
  composition-level proof that `ExternalNotifier` can notify Watchdog and
  finalize a bundle;
- `tests/NekoLib.PackageConsumers/WinFormsSmokeProgram.cs` — a type-identity
  probe only.

No `runtime_tests/` scenario references Diagnostics. The Watchdog **product**
assembly does not reference Diagnostics; only its test project does. Per the
release policy, the absence of consumers does not prove a member is unused, and
no removal below is justified by consumer count.

## Observed facts, risks, and recommended dispositions

Every finding was reproduced against the reference commit with a disposable
console probe built against the Diagnostics and Core project references and run
on `net9.0`. The probe lived outside the repository and left no tracked change.
Because the assembly has no conditional compilation and both manifests are
identical, the observed behavior applies to `net481` as well; that inference is
stated, not measured.

### DIAG-01 — A failed crash bundle is invisible to the application

**Confirmed.** `WriteCrashArtifacts` wraps its entire body in `catch { }`
([`CrashHandler.cs:483`](../../src/Diagnostics/NekoLib.Diagnostics/CrashHandler.cs#L483)).
`CrashBundleWritten` is raised only on the success path
([`CrashHandler.cs:521`](../../src/Diagnostics/NekoLib.Diagnostics/CrashHandler.cs#L521)).
There is no failure event, no note, and no return value.

Probe, with a crash root whose parent is an existing *file* so the directory
cannot be created:

```text
events observed: detected,notify
```

`CrashDetected` fired and `ExternalNotifier` fired — so an application, and a
Watchdog wired through that notifier, is told a crash was captured — while the
evidence was silently discarded. For an unattended terminal this is the single
most consequential gap in the module: the one failure the operator must know
about is the one the module cannot report.

**Recommended disposition:** add an additive `CrashBundleFailed` event with a
`CrashBundleFailedEventArgs` carrying the attempted bundle directory and a
failure description, raised with the same subscriber isolation as the existing
events, and keep the crash path non-throwing. Document that exactly one of
`CrashBundleWritten` or `CrashBundleFailed` follows a crash when
`WriteCrashFolder` is true.

### DIAG-02 — `EvidenceCollectionTimeout` is used as two budgets at once, so a well-behaved contributor is misreported

**Confirmed.** `EvidenceCollectionTimeout` is documented as "Maximum wait for
each optional crash-evidence contributor"
([`CrashHandler.cs:62`](../../src/Diagnostics/NekoLib.Diagnostics/CrashHandler.cs#L62)).
It is used simultaneously as:

- the outer wall-clock `Thread.Join` bound in `RunContributor`
  ([`CrashHandler.cs:461`](../../src/Diagnostics/NekoLib.Diagnostics/CrashHandler.cs#L461));
- the cooperative budget handed to `ILogFlusher.Flush`
  ([`CrashHandler.cs:296`](../../src/Diagnostics/NekoLib.Diagnostics/CrashHandler.cs#L296));
- the cooperative budget handed to `IInspectionSnapshotSource.CaptureSnapshot`
  ([`CrashHandler.cs:396`](../../src/Diagnostics/NekoLib.Diagnostics/CrashHandler.cs#L396)).

Both shipped implementations consume their full supplied budget before
returning a partial answer: `Logger.Flush` returns `false` when the budget
expires
([`Logger.cs:173`](../../src/Logging/NekoLib.Logging/Logger.cs#L173)) and
`InspectionRuntime.CaptureSnapshot` records `<snapshot timed out>` per provider
([`InspectionRuntime.cs:206`](../../src/Inspection/NekoLib.Inspection/InspectionRuntime.cs#L206)).
The outer join therefore always expires first, and the contributor's own
correct answer is thrown away.

Probe, with a flusher that consumes its budget and then returns `false`
exactly as `Logger.Flush` does:

```text
Logging flush: timed out
```

The truthful note is "did not complete within its budget" — the flusher
answered. The bundle instead records a hang. The same race applies to
Inspection, where a snapshot that correctly degraded to `<snapshot timed out>`
is replaced wholesale by `<contributor timed out>`.

**Recommended disposition:** keep `EvidenceCollectionTimeout` as the
**cooperative budget the contributor is given**, and make the outer
abandonment join `EvidenceCollectionTimeout + 50 ms`, a fixed settle margin
whose only purpose is letting a contributor return an answer it has already
computed. Document the effective per-contributor wall clock as
`budget + 50 ms`, and document that the outer join exists solely for
contributors that ignore their budget.

### DIAG-03 — There is no total incident budget, and the composed worst case is undocumented

**Confirmed by construction.** One crash can invoke `RunContributor` for: the
fatal log, the flush, up to four evidence sections, the dump writer, one call
per configured tail file, and — when a redactor is configured — **one call per
redacted line**. Nothing bounds the sum.

Probe, one crash with a no-op redactor and a two-line exception:

```text
exception.ToString() lines=2; redactor invocations=8; elapsed=31 ms
```

Each of those eight invocations created and joined its own dedicated OS thread
(`Sanitize` → `RunContributor` →
[`CrashHandler.cs:450`](../../src/Diagnostics/NekoLib.Diagnostics/CrashHandler.cs#L450)).
The count grows linearly with stack-trace depth and incident-note count, on the
crash path of a process that is usually dying. The `_redactorUnavailable` latch
([`CrashHandler.cs:673`](../../src/Diagnostics/NekoLib.Diagnostics/CrashHandler.cs#L673))
bounds only the *failing* case; a redactor that succeeds slowly is charged the
full per-line cost every time.

**Recommended disposition:** two parts, both behavioral.

1. Redact the crash-text header and exception block as **one bounded batch**
   under a single contributor rather than one thread per line. This preserves
   the fail-closed latch and removes the thread-per-line cost.
2. Do **not** add a total-incident budget option. Document the composed worst
   case in the module reference instead: `(2 + evidence sections + dump +
   tail files + batched redaction) × (budget + margin)`, plus unbudgeted
   subscribers. Adding a global cap would change partial-evidence semantics
   that nothing has asked to change.

### DIAG-04 — Process-wide hooks outlive every handler, and unobserved task exceptions stay suppressed forever

**Confirmed.** `EnsureGlobalHandlersInstalled` latches `_globalHandlersInstalled`
and subscribes both CLR events
([`CrashHandler.cs:173`](../../src/Diagnostics/NekoLib.Diagnostics/CrashHandler.cs#L173)).
Nothing ever unsubscribes them or resets the latch. `OnUnobservedTaskException`
calls `e.SetObserved()` unconditionally, after dispatching to a registry that
may be empty
([`CrashHandler.cs:201`](../../src/Diagnostics/NekoLib.Diagnostics/CrashHandler.cs#L201)).

Probe, after installing and then disposing the only handler:

```text
_globalHandlersInstalled=True, registry count=0,
unobserved event reached=True, already SetObserved by NekoLib=True
```

So merely having constructed and installed a `CrashHandler` once permanently
changes the host process: every later unobserved task exception is marked
observed for the rest of the process lifetime, including after the application
has explicitly disposed its crash handling, and including for library code that
never asked for it. On `net481` with legacy escalation configured, that is a
silent policy change, not an implementation detail. The Logging module already
had to work around this exact behavior
([`Logger.cs:202`](../../src/Logging/NekoLib.Logging/Logger.cs#L202)).

**Recommended disposition:** remove both process-wide subscriptions when the
last installed handler is removed from the registry, resetting the latch under
the same lock, and call `SetObserved()` only when at least one installed
handler actually received the report. A library must not alter process
exception semantics beyond its own installed lifetime. Document the retained
policy — that an *installed* handler does mark unobserved task exceptions
observed, deliberately, because it has already recorded them.

### DIAG-05 — `Dispose()` is really `Uninstall()`, and a disposed handler can be revived

**Confirmed.** `_installed` doubles as the disposal flag: `Dispose()` swaps it
back to `0`
([`CrashHandler.cs:758`](../../src/Diagnostics/NekoLib.Diagnostics/CrashHandler.cs#L758)),
which re-arms `Install()`
([`CrashHandler.cs:161`](../../src/Diagnostics/NekoLib.Diagnostics/CrashHandler.cs#L161)).

Probe:

```text
dispatches after Install=1  afterDispose=1  afterReinstall=2
```

A handler that the application believes it disposed is silently live again, and
there is no `ObjectDisposedException` anywhere. Post-disposal behavior is
currently undefined by both code and documentation.

**Recommended disposition:** separate `_disposed` from `_installed`. `Dispose()`
becomes terminal and idempotent; `Install()` after disposal throws
`ObjectDisposedException`; a crash report reaching a disposed handler is inert.
Silence is the wrong default here: an application asking a disposed handler to
protect the process must be told it will not.

### DIAG-06 — Configured evidence caps are advisory, and `ExtraLines` is unbounded

**Confirmed.** `MaxRecentLogEntries`, `MaxRecentTelemetryOperations`, and
`MaxInspectionOperations` are passed to the supplied source and never enforced
locally; the formatters iterate whatever the source returned
([`CrashHandler.cs:355`](../../src/Diagnostics/NekoLib.Diagnostics/CrashHandler.cs#L355),
[`:365`](../../src/Diagnostics/NekoLib.Diagnostics/CrashHandler.cs#L365),
[`:407`](../../src/Diagnostics/NekoLib.Diagnostics/CrashHandler.cs#L407)).
`ExtraLines` has no cap at all. Only per-line length is bounded.

Probe, with `MaxRecentLogEntries = 5` and a snapshot source that ignores its
argument:

```text
entry lines written: 5000; extra lines written: 3000
```

An option named `Max...` that a third-party implementation can ignore is not a
bound. On a terminal with a full disk this is the difference between a crash
bundle and a failed write.

**Recommended disposition:** enforce the three configured caps locally after the
source returns, so the bound holds regardless of the implementation. Do **not**
add a `MaxExtraLines` option: `ExtraLines` is the application's own delegate and
the application owns its size. Document that boundary explicitly.

### DIAG-07 — One poisoned record destroys its entire evidence section

**Confirmed.** `entry.ToString()`
([`CrashHandler.cs:362`](../../src/Diagnostics/NekoLib.Diagnostics/CrashHandler.cs#L362))
and `operation.ToString()`
([`CrashHandler.cs:408`](../../src/Diagnostics/NekoLib.Diagnostics/CrashHandler.cs#L408))
are unguarded, unlike `SafeObjectText`
([`CrashHandler.cs:439`](../../src/Diagnostics/NekoLib.Diagnostics/CrashHandler.cs#L439)).
`LogEntry.ToString()` embeds `Exception.ToString()`, which application exception
types may override.

Probe, three log entries where the middle one carries an exception whose
`ToString()` throws:

```text
good-entry-1 kept=False  good-entry-2 kept=False
section=<contributor failed: NotSupportedException>
```

Both healthy records were lost. This directly contradicts the module's
partial-evidence promise: the failure is per-record, but the loss is
per-section.

**Recommended disposition:** format each record through the existing
`SafeObjectText` guard so a poisoned record becomes one
`<ToString threw: X>` line and every other record in the section survives.

### DIAG-08 — Two tail files with the same file name silently overwrite each other

**Confirmed.** The destination is `Path.Combine(bundleDir, Path.GetFileName(f))`
([`CrashHandler.cs:624`](../../src/Diagnostics/NekoLib.Diagnostics/CrashHandler.cs#L624)),
so `C:\a\app.log` and `C:\b\app.log` land on the same path.

Probe:

```text
single file content='from-b', note recorded=False
```

The first tail is gone and nothing records that it existed.

**Recommended disposition:** disambiguate colliding destination names
deterministically (`app.log`, `app-2.log`, …) and add one artifact note when a
collision occurs. Behavioral only.

### DIAG-09 — Options are re-read live during the crash, so constructor validation does not hold

**Confirmed.** The constructor validates `CrashRootDirectory`,
`EvidenceCollectionTimeout`, the three evidence limits, and the
`MaxEvidenceLineLength ≥ 64` floor
([`CrashHandler.cs:141`](../../src/Diagnostics/NekoLib.Diagnostics/CrashHandler.cs#L141)).
The handler then stores the options object itself and reads `_o.X` during the
crash.

Probe, mutating the same options object after construction:

```text
bundles after CrashRootDirectory=null -> 0 (silent)
MaxEvidenceLineLength=1 -> 'Source: p...<truncated>'
```

Setting the crash root to `null` silently disables bundling with no event and
no note; the validated `≥ 64` floor is trivially bypassed. `TailFiles` is a
mutable `List<string>` with a public setter and is enumerated during the crash,
so a concurrent mutation can also throw inside the contributor.

**Recommended disposition:** capture the option values into the handler at
construction — including a defensive copy of `TailFiles` — so validation
actually holds, matching the accepted `Logger` and `TelemetryPipeline` decision
to copy the supplied sink array. This is a real behavioral break for any caller
mutating options after construction, and it changes when
`WindowsCrash.UseMiniDump()` must be applied: **before constructing** the
handler, not merely before installing it. That documentation line lives in
`NekoLib.Diagnostics.Windows` and is the explicit dependency between this
review and the Windows review.

### DIAG-10 — Subscribers and the external notifier are not budgeted

**Confirmed.** `CrashDetected`, `CrashBundleWritten`, and `ExternalNotifier` are
invoked inline on the crashing thread with exception isolation but no timeout
([`:255`](../../src/Diagnostics/NekoLib.Diagnostics/CrashHandler.cs#L255),
[`:530`](../../src/Diagnostics/NekoLib.Diagnostics/CrashHandler.cs#L530),
[`:268`](../../src/Diagnostics/NekoLib.Diagnostics/CrashHandler.cs#L268)),
while every contributor is thread-bounded.

Probe, with `EvidenceCollectionTimeout = 100 ms`:

```text
a blocking CrashDetected subscriber held the crash path for 1553 ms
```

**Recommended disposition:** document the asymmetry; do not change the code.
Budgeting an application event handler would add thread churn on a dying process
and still could not cancel the handler. The honest contract is that subscribers
and the notifier are application code, run on the crashing thread, and must not
block.

### DIAG-11 — Redaction is a persistence filter, not an in-process barrier

**Confirmed by construction.** `Redact` is applied only in `Sanitize` and
`SanitizeInline`, both of which feed file writes.
`CrashDetectedEventArgs.Exception` hands the raw exception object to every
`CrashDetected` subscriber and to `ExternalNotifier`
([`CrashHandler.cs:96`](../../src/Diagnostics/NekoLib.Diagnostics/CrashHandler.cs#L96)).

This is a defensible boundary — a supervisor integration usually needs the real
exception — but it is undocumented, and "Redact" reads as a stronger guarantee
than it is.

**Recommended disposition:** document it. The redactor governs persisted
artifacts; the composition root owns what its subscribers and notifier do with
the raw exception. No code change.

### DIAG-12 — Concurrent and post-terminating crash reports are dropped without a record

**Confirmed.** `_crashing` is a per-handler latch reset in `finally` only when
the report was non-terminating
([`CrashHandler.cs:225`](../../src/Diagnostics/NekoLib.Diagnostics/CrashHandler.cs#L225)).

Probe, a second report arriving while the first is still collecting evidence:

```text
bundles written=1 (second report produced no artifact and no note)
```

After a `terminating` report the latch is permanent by design, so any later
report on that handler is also dropped.

**Recommended disposition:** document both behaviors as deliberate
de-duplication. Do not queue a second bundle on a dying process. Note that the
drop is silent so operators do not read one bundle as proof of one fault.

### DIAG-13 — `CrashBundleWrittenEventArgs.DumpPath` is populated even when no dump exists

**Confirmed.** `DumpPath` is always `<bundle>/crash.dmp` and `DumpWritten` is
`false` when no writer is configured or the writer failed
([`CrashHandler.cs:505`](../../src/Diagnostics/NekoLib.Diagnostics/CrashHandler.cs#L505)).
The path names a file that may not exist.

**Recommended disposition:** document that `DumpPath` is the reserved path and
that `DumpWritten` is the only authority for existence. No code or API change;
changing the property to nullable would be a compiled-surface break for no
benefit.

### DIAG-14 — Diagnostics has no current technical reference

**Confirmed.** There is no `src/Diagnostics/NekoLib.Diagnostics/README.md`, and
[`docs/README.md`](../README.md) registers no Diagnostics owner. Core, Data,
Logging, Telemetry, Inspection, Navigation, and HTTP all have one. The
contracts this review had to derive from source — ownership, installation
lifetime, budget composition, partial-evidence rules, redaction boundary, bundle
layout, event ordering — currently have no owner at all.

**Recommended disposition:** add `src/Diagnostics/NekoLib.Diagnostics/README.md`
and register it in the documentation index and `AGENTS.md` navigation table.

### DIAG-15 — `NotifyWatchdog` is a deprecated Watchdog-shaped gate whose stated preservation window has closed

**Confirmed.** `NotifyWatchdog` is `[Obsolete]` and now does nothing but gate
`ExternalNotifier` globally
([`CrashHandler.cs:80`](../../src/Diagnostics/NekoLib.Diagnostics/CrashHandler.cs#L80)).
The Watchdog-specific policy it was named for is gone: `NEKO_UNDER_WATCHDOG`
does not appear anywhere under `src/Diagnostics` (CRASH-02 reverified, closed —
see below). Its behavior is exactly duplicated by setting
`ExternalNotifier = null`. The only repository consumer is one test that exists
to cover the gate itself.

The E6 disposition preserved it "as an obsolete source-compatibility gate
**during Phase E**". Phase E is complete and archived, and the release policy
permits a candidate correction before the first stable baseline when the module
decision records the break and supplies migration guidance.

**Recommended disposition:** remove `NotifyWatchdog`. Migration: set
`ExternalNotifier = null` when no notification is wanted. This is the only
proposed removal and the only source-breaking change in F1-DIAG. If the decision
gate prefers zero breaks, the alternative is to keep it as-is and carry a
deprecated member into the first stable baseline permanently.

## Reverification of the historical findings

- **CRASH-01 — still confirmed, no change recommended.** The plain `net9.0`
  assembly still exposes Windows minidump vocabulary: `CrashDumpLevel` with
  `MiniDumpNormal` as the default, and the reserved `crash.dmp` artifact name.
  No second-platform requirement exists, and F6 portability remains gated. The
  2026-08-08 disposition — retain the compatibility surface, do not invent a
  platform-neutral artifact API — is still the right call. The only new work is
  recording it as a known constraint in the new module reference.
- **CRASH-02 — confirmed closed.** Diagnostics contains no Watchdog environment
  detection. `ExternalNotifier` runs after artifact creation and its failures
  are isolated. The residual is DIAG-15, which is about the deprecated gate, not
  about supervisor policy.
- **WIN-01** belongs to the Windows review and is reverified there.

## Target parity

Both `net481` and `net9.0` manifests are byte-identical, and the source has no
conditional compilation. There is no intentional target-specific API and no
accidental mismatch. Every recommended change must preserve that identity.

The one framework-behavior difference worth stating in documentation is that
`TaskScheduler.UnobservedTaskException` escalation policy differs between
`net481` and `net9.0`, which is exactly why DIAG-04 matters more on `net481`.

## Likely migration cost

| Disposition | Compiled surface | Behavior | Consumer action |
|---|---|---|---|
| DIAG-01 `CrashBundleFailed` | additive | additive | none; opt in to observe failures |
| DIAG-02 budget margin | none | contributor notes become truthful | none |
| DIAG-03 batched redaction | none | fewer threads, same output | none |
| DIAG-04 hook removal | none | process semantics restored after disposal | none, unless an application relied on suppression outliving disposal |
| DIAG-05 terminal `Dispose` | none | `Install()` after `Dispose()` throws | recreate the handler instead of re-installing |
| DIAG-06 enforced caps | none | oversized snapshots truncated | none |
| DIAG-07 per-record guard | none | more evidence survives | none |
| DIAG-08 tail names | none | second collision renamed | read `app-2.log` |
| DIAG-09 option capture | none | post-construction mutation ignored | configure options fully before construction; apply `UseMiniDump()` before constructing |
| DIAG-15 remove `NotifyWatchdog` | **breaking** | none | set `ExternalNotifier = null` |

A `docs/migrations/f1-diagnostics.md` guide is required: DIAG-15 changes the
compiled surface and DIAG-05/DIAG-09 change documented behavior.

## Core-contract conflict

None found. Every Core contract Diagnostics consumes — `ILogger`,
`ILogFlusher`, `ILogSnapshotSource`, `ITelemetrySnapshotSource`,
`IInspectionSnapshotSource` — is used exactly as F1-CORE accepted it, and no
recommendation requires a Core change, a new project reference, or an Inspection
unfreeze. DIAG-02 is a Diagnostics-side budget-composition fix; the Core
signatures that take a `TimeSpan` are correct as they stand.

## Rejected alternatives

- **A platform-neutral crash-artifact contract** (CRASH-01's original idea).
  Rejected: F6 is gated and no second platform exists. Designing the abstraction
  now would guess at requirements nobody has.
- **A total incident budget option.** Rejected: it changes partial-evidence
  semantics and adds a knob nothing has asked for; documenting the composed
  worst case is enough.
- **Budgeting `CrashDetected`/`CrashBundleWritten`/`ExternalNotifier`.**
  Rejected: cannot cancel application code, adds thread churn on a dying
  process, and hides a caller defect instead of naming it.
- **`MaxExtraLines`.** Rejected: `ExtraLines` is the caller's own delegate; the
  caller owns its size. The caps that need enforcing are the ones a third-party
  implementation can ignore.
- **Making `CrashBundleWritten` fire on failure with `DumpWritten = false`.**
  Rejected: it silently changes the meaning of an existing event and leaves
  `BundleDirectory`/`CrashTextPath` pointing at nothing.
- **Making `DumpPath` nullable.** Rejected: a compiled-surface break for a
  documentation problem.
- **Keeping `Dispose()` as a reversible uninstall.** Rejected: `IDisposable`
  already means terminal, and reviving a disposed handler is a silent trap.
  Creating a second handler is cheap and explicit.
- **Removing `SetObserved()` entirely.** Rejected: an *installed* handler that
  has already recorded the incident should keep suppressing escalation. Only the
  post-disposal and no-handler cases are wrong.
- **Splitting a `Diagnostics.Abstractions` package or a crash-contributor
  plug-in model.** Rejected: no consumer needs it, and the Core contracts
  already provide the inversion.
- **Moving anything Windows-specific into Diagnostics.** Explicitly out of
  bounds and architecturally wrong.

## Proposed implementation block after acceptance

If the dispositions are accepted, one narrow commit should:

1. record the accepted decisions in `TODO.md` F1-DIAG with package-pending
   evidence and leave the checkbox unchecked;
2. implement DIAG-01 through DIAG-09 and DIAG-15 in
   `src/Diagnostics/NekoLib.Diagnostics/CrashHandler.cs`;
3. add focused dual-target regressions for every changed behavior — bundle
   failure notification, budget margin, hook removal after last disposal,
   terminal disposal, enforced caps, per-record guard, tail-name collision,
   option capture, and `NotifyWatchdog` removal;
4. add `src/Diagnostics/NekoLib.Diagnostics/README.md` covering ownership,
   installation lifetime, budget composition, partial-evidence rules, bundle
   layout, redaction boundary, event ordering, the documented-only items
   (DIAG-10 to DIAG-14), and CRASH-01 as a known constraint;
5. add `docs/migrations/f1-diagnostics.md`;
6. update `CHANGELOG.md`, `docs/README.md`, and the `AGENTS.md` navigation
   table;
7. update both `NekoLib.Diagnostics` manifests for the accepted additive event
   and the `NotifyWatchdog` removal;
8. append a reconciliation section here without rewriting the snapshot above.

The Diagnostics.Windows implementation must follow, because DIAG-09 changes when
`UseMiniDump()` must be applied.

## Review validation

Commands run on Windows at the reference commit, before this artifact was
added:

```text
dotnet test tests/NekoLib.Diagnostics.Tests/Unit/NekoLib.Diagnostics.Tests.Unit.csproj
  net481:          7 passed, 0 failed, 0 skipped
  net9.0-windows:  7 passed, 0 failed, 0 skipped

git grep NEKO_UNDER_WATCHDOG -- src/Diagnostics
  no match (CRASH-02 closed)

git grep '#if|#else|#endif' -- src/Diagnostics
  no match (no conditional compilation on either target)

diff eng/public-api/NekoLib.Diagnostics/net481.approved.txt
     eng/public-api/NekoLib.Diagnostics/net9.0.approved.txt
  identical
```

Twelve disposable probes were built against the Diagnostics and Core project
references and run on `net9.0` outside the repository. They reproduced DIAG-01
through DIAG-10 and DIAG-12 and were deleted; no repository file changed.

## Residual validation limits

- Every probe result was measured on `net9.0` only. The `net481` claim rests on
  identical manifests and the absence of conditional compilation, not on
  measurement.
- No real crash, process termination, minidump, or WER behavior was exercised;
  none was authorized.
- No package was produced and no package-consumer probe was run.
- The full solution was not rebuilt or tested for this review.
- The bundle-directory name has only millisecond resolution and no collision
  handling, so two handlers dispatching the same crash inside one millisecond
  would merge into one directory and overwrite each other's `crash.txt`. This
  did **not** reproduce — the two handlers were always more than a millisecond
  apart — so it is recorded as an unreproduced theoretical risk and is
  deliberately **not** promoted as a finding.
- `MiniDumpWriter` behavior, `CrashSuppressor`, and the WinForms hook are out of
  scope here and are covered by the Windows review.

## Decision gate

DIAG-01 through DIAG-09 and DIAG-15 are recommended as accepted work.
DIAG-10 through DIAG-14 are recommended as documentation-only. CRASH-01 is
recommended for closure with no product change. Nothing here may be implemented
until the consolidated F1 decision gate accepts or modifies these dispositions.
