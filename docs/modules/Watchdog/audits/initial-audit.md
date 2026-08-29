# Watchdog Module — First-Pass Audit

**Document ID:** WDG-AUDIT-INITIAL

**Schema version:** 1

**Kind:** audit

**Lifecycle:** historical

**Subject:** Watchdog first-pass review

**Surface:** audit

**Boundary:** watchdog

**Authority role:** evidence

**Mutation:** snapshot

**Indexing:** include

**Reference date:** 2026-06-04

**Reference commit:** not recorded

**Original path:** docs/audit/watchdog-first-pass.md

**Last reconciliation:** 2026-08-01

**Current state:** [Watchdog technical reference](../REFERENCE.md) and [`TODO.md`](../../../../TODO.md)

**Branch:** `watchdog/audit/first-pass`  
**Date:** 2026-06-04  
**Scope:** `src/Watchdog/NekoLib.Watchdog/` + `src/Watchdog/NekoLib.Watchdog.Host/`

---

## 1. Module Overview

The Watchdog module is a process supervisor for desktop applications. It monitors a target executable, restarts it on crash, exposes an RPC control channel over named pipes, and bundles crash artifacts for post-mortem analysis.

**Two deliverables:**

| Project | Output type | Targets |
|---------|------------|---------|
| `NekoLib.Watchdog` | Library | `net481` + `net9.0-windows` |
| `NekoLib.Watchdog.Host` | WinExe | `net481` + `net9.0-windows` |

**Dependency graph:**

```
NekoLib.Watchdog.Host
  └─ NekoLib.Watchdog
       ├─ NekoLib.Pipes        (PipeServer, PipeClient, PipeEventClient)
       ├─ NekoLib.Core         (ILogSink, LogEntry, ITelemetrySink)
       └─ Newtonsoft.Json 13.0.3  (net481 only; net9 uses System.Text.Json)
```

**Runtime environment variable:** `NEKO_UNDER_WATCHDOG=1` is injected into the child process's environment so apps can detect they are supervised.

---

## 2. File-by-File Inventory

### `NekoLib.Watchdog` (Library)

#### `WatchdogRuntime.cs` — 613 lines — Core supervisor

The largest and most critical file. Manages the full lifecycle of the supervised process.

**Threads started:**
- `WDG-Monitor` (foreground) — `MonitorLoop`: starts child, waits for exit, detects crash loop, restarts
- `WDG-Hotkeys` (background) — `HotkeyLoop`: Win32 message pump for global hotkeys

**Locks:**
- `_childLock` — guards `_child` `Process` reference
- `_stopLock` — makes `Stop()` idempotent (sets `_stopped` flag)
- `_logLock` — guards file write in `Log()`
- `_bufferLock` — guards `_logBuffer` ring queue

**Volatile flags:** `_enabled`, `_shutdownRequested`, `_exiting`, `_started`, `_stopped`

**RPC handlers registered:**

| Command | Action |
|---------|--------|
| `"ping"` | Returns `"pong"` |
| `"status"` | Returns `BuildTelemetry()` snapshot |
| `"pause"` | Sets `_enabled = false` |
| `"resume"` | Sets `_enabled = true` |
| `"restart"` | Kills child; monitor loop restarts it |
| `"stop"` | Calls `Stop(true)` |
| `"log_history"` | Returns ring buffer contents |
| `"exception_notify"` | Logs a crash report from the supervised app |
| `"attach_status"` | After startup readiness, returns the currently supervised PID + Host-session token identity |
| `"log_write"` / `"log_write_batch"` | Receives application logs into the structured watchdog stream |
| `"update"` | Returns explicit `not_implemented` (or `updates_disabled`) |

**Kill sequence** (`TryKill`): graceful via `CloseMainWindow()` → wait `GracefulKillTimeoutMs` → force via `taskkill /PID {n} /T /F` with hardcoded 5 s wait.

**Crash loop detection:** 5+ exits within 3 s → 10 s cooldown. Counter resets on any clean run ≥ 3 s.

**Structured log ring buffer:** max 300 entries (`MaxBufferedLogs`). Live events published via `_rpc.Events.PublishAsync("log", entry)`. On overflow, oldest entry silently dequeued — no warning emitted.

**Nested `Win32` class:** declares P/Invoke for `RegisterHotKey`, `GetMessage`, `TranslateMessage`, `DispatchMessage`, `PostThreadMessage`, `CreateWindowExW`. Uses a message-only window (`HWND_MESSAGE`) for hotkey registration.

