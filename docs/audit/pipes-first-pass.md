# Pipes Module — First-Pass Audit

**Branch:** `pipes/audit/first-pass`  
**Date:** 2026-06-04  
**Scope:** `src/Pipes/NekoLib.Pipes/`

> Audited because Watchdog depends on it (RPC + event streaming + metrics run over this transport). Findings here directly affect Watchdog's reliability — see the cross-module notes on **H2**.

---

## 1. Module Overview

`NekoLib.Pipes` is a named-pipe IPC framework providing two channels over Windows named pipes:

- **Request/response RPC** — `PipeServer` (handler map) ↔ `PipeClient` (per-call connection)
- **Pub/sub events** — `PipeEventHub` (server side) ↔ `PipeEventClient` (subscriber), on a sibling pipe `<name>.events`

Messages are length-prefixed JSON frames (`PipeFraming`). An optional metrics sink (`IPipeMetrics`) records connect/request/latency/error counters.

**Targets:** `net481` + `net9.0-windows`. **Nullable:** enabled. **ImplicitUsings:** enabled. **LangVersion:** latest.

**Dual JSON:** `System.Text.Json` on net9, `Newtonsoft.Json` on net481.

---

## 2. File-by-File Inventory

| File | Role | Notes |
|------|------|-------|
| `PipeServer.cs` (262) | RPC server | `AcceptLoop` → per-client `Task.Run` → `HandleClient` → `Dispatch`. Owns a `PipeEventHub` when `EnableEvents`. Client concurrency capped by `SemaphoreSlim(MaxClients)`. |
| `PipeClient.cs` (~161) | RPC client | Stateless: one `NamedPipeClientStream` **per call**. Correlation check (response.Id == request.Id, Type == "res"). |
| `PipeEventHub.cs` (~246) | Server event pub/sub | Byte-mode pipe `<name>.events`; subscribers in a `ConcurrentDictionary`. `PublishAsync` writes to each subscriber sequentially. |
| `PipeEventClient.cs` (~97) | Event subscriber | Connects to `<name>.events`, raises `OnEvent` per `"evt"` frame. |
| `PipeFraming.cs` (~141) | Wire framing | 4-byte length prefix + JSON; `MaxSize = 1 MB`. Dual STJ/Newtonsoft. |
| `PipeMessage.cs` (~36) | Message DTO | `Id`, `Type` ("req"/"res"/"evt"), `Name`, `Ok`, `Data` (`JsonElement?`/`JToken`), `Error`. |
| `PipeError.cs` (~9) | Error DTO | `Code`, `Message`. |
| `PipeServerOptions.cs` (~15) | Server config | `PipeName`, `MaxClients=16`, `ClientIdleTimeout=5min`, `EnableEvents=true`, `MaxEventSubscribers=16`, `Metrics`. |
| `PipeClientOptions.cs` (~26) | Client config | `PipeName`, `ConnectTimeout=3s`, `RequestTimeout=5s`, `Metrics`. |
| `IPipeMetrics.cs` (~79) | Metrics contract | Interface + `NoopPipeMetrics` (no-op default; `Snapshot()` returns `null`). |
| `SimplePipeMetrics.cs` (~215) | Metrics impl | `Interlocked` counters; latency last/max/avg/EMA. |
| `PipeMetricsSnapshot.cs` (~145) | Snapshot DTOs | Immutable Server/Client/Event/Error metric records. |
| `PlatformGuards.cs` (~17) | TFM const | `IsModern` (`true` on net9). **Unreferenced — dead code.** |

---

## 3. Dependency Graph

```
NekoLib.Watchdog ──> NekoLib.Pipes
  - WatchdogRuntime:  PipeServer (RPC + events), SimplePipeMetrics
  - WatchdogController: PipeClient (RequestTimeout 3s), PipeEventClient (log stream)
  - Supervisor_481 runtime app: PipeClient + PipeEventClient
```

`NekoLib.Pipes` itself has **no project dependencies** (only `Newtonsoft.Json` on net481).

---

## 4. Issues Found

### High

| # | Issue | Location |
|---|-------|----------|
| H1 | **No unit tests** — zero assertion coverage for framing, RPC round-trip, correlation, events, or metrics. The only `Pipe*` files under `tests/` are build-artifact DLLs. | — |
| H2 | **net481 timeouts are non-functional.** The net481 framing path (`PipeFraming.WriteAsync`/`ReadAsync`) runs the blocking I/O in `Task.Run` and **ignores the `CancellationToken`**. As a result: (a) `PipeClient.RequestTimeout` is never enforced on net481 — a hung server blocks the caller indefinitely (the `timeoutCts` is created but its token is never passed to the net481 framing calls); (b) `PipeServer.ClientIdleTimeout` never disconnects idle clients on net481 (the idle `ct` isn't passed to `ReadAsync`, and even if it were it'd be ignored). net9 is fully cancellable, so behavior diverges by TFM. **Directly affects Watchdog**, whose `WatchdogController` relies on a 3 s `RequestTimeout` on net481. | `PipeFraming.cs` (net481 `ReadAsync`/`WriteAsync`), `PipeClient.cs`, `PipeServer.cs` |

