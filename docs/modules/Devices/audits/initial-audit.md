# Devices Module Audit — Historical Snapshot

**Document ID:** DEV-AUDIT-INITIAL

**Schema version:** 1

**Kind:** audit

**Lifecycle:** historical

**Subject:** Devices first-pass review

**Surface:** audit

**Boundary:** devices

**Authority role:** evidence

**Mutation:** snapshot

**Indexing:** include

**Reference date:** 2026-06-10

**Reference commit:** not recorded

**Original path:** docs/audit/devices-first-pass.md

**Last reconciliation:** 2026-08-01

**Current state:** [Devices technical reference](../REFERENCE.md) and [`TODO.md`](../../../../TODO.md)

## Scope

This audit covered only `src/Devices/NekoLib.Devices` and its new unit test project under `tests/NekoLib.Devices.Tests/Unit`.

The module is a serial-port hardware abstraction layer:

- `HardwareOperation` describes a command request.
- `IHardwareProtocol` builds command bytes and parses reply bytes.
- `HardwareEngine` coordinates protocol config, transport open/write/read, parsing, logging, and error mapping.
- `ICommTransport` abstracts communication transport.
- `SerialCommTransport` is the current `System.IO.Ports.SerialPort` implementation.
- `ProtocolRaw` is the only protocol currently included and supports raw byte commands or ASCII text commands.

## Changes Made

### Stability fixes

- Fixed `SerialCommTransport.Configure` so protocol-provided `SerialConfig.PortName` is applied to the underlying serial port.
- Prevented the default `SerialPort.PortName` value (`COM1`) from being treated as an explicit user/protocol port.
- Fixed `SerialCommTransport.Open(string)` so it validates blank names and opens under the same semaphore lock, avoiding a race between setting `PortName` and opening.
- Made `Open(string)` fail clearly if the transport is already open on a different port.
- Made `Open()` fail clearly if no explicit port has been configured.
- Fixed newline config so `"\n"` is preserved; only null/empty `NewLine` defaults to `"\r\n"`.
- Added null/range validation for write buffers, read lengths, and timeout values.
- Added disposal of the internal `SemaphoreSlim` in `Dispose` / `DisposeAsync`.
- Removed a dead/confusing `net481` `System.IO.Ports` reference block from `NekoLib.Devices.csproj`; the project builds cleanly without it.

### Engine behavior

- `HardwareEngine` now validates constructor dependencies, null operations, and negative timeouts.
- Cancellation now propagates as `OperationCanceledException` instead of being converted into a failed `HardwareResponse`.
- Non-cancellation failures are still mapped to `HardwareResponse { Success = false }`.
- Parsed responses now get the original `HardwareOperation` assigned when the protocol did not set it.
- A null protocol response is treated as a protocol error.
- If a transport is already open, `HardwareEngine` now verifies it is open on the expected configured port before sending.

### ProtocolRaw behavior

- `ProtocolRaw.BuildCommand` now validates null operations and null `Args`.
- Invalid `RawBytes` values now throw a clear `ArgumentException` instead of an unsafe cast failure.
- Invalid `RawText` values now throw a clear `ArgumentException`.
- Existing raw behavior remains:
  - `RawBytes` sends the provided byte array as-is.
  - `RawText` is encoded with `Encoding.ASCII`.
  - Null replies parse to `Success = false`, `Status = "NoResponse"`.

### Tests added

Added `tests/NekoLib.Devices.Tests/Unit/NekoLib.Devices.Tests.Unit.csproj` and wired it into `NekoLib.sln`.

Coverage includes:

- `ProtocolRaw` byte/text command building and response parsing.
- `HardwareEngine` configure/open/write/read/parse order with fake transport/protocol.
- Explicit port send behavior.
- Ordinary exception mapping to failed `HardwareResponse`.
- Cancellation propagation.
- Missing configured port behavior.
- Already-open transport on the same expected port.
- Already-open transport on a different port.
- `SerialCommTransport` config behavior that does not require real serial hardware.
- Input validation for write/read methods.

