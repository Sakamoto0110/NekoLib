# Watchdog Module — First-Pass Audit

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
       ├─ NekoLib.Diagnostics  (ILogSink, LogEntry)
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

Static utility. Not called from `WatchdogRuntime` — caller must invoke it (e.g., in `exception_notify` handler or after child exit).

**`TryFinalizeLatestCrashBundle()`:**
1. Finds newest `crash-*` folder under `PendingCrashRoot`
2. Copies it to `BundleRoot/bundle-{timestamp}/`
3. Appends watchdog status snapshot (`GetWatchdogStatus` callback)
4. Appends tail of watchdog log file
5. Writes `manifest.json` (handwritten JSON with schema version, timestamps, app/watchdog versions, file list with optional SHA256 checksums)
6. Deletes pending folder
7. Enforces `MaxBundles` by deleting oldest `bundle-*` dirs

**Note:** `CrashBundler` is never actually called by `WatchdogRuntime`. The integration is the caller's responsibility.

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

#### `WatchdogLogPipeServer.cs` — 157 lines — Named-pipe log streaming server

Separate from the main RPC pipe. Accepts log line connections, dispatches from a `BlockingCollection<string>` (max 2048). Two threads: `WDG-LogPipe-Accept` and `WDG-LogPipe-Dispatch`. `Enqueue()` drops silently when full. **Not used by `WatchdogRuntime`** — caller must wire it up manually.

---

#### `WatchdogPipeLogSink.cs` — 71 lines — `ILogSink` push adapter

Implements `ILogSink` (from `NekoLib.Diagnostics`). Connects to a named pipe and writes log lines. Reconnects on failure with a 200 ms timeout. Default pipe name `"NekoLib.Watchdog.logs"` does **not** match the auto-generated pipe name format used by `WatchdogRuntime` — needs manual coordination.

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

#### `Program.cs` — 29 lines — Entry point

Minimal. Parses args → creates `WatchdogRuntime` → `Start()` → `WaitForExit()`. Fatal exceptions written to `watchdog_host_fatal.log` (relative path — lands in CWD).

#### `HostArgumentParser.cs` — 46 lines — CLI argument parser

Accepts `--target <path>` and `--args <args>`. Validates the target file exists. Returns a `WatchdogOptions` with only `TargetPath` and `TargetArguments` set — all other options use defaults from `Normalize()`.

---

## 3. Test Coverage

| Location | Type | Status |
|----------|------|--------|
| `tests/NekoLib.Watchdog.Tests/Watchdog/` | WinForms runtime sim app (`WatchdogQuickTests`) — **not xUnit** | Misplaced in tests/; should be under `runtime_tests/` |
| xUnit unit tests | — | **None exist** |

`WatchdogQuickTests` is a WinForms app (`DummyForm`) that exercises the watchdog integration scenario (crash handler → notify watchdog → supervised launch). It is a scenario runner, not an assertion harness.

---

## 4. Issues Found

### High

| # | Issue | Location |
|---|-------|----------|
| H1 | **Zero xUnit unit tests** — no assertion coverage for any watchdog behavior | — |
| H2 | **`NativeMethods.cs` is dead code** — duplicate P/Invoke declarations, nothing references it | `NativeMethods.cs` |
| H3 | **Pervasive silent `catch { }`** — failures in critical paths (kill, telemetry, log flush) swallowed with no trace | Throughout `WatchdogRuntime`, `CrashBundler` |

### Medium

