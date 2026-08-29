# NekoLib.Devices Findings

**Document ID:** DEV-FINDINGS

**Schema version:** 1

**Kind:** reference

**Lifecycle:** current

**Subject:** unconfirmed and non-normative observations about NekoLib.Devices

**Surface:** findings

**Boundary:** devices

**Authority role:** non-normative

**Mutation:** authored

**Indexing:** include

Everything here is non-normative. A finding becomes a confirmed defect only
after verification, and becomes scheduled work only through explicit promotion
to [`TODO.md`](../../../TODO.md).

The historical leads in [`audits/`](audits/) remain true only for their recorded
baselines. Every `DEV-01` through `DEV-15` disposition and the first-pass item
list were reverified against current source during the 2026-08-29 module review;
the results are summarized below and the originals stay unmodified at their
recorded baselines.

| Historical ID | Origin | Current source state |
|---|---|---|
| First-pass items 1–4 | [`audits/initial-audit.md`](audits/initial-audit.md) | Superseded. Both transports return `null` from `ReadLine` on timeout, `ValidateSerialConfig` and `ValidateStreamConfig` exist, `ThrowIfDisposed` guards every public member of both transports, and the text-encoding boundary is a documented decision. |
| `DEV-01` | [`audits/public-api-review-2026-08-17.md`](audits/public-api-review-2026-08-17.md) | Partly superseded. The default is unchanged and still lets a late reply satisfy the next operation, but it is now documented, pinned by two loopback regressions, and closable through `HardwareEngine.CloseTransportOnNoResponse`. The serial half of the remedy — `DiscardInBuffer` on open — is implemented and still **unverified against a real port**. |
| `DEV-02` | [`audits/public-api-review-2026-08-17.md`](audits/public-api-review-2026-08-17.md) | Superseded. `ExecuteCore` passes `CopyOf(_protocol.PortConfig)`, `StreamCommTransport.Configure` clones into its own state, and `SerialCommTransport.Configure` only reads. `SendAsync_DoesNotMutateTheProtocolConfiguration` pins it. |
| `DEV-03` | [`audits/public-api-review-2026-08-17.md`](audits/public-api-review-2026-08-17.md) | Superseded. `HardwareResponse.Failure` is set on the engine's catch path and pinned by `SendAsync_Failure_CarriesTheExceptionNotJustItsMessage` and `SendAsync_Success_LeavesFailureNull`. |
| `DEV-04` | [`audits/public-api-review-2026-08-17.md`](audits/public-api-review-2026-08-17.md) | Superseded. The three read methods, `ParseResponse`, `Log`, and the `SerialCommTransport` constructor are annotated in source and in both accepted baselines; no `null!` suppression remains in `StreamCommTransport`. |
| `DEV-05` | [`audits/public-api-review-2026-08-17.md`](audits/public-api-review-2026-08-17.md) | Superseded. `Protocols.HardwareProtocol` is absent from source and from both accepted baselines. |
| `DEV-06` | [`audits/public-api-review-2026-08-17.md`](audits/public-api-review-2026-08-17.md) | Open as accepted behaviour. The constructor and the `Log` setter still take over `ICommTransport.Log` and `IProtocolWithLogging.Log`; the disposition was documentation, and [`REFERENCE.md`](REFERENCE.md) states the ownership transfer. |
| `DEV-07` | [`audits/public-api-review-2026-08-17.md`](audits/public-api-review-2026-08-17.md) | Superseded in source, unverified in execution. `SerialCommTransport.Dispose` now takes `_gate` before closing, matching `StreamCommTransport`. No test or scenario disposes a transport during an in-flight **serial** read; the com0com `dispose-during-an-active-operation` check covers the serial path but was executed on `net9.0` only. |
| `DEV-08` | [`audits/public-api-review-2026-08-17.md`](audits/public-api-review-2026-08-17.md) | Open as accepted divergence. `SerialCommTransport.PortName` still has a public setter the interface does not declare. Documented rather than changed; removing or promoting it is a public API decision. |
| `DEV-09` | [`audits/public-api-review-2026-08-17.md`](audits/public-api-review-2026-08-17.md) | Superseded. Both `Checksum` methods throw `ArgumentNullException(nameof(bytes))`, pinned by `Checksum_NullInput_ThrowsArgumentNullExceptionConsistently`. |
| `DEV-10` | [`audits/public-api-review-2026-08-17.md`](audits/public-api-review-2026-08-17.md) | Superseded as documentation. Lossy `RawText` on a binary reply, `RawBytes` precedence, and hardcoded-ASCII `Write(string)` are unchanged in source and all three are stated in [`REFERENCE.md`](REFERENCE.md). |
| `DEV-11` | [`audits/public-api-review-2026-08-17.md`](audits/public-api-review-2026-08-17.md) | Open as accepted design. `SerialConfig`, `HardwareOperation`, and `HardwareResponse` remain public mutable fields. Converting them is binary-breaking and was explicitly rejected for F1. |
| `DEV-12` | [`audits/public-api-review-2026-08-17.md`](audits/public-api-review-2026-08-17.md) | Superseded. Every recorded positive still holds in current source, and each is stated in [`REFERENCE.md`](REFERENCE.md). |
| `DEV-13` | [`audits/public-api-review-2026-08-17.md`](audits/public-api-review-2026-08-17.md) | Open as documented characteristic. All three serial reads still poll inside `Task.Run` with `Thread.Sleep(5)`. The thread-occupancy and latency claims remain a source reading; nothing measures them. |
| `DEV-14` | [`audits/public-api-review-2026-08-17.md`](audits/public-api-review-2026-08-17.md) | Superseded. The boundary now owns [`REFERENCE.md`](REFERENCE.md) and the surrounding module surfaces, registered in the documentation index. |
| `DEV-15` | [`audits/public-api-review-2026-08-17.md`](audits/public-api-review-2026-08-17.md) | Partly superseded. Ten focused regressions cover `DEV-01`, `DEV-02`, `DEV-03`, and `DEV-09`, and `DEV-04` is enforced at compile time. The gap the review named for `DEV-07` — disposal racing an in-flight serial read — is still uncovered by the focused suite. |

