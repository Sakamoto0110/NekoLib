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
| H1 | ✅ **FIXED** — added `tests/NekoLib.Pipes.Tests/Unit/` (16 tests, net481 + net9): RPC round-trip / not_found / handler-exception / correlation / request-timeout, event pub-sub, metrics counters + latency math + concurrency. Run serially with raised pool minimums (in-process IPC). (commit `ea72043`) | — |
| H2 | ✅ **FIXED** — net481 framing now honors the `CancellationToken`: `PipeFraming` wraps the blocking work so the await observes the token, and the call sites pass it. `PipeClient.RequestTimeout` and `PipeServer.ClientIdleTimeout` now take effect on net481, matching net9. Verified with a timeout test + watchdog RPC E2E (commit `4f1d779`). | `PipeFraming.cs`, `PipeClient.cs`, `PipeServer.cs`, `PipeEventHub.cs` |

### Medium

| # | Issue | Location |
|---|-------|----------|
| M1 | ✅ **FIXED** — the latency quad + a per-channel sample counter are now guarded by a per-channel lock, read under the same lock in `Snapshot`; +concurrency test (commit `0ad161d`). | `SimplePipeMetrics.cs` |
| M2 | ✅ **FIXED** — `_handlers` is now a `ConcurrentDictionary`, so concurrent `Map`/`Dispatch` can't tear or crash (no restriction on dynamic mapping); +concurrent-requests test (commit `0a14c07`). | `PipeServer.cs` |
| M3 | ✅ **FIXED** — `PublishAsync` now writes to all subscribers concurrently (per-subscriber send task + `Task.WhenAll`), so a slow subscriber no longer stalls the others; +fan-out test (commit `e4df3e1`). A full per-subscriber bounded queue + drop policy remains possible future hardening. | `PipeEventHub.cs` |
| M4 | ✅ **FIXED** — `PipeEventClient` now auto-reconnects (with a bounded connect timeout), raises `OnConnected`/`OnDisconnected`, and isolates throwing handlers. Additive defaults; Watchdog log streaming now survives a watchdog restart. +reconnect + handler-isolation tests (commit `599ec63`). Also completed M6 for the event hub on net9 (dispose pending accept in `finally`). | `PipeEventClient.cs`, `PipeEventHub.cs` |
| M5 | ✅ **FIXED** — `PipeFraming` throws a distinguishable `PipeFrameTooLargeException`; `HandleClient` catches it on the response write and replies with a structured `response_too_large` error instead of dropping the connection. +test (commit `670f427`). | `PipeFraming.cs`, `PipeServer.cs` |
| M6 | ✅ **FIXED** — net481 registers the accept's `CancellationToken` to dispose the pending pipe on cancel, so `WaitForConnection` throws and releases its thread at shutdown (was leaking up to `MaxClients` blocked threads per server until GC). Same in `PipeEventHub`. Verified: net481 suite stable across repeated runs (commit `c0cfa3d`). | `PipeServer.cs`, `PipeEventHub.cs` |

### Low

| # | Issue | Location |
|---|-------|----------|
| L1 | ✅ **FIXED** — nullable annotations completed (`PipeMessage.Data/Error`, `PipeServer._cts/Events/pipe`, `IPipeMetrics` `errorCode` params); Pipes now builds with zero warnings (commit `dc17863`). | `PipeMessage.cs`, `PipeServer.cs`, `IPipeMetrics.cs` |
| L2 | ✅ **FIXED** — `PlatformGuards.cs` deleted (commit `dc17863`). | — |
| L3 | ✅ **FIXED** — duplicate `using` removed (commit `dc17863`). | `PipeClientOptions.cs` |
| L4 | ✅ **FIXED** — `ImplicitUsings` leading space corrected; dead `Internal\**` excludes removed (commit `dc17863`). | `NekoLib.Pipes.csproj` |
| L5 | ✅ **FIXED (annotation)** — `IPipeMetrics.Snapshot()` return is now annotated `PipeMetricsSnapshot?` to match the existing null-returning Noop contract (clears CS8603); kept returning `null` by design rather than an empty snapshot (commit `dc17863`). | `IPipeMetrics.cs` |

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

- ~~Unit tests (zero coverage)~~ → ✅ 20 tests
- ~~net481 cancellation/timeout parity with net9~~ → ✅ H2
- ~~`PipeEventClient` reconnect + disconnect notification; isolation of throwing handlers~~ → ✅ M4
- Event backpressure handling (per-subscriber **bounded queue + drop policy**) — M3 parallelized delivery but did not add per-subscriber queues.
- Pipe ACL/security configuration (created with default security).
- Graceful drain of in-flight client tasks on `Dispose`.
- `_handlers` thread-safety / freeze-after-Start (M2, open).

---

## 7. Remediation Log

| Date | Findings | Change | Commits |
|------|----------|--------|---------|
| 2026-06-04 | H1 | Added `tests/NekoLib.Pipes.Tests/Unit/` — 16 tests (RPC, events, metrics) on net481 + net9 | `ea72043` |
| 2026-06-04 | H2 | net481 framing now honors the `CancellationToken` (RequestTimeout / ClientIdleTimeout work) | `4f1d779` |
| 2026-06-04 | M1 | `SimplePipeMetrics` latency stats made thread-safe (per-channel lock) | `0ad161d` |
| 2026-06-04 | M6 | net481 accept threads released on shutdown (dispose-on-cancel) | `c0cfa3d` |
| 2026-06-04 | M4 | `PipeEventClient` reconnect + connect/disconnect signals + handler isolation (also completed M6 for the hub on net9) | `599ec63` |
| 2026-06-04 | M3 | Event publish parallelized (no head-of-line blocking) | `e4df3e1` |
| 2026-06-04 | M5 | Oversized response → structured `response_too_large` error instead of dropped connection | `670f427` |
| 2026-06-04 | L1–L5 | Nullable annotations completed (zero-warning build), dead `PlatformGuards` removed, duplicate using + csproj quirks fixed | `dc17863` |
| 2026-06-04 | M2 | `_handlers` → `ConcurrentDictionary` | `0a14c07` |
| 2026-06-04 | (config) | `ClientIdleTimeout` corrected to a true idle timeout that resets on activity (was a hard max-session cap) | `94808e6` |
| 2026-06-04 | (config) | Max frame size configurable via `MaxMessageBytes` (server/client options); also fixed a latent net9 client call site capped at 1 MB | `753cb82` |

**Watchdog compatibility:** every change is internal or additive — no public Pipes API touched (new options default to prior behavior). After the full set, Watchdog was rebuilt against modified Pipes and verified end-to-end: RPC `ping→pong` + `status` + `pause`, **and** live event delivery to a `PipeEventClient` subscriber. All green.

**Still open / future hardening:** per-subscriber bounded event queue with a drop policy (beyond the M3 parallelization); pipe ACL/security configuration; graceful drain of in-flight client tasks on `Dispose`. No known correctness/threading bugs remain in the request/response or event paths.
