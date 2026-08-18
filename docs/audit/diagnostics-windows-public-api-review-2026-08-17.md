# Diagnostics.Windows Public API Review — 2026-08-17

**Kind:** audit

**Lifecycle:** current

**Subject:** F1-WIN compiled public surface, WinForms exception-hook ownership
and installation contract, minidump composition and native failure handling,
WER-suppression scope, process-wide state ownership, target parity, and the
Diagnostics package boundary

**Status:** all dispositions accepted and implemented, with one recorded
deviation; package gate pending

**Reference date:** 2026-08-17

**Reference commit:** `ef533e2bca9ae8f86a8ecec7ae4d7bcf778077bf`

**Last reconciliation:** 2026-08-17 — dispositions accepted and implemented

**Current state:** [`TODO.md`](../../TODO.md) F1-WIN

## Baseline and authority

This review covers committed `HEAD` on branch
`phase-e/sqlserver-and-orchestration`, which at the time of review was the
F1-DIAG review commit. The reviewed product source is unchanged from
`89f05b667be10104e8ef966ac9bebba7b7f13a23`; the only commit in between added the
companion Diagnostics review artifact and its index entries. The worktree and
index were clean before this artifact was added, the branch was 24 commits ahead
of `origin/phase-e/sqlserver-and-orchestration`, and nothing was pushed.

The reviewed authority is the `NekoLib.Diagnostics.Windows` project, all three
of its source files, its project file, the two assembly-derived manifests under
[`eng/public-api/NekoLib.Diagnostics.Windows/`](../../eng/public-api/NekoLib.Diagnostics.Windows),
[`WindowsCrashTests.cs`](../../tests/NekoLib.Diagnostics.Tests/Unit/WindowsCrashTests.cs),
the `NekoLib.Diagnostics` surface it extends, the
[public API and release policy](../public-api-release-policy.md), and current
repository consumer source. The historical
[Diagnostics boundaries review](diagnostics-boundaries-review-2026-07-30.md)
supplied WIN-01 as a lead only; it was reverified against current code.

This review changes no product source, test, API baseline, package, changelog,
migration guide, or roadmap item.

## Dependency on the proposed Diagnostics decisions

This review is written against the **proposed** F1-DIAG dispositions in
[`diagnostics-public-api-review-2026-08-17.md`](diagnostics-public-api-review-2026-08-17.md).
Two of them change what this package must do, and both must be reverified
against the accepted Diagnostics implementation before F1-WIN is implemented:

