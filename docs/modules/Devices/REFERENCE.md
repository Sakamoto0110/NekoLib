# NekoLib.Devices — Technical Reference

**Document ID:** DEV-REFERENCE

**Schema version:** 1

**Kind:** reference

**Lifecycle:** current

**Subject:** normative technical contract for the NekoLib.Devices boundary

**Surface:** technical-reference

**Boundary:** devices

**Authority role:** normative

**Mutation:** authored

**Indexing:** include

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

`HardwareEngine` itself owns no disposable resource and is deliberately not
`IDisposable`. Its internal operation gate is the only state it holds; dropping
the reference is enough.

`System.IO.Ports` is a **public** dependency on `net9.0`: `SerialConfig` exposes
`Parity`, `StopBits`, and `Handshake`, so the package's compiled surface is bound
to it. That is the cost of one `SerialConfig` shape for every transport.

## Operation boundaries

`HardwareEngine.SendAsync` serializes complete transactions: configure, resolve
the endpoint, open if needed, build, write, read, parse. Concurrent calls queue
on an internal gate, and the gate is released on every path including failure and
cancellation.

The receive step is always `ReadAll(timeout, 50, ct)`. **The 50 ms quiet period
is fixed and not configurable through the engine.** It starts only after the
first byte arrives, so a slow device is not cut off before it answers — but a
device that pauses for more than 50 ms *in the middle of a frame* returns a
truncated prefix, and the remainder stays buffered for the next read. That is the
contract, not a defect; the com0com scenario asserts exactly this behaviour under
`a-gap-beyond-the-quiet-period-ends-the-read`. A protocol whose frames contain
longer intra-frame gaps must drive `ICommTransport` directly rather than through
`SendAsync`.

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

A close that itself fails is logged and swallowed: it does not become the
operation's outcome, and the next operation reports whatever state the transport
is actually in.

Correlation inside the protocol — a sequence number or a framed reply — is the
only way to make the boundary airtight, and that belongs to your
`IHardwareProtocol`, not to the engine.

If the transport is already open on a **different** endpoint than the one the
engine resolved, the operation is refused. That refusal is an
`InvalidOperationException` raised inside the transaction, so it surfaces as a
failed response carrying `Failure`, not as a thrown exception.

## Configuration ownership

`IHardwareProtocol.PortConfig` is **owned by the protocol** and is treated as
read-only by everything else. The engine hands the transport a copy, and neither
shipped transport writes back into the config you supply.

The endpoint is resolved in this order:

1. the explicit endpoint passed to `SendAsync(port, op, timeout)`;
2. `PortConfig.PortName`;
3. the endpoint the transport already reports through `ICommTransport.PortName`.

Only if all three are blank does the engine fail with "did not define
SerialConfig.PortName or a transport endpoint". Step 3 means a transport that
reports a non-blank endpoint before it has been configured supplies that endpoint
to the engine — see [`DEV-FINDING-001`](FINDINGS.md).

Read the resolved value from `ICommTransport.PortName` or `PortInfo` — never by
inspecting the config object you passed in.

`Configure` behaves differently on an already-open transport, and the difference
is deliberate on neither side — it is simply what the two implementations do:

| | `StreamCommTransport` (TCP, named pipe) | `SerialCommTransport` |
|---|---|---|
| Validates | read/write timeouts only | baud rate, data bits, stop bits, handshake, both timeouts |
| Endpoint change while open | rejected with `InvalidOperationException` | rejected with `InvalidOperationException` |
| Other fields while open | **applied** (newline and timeouts take effect) | **silently ignored**; the call returns without validating |

A default-constructed `SerialConfig` is therefore accepted by a stream transport
and rejected by the serial transport, whose `BaudRate` must be greater than zero.
Empty or null `NewLine` becomes `"\r\n"` on both.

`SerialConfig`, `HardwareOperation`, and `HardwareResponse` are mutable,
uncopied, caller-owned data carriers built from public fields. Do not mutate one
while an operation is in flight.

## The failure model

`SendAsync` is deliberately fail-soft: a device fault must not take down an
unattended shell. Every non-cancellation failure raised *inside* the transaction
becomes a response rather than an exception.

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
is not a failed response. It also propagates from the operation gate, so a token
that is already cancelled fails the call before any transport work begins.