---

#### `WatchdogController.cs` — 236 lines — Client-side static facade

Used by the supervised application to communicate with its watchdog.

- **Pipe name resolution:** SHA1 of the current process's full exe path (lowercased), first 16 hex chars → `NekoLib.Watchdog.<16-char-hash>`. Computed once at class load, cached in `_pipeName`.
- **`Send(cmd)`:** synchronous wrapper around `PipeClient.SendAsync`. Returns string; surfaces `TimeoutException` as `"error=watchdog_not_running"`, all others as `"error=pipe_io"`.
- **`NotifyException(type, message, source)`:** fire-and-forget, always swallows exceptions. Intended for unhandled exception handlers.
- **Log subscription:** `SubscribeLogs(Action<LogEvent>)` — replays history via `log_history` RPC then subscribes live via `PipeEventClient`. Returns `IDisposable` (the event client).
- **Dual JSON deserialization:** `#if NET9` uses `System.Text.Json`; `#else` uses `Newtonsoft.Json.Linq`.

---

#### `WatchdogOptions.cs` — 188 lines — Immutable configuration DTO

All configuration for `WatchdogRuntime`. Validated and defaults applied by `Normalize()`.

**Key properties and defaults:**

| Property | Default | Notes |
|----------|---------|-------|
| `TargetPath` | — | Required; resolved to full path |
| `PipeName` | auto | Overwritten by `Normalize()` with SHA1 hash |
| `RestartDelayMs` | 1000 | Clamped to min 200 ms |
| `GracefulKillTimeoutMs` | 1000 | Used in `TryKill` |
| `ForceKillTimeoutMs` | 1000 | **Defined but never used** — taskkill uses hardcoded 5000 ms |
| `HeartbeatIntervalMs` | 5000 | **Defined but never read** |
| `MaxLogBytes` | 2 MB | **Defined but log rotation not implemented** |
| `EnableUpdates` | true | **No RPC handler wired up** |
| `UpdateStagingRoot` | `WorkingDirectory\updates` | Stubs only |
| `UseAtomicDirectorySwap` | true | Stubs only |

`Normalize()` also eagerly creates directories (`PendingCrashRoot`, `BundleRoot`, `UpdateStagingRoot`) via `TryCreateDirectory` — which silently swallows errors.

---

#### `CrashBundler.cs` — 206 lines — Post-mortem bundle finalizer

Static utility invoked by `WatchdogRuntime` after each non-shutdown child exit
when `EnableCrashBundling` is enabled.

**`TryFinalizeLatestCrashBundle()`:**
1. Finds newest `crash-*` folder under `PendingCrashRoot`
2. Copies it to `BundleRoot/bundle-{timestamp}/`
3. Appends watchdog status snapshot (`GetWatchdogStatus` callback)
4. Appends tail of watchdog log file
5. Writes `manifest.json` (handwritten JSON with schema version, timestamps, app/watchdog versions, file list with optional SHA256 checksums)
6. Deletes pending folder
7. Enforces `MaxBundles` by deleting oldest `bundle-*` dirs

`exception_notify` records the richer restart reason; finalization occurs from
the monitor loop after the process exits, with a fallback exit reason when no
notification arrived.

---

#### `CrashBundlerOptions.cs` — 28 lines — Config DTO for bundler

Callbacks (`GetWatchdogStatus`, `GetAppVersion`, `GetWatchdogVersion`) are `Func<string>` — nullable. `SafeCall` in `CrashBundler` wraps them.

---

#### `CrashChecksums.cs` — 21 lines — SHA256 helper (internal)

Single method `Sha256Hex(filePath)`. Straightforward. Used only by `CrashBundler.WriteManifest`.

---

#### `WatchdogHotkeys.cs` — 42 lines — Win32 constants + helpers (public)

Exposes `MOD_ALT`, `MOD_CONTROL`, `MOD_SHIFT`, `MOD_WIN`, `VK_F1`–`VK_F24`, `Mods()` builder, and `EnumerateAllVirtualKeys()` generator. **Duplicates** `MOD_ALT` and `MOD_CONTROL` that are also declared in `WatchdogRuntime.Win32` nested class.

The actual hotkey registration in `WatchdogRuntime` does **not** use `WatchdogHotkeys` — it uses raw literals (`0x50`, `0x52`, `0x51`) for P/Q/R keys.

