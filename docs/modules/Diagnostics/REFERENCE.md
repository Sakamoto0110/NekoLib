# NekoLib.Diagnostics

**Document ID:** DIAG-REFERENCE

**Schema version:** 1

**Kind:** reference

**Lifecycle:** current

**Subject:** incident collection, crash-bundle composition, evidence budgets, redaction boundary, handler lifecycle, and the Windows crash adapter

**Surface:** technical-reference

**Boundary:** diagnostics

**Authority role:** normative

**Mutation:** authored

**Indexing:** include

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

Construction rejects a null options object, an enabled crash folder without a
non-blank root, a non-positive `EvidenceCollectionTimeout`, negative log,
telemetry, or inspection caps, and a `MaxEvidenceLineLength` below 64.
`TailLines` may be non-positive; that intentionally writes no tail content.

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

`ExtraLines` is deliberately **not count-bounded** by Diagnostics: it is your
own delegate and you own how many records it returns. The callback still runs
as a time-bounded contributor, and every line that reaches persistence is
redacted and truncated to `MaxEvidenceLineLength`.

A record whose `ToString()` throws becomes a single `<ToString threw: X>` line;
the rest of its section survives. One poisoned record never destroys a section.

`ILogFlusher` and `IInspectionSnapshotSource` receive the cooperative timeout.
The log and telemetry snapshot contracts receive only their maximum-item count;
Diagnostics supplies their outer contributor-thread budget. These contracts
cannot cancel an abandoned worker, and their returned records remain
caller-owned. Configured tail-file count is also not bounded: existing,
non-blank paths are processed sequentially, each on its own bounded contributor,
while `TailLines` bounds the retained line queue for each file.

## Redaction

`Redact` is applied to the crash-text header, the exception block, incident and
artifact notes, evidence lines, and file tails, before anything is persisted.

- The crash-text block is redacted as **one bounded batch**, not once per line.
- Redaction **fails closed**: if the redactor throws or outlives its budget,
  nothing from that block is persisted unredacted, and that handler latches the
  redactor unavailable for the rest of its lifetime. Later lines and later
  accepted incidents receive an unavailable/failure marker instead of another
  redaction attempt.

**`Redact` is a persistence filter, not an in-process barrier.**
`CrashDetectedEventArgs.Exception` hands the raw exception to every
`CrashDetected` subscriber and to `ExternalNotifier`. What those do with it is
yours to decide. Redaction is never applied to native dump bytes; dump access,
storage, and retention require a separate secrecy policy.

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

Directory allocation has millisecond resolution and no handler-specific nonce.
Applications installing multiple handlers should use distinct roots unless they
explicitly accept the possibility that handlers reporting the same process fault
can select one directory. This is tracked as
[`DIAG-FINDING-001`](FINDINGS.md#diag-finding-001), not as a confirmed defect.

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

## Extension and callback boundaries

Diagnostics deliberately exposes small composition seams rather than a general
crash-contributor plug-in model:

| Seam | Execution and ownership contract |
|---|---|
| `ILogger` | Receives the fatal event on a bounded contributor thread. Diagnostics does not own or dispose the logger. |
| `ILogFlusher` | Receives the cooperative contributor budget. A timeout abandons the worker; it does not cancel it. |
| `ILogSnapshotSource` | Supplies recent entries; Diagnostics locally enforces `MaxRecentLogEntries`. |
| `ITelemetrySnapshotSource` | Supplies completed operations; Diagnostics locally enforces `MaxRecentTelemetryOperations`. |
| `IInspectionSnapshotSource` | Supplies a budget-aware snapshot; Diagnostics locally enforces `MaxInspectionOperations` and cannot invoke Inspection actions. |
| `ExtraLines` | Supplies an unbounded number of application-defined lines on a bounded contributor thread. Persisted lines are redacted and truncated. |
| `TailFiles` | Supplies caller-owned paths. The list is copied at construction; files remain caller-owned and are read sequentially under per-file contributor budgets. Missing and blank paths are skipped. |
| `Redact` | Filters dynamic crash text before persistence, fails closed, and is latched unavailable for the rest of the handler lifetime after failure or timeout. It never filters dump bytes or in-process callbacks. |
| `CrashDumpWriter` | Creates the reserved dump artifact on a bounded contributor thread without cancellation. |
| Events and `ExternalNotifier` | Run inline, receive raw in-process data, are exception-isolated, and have no timeout. The application must keep them non-blocking. |

The handler does not dispose any supplied dependency. Composition, dependency
lifetime, access control, retention, transport, and out-of-process notification
remain application responsibilities.

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
After a timeout, the abandoned writer can still finish and create a late file.
`CrashBundleWrittenEventArgs.DumpWritten`, not file-path presence alone, is the
event-time reported outcome.

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

The built-in writer always targets the current process. On the contributor
thread there is normally no native exception in flight, so it passes a NULL
exception parameter rather than falsely labelling that background thread as the
faulting one. When a native exception pointer is available, it supplies the
current native thread id and that pointer. This is crash-time best effort, not a
guarantee that a dump contains an exception stream or identifies the managed
faulting thread.

`FileMode.Create` can leave an incomplete artifact when the native call fails.
The adapter therefore attempts to delete `crash.dmp` after a false native return
or an exception. Cleanup is best effort; `DumpWritten` remains false even if an
incomplete file cannot be removed.

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
exercised by it and remain unverified platform behaviour. See
[`VALIDATION_REQUIREMENTS.md`](VALIDATION_REQUIREMENTS.md) for the qualifying
evidence contract and [`VALIDATIONS.md`](VALIDATIONS.md) for preserved results.