The observations raised by that reverification are recorded as findings below.

## DEV-FINDING-001

**Status:** open

**Confidence:** medium

**Observation:** The engine's third endpoint fallback is `ICommTransport.PortName`, and `SerialCommTransport` reports the underlying `SerialPort.PortName` unconditionally — including before any explicit port has been set. With a fresh `SerialCommTransport()` and a protocol whose `PortConfig.PortName` is blank, the engine would therefore resolve a non-blank endpoint from the transport and attempt to open it, instead of failing with its documented "did not define SerialConfig.PortName or a transport endpoint" message.

**Evidence:** `src/Devices/NekoLib.Devices/Core/Engine/HardwareEngine.cs:200-210` resolves the endpoint and only raises `InvalidOperationException` when all three sources are blank. `src/Devices/NekoLib.Devices/Core/Transport/SerialCommTransport.cs:31-42` returns `_port.PortName` from the getter while tracking user intent separately in `_hasExplicitPortName`, which `Open()` — not `PortName` — consults. That separate latch, and the first-pass entry "Prevented the default `SerialPort.PortName` value (`COM1`) from being treated as an explicit user/protocol port" in [`audits/initial-audit.md`](audits/initial-audit.md), are dated repository evidence that the underlying default is non-blank.

**Hypothesis:** The unreachable error path is cosmetic on a machine without that default port, because the open fails and the failure surfaces through `HardwareResponse.Failure` anyway. On a machine that does have it, the operation would be attempted against an unrelated port rather than refused. The stream transports are unaffected: their `PortName` getter returns `string.Empty` until an endpoint is set.

**Evidence limit:** the `SerialPort.PortName` default was **not executed** on either target by this review, and no test covers the engine path with a real `SerialCommTransport`. Confirm it before treating this as a defect.

**Disposition:** Record only, and document the resolution order precisely in [`REFERENCE.md`](REFERENCE.md). Changing either the fallback order or the `PortName` getter is a behavioural and public-contract decision that this documentation review is not authorized to make.

**Outcome link:** none

## DEV-FINDING-002

**Status:** open

**Confidence:** high