---

#### `WatchdogLogPipeServer.cs` — Legacy named-pipe log streaming server

Obsolete compatibility type for the superseded raw-text side pipe. New code
uses the main RPC/event pipe and `WatchdogController.SubscribeLogs`.

---

#### `WatchdogPipeLogSink.cs` — Buffered `ILogSink` push adapter

Implements Core's `ILogSink`, queues without blocking the application and sends
batches through `WatchdogController`, which resolves the same target-derived
control-pipe identity as the runtime. The runtime maps `log_write_batch`, adds
received entries to history/live events and deliberately does not forward them
back into pipe sinks, preventing a feedback loop.

---

#### `NativeMethods.cs` — 46 lines — **Dead code**

`internal static class NativeMethods` — declares `RegisterHotKey`, `UnregisterHotKey`, `GetMessage`, `TranslateMessage`, `DispatchMessage`, `PostQuitMessage`, `MSG`, `POINT`. **None of these are used anywhere.** `WatchdogRuntime` uses its own nested `Win32` class instead. This file is a dead duplicate.

Differences vs `Win32` nested class:
- Has `UnregisterHotKey` (not in `Win32`)
- Has `PostQuitMessage` (not in `Win32`; `Win32` uses `PostThreadMessage`)
- `MSG.wParam` is `IntPtr` here vs `UIntPtr` in `Win32`
- `MSG.pt` is a `POINT` struct here vs two `int` fields in `Win32`

---

### `NekoLib.Watchdog.Host` (Standalone WinExe)

#### `Program.cs` — Entry point

Minimal. Parses args → creates `WatchdogRuntime` → `Start()` → `WaitForExit()`.
Fatal exceptions are written best-effort to `watchdog_host_fatal.log` (relative
path — lands in CWD) and return exit code 1. A log-write failure is contained so
it cannot replace the original startup failure.

#### `HostArgumentParser.cs` — CLI argument parser

Accepts `--target <path>`, `--args <args>`, `--workdir <path>`,
`--attach-pid <pid>` and `--attach-token <token>`. It validates the target,
positive PID and attach token pairing, then maps the values into
`WatchdogOptions`. Unknown and duplicate flags are rejected; remaining policy
uses defaults from `Normalize()`.

---

## 3. Test Coverage

| Location | Type | Status |
|----------|------|--------|
| `tests/NekoLib.Watchdog.Tests/Unit/` | xUnit | 81/81 passing per target (`net481`, `net9.0-windows`) on 2026-07-27 |
| `runtime_tests/Supervisor_481/` | Interactive WinForms scenario | Runtime-only, intentionally outside `NekoLib.sln` |

The old misplaced `WatchdogQuickTests` project was removed. Runtime-only
coverage lives under the ignored `runtime_tests/` tree; unit and RPC/child
process integration coverage lives in the dual-target xUnit project.

---

## 4. Issues Found

### High

| # | Issue | Location |
|---|-------|----------|
| H1 | 🟡 **PARTIAL** — **Zero xUnit unit tests** → now 81 tests per target. Options, bundling, hotkeys, logs, RPC, initial attach, bootstrap identity/budget, restart/environment handoff and spawn retry are automated on both targets; taskkill and interactive hotkey behavior still rely on runtime scenarios. | — |
| H2 | ✅ **FIXED** — **`NativeMethods.cs` is dead code** — removed (commit `4e1a664`). | `NativeMethods.cs` |
| H3 | 🟡 **MOSTLY ADDRESSED** — critical paths now surface failures: `StartChild` logs + retries instead of killing the monitor thread; `TryKill` logs graceful/taskkill failures (commit `b8d0f06`). Shutdown-cleanup and logging-path catches are intentionally left silent (the latter to avoid log-recursion). | `WatchdogRuntime.cs` |

### Medium