## Validation Run

These commands passed after the latest fixes:

```powershell
dotnet build src/Devices/NekoLib.Devices/NekoLib.Devices.csproj
dotnet test tests/NekoLib.Devices.Tests/Unit/NekoLib.Devices.Tests.Unit.csproj
git diff --check
```

Results:

- Devices build: `net481` and `net9.0`, 0 warnings, 0 errors.
- Devices tests: 22 passed on `net481`, 22 passed on `net9.0`.
- `git diff --check`: passed, only CRLF normalization warnings from Git.

Note: sandboxed dotnet commands fail in this environment because dotnet cannot read `C:\Users\rafae\AppData\Roaming\NuGet\NuGet.Config`. The successful build/test runs were executed with elevated permission.

## Remaining Review Items

These are not urgent blockers, but they are good cleanup before or during COM-port emulator work:

1. Standardize `ReadLine` timeout behavior.
   - Current: returns empty string on timeout.
   - Recommended: return `null` on timeout, matching `ReadAll` and `ReadExact`.
   - Reason: an emulator should be able to distinguish an empty line from no line received.

2. Add explicit `SerialConfig` validation.
   - Validate `BaudRate`, `DataBits`, `StopBits`, `ReadTimeout`, and `WriteTimeout` before assigning to `SerialPort`.
   - Reason: clearer errors and easier diagnosis in emulator/device tests.

3. Add `ThrowIfDisposed()`.
   - Current: methods rely on native `SerialPort` / `SemaphoreSlim` disposed errors.
   - Recommended: fail consistently with `ObjectDisposedException`.

4. Decide whether `ProtocolRaw.RawText` should stay ASCII-only.
   - Current: `RawText` uses ASCII.
   - Recommended for v1: keep ASCII, but document that non-ASCII/binary commands should use `RawBytes`.
   - Future option: allow configurable encoding if real devices need it.

## COM-Port Emulator Next Steps

When returning to this module, the best next step is a runtime/integration test harness using paired virtual COM ports.

Recommended scenarios:

- Open configured port successfully.
- Write raw bytes and verify the emulator receives the exact bytes.
- Emulator sends bytes; validate `ReadAll`.
- Emulator sends exact-size frame; validate `ReadExact`.
- Emulator sends newline-terminated text; validate `ReadLine`.
- Timeout cases:
  - no data for `ReadAll`;
  - partial frame for `ReadExact`;
  - no terminator for `ReadLine`.
- Cancellation cases while blocked in read.
- Reopen behavior:
  - same port already open;
  - different port already open;
  - close then reopen.
- Protocol-level round trip through `HardwareEngine` + `ProtocolRaw`.

Place live serial/emulator scenarios under `runtime_tests/`, not xUnit unit tests, unless the emulator is fully in-process and hardware-independent.

## Snapshot Status at 2026-06-10

The `Devices` module is now in a much safer baseline state for unit-level behavior. The main remaining risk is real serial I/O behavior, which should be validated with a COM-port emulator because the current unit tests intentionally avoid physical or virtual serial hardware.

## Reconciliation — 2026-08-01

This section records later outcomes without rewriting the 2026-06-10 snapshot:

- Commit `d352fa8` closed all four items under "Remaining Review Items":
  nullable `ReadLine` timeout behavior, `SerialConfig` validation,
  `ThrowIfDisposed()`, and the documented text-encoding boundary.
- Commit `ddd09d3` later added TCP and named-pipe stream transports, serialized
  complete `HardwareEngine` operations, and allowed `ProtocolRaw` callers to
  select a text encoding while preserving ASCII as the default.
- The versioned `runtime_tests/Devices/Com0Com` scenario later closed the
  virtual-COM execution gap on 2026-08-01 for both `net481` and `net9.0`. It
  validates PCB-A and PCB-B through an independent emulator/protocol oracle;
  physical UART and electrical behavior remain outside that evidence.
