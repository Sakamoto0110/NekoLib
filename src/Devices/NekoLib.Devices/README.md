# NekoLib.Devices

**Kind:** reference

**Lifecycle:** current

**Subject:** hardware engine orchestration, transport and protocol contracts,
operation boundaries, configuration ownership, encoding, and disposal

**Reference date:** 2026-08-18

`NekoLib.Devices` talks to hardware over a byte stream. It is an opt-in
`net481`/`net9.0` package with **no NekoLib project reference**; it takes
`System.IO.Ports` on `net9.0` and `Microsoft.Bcl.AsyncInterfaces` on `net481`.

```csharp
var protocol  = new ProtocolRaw(new SerialConfig { PortName = "COM3", BaudRate = 9600 });
using var transport = new SerialCommTransport();
var engine    = new HardwareEngine(transport, protocol) { Log = (level, msg) => log.Write(level, msg) };

var response = await engine.SendAsync(
    new HardwareOperation
    {
        Operation = "ping",
        Args = { ["RawText"] = "PING\r\n" }
    },
    timeout: 2000);
```

## Who owns what

| | |
|---|---|
| **You** | transport construction, endpoint selection, the engine's lifetime, and **disposing the transport** |
| **`HardwareEngine`** | orchestration only: applying configuration, opening on demand, serializing complete transactions, writing, reading, delegating parsing, logging |
| **`IHardwareProtocol`** | frame construction and response interpretation — everything device-specific |
| **`ICommTransport`** | connection, buffering, reads, writes, timeouts, and disposal |

`ICommTransport` does not extend `IDisposable`, so a caller holding the interface
must dispose the concrete transport. The engine never closes or disposes it —
except for the opt-in case below.

`System.IO.Ports` is a **public** dependency on `net9.0`: `SerialConfig` exposes
`Parity`, `StopBits`, and `Handshake`, so the package's compiled surface is bound
to it. That is the cost of one `SerialConfig` shape for every transport.

## Operation boundaries

`HardwareEngine.SendAsync` serializes complete transactions: configure, open,
build, write, read, parse. Concurrent calls queue.

**A timed-out operation leaves the transport in an indeterminate receive state.**
The reply may still arrive afterwards, and the receive buffer survives the
operation boundary — so by default a late reply to operation *N* can be returned
as operation *N+1*'s successful response:

```text
op1 (200 ms budget)  -> Success=False  Status=NoResponse
op2 (never answered) -> Success=True   RawText="LATE-REPLY-TO-OP1"
```

That default is preserved because some devices legitimately push unsolicited
data, and draining unconditionally would discard it.

When your protocol is strict request/response, opt in:

```csharp
var engine = new HardwareEngine(transport, protocol)
{
    CloseTransportOnNoResponse = true
};
```

The engine then closes the transport after any operation that received no bytes.
The next `SendAsync` reopens, and opening clears the receive buffer —
`StreamCommTransport` clears its own buffer, and `SerialCommTransport` discards
the port's input buffer — so the next operation starts clean.

Two consequences worth knowing before enabling it:

- a protocol with legitimate fire-and-forget commands reconnects after every
  unanswered operation;
- if reconnection fails, the **next** operation reports the connect error rather
  than a missing response. `ConnectTimeout` defaults to 5000 ms on the TCP and
  named-pipe transports.

Correlation inside the protocol — a sequence number or a framed reply — is the
only way to make the boundary airtight, and that belongs to your
`IHardwareProtocol`, not to the engine.

## Configuration ownership

`IHardwareProtocol.PortConfig` is **owned by the protocol** and is treated as
read-only by everything else. The engine hands the transport a copy, and neither
shipped transport writes back into the config you supply.

The endpoint is resolved in this order:

1. the explicit endpoint passed to `SendAsync(port, op, timeout)`;
2. `PortConfig.PortName`;
3. the endpoint the transport was constructed with.

Read the resolved value from `ICommTransport.PortName` or `PortInfo` — never by
inspecting the config object you passed in.

`SerialConfig`, `HardwareOperation`, and `HardwareResponse` are mutable,
uncopied, caller-owned data carriers built from public fields. Do not mutate one
while an operation is in flight.

## The failure model

`SendAsync` is deliberately fail-soft: a device fault must not take down an
unattended shell. Every non-cancellation failure becomes a response rather than
an exception.

```csharp
var response = await engine.SendAsync(op, 2000);
if (!response.Success)
{
    log.Error(response.Status);          // protocol-facing string
    if (response.Failure != null)        // transport or engine exception, when one occurred
        log.Error(response.Failure.ToString());
}
```

`Status` is the protocol's vocabulary — `"Ok"`, `"NoResponse"`, or whatever your
protocol returns. `Failure` carries the actual exception, which is what lets you
tell a silent device apart from a disposed transport or a caller bug. It is null
for protocol-level failures and for success.

**Cancellation is never converted.** `OperationCanceledException` propagates; it
is not a failed response.

Argument validation still throws: a null operation, a null transport or protocol,
and a negative timeout are caller errors and surface as exceptions.

## Transports