**Observation:** `StreamCommTransport`'s receive buffer has no upper bound. The pump appends every received byte to a `List<byte>` that is cleared only on open and on connection teardown, so a peer that pushes continuously into a transport nobody reads grows the buffer until the process runs out of memory.

**Evidence:** `src/Devices/NekoLib.Devices/Core/Transport/StreamCommTransport.cs:462-484` — the pump reads into an 8 KiB stack buffer and appends each byte to `_receiveBuffer` with no cap or drop policy. The only clears not driven by a caller read are `OpenCore` at `:445` and `StopConnection` at `:527`. `TakeAvailable`, `TryTakeExact`, and `TryTakeLine` remove bytes only when a caller reads.

**Hypothesis:** The exposure is real but narrow in the module's intended use: a PDV terminal issues a request and reads the reply, so the buffer drains each operation. It matters for a device that streams unsolicited telemetry, or for a transport left open and unread — which is exactly the traffic pattern the `CloseTransportOnNoResponse` default was kept off to preserve. A related cost is that `TryTakeLine` rescans the whole buffer on each wakeup, so a large unterminated buffer also makes `ReadLine` progressively more expensive.

**Evidence limit:** no test, scenario, or measurement drives an unread transport under sustained input. Neither the growth rate nor a practical failure point is measured.

**Disposition:** Record only, and state the unbounded buffer as a known limit in [`REFERENCE.md`](REFERENCE.md). A bound needs an accepted drop or fail policy, which is a public behavioural decision; the `transport` validation profile's resource-stability requirement is recorded as [`DEV-VALREQ-009`](VALIDATION_REQUIREMENTS.md) instead.

**Outcome link:** [`VALIDATION_REQUIREMENTS.md`](VALIDATION_REQUIREMENTS.md)

## DEV-FINDING-003

**Status:** open

**Confidence:** high

**Observation:** On a freshly constructed parameterless `StreamCommTransport`, the two documented ways to read the resolved endpoint disagree: `PortName` returns `string.Empty` while `PortInfo.PortName` returns `null`.

**Evidence:** `src/Devices/NekoLib.Devices/Core/Transport/StreamCommTransport.cs:49-56` returns `_endpoint ?? string.Empty`, whereas `PortInfo` at `:39-46` clones `_config`, which starts as `CreateDefaultConfig()` at `:725-737` and never assigns `PortName`. The divergence closes as soon as any of the endpoint constructor, `Configure`, or `Open` runs, since `SetEndpoint` and `Configure` write both.

**Hypothesis:** Low impact, but it lands on the exact API pair that [`migrations/f1.md`](migrations/f1.md) tells consumers to read instead of the config object they passed in. A consumer that null-checks one and empty-checks the other behaves differently before first use.

**Disposition:** Record only. Aligning them is a small behavioural change with no requester and would need a decision; the reference documents `PortName` as the endpoint to read.

**Outcome link:** none

## DEV-FINDING-004

**Status:** open

**Confidence:** medium

**Observation:** Both transports set their `_disposed` flag inside the `try` block of `Dispose`, while the `finally` disposes the gate unconditionally. If teardown throws, the transport is left with a disposed gate and a `_disposed` flag still `false`, so `ThrowIfDisposed` passes and the next call fails from the disposed semaphore instead, and a retried `Dispose` throws rather than returning.

**Evidence:** `src/Devices/NekoLib.Devices/Core/Transport/SerialCommTransport.cs:411-432` sets `_disposed = true` after `_port.Close()` inside the `try`, with `_port.Dispose()`, `_gate.Release()`, and `_gate.Dispose()` in the `finally`. `src/Devices/NekoLib.Devices/Core/Transport/StreamCommTransport.cs:412-429` has the same shape around `StopConnection().GetAwaiter().GetResult()`.

**Hypothesis:** Unreachable on the normal path and probably unreachable in practice, since `SerialPort.Close` and stream disposal rarely throw. The observable exception type is `ObjectDisposedException` either way; only the object name and the idempotence of a retried `Dispose` differ.

**Evidence limit:** no test injects a throwing teardown. This is a source reading only.

**Disposition:** Record only. [`REFERENCE.md`](REFERENCE.md) states the terminal, idempotent disposal contract, which holds on every path this repository executes.

**Outcome link:** none