| # | Issue | Location |
|---|-------|----------|
| M1 | `ForceKillTimeoutMs` option defined but `TryKill` uses hardcoded `5000` ms for taskkill | `WatchdogRuntime.cs:432`, `WatchdogOptions.cs:50` |
| M2 | `HeartbeatIntervalMs` option defined (default 5000) but never read | `WatchdogOptions.cs:55` |
| M3 | `MaxLogBytes` defined but file log rotation never implemented — log grows unbounded | `WatchdogOptions.cs:40`, `WatchdogRuntime.cs:494` |
| M4 | Hotkeys (`Ctrl+Alt+P/R/Q`) hardcoded as raw VK literals in `HotkeyLoop`; not wired to `WatchdogOptions` or `WatchdogHotkeys` | `WatchdogRuntime.cs:510-512` |
| M5 | `EnableUpdates` / `UpdateStagingRoot` / `UseAtomicDirectorySwap` defined but no RPC handler or implementation exists | `WatchdogOptions.cs:80-97` |
| M6 | `CrashBundler.TryFinalizeLatestCrashBundle()` is never called from `WatchdogRuntime` — callers must wire it manually, but no documentation or hook point exists | `CrashBundler.cs`, `WatchdogRuntime.cs` |
| M7 | `WatchdogLogPipeServer` is never started from `WatchdogRuntime` — callers must wire it manually | `WatchdogLogPipeServer.cs` |
| M8 | Pipe name SHA1 truncated to 16 hex chars — theoretical collision for paths that share a common prefix after lowercasing | `WatchdogController.cs:47`, `WatchdogOptions.cs:169` |
| M9 | Ring buffer silently drops oldest entries at 300 with no log warning | `WatchdogRuntime.cs:480-482` |
| M10 | `WatchdogQuickTests` WinForms app sits in `tests/` instead of `runtime_tests/` | `tests/NekoLib.Watchdog.Tests/Watchdog/` |

### Low

| # | Issue | Location |
|---|-------|----------|
| L1 | RPC command names (`"ping"`, `"status"`, etc.) are magic strings — no constants | `WatchdogRuntime.cs:177-252` |
| L2 | `WatchdogHotkeys.MOD_ALT`/`MOD_CONTROL` duplicate `WatchdogRuntime.Win32.MOD_ALT`/`MOD_CONTROL` | `WatchdogHotkeys.cs:11-12`, `WatchdogRuntime.cs:551-552` |
| L3 | `Dispose()` calls `Stop(true)` which is idempotent via `_stopped` flag — but only if `_stopped` is set before any error path; double-dispose is safe in practice but fragile | `WatchdogRuntime.cs:537-539` |
| L4 | `HostArgumentParser` accepts `--args` as a single quoted string — no support for per-argument arrays; multi-word arguments require quoting workarounds | `HostArgumentParser.cs` |
| L5 | `watchdog_host_fatal.log` uses a relative path in `Program.Main` — lands in CWD, not a predictable log directory | `Program.cs:24` |
| L6 | `WatchdogPipeLogSink` default pipe name `"NekoLib.Watchdog.logs"` does not match the auto-generated `NekoLib.Watchdog.<hash>` format — callers must pass the correct name manually | `WatchdogPipeLogSink.cs:18` |
| L7 | `BringToFrontOnStartIfRunning` option defined in `WatchdogOptions` but never referenced in code | `WatchdogOptions.cs:57` |

---

## 5. Strengths

- Clean layering: client facade (`WatchdogController`) is fully decoupled from runtime internals
- `Stop()` is properly idempotent via `_stopped` flag + `_stopLock`
- Crash loop detection (5 fast crashes → 10 s cooldown) is a solid heuristic
- Dual-JSON support handled consistently with `#if NET9` blocks
- Log ring buffer + replay-on-subscribe is a useful operational feature
- `Normalize()` eagerly validates and fills defaults — fails fast at startup, not mid-run
- Global mutex (`Global\NekoLib.Watchdog::<pipename>`) prevents duplicate watchdog instances per target

---

## 6. Missing Pieces (Summary)

- xUnit unit tests (zero coverage)
- File log rotation (`MaxLogBytes` is a stub)
- Update mechanism (options exist, no implementation)
- `CrashBundler` integration into `WatchdogRuntime` (caller must wire manually)
- `WatchdogLogPipeServer` integration into `WatchdogRuntime` (caller must wire manually)
- Configurable hotkeys
- Self-restart for the watchdog host process itself
- `ForceKillTimeoutMs` respected in kill sequence