| Transport | Endpoint form |
|---|---|
| `SerialCommTransport` | `COM3` |
| `TcpCommTransport` | `tcp://host:port`, or `host:port` |
| `NamedPipeCommTransport` | a bare pipe name, `\\.\pipe\name`, or `pipe://\\.\pipe\name` |

`StreamCommTransport` is a real public extension point: implement
`NormalizeEndpoint` and `ConnectStream` and you inherit the whole buffered
receive-pump implementation.

`ReadLine`, `ReadExact`, and `ReadAll` **return null on timeout** — that is the
normal way a silent device is expected to report, and the signatures are
annotated for it. `ReadExact` never returns a partial buffer. `ReadAll`'s quiet
period only starts once the first byte arrives, so a slow device is not cut off
before it answers.

The two implementations differ in how they wait, which matters if you drive many
devices from one process:

- `StreamCommTransport` runs a background receive pump, so a timed-out caller
  never leaves an orphaned stream read;
- `SerialCommTransport` polls the port inside `Task.Run`, so each read occupies a
  thread-pool thread for up to its timeout and observes cancellation with about
  5 ms of latency.

Disposal is terminal and takes the transport's gate: it does not race an
in-flight operation.

## Extending Devices

`IHardwareProtocol` is the supported device-specific extension seam. Implement
it when framing or reply interpretation differs from the shipped raw protocol:

```csharp
public sealed class AcmeProtocol : IHardwareProtocol
{
    public ControllerModel Model => ControllerModel.ControllerRaw;

    public SerialConfig PortConfig { get; } = new SerialConfig
    {
        PortName = "tcp://127.0.0.1:9000",
        NewLine = "\r\n"
    };

    public byte[] BuildCommand(HardwareOperation operation)
    {
        if (operation == null) throw new ArgumentNullException(nameof(operation));
        return Encoding.ASCII.GetBytes(operation.Operation + "\r\n");
    }

    public HardwareResponse ParseResponse(byte[]? reply, HardwareOperation operation)
    {
        if (operation == null) throw new ArgumentNullException(nameof(operation));

        return new HardwareResponse
        {
            Success = reply != null,
            Status = reply == null ? "NoResponse" : "Ok",
            RawBytes = reply ?? Array.Empty<byte>(),
            RawText = reply == null ? string.Empty : Encoding.ASCII.GetString(reply),
            Request = operation,
            PrettyText = reply == null ? "No response" : Encoding.ASCII.GetString(reply)
        };
    }
}
```

Return the complete frame from `BuildCommand`; do not open the transport or own
retry policy there. `ParseResponse` receives `null` for a timeout and should
normally represent device/protocol outcomes in `HardwareResponse` instead of
throwing. If requests can overlap with unsolicited or late data, put the
correlation rule in the protocol.

For a new byte-stream transport, normally derive from `StreamCommTransport` and
implement only endpoint normalization and connected-stream creation. The base
class owns buffering, the receive pump, timeouts, cancellation, and stream
disposal. A direct `ICommTransport` implementation must reproduce those public
semantics itself, and its concrete type must provide a disposal mechanism
because the interface does not.

`IProtocolWithLogging` is only an opt-in logging capability, not discovery or
activation. `HardwareLogHandler`, `SerialConfig`, `HardwareOperation`, and
`HardwareResponse` are composition/data contracts; none defines a plug-in
loader or reflection-based extension model.

## Encoding and raw bytes

Binary payloads are preserved exactly. `HardwareResponse.RawBytes` is the reply
array unchanged.

Text is the documented exception. `ICommTransport.Write(string)` and
`StreamCommTransport.ReadLine` use **ASCII**, always. `ProtocolRaw` decodes with
its configured `TextEncoding` (ASCII by default) and you may supply another
single-byte encoding.

`ProtocolRaw.ParseResponse` fills `RawText` on every reply, including binary
ones — so on a binary payload `RawText` is lossy and must never be used to
reconstruct it. Use `RawBytes`.

When an operation supplies both `RawBytes` and `RawText`, **`RawBytes` wins**.

`Checksum` and `LogUtil` are public helpers for protocol authors: additive and
XOR checksums, hex formatting, and control-character escaping. Both `Checksum`
methods reject a null buffer with `ArgumentNullException`.

## Logging

Constructing a `HardwareEngine` **transfers ownership** of `ICommTransport.Log`
and, for a protocol implementing `IProtocolWithLogging`, the protocol's `Log`.
Setting `engine.Log` overwrites both again. A logger you wired on the transport
before constructing the engine is replaced without a signal.

The delegate is `HardwareLogHandler`, deliberately — Devices takes no dependency
on `NekoLib.Core` or any logging package. Bridge it yourself at the composition
root.

## Verification

```powershell
dotnet test tests\NekoLib.Devices.Tests\Unit\NekoLib.Devices.Tests.Unit.csproj
```

The suite covers both targets using in-memory fakes plus **real loopback TCP and
in-process named pipes**. That is not serial evidence: no physical UART, no
virtual COM pair, and no electrical behaviour is exercised anywhere in it.

Real serial behaviour is validated separately by the
[com0com scenario](../../../runtime_tests/Devices/Com0Com/README.md), which must
be launched explicitly. Do not treat a fake or loopback test as proof of serial
I/O.