1. **DIAG-09 (option capture at construction).** If accepted, option values are
   snapshotted by the `CrashHandler` constructor, so
   `WindowsCrash.UseMiniDump()` must be applied **before constructing** the
   handler. Its XML documentation currently says "Call before installing the
   handler"
   ([`WindowsCrash.cs:22`](../../src/Diagnostics/NekoLib.Diagnostics.Windows/WindowsCrash.cs#L22)),
   which would become wrong. If DIAG-09 is rejected, that line stays correct and
   WIN-04 below reduces to a clarification.
2. **DIAG-02/DIAG-03 (contributor budgets).** These keep the dump writer running
   on a `CrashHandler`-owned background thread. WIN-03 below is a direct
   consequence of that placement and would change shape if the decision gate
   instead moved dump writing onto the crashing thread.

No proposed Windows disposition requires a Diagnostics API change, and none
moves Windows behavior into `NekoLib.Diagnostics`.

## Scope

Included:

- both compiled public type declarations and all three public members, on both
  target frameworks;
- `WindowsCrash.HookWinForms` installation, idempotence, failure behavior, and
  ownership of process-wide WinForms exception policy;
- WinForms UI-thread exception routing and terminating semantics;
- `WindowsCrash.UseMiniDump` delegate installation and options ownership;
- `MiniDumpWriter` level mapping, exception-context composition, native failure
  handling, and artifact hygiene;
- `MiniDumpWriter` remaining internal behind the focused public facade;
- `CrashSuppressor` scope, restoration, nesting, and process-wide state;
- concurrency and process-wide state in all three types;
- unsupported-platform and target behavior;
- the Diagnostics versus Diagnostics.Windows package boundary;
- target parity and documentation ownership.

Excluded:

- implementing any recommendation, editing product source or tests, or updating
  an accepted API manifest;
- generating a real minidump, launching a real crash, terminating a process, or
  exercising WER — explicitly gated, and deliberately not done;
- the `NekoLib.Diagnostics` surface, reviewed in the companion artifact;
- enabling `Nullable` on this project, which the campaign fixes as disabled;
- any Linux or second-platform design.

## Package, ownership, and lifecycle boundary

`NekoLib.Diagnostics.Windows` targets `net481;net9.0-windows`, sets
`UseWindowsForms`, **disables `Nullable`**, disables `ImplicitUsings`, defines
`NEKOLIB` and `WINFORMS` plus the conditional `NETFRAMEWORK`/`NET_9` symbols,
and references only `NekoLib.Diagnostics`
([`NekoLib.Diagnostics.Windows.csproj`](../../src/Diagnostics/NekoLib.Diagnostics.Windows/NekoLib.Diagnostics.Windows.csproj)).
It contains no conditional compilation, so both targets compile the same text.

The boundary is correct and should be preserved exactly as it is: every
`DllImport`, every WinForms type reference, and every Windows policy decision
lives here, which is what lets `NekoLib.Diagnostics` stay on plain `net9.0`.

What this package actually owns, and what nothing documents today, is
**process-wide operating-system and WinForms state**:

| Call | Process-wide effect | Reversible | Observable |
|---|---|---|---|
| `WindowsCrash.HookWinForms()` | sets the application-wide `UnhandledExceptionMode` and adds a permanent `Application.ThreadException` subscriber | no | no — returns `void` and swallows failures |
| `WindowsCrash.UseMiniDump()` | none; mutates the supplied options object | yes, by reassigning `DumpWriter` | yes, via `CrashHandlerOptions.DumpWriter` |
| `CrashSuppressor.Enable()` | replaces the whole process error mode | no — the previous mode is discarded | no |

Two of the three are irreversible, unobservable process-state mutations. That is
defensible for a kiosk composition root calling them once in `Main`, and it is
the accepted E6 policy — but it must be stated, and the failure of
`HookWinForms()` must stop being silent.

## Compiled-surface inventory and recommended classification

| Type | Kind | Public members | Recommended class |
|---|---|---|---|
| `WindowsCrash` | static class | `HookWinForms()`, `UseMiniDump(this CrashHandlerOptions)` | Stable candidate |
| `CrashSuppressor` | static class | `Enable()` | Stable candidate |

Totals: **2 public types, 3 public member declarations** on each target. Nothing
is recommended for removal, internalization, or the experimental class, and
nothing is recommended for addition. This is the smallest surface in the F1
campaign and the recommendation is that the whole of it is intentionally stable;
every finding below is behavioral or documentary.

`MiniDumpWriter` is `internal`
([`MiniDumpWriter.cs:8`](../../src/Diagnostics/NekoLib.Diagnostics.Windows/MiniDumpWriter.cs#L8))
and must stay internal. It is reachable only as the delegate that
`UseMiniDump()` installs, which is the correct shape: the P/Invoke signature,
the `MINIDUMP_*` structures, and the flag enum are implementation detail behind
a two-word facade.

## Downstream usage

- `tests/NekoLib.Diagnostics.Tests/Unit/WindowsCrashTests.cs` — one test,
  covering hook idempotence and single dispatch.
- `tests/NekoLib.PackageConsumers/WinFormsSmokeProgram.cs` — a type-identity
  probe referencing `NekoLib.Diagnostics.Windows.WindowsCrash`.

`CrashSuppressor.Enable()` and `WindowsCrash.UseMiniDump()` have **no** automated
coverage anywhere in the repository, and no `runtime_tests/` scenario exercises
this package. Per the release policy, that absence justifies no removal.

## Observed facts, risks, and recommended dispositions

Findings marked *probe-confirmed* were reproduced with a disposable dual-target
WinForms console probe built outside the repository and run on **both**
`net481` and `net9.0-windows`. No minidump was generated and no crash was
launched.

### WIN-01 (historical) — reverified and closed

**Closed.** `HookWinForms()` now takes a lock, guards on
`_winFormsHookInstalled`, and installs one named handler for the process
lifetime
([`WindowsCrash.cs:39`](../../src/Diagnostics/NekoLib.Diagnostics.Windows/WindowsCrash.cs#L39)).
The two misspelled filenames are gone: the tracked files are
`CrashSuppressor.cs` and `MiniDumpWriter.cs`. `WindowsCrashTests` calls
`HookWinForms()` twice and asserts exactly one dispatch. The 2026-08-08
disposition — one-shot process policy, no reversible handle, because
`CrashHandler`'s registry already controls active recipients — is still the
right call and no further work is recommended for it.

WIN-02 below is a *different* defect in the same method that the WIN-01
disposition did not consider.

### WIN-02 — `HookWinForms()` silently installs nothing when a window already exists

**Confirmed, probe-confirmed on both targets.** The method wraps the mode change
and the subscription in one `try`
([`WindowsCrash.cs:46`](../../src/Diagnostics/NekoLib.Diagnostics.Windows/WindowsCrash.cs#L46)):

```csharp
Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
Application.ThreadException += OnThreadException;
_winFormsHookInstalled = true;
```

`Application.SetUnhandledExceptionMode` throws `InvalidOperationException` once
a window has been created on the thread. The `catch { }` swallows it, the
subscription line **never runs**, and `_winFormsHookInstalled` stays `false`.
The method returns `void`, so the application is told nothing.

Probe, after forcing `Form.Handle` and then calling the two statements
separately:

```text
net9.0-windows:  SetUnhandledExceptionMode threw=True
                 ThreadException subscription alone caught the UI-thread fault: True
net481:          SetUnhandledExceptionMode threw=True
                 ThreadException subscription alone caught the UI-thread fault: True
```

Two things follow. First, any WinForms application that calls
`WindowsCrash.HookWinForms()` after constructing its shell — which the current
documentation, "Call at startup, after handlers are installed"
([`WindowsCrash.cs:36`](../../src/Diagnostics/NekoLib.Diagnostics.Windows/WindowsCrash.cs#L36)),
does not warn against — gets **no UI-thread crash reporting at all**, silently.
Second, the mode call is not required: a bare `Application.ThreadException`
subscription received the UI-thread fault on both targets.

The existing regression does not catch this because the xunit host has no window
on the test thread, so the mode call succeeds there.

**Recommended disposition:** best-effort the mode change in its own `try`, then
**always** attempt the subscription, and set `_winFormsHookInstalled` from the
subscription rather than from the pair. Correct the XML documentation to say
that the mode change requires being called before any window is created, and
that the hook still functions without it. No public signature changes. Add a
regression that installs the hook after a window handle exists and asserts that
dispatch still happens.

### WIN-03 — the minidump records a bystander thread and a null exception context

**Confirmed, probe-confirmed on both targets.** `MiniDumpWriter.TryWrite` builds
`MINIDUMP_EXCEPTION_INFORMATION` from *ambient thread state*
([`MiniDumpWriter.cs:67`](../../src/Diagnostics/NekoLib.Diagnostics.Windows/MiniDumpWriter.cs#L67)):

```csharp
ThreadId = GetCurrentThreadId(),
ExceptionPointers = Marshal.GetExceptionPointers(),
ClientPointers = false
```

But `CrashHandler` does not call the dump writer on the crashing thread. It runs
every contributor, including the dump writer, on a freshly created background
thread
([`CrashHandler.cs:450`](../../src/Diagnostics/NekoLib.Diagnostics/CrashHandler.cs#L450),
[`:510`](../../src/Diagnostics/NekoLib.Diagnostics/CrashHandler.cs#L510)).
Both values are therefore read from a thread that has no exception in flight.

Probe reproducing exactly that shape — throw, catch, then start a background
thread named like the contributor:

```text
net9.0-windows:  faulting thread    id=49692 exceptionPointers=304057470544
                 contributor thread id=51116 exceptionPointers=0
net481:          faulting thread    id=25292 exceptionPointers=1550532100200
                 contributor thread id=46280 exceptionPointers=0
```

So the structure handed to `MiniDumpWriteDump` names a NekoLib worker thread and
a **null** `ExceptionPointers`, while claiming `ClientPointers = false` — that
is, claiming those pointers are valid in this process. A dump still contains
every thread, so it is not worthless, but the exception stream that makes a
debugger open on the fault is fabricated from the wrong thread.

`crash.txt` cannot rescue this either: it records
`Thread.CurrentThread.ManagedThreadId`
([`CrashHandler.cs:574`](../../src/Diagnostics/NekoLib.Diagnostics/CrashHandler.cs#L574)),
a **managed** id, while a minidump indexes **native** thread ids. There is no
way for an analyst to correlate the two.

**Recommended disposition, Windows-side and sufficient on its own:** stop
asserting an exception context that does not exist. When
`Marshal.GetExceptionPointers()` returns `IntPtr.Zero`, pass `NULL` for the
`ExceptionParam` argument through an overload that takes `IntPtr`, so the dump
is honest rather than mislabelled. Keep the current structure only when the
writer genuinely runs on a thread with an exception in flight.

**Companion, Diagnostics-side — requires extending the accepted F1-DIAG set:**
also record the crashing thread's **native** thread id in `crash.txt` alongside
the managed id, so the faulting thread can be located inside the dump. This is
raised here because the defect is only visible from the Windows package, and it
is flagged explicitly for the decision gate rather than folded silently into
F1-DIAG.

**Explicitly unresolved:** whether `MiniDumpWriteDump` succeeds, fails, or
produces a dump without an exception stream when given a zero
`ExceptionPointers` was **not** measured, because generating a minidump is
gated. Resolving it requires explicit authorization.

### WIN-04 — `UseMiniDump()` overwrites any existing dump writer, and its "before installing" guidance is fragile

**Confirmed.** `UseMiniDump` assigns unconditionally
([`WindowsCrash.cs:25`](../../src/Diagnostics/NekoLib.Diagnostics.Windows/WindowsCrash.cs#L25)),
so a previously configured `CrashDumpWriter` is replaced without a signal. The
null check is correct and throws `ArgumentNullException`.

Its documented contract is "Call before installing the handler". That is
correct today only because `CrashHandler` re-reads options live — the exact
behavior DIAG-09 proposes to remove.

**Recommended disposition:** document that `UseMiniDump()` replaces any existing
writer and that it must be applied before the handler is constructed if DIAG-09
is accepted, and correct the XML documentation accordingly. Do not add a
"don't overwrite" guard: last-writer-wins on an options property is the normal
expectation, and a silent no-op would be worse.

### WIN-05 — a failed dump leaves an empty or truncated `crash.dmp` in the bundle

**Confirmed by construction.** `TryWrite` opens the file with `FileMode.Create`
([`MiniDumpWriter.cs:63`](../../src/Diagnostics/NekoLib.Diagnostics.Windows/MiniDumpWriter.cs#L63))
before calling `MiniDumpWriteDump`. If the native call returns `false`, or the
`using` block unwinds on an exception, the created — and possibly zero-length or
partially written — file stays in the crash bundle. `CrashHandler` records
`Dump writer: no dump was written.`
([`CrashHandler.cs:515`](../../src/Diagnostics/NekoLib.Diagnostics/CrashHandler.cs#L515))
while a file named `crash.dmp` sits next to `crash.txt`.

The `DllImport` declares `SetLastError = true`
([`MiniDumpWriter.cs:29`](../../src/Diagnostics/NekoLib.Diagnostics.Windows/MiniDumpWriter.cs#L29))
but the last error is never read, so the failure reason is discarded.

**Recommended disposition:** delete the artifact when the native call does not
succeed, so `DumpWritten = false` and "no `crash.dmp` in the bundle" agree.
Capture `Marshal.GetLastWin32Error()` immediately after the call so the reason
is available to a future diagnostic path; surfacing it through the current
`bool`-returning `CrashDumpWriter` contract would be a public delegate break and
is rejected.

### WIN-06 — the `CrashDumpLevel` ladder is not cumulative

**Confirmed.** `Map` returns exactly one `MiniDumpType` flag per level
([`MiniDumpWriter.cs:42`](../../src/Diagnostics/NekoLib.Diagnostics.Windows/MiniDumpWriter.cs#L42)).
Because `MiniDumpNormal` is `0`, each level is "normal plus one flag" — so
`WithFullMemory` does **not** include handle data or thread info, and
`WithThreadInfo` does not include data segments. The `CrashDumpLevel`
documentation describes them as increasing levels where "Bigger levels = bigger
files"
([`CrashDumpLevel.cs:3`](../../src/Diagnostics/NekoLib.Diagnostics/CrashDumpLevel.cs#L3)),
which reads as a nested ladder that does not exist.

**Recommended disposition:** document the exact level-to-flag mapping in the
module reference. Do **not** make the mapping cumulative: that would silently
change dump content and size for every existing consumer of `WithHandleData` and
`WithThreadInfo`, which is a behavioral break with no requester.

### WIN-07 — an out-of-range dump level silently degrades to a normal dump

**Confirmed.** `Map`'s `default` branch returns `MiniDumpNormal`
([`MiniDumpWriter.cs:51`](../../src/Diagnostics/NekoLib.Diagnostics.Windows/MiniDumpWriter.cs#L51)),
so a cast integer outside the enum produces a smaller dump than requested with
no signal. `CrashDumpLevel.None` is handled correctly and earlier, returning
`false` without creating a file
([`MiniDumpWriter.cs:57`](../../src/Diagnostics/NekoLib.Diagnostics.Windows/MiniDumpWriter.cs#L57)).

**Recommended disposition:** document the fallback. Throwing on the crash path
would be wrong, and the current degradation is the safe direction.

### WIN-08 — `CrashSuppressor.Enable()` replaces the whole process error mode and cannot be restored

**Confirmed, probe-confirmed on both targets.** `Enable()` calls `SetErrorMode`
with a fixed flag set and discards the returned previous mode
([`CrashSuppressor.cs:29`](../../src/Diagnostics/NekoLib.Diagnostics.Windows/CrashSuppressor.cs#L29)).
There is no restore, no nesting, no query, and no way to learn what the process
mode was.

Probe, reading the mode before any NekoLib call on a plain WinForms host:

```text
net9.0-windows:  GetErrorMode before -> 0x8001
net481:          GetErrorMode before -> 0x8001
```

The host already had `SEM_FAILCRITICALERRORS | SEM_NOOPENFILEERRORBOX` set. In
this case the replacement happens to be a superset, but the pattern is a
process-wide stomp: any flag another component set that is not in NekoLib's
three is discarded. `SEM_NOALIGNMENTFAULTEXCEPT` is declared in the internal enum
and never used
([`CrashSuppressor.cs:18`](../../src/Diagnostics/NekoLib.Diagnostics.Windows/CrashSuppressor.cs#L18)).

**Recommended disposition:** merge instead of replace — `GetErrorMode()` is
available on every supported Windows version, so `SetErrorMode(GetErrorMode() |
flags)` preserves the host's existing flags. This is the same principle as
DIAG-04: a library must not silently discard process state it did not set.
Document that the effect is process-wide, permanent for the process lifetime,
not nestable, and that it suppresses the interactive error **UI** only — it does
not stop WER from generating or queueing a report.

### WIN-09 — WinForms UI-thread exceptions are reported as non-terminating, and the shell keeps running

**Confirmed by construction.** `OnThreadException` forwards with
`terminating: false`
([`WindowsCrash.cs:58`](../../src/Diagnostics/NekoLib.Diagnostics.Windows/WindowsCrash.cs#L58)).
Combined with `CrashHandler`'s reentrancy latch, which resets only for
non-terminating reports
([`CrashHandler.cs:249`](../../src/Diagnostics/NekoLib.Diagnostics/CrashHandler.cs#L249)),
this means a WinForms application keeps running after a UI-thread fault and can
produce one crash bundle per fault.

For an unattended kiosk that is very likely the desired behavior — the shell
survives a page-level bug — but it is a deliberate policy that nothing states,
and combined with `SetUnhandledExceptionMode(CatchException)` it converts what
would otherwise terminate the process into a recoverable event.

**Recommended disposition:** document it as the intended contract. Do not
change the flag.

### WIN-10 — nullability annotations are absent from this package alone

**Confirmed and required.** `Nullable` is `disable` here and `enable` in every
other `src/` project, so the two public signatures carry no nullability metadata
while the `NekoLib.Diagnostics` types they reference do. The difference is
visible in the manifests: `UseMiniDump(this CrashHandlerOptions options)` has no
annotation, though the method does throw `ArgumentNullException` for a null
argument
([`WindowsCrash.cs:27`](../../src/Diagnostics/NekoLib.Diagnostics.Windows/WindowsCrash.cs#L27)).

The campaign fixes this setting as disabled, and enabling it would change the
compiled manifests on both targets.

**Recommended disposition:** document the deliberate difference and the runtime
null contract in the module reference. No project or code change.

### WIN-11 — automated coverage exists for one of the three public members

**Confirmed.** `WindowsCrashTests` covers `HookWinForms` idempotence only.
`UseMiniDump` and `CrashSuppressor.Enable` have no test at all, and both have
deterministic, dump-free behavior that is straightforward to cover:
`UseMiniDump` installs a non-null `DumpWriter`, returns the same instance, and
throws on null; `Enable()` is safe to call repeatedly and — under the WIN-08
disposition — preserves pre-existing mode flags.

**Recommended disposition:** add those focused dual-target regressions alongside
the WIN-02 post-window-creation regression. None of them generates a dump or
launches a crash.

### WIN-12 — Diagnostics.Windows has no documentation owner

**Confirmed.** There is no README for this package and
[`docs/README.md`](../README.md) registers no owner for it. Every process-wide
effect catalogued above is currently undocumented.

`NekoLib.Navigation.WinForms` and `NekoLib.Navigation.Wpf` set the repository
precedent: platform adapters have no separate README and are covered by their
owning module's reference.

**Recommended disposition:** do **not** create a second README for a
three-member package. Cover Diagnostics.Windows as a dedicated section inside
the new `src/Diagnostics/NekoLib.Diagnostics/README.md` proposed by DIAG-14,
covering the hook contract and its window-creation ordering requirement, the
UI-thread non-terminating policy, the dump level mapping, the exception-context
limitation, WER-suppression scope, and the nullability difference. Register that
one file as the owner for both packages.

## Target parity

The two manifests differ only by two SDK-generated assembly attributes present
on `net9.0-windows` and absent on `net481`:

```text
[assembly: System.Runtime.Versioning.SupportedOSPlatform("Windows7.0")]
[assembly: System.Runtime.Versioning.TargetPlatform("Windows7.0")]
```

That is an intentional target difference, not an accidental mismatch: it is how
the `-windows` TFM declares platform support, and it is what stops a non-Windows
.NET 9 consumer from referencing the package cleanly. `net481` has no equivalent
concept. Every public type, member, and signature is otherwise identical, and
every recommended change must preserve that.

All probe results in this review were obtained on **both** targets and agreed on
both, including the `SetUnhandledExceptionMode` failure, the bare-subscription
routing, the contributor-thread exception pointers, and the pre-existing process
error mode.

## Likely migration cost

| Disposition | Compiled surface | Behavior | Consumer action |
|---|---|---|---|
| WIN-02 subscribe independently | none | the hook now installs after window creation | none; previously broken calls start working |
| WIN-03 null exception param | none | dump no longer claims a false exception context | none |
| WIN-03 companion native thread id | none in this package | `crash.txt` gains a field | none |
| WIN-04 documentation | none | none | apply `UseMiniDump()` before constructing the handler if DIAG-09 is accepted |
| WIN-05 delete failed dump | none | no empty `crash.dmp` in the bundle | none |
| WIN-08 merge error mode | none | host flags preserved | none |
| WIN-06/07/09/10 | none | none | none |

No public type, member, signature, target, or dependency changes. A
`docs/migrations/f1-diagnostics-windows.md` guide is **not** required on its own
merits; the one consumer-visible instruction — when to call `UseMiniDump()` —
belongs in `docs/migrations/f1-diagnostics.md` next to the DIAG-09 change that
causes it. If the decision gate prefers a per-module guide for symmetry, a short
one is cheap.

## Core-contract conflict

None. This package references only `NekoLib.Diagnostics` and touches no Core
contract. No recommendation requires a Core change, a new project reference, a
frozen-module change, or an Inspection unfreeze.

## Rejected alternatives

- **Moving `MiniDumpWriter`, `CrashSuppressor`, or the WinForms hook into
  `NekoLib.Diagnostics`.** Explicitly out of bounds and architecturally wrong;
  it would force the cross-platform assembly onto `net9.0-windows`.
- **Making `MiniDumpWriter` public.** Rejected: the P/Invoke signature and the
  `MINIDUMP_*` structures are implementation detail, and `UseMiniDump()` is the
  supported entry point. Publishing it would freeze a native interop contract.
- **Changing `HookWinForms()` to return `bool`.** Rejected: `void` → `bool` is
  binary-breaking, and WIN-02 removes the failure it would report. If a future
  need for observability appears, an additive overload is available.
- **Adding `UnhookWinForms()` or a disposable hook handle.** Rejected, and this
  reaffirms the 2026-08-08 WIN-01 disposition: `CrashHandler`'s installed-handler
  registry already controls who receives a report, so a second removal mechanism
  adds process-wide state without adding capability.
- **Changing the `CrashDumpWriter` delegate to return a richer result** so the
  Win32 error can be reported. Rejected: a public delegate break for a
  diagnostic nicety.
- **Making the dump-level mapping cumulative.** Rejected: a silent content and
  size change for existing consumers.
- **Restoring the previous error mode on some `Disable()` call.** Rejected: WER
  suppression is a process-lifetime kiosk decision, not a scope; the merge in
  WIN-08 addresses the real problem, which is discarding other components'
  flags.
- **A separate `src/Diagnostics/NekoLib.Diagnostics.Windows/README.md`.**
  Rejected: three public members, and the Navigation adapters set the opposite
  precedent.
- **Enabling `Nullable` on this project.** Out of bounds for this campaign and
  it would change both manifests.

## Proposed implementation block after acceptance

If the dispositions are accepted, one narrow commit **after** the Diagnostics
implementation should:

1. reverify DIAG-02, DIAG-03, and DIAG-09 against the landed Diagnostics
   implementation before writing any code, and stop if the accepted shape
   differs from the assumptions recorded above;
2. record the accepted decisions in `TODO.md` F1-WIN with package-pending
   evidence and leave the checkbox unchecked;
3. implement WIN-02, WIN-03 (Windows half), WIN-05, and WIN-08 in
   `src/Diagnostics/NekoLib.Diagnostics.Windows/`;
4. add the focused dual-target regressions described in WIN-11 and WIN-02;
5. add the Diagnostics.Windows section to
   `src/Diagnostics/NekoLib.Diagnostics/README.md`, covering WIN-04, WIN-06,
   WIN-07, WIN-09, WIN-10, and the process-state table above;
6. update `CHANGELOG.md` and, if DIAG-09 is accepted, the `UseMiniDump()`
   instruction in `docs/migrations/f1-diagnostics.md`;
7. verify that both `NekoLib.Diagnostics.Windows` manifests are **unchanged**;
8. append a reconciliation section here without rewriting the snapshot above.

## Review validation

Commands run on Windows at the reference commit:

```text
dotnet test tests/NekoLib.Diagnostics.Tests/Unit/NekoLib.Diagnostics.Tests.Unit.csproj
  net481:          7 passed, 0 failed, 0 skipped
  net9.0-windows:  7 passed, 0 failed, 0 skipped

git grep '#if|#else|#endif' -- src/Diagnostics
  no match (no conditional compilation on either target)

diff eng/public-api/NekoLib.Diagnostics.Windows/net481.approved.txt
     eng/public-api/NekoLib.Diagnostics.Windows/net9.0-windows.approved.txt
  two SDK-generated platform attributes only
```

A disposable dual-target WinForms console probe was built and run on both
`net481` and `net9.0-windows` outside the repository, then deleted. It measured
`Marshal.GetExceptionPointers()` on faulting versus contributor threads,
`Application.SetUnhandledExceptionMode` after window creation, bare
`Application.ThreadException` routing through a real message loop, `dbghelp.dll`
resolution, and the pre-existing process error mode. No repository file changed.

## Residual validation limits

- **No minidump was generated.** Whether `MiniDumpWriteDump` succeeds, fails, or
  omits the exception stream when given a zero `ExceptionPointers` is
  unresolved and needs explicit authorization to settle. `dbghelp.dll` was
  confirmed loadable on this machine on both targets; that is a resolution check,
  not dump evidence.
- No real crash, process termination, or WER dialog was exercised.
- `CrashSuppressor.Enable()` was **not** invoked; the probe only read the
  process error mode. Its suppression effect is unverified by this review.
- The WIN-02 probe reproduced the failing call sequence directly rather than
  through `WindowsCrash.HookWinForms()`, because the real method's process-wide
  latch cannot be reset within one process. The source path is unambiguous.
- No package was produced and no package-consumer probe was run.
- The full solution was not rebuilt or tested for this review.

## Decision gate

WIN-02, WIN-03, WIN-05, and WIN-08 are recommended as accepted work. WIN-04,
WIN-06, WIN-07, WIN-09, WIN-10, and WIN-12 are recommended as
documentation-only. WIN-11 is recommended as test-only. WIN-01 is confirmed
closed with no further work. The WIN-03 Diagnostics-side companion explicitly
extends the F1-DIAG accepted set and must be approved separately. Nothing here
may be implemented until the consolidated F1 decision gate accepts or modifies
these dispositions, and the Diagnostics assumptions must be reverified first.

## Reconciliation — 2026-08-17: dispositions accepted and implemented

The observed facts, probe output, and original recommendations above are the
snapshot and are unchanged. This section records the decision-gate outcome and
the implementation.

### Diagnostics assumptions reverified before implementation

The three dependencies this review declared on the proposed F1-DIAG decisions
were reverified against the landed implementation, as the review required:

- **DIAG-02/DIAG-03 hold.** `WriteCrashArtifacts` still invokes the dump writer
  through `RunContributor`, so it runs on a `CrashHandler`-owned background
  thread. WIN-03 stands exactly as written.
- **DIAG-09 was accepted and landed.** `_dumpWriter = options.DumpWriter` is
  captured by the constructor, so `UseMiniDump()` must be applied before
  constructing the handler. The XML documentation and the F1-DIAG migration guide
  say so.

No proposed Windows disposition needed to change shape.

### Accepted and implemented

WIN-02, WIN-03, WIN-05, and WIN-08 landed as code. WIN-04, WIN-06, WIN-07,
WIN-09, WIN-10, and WIN-12 landed as the Diagnostics.Windows section inside
`src/Diagnostics/NekoLib.Diagnostics/README.md`, following the Navigation adapter
precedent rather than adding a second README. WIN-11 landed as five focused
regressions. WIN-01 remains closed.

### One deliberate deviation

WIN-05 recommended capturing `Marshal.GetLastWin32Error()` after a failed native
call "so the reason is available to a future diagnostic path". **That half was
not implemented.** `NekoLib.Diagnostics.Windows` declares no `InternalsVisibleTo`,
the `CrashDumpWriter` delegate returns `bool` and cannot carry a reason, and no
other consumer exists — so the captured value would be unreadable dead state in a
shipped assembly, which is precisely what an API finalization pass should avoid.

The substantive half of WIN-05 — deleting the file the writer created when the
dump did not succeed — is implemented. If a diagnostic channel for the native
error is ever wanted, it needs a real consumer and its own decision.

### Validation

```text
dotnet build src/Diagnostics/NekoLib.Diagnostics.Windows/NekoLib.Diagnostics.Windows.csproj
  net481 and net9.0-windows: 0 warnings, 0 errors

dotnet test tests/NekoLib.Diagnostics.Tests/Unit/NekoLib.Diagnostics.Tests.Unit.csproj
  net481:          21 passed, 0 failed, 0 skipped
  net9.0-windows:  21 passed, 0 failed, 0 skipped

eng/verify-public-api.ps1 -PackageId NekoLib.Diagnostics.Windows
  both baselines verified UNCHANGED, as this review predicted

eng/verify-public-api.ps1 -PackageId NekoLib.Diagnostics
  both baselines still verified

eng/verify-docs.ps1        passed
git diff --check           clean
```

The five new regressions cover the post-window hook path, `UseMiniDump`
installation, writer replacement, null-options rejection, and repeatable
error-mode merging. The post-window test asserts that
`Application.SetUnhandledExceptionMode` really does throw in that state, re-arms
the process-wide latch through reflection, and restores both the latch and the
subscription count afterwards.

### Residual limits carried forward

Every limit recorded in the original snapshot still applies, and the most
important one is unchanged:

- **No minidump was generated.** Whether `MiniDumpWriteDump` succeeds with a NULL
  exception parameter — the WIN-03 change — is still **unverified**. The
  implementation is a correctness argument about not asserting a false exception
  context, not measured dump evidence. Settling it requires explicit
  authorization to generate a dump.
- WIN-05's delete-on-failure path is likewise unexercised, because provoking a
  real native failure requires the same authorization.
- `CrashSuppressor.Enable()` is now invoked by a test, but only its merge
  behaviour is asserted; no WER dialog was suppressed or observed.
- No full-solution build or test run was performed for this module block, no
  package was produced, and no PackageReference consumer probe was run.