The boundary between "throws" and "returns a failed response" is the argument
check at the top of `SendAsync`:

| Condition | Result |
|---|---|
| null `op`, null transport or protocol, negative `timeout`, blank explicit `port` | **throws** — caller error |
| cancelled token | **throws** `OperationCanceledException` |
| no endpoint resolved, transport open on another endpoint, connect failure, write failure, protocol exception, null parsed response | **failed response** with `Failure` |
| device silent | protocol-defined response for a `null` reply, `Failure` null |

## Transports

| Transport | Endpoint form |
|---|---|
| `SerialCommTransport` | `COM3` |
| `TcpCommTransport` | `tcp://host:port`, or `host:port` |
| `NamedPipeCommTransport` | a bare pipe name, `\\.\pipe\name`, or `pipe://\\.\pipe\name` |

Both stream transports canonicalize and validate the endpoint before connecting
and reject malformed forms with a clear `ArgumentException`. Both dispose the
half-built client on every failure path.

`ConnectTimeout` defaults to 5000 ms on both. A negative value is rejected when
opening. Zero disables the **client-side** timeout, which is not the same thing
on both: TCP then falls back to the operating system's own connect timeout,
whereas the named-pipe client waits indefinitely and can only be released by the
cancellation token.

`StreamCommTransport` is a real public extension point: implement
`NormalizeEndpoint` and `ConnectStream` and you inherit the whole buffered
receive-pump implementation.

### Reads

`ReadLine`, `ReadExact`, and `ReadAll` **return null on timeout** — that is the
normal way a silent device is expected to report, and the signatures are
annotated for it. `ReadExact` never returns a partial buffer; it returns the
complete requested length or `null`.

Two behaviours belong to the stream transports specifically:

- **Buffered bytes outlive the connection.** A read succeeds against a closed
  connection while data remains buffered, and only throws `InvalidOperationException`
  ("Transport not open") once the buffer is also empty.
- **A closed, empty connection ends a read immediately** rather than waiting out
  the remaining budget.

At `timeoutMs: 0` the two implementations diverge. A stream transport still
returns whatever is already buffered — `ReadAll(0, 0)` is an effective drain. The
serial transport's polling loop never executes at zero and returns `null`, so the
same call is a no-op there. Do not write a transport-neutral drain on top of it.

### How they wait

The two implementations differ in how they wait, which matters if you drive many
devices from one process:

- `StreamCommTransport` runs a background receive pump, so a timed-out caller
  never leaves an orphaned stream read;
- `SerialCommTransport` polls the port inside `Task.Run`, so each read occupies a
  thread-pool thread for up to its timeout and observes cancellation with about
  5 ms of latency.

The pump's receive buffer is **unbounded**. A device that pushes continuously
into a transport nobody reads grows it without limit; see
[`DEV-FINDING-002`](FINDINGS.md). Pump shutdown is bounded at 1000 ms, and a pump
that outlives it is logged rather than waited on forever.

### Lifetime

Disposal is terminal, idempotent, and takes the transport's gate: it does not
race an in-flight operation, and `StreamCommTransport` also stops the receive
pump and disposes the stream. An operation that was **already queued** on that
gate when disposal completes fails with `ObjectDisposedException` from the
disposed gate rather than from the transport's own disposed check; the exception
type a caller observes is the same either way. Every public member throws
`ObjectDisposedException` afterwards, including `Close` and `Open`.

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
throwing. Returning `null` from `ParseResponse` is treated as a protocol error
and becomes a failed response. The engine fills `Request` when the protocol left
it null and always overwrites `Elapsed`; every other field is yours. If requests
can overlap with unsolicited or late data, put the correlation rule in the
protocol.

`ControllerModel` is an identity label reported by the protocol. The engine uses
it only in log messages and does not dispatch on it, so an external protocol for
a controller not in the enum can report `ControllerRaw`.

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

Text is the documented exception, and the ASCII paths are not all reachable
through the same knob:

