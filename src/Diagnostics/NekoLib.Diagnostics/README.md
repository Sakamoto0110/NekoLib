# NekoLib.Diagnostics

**Kind:** reference

**Lifecycle:** current

**Subject:** incident collection, crash-bundle composition, evidence budgets,
redaction boundary, handler lifecycle, and the Windows crash adapter

**Reference date:** 2026-08-17

`NekoLib.Diagnostics` turns an unhandled exception into a readable, bounded
crash bundle. It is an opt-in `net481`/`net9.0` package that references only
`NekoLib.Core`. Windows-specific facilities live in the separate
`NekoLib.Diagnostics.Windows` package, documented at the end of this file.

## Composition

```csharp
var crashes = new CrashHandler(new CrashHandlerOptions
{
    CrashRootDirectory = Path.Combine(AppContext.BaseDirectory, "crash"),
    Logger = logger,
    TelemetrySnapshotSource = telemetry,
    InspectionSnapshotSource = inspection,
    EvidenceCollectionTimeout = TimeSpan.FromMilliseconds(250)
});

crashes.CrashBundleWritten += (s, e) => Console.WriteLine("bundle: " + e.BundleDirectory);
crashes.CrashBundleFailed  += (s, e) => Console.WriteLine("LOST: " + e.Reason);

crashes.Install();
```

The composition root owns the crash root directory, the logger, the optional
flusher and snapshot sources, the dump writer, the redactor, the external
notifier, and every budget. Diagnostics owns incident sequencing, contributor
isolation, bundle layout, formatting, truncation, and the guarantee that the
crash path never throws.

Diagnostics never references the concrete Logging, Telemetry, or Inspection
packages. It consumes only the Core contracts you supply.

## Options are captured at construction

**Every option value is read once, by the constructor.** Mutating the
`CrashHandlerOptions` instance afterwards does not affect that handler, and
`TailFiles` is copied rather than aliased.

This makes the constructor's validation meaningful: a handler cannot later find
itself with a null crash root or a `MaxEvidenceLineLength` below the documented
floor. Configure the options object completely before constructing the handler —
including `WindowsCrash.UseMiniDump()`.

## Installation and lifetime

`Install()` adds the handler to a process-wide registry and, for the first
installed handler, subscribes `AppDomain.UnhandledException` and
`TaskScheduler.UnobservedTaskException`.

- `Install()` is idempotent.
- `Install()` and `Dispose()` serialize their registry transition. If they race,
  terminal disposal wins and a disposed handler cannot be registered afterwards.
- **`Dispose()` is terminal**, not a reversible uninstall. A disposed handler
  stops receiving reports, and `Install()` on it throws
  `ObjectDisposedException`. Create a new handler instead of re-arming one.
- When the **last** installed handler is disposed, both process-wide
  subscriptions are removed and the process returns to its prior exception
  semantics. A later `Install()` re-arms them.

While at least one handler is installed, an unobserved task exception is marked
observed after it has been recorded — deliberately, because the incident is
already captured and escalation would kill a process that has just written its
evidence. When **no** handler records the report, `SetObserved()` is not called:
the library does not change process behaviour on behalf of code that never asked
for it.

## Crash sequence

For each report a handler processes, in order:

1. `CrashDetected` fires;
2. the fatal event is logged and a bounded log flush is requested;
3. evidence contributors run;
4. the bundle is written;
5. `CrashBundleWritten` **or** `CrashBundleFailed` fires — exactly one of them
   when `WriteCrashFolder` is true;
6. `ExternalNotifier` runs, if configured.

`CrashBundleFailed` exists so an unattended terminal can observe that incident
evidence was lost. Without subscribing to it, a failed bundle is indistinguishable
from a successful one: `CrashDetected` and the notifier still fire.

### Reentrancy and concurrency

A handler processes one crash at a time. A second report arriving while the first
is still collecting is **dropped silently** — deliberate de-duplication, not a
queue. After a report marked `terminating`, the handler is latched permanently
and processes nothing further.

Do not read one bundle as proof of exactly one fault.

### Subscribers are not budgeted

`CrashDetected`, `CrashBundleWritten`, `CrashBundleFailed`, and `ExternalNotifier`
run inline on the crashing thread. Their exceptions are isolated, but there is no
timeout: a blocking subscriber holds the crash path for as long as it blocks.
They are application code and must not block.