| # | Issue | Location |
|---|-------|----------|
| M1 | ✅ **FIXED** — `ForceKillTimeoutMs` now used by `TryKill`; default raised 1000→5000 to preserve prior behavior (commit `25e37dc`). | `WatchdogRuntime.cs`, `WatchdogOptions.cs` |
| M2 | ✅ **FIXED** — `HeartbeatIntervalMs` now drives a periodic `[heartbeat]` log + telemetry publish via a timer; 0 disables. Verified end-to-end (commit `42e3c67`). | `WatchdogRuntime.cs` |
| M3 | ✅ **FIXED** — `MaxLogBytes` now enforced via `WatchdogLogFile.Append` (single-backup rotation to `<path>.1`); +8 tests (commit `25e37dc`). | `WatchdogLogFile.cs`, `WatchdogRuntime.cs` |
| M4 | ✅ **FIXED** — hotkeys now configurable via `WatchdogOptions` (`EnableHotkeys` + `WatchdogHotkey` bindings); defaults preserve Ctrl+Alt+P/R/Q. Registration failures logged (commit `ffccc68`). | `WatchdogRuntime.cs`, `WatchdogOptions.cs`, `WatchdogHotkeys.cs` |
| M5 | `EnableUpdates` / `UpdateStagingRoot` / `UseAtomicDirectorySwap` defined but no RPC handler or implementation exists | `WatchdogOptions.cs:80-97` |
| M6 | ✅ **FIXED** — `WatchdogRuntime` now calls `CrashBundler` after each non-shutdown child exit (gated on `EnableCrashBundling`); verified end-to-end (commit `f084cd7`). | `CrashBundler.cs`, `WatchdogRuntime.cs` |
| M7 | ✅ **FIXED** — application logs now flow through the target-derived control pipe in buffered batches, enter the watchdog history/live stream, expose drop counts and cannot loop back into `WatchdogPipeLogSink`. The old raw-text server is obsolete. | `WatchdogPipeLogSink.cs`, `WatchdogController.cs`, `WatchdogRuntime.cs` |
| M8 | Pipe name SHA1 truncated to 16 hex chars — theoretical collision for paths that share a common prefix after lowercasing | `WatchdogController.cs:47`, `WatchdogOptions.cs:169` |
| M9 | Ring buffer silently drops oldest entries at 300 with no log warning | `WatchdogRuntime.cs:480-482` |
| M10 | ✅ **FIXED** — misplaced `WatchdogQuickTests` sim removed from `tests/` (commit `877d502`); supervision scenario now covered by `runtime_tests/Supervisor_481`. | `tests/NekoLib.Watchdog.Tests/Watchdog/` |

### Low

| # | Issue | Location |
|---|-------|----------|
| L1 | ✅ **FIXED** — RPC command names centralized in `WatchdogCommands` constants; wire values pinned by a test (commit `35a749a`). | `WatchdogCommands.cs` |
| L2 | ✅ **FIXED** — duplicate `MOD_ALT`/`MOD_CONTROL` removed from the nested `Win32` class; `WatchdogHotkeys` is the single source (commit `ffccc68`). | `WatchdogRuntime.cs` |
| L3 | `Dispose()` calls `Stop(true)` which is idempotent via `_stopped` flag — but only if `_stopped` is set before any error path; double-dispose is safe in practice but fragile | `WatchdogRuntime.cs:537-539` |
| L4 | ✅ **FIXED** — application bootstrap now re-encodes the original argument array with Windows command-line quoting; the Host parser preserves the resulting `--args` value and explicitly accepts `--workdir`, `--attach-pid` and `--attach-token`. | `WatchdogBootstrap.cs`, `HostArgumentParser.cs` |
| L5 | `watchdog_host_fatal.log` uses a relative path in `Program.Main` — lands in CWD, not a predictable log directory | `Program.cs:24` |
| L6 | ✅ **FIXED** — `WatchdogPipeLogSink` delegates to `WatchdogController`, so pipe identity is derived from the current target consistently and no pipe-name argument exists. | `WatchdogPipeLogSink.cs`, `WatchdogController.cs` |
| L7 | ✅ **FIXED** — when the named single-instance guard is already held and the option is enabled, startup restores and foregrounds the existing target window before reporting the duplicate-host error. | `WatchdogRuntime.cs` |

---

## 5. Strengths

- Clean layering: client facade (`WatchdogController`) is fully decoupled from runtime internals
- `Stop()` is properly idempotent via `_stopped` flag + `_stopLock`
- Crash loop detection (5 fast crashes → 10 s cooldown) is a solid heuristic
- Dual-JSON support handled consistently with `#if NET9` blocks
- Log ring buffer + replay-on-subscribe is a useful operational feature
- `Normalize()` eagerly validates and fills defaults — fails fast at startup, not mid-run
- A one-permit named `Semaphore` (`Global\NekoLib.Watchdog::<pipename>`) prevents
  duplicate watchdog instances per target. A `Mutex` is intentionally not used:
  shutdown may release the permit from a different thread, while mutex ownership
  is thread-affine.

