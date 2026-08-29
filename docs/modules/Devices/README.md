# NekoLib.Devices

**Document ID:** DEV-INTRODUCTION

**Schema version:** 1

**Kind:** reference

**Lifecycle:** current

**Subject:** concise consumer introduction to NekoLib.Devices

**Surface:** introduction

**Boundary:** devices

**Authority role:** non-normative

**Mutation:** authored

**Indexing:** include

NekoLib.Devices drives a device that speaks over a byte stream: a serial
controller, a locker or dispenser board, a scale, or anything reachable over TCP
or a named pipe. It is aimed at unattended PDV/DM terminals, where a device fault
must degrade into a reported failure rather than take the shell down.

Reference the `NekoLib.Devices` package. It has no NekoLib project dependency, so
it can be adopted without taking `NekoLib.Core`, and it carries no logging
framework — diagnostics arrive through one plain delegate you bridge yourself.

Three pieces compose: a **transport** owns the connection and the bytes, a
**protocol** owns framing and reply interpretation, and `HardwareEngine`
orchestrates one serialized transaction across the two.

```csharp
var protocol  = new ProtocolRaw(new SerialConfig { PortName = "COM3", BaudRate = 9600 });
using var transport = new SerialCommTransport();
var engine    = new HardwareEngine(transport, protocol);

var response = await engine.SendAsync(
    new HardwareOperation { Operation = "ping", Args = { ["RawText"] = "PING\r\n" } },
    timeout: 2000);
```

`SerialCommTransport`, `TcpCommTransport`, and `NamedPipeCommTransport` ship in
the box. `ProtocolRaw` forwards bytes or ASCII text unchanged, which is enough
for an undocumented board; anything with real framing wants your own
`IHardwareProtocol`.

Two boundaries decide most integration questions. **You own the transport's
lifetime** — the interface is not `IDisposable`, and the engine never disposes
what you handed it. And **a timed-out operation leaves the receive state
indeterminate**: by default a late reply can satisfy the *next* operation, which
is why strict request/response protocols opt into `CloseTransportOnNoResponse` or
carry their own correlation.

Explicit non-goals: no device discovery, no retry or reconnect policy, no driver
installation, no device registry, and no protocol library beyond the raw
passthrough. The module also emits no Inspection or Telemetry instrumentation and
holds no `NekoLib.Core` reference.

Start from the [manifest](MANIFEST.md) for identity and routing, and the
[technical reference](REFERENCE.md) for the normative contract — ownership,
operation boundaries, encoding, failure model, and disposal.