This is the opposite of evidence contributors, which are thread-bounded.

## Evidence budgets

`EvidenceCollectionTimeout` is the **cooperative budget handed to each
contributor**. It is passed to `ILogFlusher.Flush` and to
`IInspectionSnapshotSource.CaptureSnapshot` so those implementations can return a
correct partial answer.

Each contributor also runs on its own thread, joined for
`EvidenceCollectionTimeout + 50 ms`. That settle margin exists only so a
well-behaved contributor can return an answer it has already computed; joining on
the exact budget would report a correct partial result as a hang. The outer join
is a safety net for contributors that ignore their budget.

**A timed-out contributor is abandoned, not cancelled.** Its thread keeps running
and its result is discarded.

There is no total incident budget. The composed worst case is roughly:

```text
(fatal log + flush + evidence sections + dump + tail files + one redaction batch)
    x (EvidenceCollectionTimeout + 50 ms)
```

plus whatever unbudgeted subscribers take. Size `EvidenceCollectionTimeout` with
that in mind.

## Bounded evidence

`MaxRecentLogEntries`, `MaxRecentTelemetryOperations`, and
`MaxInspectionOperations` are passed to the supplied source **and enforced
locally**. A source that ignores its argument is truncated, and the report records
`<truncated at N …>`. The bound holds regardless of the implementation you supply.

`MaxEvidenceLineLength` truncates each persisted line and must be at least 64.

`ExtraLines` is deliberately **not** bounded by Diagnostics: it is your own
delegate and you own its size.

A record whose `ToString()` throws becomes a single `<ToString threw: X>` line;
the rest of its section survives. One poisoned record never destroys a section.

## Redaction

`Redact` is applied to the crash-text header, the exception block, incident and
artifact notes, evidence lines, and file tails, before anything is persisted.

- The crash-text block is redacted as **one bounded batch**, not once per line.
- Redaction **fails closed**: if the redactor throws or outlives its budget,
  nothing from that block is persisted unredacted, and the redactor is not
  retried for the rest of the incident.

**`Redact` is a persistence filter, not an in-process barrier.**
`CrashDetectedEventArgs.Exception` hands the raw exception to every
`CrashDetected` subscriber and to `ExternalNotifier`. What those do with it is
yours to decide.

## Bundle layout

```text
<CrashRootDirectory>/crash-<yyyy-MM-dd_HH-mm-ss-fffZ>/
    crash.txt
    crash.dmp          (only when a dump writer succeeded)
    <tail files>
```

`CrashBundleWrittenEventArgs.DumpPath` is the **reserved** path. The file exists
only when `DumpWritten` is true; `DumpWritten` is the only authority.

Two configured tail files sharing a file name are disambiguated — `app.log`,
`app-2.log` — and the collision is recorded in the artifact notes rather than one
silently overwriting the other.

### Locating the faulting thread in a dump

`crash.txt` records `ManagedThreadId`, which is a managed identifier. A minidump
indexes **native** OS thread ids, and the two do not correlate. There is no
portable way to obtain a native thread id from a cross-platform assembly, so
Diagnostics deliberately does not print one.

To find the faulting thread in a dump, match the exception stack trace recorded
in `crash.txt` against the thread stacks in the dump.

## Known constraint: Windows vocabulary in a cross-platform assembly

`CrashDumpLevel` uses Windows minidump vocabulary, defaults to `MiniDumpNormal`,
and the bundle reserves the fixed name `crash.dmp` — in a package that targets
plain `net9.0`. This is a deliberate, retained compatibility surface. Introducing
a platform-neutral crash-artifact contract requires a concrete second-platform
requirement, which does not exist today.

Windows P/Invoke stays isolated in `NekoLib.Diagnostics.Windows`.

## Writing a custom dump writer

`CrashDumpWriter` is the platform extension seam behind
`CrashHandlerOptions.DumpWriter`. It is a synchronous delegate that receives the
reserved `crash.dmp` path and the captured `CrashDumpLevel`. Configure it before
constructing `CrashHandler`, because options are copied at construction.