---

## 6. Missing Pieces (Summary)

- ~~xUnit unit tests (zero coverage)~~ → ✅ 81 tests per target now cover pure logic plus RPC, attach/bootstrap/restart and spawn-retry runtime paths
- ~~File log rotation (`MaxLogBytes` is a stub)~~ → ✅ implemented
- ~~`ForceKillTimeoutMs` respected in kill sequence~~ → ✅ wired
- ~~`CrashBundler` integration into `WatchdogRuntime`~~ → ✅ wired
- The bootstrap protocol is covered component by component, but executing
  `WatchdogBootstrap.EnsureStarted` against the packaged/deployed sidecar and
  proving forced termination after a failed handshake remains a package/runtime
  scenario; no ignored `runtime_tests/` app was recreated for this audit.
- Update mechanism (options exist, no implementation)
- ~~Application-log forwarding~~ → ✅ buffered RPC batches feed history/live events without feedback loops
- ~~Configurable hotkeys~~ → ✅ `WatchdogOptions` bindings preserve the original defaults
- Self-restart for the watchdog host process itself

---

## 7. Remediation Log

| Date | Findings | Change | Commits |
|------|----------|--------|---------|
| 2026-06-04 | H1 (partial) | Added `tests/NekoLib.Watchdog.Tests/Unit/` (52 tests: Options, CrashBundler, Hotkeys, LogFile) + `runtime_tests/Supervisor_481/` WinForms supervisor dashboard | `6074e01` |
| 2026-06-04 | H2 | Removed dead `NativeMethods.cs` | `4e1a664` |
| 2026-06-04 | M1, M3 | Wired `ForceKillTimeoutMs`; implemented `MaxLogBytes` rotation via new `WatchdogLogFile` | `25e37dc` |
| 2026-06-04 | M10 | Removed misplaced `WatchdogQuickTests` sim from `tests/` | `877d502` |
| 2026-06-04 | M6 | Wired `CrashBundler` into the monitor loop (finalize after child exit); verified end-to-end | `f084cd7` |
| 2026-06-04 | M4, L2 | Configurable control hotkeys; removed duplicate `Win32` modifier constants | `ffccc68` |
| 2026-06-04 | L1 | RPC command names centralized in `WatchdogCommands` constants | `35a749a` |
| 2026-06-04 | H3 | Spawn + kill paths now log failures (and `StartChild` retries instead of crashing the monitor thread) | `b8d0f06` |
| 2026-06-04 | M2 | Implemented `HeartbeatIntervalMs` (periodic beat + telemetry) | `42e3c67` |
| 2026-07-27 | Bootstrap/attach, L4 | Added `WatchdogBootstrap.EnsureStarted`, bounded PID/token handshake, initial-process attach in `WatchdogRuntime`, parser support and dual-target regression tests. The first process is monitored in place; later restarts keep the existing `StartChild` flow and receive `NEKO_UNDER_WATCHDOG=1`. | uncommitted |
| 2026-07-27 | Bootstrap preflight | Existing-host detection now requires `attach_status` identity for the current PID (a mere `ping` is insufficient), reports target/PID conflicts clearly, serializes concurrent in-process bootstrap calls and treats `handshakeTimeoutMs` as one total budget across preflight, launch and cancellable handshake I/O. `attach_status` is exposed only after runtime startup and follows the currently supervised PID across restarts while retaining the host-session token. | uncommitted |
| 2026-07-27 | M7, L6, L7, guard docs | Reconciled the audit with implemented application-log forwarding, target-window activation and the intentionally cross-thread-safe named `Semaphore`. | uncommitted |

**Still open:** M5 (update mechanism — large, genuinely unimplemented), M8
(pipe-name hash length / collision), M9 (the 300-entry replay buffer evicts its
oldest entry without a dedicated counter) and L5 (`watchdog_host_fatal.log`
relative path).

## Reconciliation — 2026-08-01

The three remediation-log rows dated 2026-07-27 and labeled `uncommitted`
landed in commit `1727a1c`. Their original labels remain part of the historical
log; this reconciliation records the later Git outcome. Items named "Still
open" above are historical leads, not an independent roadmap. Only verified and
accepted entries in `TODO.md` are current work.
