# NekoLib Refactor Notes (v2 -> refactored)

## What changed

### 1) New project: `NekoLib.Diagnostics.Abstractions`
- Added a dedicated abstractions project so feature modules can depend only on contracts.
- Contains: `ILogger`, `ILogSink`, `ITelemetrySink`, `IDiagnosticContext`, `LogEntry`, `TelemetryEvent`, `NullLogger`, `NullDiagnosticContext`.

### 2) `NekoLib.Core` contracts wired up
- Replaced placeholder/incorrect internal interfaces with real public contracts:
  - `INekoBuilder`, `INekoHost`, `INekoModule`, `INekoModuleContext`, `INekoServiceRegistry`, `INekoConfiguration`, `INekoEnvironment`.
- Added a minimal implementation: `NekoServiceRegistry` (lazy singletons).

### 3) `NekoLib.Diagnostics` implemented as runtime
- Implemented `DiagnosticsRuntime` + `IDiagnosticsRuntime`.
- Added a few basic sinks (`ConsoleLogSink`, `MemoryTelemetrySink`, `NullTelemetrySink`).

### 4) Navigation telemetry removed; replaced by hooks
- `NekoLib.Navigation` no longer writes logs to disk.
- Old `PageLogger` / `PageLoggerService` replaced by `NavigationDiagnostics` which:
  - emits to `IDiagnosticContext` if provided
  - raises an event (`NavigationLogged`) for UI overlays/tests
- `NavigationContextBuilder.UseDiagnostics(...)` sets the diagnostics hook.
- `PageNavBootstrap.UseDiagnostics(...)` flows the context into the runtime.

### 5) `NekoLib` became the entrypoint/bootstrap facade
- Added:
  - `Neko.CreateBuilder()`
  - `NekoBuilder` (modules + configuration + environment + diagnostics wiring)
  - `NekoHost` (start/stop lifecycle)
  - `IDiagnosticsBuilder`

## Layering rules (enforced by references)
- Feature modules should reference **only**:
  - `NekoLib.Core`
  - (optionally) `NekoLib.Diagnostics.Abstractions`
- Only the entrypoint (`NekoLib`) should reference `NekoLib.Diagnostics` runtime.

## Breaking behavior changes

### Pipes — connection close now returns `Ok=false` (was an exception)
`PipeClient.SendAsync` no longer throws `EndOfStreamException` when the server
closes before sending a response. It returns a `PipeMessage` with `Ok=false` and
`Error.Code="connection_closed"`. Replace `try/catch` around the close case with a
check on `response.Ok` / `response.Error?.Code`. See
`docs/audit/pipes-first-pass.md` for details.

