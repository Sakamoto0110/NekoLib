# NekoLib.Pipes

**Document ID:** PIPE-INTRODUCTION

**Schema version:** 1

**Kind:** reference

**Lifecycle:** current

**Subject:** concise consumer introduction to NekoLib.Pipes

**Surface:** introduction

**Boundary:** pipes

**Authority role:** non-normative

**Mutation:** authored

**Indexing:** include

NekoLib.Pipes is a small local IPC package for cooperative processes on one
Windows machine. It gives an application two channels over named pipes: framed
JSON request/response RPC, and an optional bounded best-effort event stream on a
sibling `<name>.events` endpoint. It is the transport under NekoLib.Watchdog's
control channel, and it is usable on its own.

Reference the `NekoLib.Pipes` package. It has no NekoLib project dependency, so
it can be adopted without taking `NekoLib.Core`. Host `PipeServer`, map handlers
by operation name, and call from `PipeClient`; each `SendAsync` owns and closes
its own connection, so the client needs no disposal. Publish through
`PipeServer.Events` or a standalone `PipeEventHub`, and subscribe with
`PipeEventClient`.

Two boundaries matter before adopting it. **Pipes is not an authorization
boundary:** `CurrentUserOnly` is opt-in, the compatibility default is platform
pipe security, and neither defends against a hostile process already running as
the same user — the application owns operation authorization. **Event delivery
is bounded and best effort:** each subscriber has a capped queue, and a full
queue either drops the event or disconnects that subscriber, by configured
policy.

Start with the [technical reference](REFERENCE.md) for the complete lifecycle,
framing, error, metrics, and security contract. Use the
[module manifest](MANIFEST.md) for packages, targets, API baselines, tests,
scenarios, audits, and migration routes. Consumers upgrading from a pre-`1.0.0`
candidate should read the [F1-PIPE migration](migrations/f1.md) first.