| Path | Encoding | Configurable? |
|---|---|---|
| `ICommTransport.Write(string)`, both transports | ASCII | no |
| `StreamCommTransport.ReadLine` | ASCII | no |
| `SerialCommTransport.ReadLine` | `SerialPort.Encoding`, which this module never assigns | no — `SerialConfig` has no encoding field |
| `ProtocolRaw` `RawText` build and decode | `ProtocolRaw.TextEncoding` | yes, via the constructor; ASCII by default |

`ProtocolRaw`'s constructor accepts any `Encoding`, but the shipped guidance and
the only executed evidence cover **single-byte** encodings. A Latin-1 payload has
been shown to survive a real virtual-COM round trip **as bytes**; that is not
UTF-8 support, and no multi-byte encoding is exercised anywhere. Send binary and
non-ASCII payloads as `RawBytes`.

`ProtocolRaw.ParseResponse` fills `RawText` on every reply, including binary
ones — so on a binary payload `RawText` is lossy and must never be used to
reconstruct it. Use `RawBytes`.

When an operation supplies both `RawBytes` and `RawText`, **`RawBytes` wins**.
The two arguments also handle a null value differently: a null `RawBytes`
fails the type check and throws `ArgumentException`, while a null `RawText` is
treated as an empty string and produces an empty frame.

`ProtocolRaw` never sets `HardwareResponse.PrettyText`; it is a protocol-owned
presentation field that the engine does not populate either.

`Checksum` and `LogUtil` are public helpers for protocol authors: additive and
XOR checksums, hex formatting, and control-character escaping. Both `Checksum`
methods reject a null buffer with `ArgumentNullException`. Neither helper has a
caller inside this repository; they exist for external protocol implementations.

## Logging

Constructing a `HardwareEngine` **transfers ownership** of `ICommTransport.Log`
and, for a protocol implementing `IProtocolWithLogging`, the protocol's `Log`.
Setting `engine.Log` overwrites both again. A logger you wired on the transport
before constructing the engine is replaced without a signal.

The delegate is `HardwareLogHandler`, deliberately — Devices takes no dependency
on `NekoLib.Core` or any logging package. Bridge it yourself at the composition
root. It is invoked synchronously on the calling thread, including from the
receive pump's thread-pool thread, so a slow or throwing handler is the caller's
problem: the module does not isolate handler exceptions.

`LogLevel.Raw` carries full frame contents in hex and decoded text. Treat it as
sensitive if your device protocol carries anything sensitive.

## Targets and platform

Both targets compile the same source: there is no conditional compilation in the
module, and the declared `NETFRAMEWORK` and `NET_9` constants are currently
unused. The two accepted API manifests are identical apart from the
`TargetFramework` assembly attribute.

`NEKOLIB` is **not** declared by this project, unlike most of the family.

The two target-specific inputs are packaging requirements rather than behavioural
differences: `System.IO.Ports 9.0.0` on `net9.0`, and
`Microsoft.Bcl.AsyncInterfaces 10.0.1` on `net481` to supply `IAsyncDisposable`.

Serial support is whatever `System.IO.Ports` provides on the host. All executed
evidence for this module is Windows-only.

## Verification and evidence

Requirements live in [`VALIDATION_REQUIREMENTS.md`](VALIDATION_REQUIREMENTS.md)
and executed results in [`VALIDATIONS.md`](VALIDATIONS.md). The short version:

```powershell
dotnet test tests\NekoLib.Devices.Tests\Unit\NekoLib.Devices.Tests.Unit.csproj
```

The suite covers both targets using in-memory fakes plus **real loopback TCP and
in-process named pipes**. That is not serial evidence: no physical UART, no
virtual COM pair, and no electrical behaviour is exercised anywhere in it.

Real serial behaviour is validated separately by the
[com0com scenario](../../../runtime_tests/Devices/Com0Com/README.md), which must
be launched explicitly and is evidence only for a Windows machine with the
configured virtual pairs. A virtual pair does not implement baud, parity,
framing, or flow control on the wire, so even a passing run says nothing about
UART levels, cabling, USB adapters, or electrical conditions. Do not treat a
fake, loopback, or virtual-COM result as proof of physical serial I/O.