### Medium

| # | Issue | Location |
|---|-------|----------|
| M1 | **Metrics latency data race.** `SimplePipeMetrics.UpdateLatency` updates the `avg` and `ema` doubles with plain read-modify-write (no lock/Interlocked) and takes `count` as a non-atomic `_ok + _fail`. Concurrent server/client responses tear and lose updates. Counters via `Interlocked` are fine; only the derived doubles race. (Impact: inaccurate stats, not functional failure.) | `SimplePipeMetrics.cs` `UpdateLatency` |
| M2 | **`_handlers` is a plain `Dictionary`** read by concurrent client tasks (`Dispatch`) with no synchronization and no freeze after `Start`. Safe only if `Map` is always called before `Start` (current usage), but a `Map` after `Start` races `Dispatch`. Use `ConcurrentDictionary` or reject `Map` after `Start`. | `PipeServer.cs` |
| M3 | **Event publish head-of-line blocking.** `PipeEventHub.PublishAsync` awaits `WriteAsync` to each subscriber **sequentially**; one slow/backed-up subscriber stalls delivery to all others. No per-subscriber queue or drop policy. | `PipeEventHub.cs` `PublishAsync` |
| M4 | **`PipeEventClient` dies silently.** `ListenLoop` catches all exceptions and exits with no reconnect and no disconnect callback — so a server restart, a dropped pipe, **or a throwing `OnEvent` handler** permanently ends the subscription with no signal to the consumer. (e.g. the Supervisor dashboard would silently stop receiving logs.) | `PipeEventClient.cs` `ListenLoop` |
| M5 | **Oversized response drops the connection.** A response > `MaxSize` (1 MB) throws `InvalidOperationException` inside the write path; `HandleClient` catches and `break`s, dropping the connection without sending a structured error. The client sees an `EndOfStream`, not an error code. | `PipeFraming.cs` `WriteCore`, `PipeServer.cs` `HandleClient` |
| M6 | **net481 accept thread not interruptible on shutdown.** `AcceptLoop` (and `PipeEventHub`) block on `Task.Run(() => pipe.WaitForConnection())`; on `Dispose` the cancellation can't interrupt the blocked native wait, so a pending accept lingers until the next connection. net9 uses `WaitForConnectionAsync(ct)` and shuts down cleanly. | `PipeServer.cs`, `PipeEventHub.cs` (net481 paths) |

### Low

| # | Issue | Location |
|---|-------|----------|
| L1 | `PipeMessage.Error` / `Data` are non-nullable despite `Nullable=enable`, producing CS8618 warnings; they are effectively optional and should be annotated nullable. | `PipeMessage.cs` |
| L2 | `PlatformGuards.IsModern` is dead code — declared, never referenced. | `PlatformGuards.cs` |
| L3 | Duplicate `using System;` (one inside the namespace) in `PipeClientOptions.cs`. | `PipeClientOptions.cs` |
| L4 | csproj quirks: `<ImplicitUsings> enable</ImplicitUsings>` has a leading space in the value; `<Compile Remove="Internal\**" />` excludes a folder that does not exist. | `NekoLib.Pipes.csproj` |
| L5 | `NoopPipeMetrics.Snapshot()` returns `null` (via explicit interface impl), forcing every caller to null-check; an empty snapshot would be friendlier. | `IPipeMetrics.cs` |

---

## 5. Strengths

- Clean layering: framing → transport → RPC/events → metrics, each in its own type.
- Length-prefixed framing validates size before allocating (`size <= 0 || size > MaxSize` → reject), preventing unbounded-read DoS.
- Request/response correlation is validated (Id match + `"res"` type).
- Metrics are an abstraction with a zero-cost `NoopPipeMetrics` default; counters use `Interlocked`.
- Dual-runtime JSON handled consistently behind `#if NET9`.
- The **net9 path is fully async and cancellable** end to end.
- `PipeEventHub` prunes dead subscribers on publish failure and caps subscribers with a semaphore.

---

## 6. Missing Pieces (Summary)

- Unit tests (zero coverage) — framing round-trip, RPC dispatch/not-found/exception, correlation mismatch, event fan-out, metrics math.
- net481 cancellation/timeout parity with net9.
- Event backpressure handling (per-subscriber queue + drop policy).
- `PipeEventClient` reconnect + disconnect notification; isolation of throwing handlers.
- Pipe ACL/security configuration (created with default security).
- Graceful drain of in-flight client tasks on `Dispose`.

---

## 7. Remediation Log

_(none yet — first-pass read only)_

**Suggested order:** H1 (tests — lock current behavior first, incl. a framing round-trip that's pure and easy to assert), then H2 (net481 cancellation — highest reliability impact, affects Watchdog), then M1/M4 (metrics race, event-client resilience).