The writer must create the artifact at the supplied path and return `true` only
after the file is complete. Return `false` when no dump was written. The handler
passes `CrashDumpLevel.None` to a configured custom writer rather than suppressing
the call; a writer that follows the built-in meaning must return `false` without
creating a file for that value. Writer exceptions, false returns, and timeouts
become artifact notes and do not suppress `crash.txt` or the remaining tails.

The callback runs as a bounded Diagnostics contributor without a cancellation
token. Keep it self-bounded and avoid process-wide state changes. It may execute
on a background thread during an unhandled incident, so it must not require the
UI thread, a healthy request context, or services that are already tearing down.
`CrashBundleWrittenEventArgs.DumpWritten`, not file-path presence alone, is the
reported outcome.

## NekoLib.Diagnostics.Windows

The Windows adapter targets `net481`/`net9.0-windows` and references only
`NekoLib.Diagnostics`. It is the only `src/` project with `Nullable` disabled, so
its two public methods carry **no nullability annotations** — `UseMiniDump` still
throws `ArgumentNullException` for a null argument.

```csharp
CrashSuppressor.Enable();                       // as early as possible in Main
var options = new CrashHandlerOptions { /* … */ }.UseMiniDump();
var crashes = new CrashHandler(options);
crashes.Install();
WindowsCrash.HookWinForms();                    // before creating any window
```

### `WindowsCrash.HookWinForms()`

Installs the WinForms `Application.ThreadException` hook, forwarding to
`CrashHandler.ReportExternalCrash`. Since A4 this hook is **not** automatic; a
WinForms application must call it explicitly.

**Call it before creating any window.** It also sets the application-wide
`UnhandledExceptionMode`, and .NET throws `InvalidOperationException` if that is
attempted after a window exists on the thread. The mode change is best-effort and
the `ThreadException` subscription is installed either way, so the hook still
functions when the mode could not be set — but calling it early is what makes the
behaviour deterministic.

The subscription is installed at most once for the process lifetime; repeated
calls are safe and do not multiply dispatch. There is no unhook: `CrashHandler`'s
installed-handler registry already controls who receives a report.

UI-thread exceptions are reported as **non-terminating**, so the shell keeps
running and can produce one bundle per fault. For an unattended kiosk that is
usually what you want; be aware that the application continues after a UI fault.

### `WindowsCrash.UseMiniDump()`

Routes crash dumps through the `dbghelp.dll` minidump writer. Apply it to the
options **before constructing the handler**, since options are captured at
construction. It replaces any previously configured `DumpWriter`.

Level mapping — note that the levels are **not cumulative**, each is
`MiniDumpNormal` plus one flag:

| `CrashDumpLevel` | `MiniDumpWriteDump` flags |
|---|---|
| `None` | no dump is written, and no file is created |
| `MiniDumpNormal` | `MiniDumpNormal` |
| `WithDataSegs` | `MiniDumpWithDataSegs` |
| `WithHandleData` | `MiniDumpWithHandleData` |
| `WithThreadInfo` | `MiniDumpWithThreadInfo` |
| `WithFullMemory` | `MiniDumpWithFullMemory` |

`WithFullMemory` does not include handle data or thread info. A value outside the
enum degrades to a normal dump rather than throwing on the crash path.

Bigger levels mean bigger files and more sensitive process memory on disk. Keep
`MiniDumpNormal` for field machines.

### `CrashSuppressor.Enable()`

Suppresses the interactive Windows error UI — WER dialogs and critical-error
popups — by merging `SEM_FAILCRITICALERRORS`, `SEM_NOGPFAULTERRORBOX`, and
`SEM_NOOPENFILEERRORBOX` into the current process error mode.

It is **process-wide, permanent for the process lifetime, and not nestable**.
It merges rather than replaces, so flags another component set are preserved. It
suppresses the **UI only** — it does not stop WER from generating or queueing a
report.

## Verification

```powershell
dotnet test tests\NekoLib.Diagnostics.Tests\Unit\NekoLib.Diagnostics.Tests.Unit.csproj
```

The suite covers both packages on `net481` and `net9.0-windows`, using temporary
filesystem crash bundles. Real minidump generation, WER-dialog suppression, and
live `Application.ThreadException` dispatch through a message loop are **not**
exercised by it and remain unverified platform behaviour.
