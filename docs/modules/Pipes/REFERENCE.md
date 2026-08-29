# NekoLib.Pipes — Technical Reference

**Document ID:** PIPE-REFERENCE

**Schema version:** 1

**Kind:** reference

**Lifecycle:** current

**Subject:** normative technical contract for the NekoLib.Pipes boundary

**Surface:** technical-reference

**Boundary:** pipes

**Authority role:** normative

**Mutation:** authored

**Indexing:** include

`NekoLib.Pipes` is a small, instance-owned local IPC package. It provides one
request/response connection per `PipeClient.SendAsync`, a multi-client
`PipeServer`, and an optional bounded best-effort event channel. It has no
NekoLib project reference.

```csharp
var server = new PipeServer(new PipeServerOptions
{
    PipeName = "my.app.control",
    AccessPolicy = PipeAccessPolicy.CurrentUserOnly
});

server.Map("ping", (request, cancellationToken) =>
    Task.FromResult(new PipeMessage { Ok = true }));
server.Start();

var client = new PipeClient(new PipeClientOptions
{
    PipeName = "my.app.control"
});

var response = await client.SendAsync("ping");
await server.ShutdownAsync();
```

## Targets and package boundary

The package targets `net481` and `net9.0`. Its public payload DOM is deliberately
target-specific:

| Target | `PipeMessage.Data` | Direct package dependency |
|---|---|---|
| `net481` | `Newtonsoft.Json.Linq.JToken?` | `Newtonsoft.Json` 13.0.3 |
| `net9.0` | `System.Text.Json.JsonElement?` | none |

The wire is JSON on both targets. Shared multi-target handlers either use
conditional compilation for DOM-specific operations or restrict themselves to
neutral operations such as `ToString()`. Same-target tests do not prove
cross-target wire compatibility; a separate-process `net481`/`net9.0` matrix
remains an open evidence gap recorded in
[`VALIDATION_REQUIREMENTS.md`](VALIDATION_REQUIREMENTS.md) and
[`VALIDATIONS.md`](VALIDATIONS.md).

The project declares `NET481` and `NET9`; it does not declare `NEKOLIB`. It is
in `NekoLib.sln`, is packable as `NekoLib.Pipes`, and ships XML member
documentation beside both target assemblies. The `net9.0` asset is unqualified,
but the implementation uses Windows named-pipe semantics and Windows-only ACL
APIs on `net481`; no non-Windows execution is claimed. The
[module manifest](MANIFEST.md) owns the routed topology and API baselines.

`PipeFraming` and its frame-size exception are internal protocol details.
`IPipeMetrics`, public snapshot constructors, `PipeServer.Map`, and the
standalone `PipeEventHub` are deliberate extension surfaces.
`SimplePipeMetrics` is sealed; implement `IPipeMetrics` to customize collection.

## Configuration ownership and validation

`PipeClient` and `PipeServer` capture one coherent snapshot of every option in
their constructors. Later mutation of the caller-owned options object has no
effect on the existing endpoint, including pipe names, limits, timeouts,
metrics sinks, event settings, and access policy.

Construction rejects:

- blank pipe names;
- non-positive client, subscriber, queue, and message-size limits;
- non-positive timeouts or timeouts greater than `Int32.MaxValue` milliseconds;
- undeclared `PipeAccessPolicy` or `PipeEventQueueOverflowPolicy` values.

`PipeServer` validates every event setting even when `EnableEvents` is `false`.

`PipeEventClient` captures its base pipe name at construction. Its
`ConnectTimeout` must be positive, and `ReconnectDelay` must be non-negative;
both also have the `Int32.MaxValue`-millisecond ceiling. Their values are
captured separately for each connection attempt or reconnect wait.
`AutoReconnect` is the one intentional live switch and may be changed while the
background loop is running.

The RPC server name is the configured base name. The event endpoint always uses
the same captured base plus `.events`. Neither name is prefixed or hashed by
this package; the application owns name selection and uniqueness.

The metrics default is asymmetric on purpose. A `PipeServer` with
`Metrics = null` creates an instance-owned `SimplePipeMetrics`, and its owned
`Events` hub shares that same instance. A standalone `PipeEventHub` constructed
with `metrics: null` uses `NoopPipeMetrics` and records nothing.

## Ownership and lifecycle

| Type | Ownership contract |
|---|---|
| `PipeClient` | Stateless between calls. Each `SendAsync` creates, owns, and closes its stream. The client has no disposal contract. Concurrent calls are independent. |
| `PipeServer` | Owns its cancellation source, accept loop, admitted RPC operations, streams, limiter, and optional `Events` hub. |
| `PipeEventHub` | Owns its cancellation source, accept loop, subscriber operations, streams, per-subscriber queues, and writers. |
| `PipeEventClient` | Owns one reconnect/listen loop, its cancellation source, and the current stream. |

A supplied `IPipeMetrics` sink is never owned or disposed by any endpoint.

`Start()` is one-shot and race-safe on every stateful endpoint. A concurrent
second start fails with `InvalidOperationException`. Once shutdown starts,
`Start`, `PipeServer.Map`, and `PipeEventHub.PublishAsync` fail with
`ObjectDisposedException`; disposal before start is terminal too.

`PipeServer`, `PipeEventHub`, and `PipeEventClient` expose the same cross-target
contract:

```csharp
await endpoint.ShutdownAsync();
```

It atomically begins shutdown, cancels and closes owned transports, and waits
for all work already admitted by that endpoint. The returned task is idempotent:
repeated calls observe the same completion. A mapped handler that initiates its
own server's shutdown must not await the returned task from inside that same
handler: the task includes that handler. Initiate it, return, and await it from
the owner.

The same self-wait rule applies to `PipeEventClient` callbacks. A callback may
initiate `ShutdownAsync`, but must not await that task or `DisposeAsync` before
returning because completion includes the callback loop. Synchronous `Dispose`
detects this case, initiates shutdown, and returns immediately.

`Dispose()` initiates the same shutdown but waits synchronously for at most two
seconds; uncooperative user work may therefore outlive the call. When it does,
the endpoint's owned cancellation source and limiter are disposed later, by the
shutdown completion rather than by `Dispose`. Use `ShutdownAsync()` whenever
definitive completion matters. On `net9.0`, all three stateful types also
implement `IAsyncDisposable`, and `DisposeAsync()` awaits the real shutdown. No
async-disposal dependency is added to `net481`.

`PipeEventClient` remains started after an `AutoReconnect=false` loop ends; it
cannot be restarted. Call `ShutdownAsync` or dispose it to enter the terminal
state and release its cancellation source.

## Admission and concurrency

`MaxClients` and `MaxEventSubscribers` bound admitted operations, not accepted
connections after the fact. Each endpoint holds a semaphore of that size, and
every slot covers one operation from pipe creation through disconnect —
including the interval in which that operation is still waiting for a peer. At
most one slot is therefore a pending listener at any moment, and when every slot
holds an established peer no listener is outstanding: a further peer's connect
waits until a slot frees and fails on its own `ConnectTimeout` if none does.

`NamedPipeServerStream.MaxAllowedServerInstances` is passed to the operating
system; the semaphore, not that value, is the enforced admission bound.

`Map` may be called while the server is running and replaces an existing handler
for the same name. Handlers are invoked concurrently for independent clients,
are dispatched by exact name match, and are not marshalled to a captured
synchronization context.

## RPC, cancellation, and errors

`PipeClient.SendAsync` validates a nonblank operation name, opens one duplex
connection, sends one request, reads one response, validates its ID and `res`
type, and closes the stream. There is no connection pooling, retry, or
keep-alive; concurrency comes from issuing concurrent calls.

`ConnectTimeout` bounds only connection establishment. `RequestTimeout` starts
after connection and bounds request write plus response read. The caller token
applies to both phases. Timeout or caller cancellation surfaces as
`OperationCanceledException`; the library does not convert it to a
`PipeMessage`. On `net481`, the blocking connect and framing workers are raced
against cancellation, observed if abandoned, and unblocked when the owned
stream closes.

The server's `ClientIdleTimeout` measures inactivity while waiting for the next
request. It is paused during handler execution and response writing, so it does
not impose a handler deadline, and it is re-armed before each subsequent read.
Expiry cancels only that connection's linked token; the server keeps running and
other connections are unaffected. A frame whose `Type` is not `req` is ignored,
and the idle window is re-armed for the next read.

Server shutdown cancels the token passed to a mapped handler; application
handlers are responsible for observing it. A peer that disconnects mid-handler
does **not** cancel that token — the handler runs to completion and the response
write then fails, ending that connection's loop. If shutdown is requested while
a handler is running, no response is written for that request and the client
observes a closed connection.

`Dispatch` normalizes the handler's returned envelope in place, overwriting its
`Id`, `Type`, and `Name`. Return a fresh `PipeMessage` per call; a shared or
cached instance is mutated by every request that returns it.

Framework-generated failures use `PipeErrorCodes`. Application handlers may
return other string codes; this is not a closed enum.

| Condition | Client outcome |
|---|---|
| no mapped handler | `Ok=false`, `PipeErrorCodes.NotFound` |
| handler throws | `Ok=false`, `PipeErrorCodes.Exception`, message `The handler failed.` |
| handler response exceeds the server cap | `Ok=false`, `PipeErrorCodes.ResponseTooLarge` |
| clean EOF before a response frame | `Ok=false`, `PipeErrorCodes.ConnectionClosed` |
| caller cancellation, connect timeout, or request timeout | `OperationCanceledException` |
| connect, serialization, write, parse, or truncated-frame failure | exception propagates |
| response ID mismatch or type other than `res` | `InvalidOperationException` |
| malformed or oversized incoming request | server closes the connection; no correlated response is fabricated |

Handler exception details stay local: the wire receives only the sanitized
message, while the configured metrics sink receives the original exception.

`SendAsync` is asynchronous throughout, so every failure above — including
argument validation and an oversized request — surfaces through the returned
task rather than synchronously at the call site.

## Wire contract

Every frame is a four-byte length prefix followed by one UTF-8 JSON document.
The prefix is written and read with `BitConverter` in the host's native byte
order; because the transport is same-machine, both peers use the same order.

The JSON document is a serialized `PipeMessage` with default serializer settings
on both targets, so member names appear on the wire exactly as declared — `Id`,
`Type`, `Name`, `Ok`, `Data`, `Error` — and `Id` is a GUID string. `Type` carries
the discriminator `req`, `res`, or `evt`. A response copies the request's `Id`
and `Name`. `Error` appears on unsuccessful envelopes and carries `Code` and
`Message`.

The two targets therefore agree on member names and value shapes by
construction. That is a source-level property, not executed evidence: no
`net481` to `net9.0` separate-process probe has been run.

No serializer depth, schema, or duplicate-ID policy is configured beyond the
serializer defaults, and request IDs correlate responses without being retained
or deduplicated. Replay protection and payload schema validation are application
concerns.

## Framing and limits

RPC requests and responses default to a 1 MiB maximum and use the captured
`MaxMessageBytes` on each side. Each side enforces only its own limit, so a
client configured above the server's cap still has its oversized frames refused
by the server. A writer validates the serialized size before it emits any bytes.
A declared length less than one or greater than the receiving limit is invalid.

EOF before any byte of a new length prefix is a clean close. EOF after a partial
prefix or payload is a truncated frame and throws `EndOfStreamException`.
Malformed JSON propagates its serializer parse exception.

Events have a fixed 1 MiB frame maximum in this release; `MaxMessageBytes` does
not apply to them. `PublishAsync` serializes and preflights the event before
inspecting subscriber queues. An oversized event throws an
`InvalidOperationException`, changes no queue, does not disconnect subscribers,
and does not increment the publication metric. There is no chunking,
persistence, or retry protocol.

`PublishAsync` performs its lifecycle checks, serialization, preflight, and
enqueue attempts synchronously and then returns an already-completed task. Its
`InvalidOperationException`, `ObjectDisposedException`, and serialization
failures are raised at the call site rather than through the returned task.

## Events, backpressure, and callback order

Each subscriber has one bounded FIFO queue and one serialized writer. One slow
subscriber never blocks another subscriber or the publisher, and that single
writer is what keeps concurrent publications from interleaving frames on one
subscriber stream.

`EventSubscriberQueueCapacity` controls the per-subscriber bound:

- `DropNewest` marks that subscriber's delivery failed and keeps it connected;
- `DisconnectSubscriber` marks queued deliveries failed, closes the slow
  subscriber, and leaves other subscribers alone.

`PublishAsync` confirms the enqueue attempt only; it does not wait for pipe I/O
or promise delivery. A cancelled publish token marks each current subscriber's
attempt failed instead of throwing. With no subscribers, publication succeeds
and records one publication with zero deliveries. The event name is placed on
the wire without validation; applications should define a stable nonblank
vocabulary.

The hub creates the event endpoint as a duplex pipe and uses a pending read as
its subscriber-liveness signal. Anything a subscriber writes is discarded and
cannot corrupt event delivery. `PipeEventClient` connects read-only and never
writes.

`PipeEventClient` processes frames and callbacks serially on its background loop.
For one established connection, the order is:

```text
OnConnected -> zero or more OnEvent -> optional OnError -> OnDisconnected
```

`OnError` observes connection, framing, parsing, and listen failures. A failed
connection attempt raises `OnError` but never `OnDisconnected`, because no
connection existed. Clean remote EOF and local shutdown disconnect without an
error. Every subscriber of every event is invoked in registration order and
isolated individually; one throwing callback does not skip later callbacks or
stop the listen loop. No callback is marshalled to a UI or captured
synchronization context.

## Metrics

`IPipeMetrics` is optional synchronous observation. Every callback made by a
client, server, or event hub is protected: a throwing metrics implementation
cannot abort a request, replace the original failure, disconnect a peer, or
fault publication. The sink is not called recursively to report its own error.
Calling `Snapshot()` is consumer-owned and is not wrapped by a transport path.

`OnServerResponseSent` records a response *attempt*: it fires even when the
write failed and the connection is about to close, and its elapsed time spans
handler dispatch through the write attempt. `OnServerClientConnected` and
`OnServerClientDisconnected` are raised by the event hub as well as the RPC
server, carrying the `.events` endpoint name, so a sink shared by both — the
default for `PipeServer.Events` — aggregates RPC clients and event subscribers
into one connected count.

`SimplePipeMetrics.Snapshot()` is cumulative and has no reset operation:

- server requests and response attempts, success/failure, connected RPC and
  event clients, and response latency;
- client connection attempts, request outcomes, and total latency;
- event publications plus successful and failed subscriber deliveries;
- total locally observed transport/handler errors.

`Published` advances when all current subscriber delivery trackers complete,
which may be after `PublishAsync` returns. Rejected oversized events do not
advance it. Latency values are whole milliseconds derived from elapsed time.
Snapshots are new DTOs over current counters and are point-in-time observational
evidence rather than transactionally consistent protocol state; a fresh metrics
instance is the supported reset boundary. `NoopPipeMetrics` records nothing and
returns null through `IPipeMetrics.Snapshot()`, which it implements explicitly —
call it through the interface, not through the concrete type.

### Writing custom pipe metrics

Implement `IPipeMetrics` and assign the instance through `PipeServerOptions` or
`PipeClientOptions`. Supply the same instance to both only when one aggregate is
intentional. Callbacks are synchronous, may arrive concurrently from different
connections and event-delivery trackers, and are not marshalled to a captured
synchronization context. Keep them short, non-blocking, and thread-safe; enqueue
external export work instead of performing network I/O inline.

Transport-owned callback invocations are failure-isolated, so throwing does not
change the RPC/event outcome. `Snapshot()` is different: the consumer calls it
directly, and Pipes neither catches its exception nor imposes a snapshot shape.
Return a new immutable/caller-owned DTO, or null when the implementation exposes
no snapshot. Metrics are observational only; do not use them for authorization,
protocol decisions, retries, or delivery control.

```csharp
public sealed class ErrorCountingMetrics : IPipeMetrics
{
    private long _errors;

    public void OnError(string pipeName, string where, Exception error)
        => Interlocked.Increment(ref _errors);

    public void OnServerClientConnected(string pipeName) { }
    public void OnServerClientDisconnected(string pipeName) { }
    public void OnServerRequestReceived(string pipeName, string name) { }
    public void OnServerResponseSent(
        string pipeName, string name, bool ok, TimeSpan elapsed) { }
    public void OnServerEventPublished(
        string pipeName, string eventName, int subscribers, int success, int failed) { }
    public void OnClientConnect(
        string pipeName, TimeSpan elapsed, bool ok, string? errorCode) { }
    public void OnClientRequest(string pipeName, string name) { }
    public void OnClientResponse(
        string pipeName, string name, bool ok, TimeSpan elapsed, string? errorCode) { }

    public PipeMetricsSnapshot? Snapshot() => null;
}
```

`IPipeMetrics` is the package's only consumer-implemented public interface.
`PipeServer.Map` registers application handlers, `PipeEventClient` events are
callbacks, public `PipeMetricsSnapshot` constructors compose DTOs, and a
standalone `PipeEventHub` is an application-owned endpoint; none is plug-in
discovery or activation. Framing, serialization, stream creation, retry, and
authorization are not replaceable provider contracts. Exception objects and
pipe/location labels supplied to metrics may contain sensitive operational
evidence, so a custom exporter owns redaction and access control.

## Security boundary

Pipes is a local same-machine transport for cooperative processes. It is not an
authentication or authorization framework.

- `PlatformDefault` preserves operating-system default pipe security and is the
  compatibility default.
- `CurrentUserOnly` restricts the server to the operating-system user that
  created it. On `net481` this is an explicit Windows pipe ACL with inheritance
  disabled, the current user as owner, and a single full-control rule; on
  `net9.0` it is `PipeOptions.CurrentUserOnly`.

The policy is captured at construction, applies to both the RPC and the event
endpoint, and is exposed as `PipeServer.AccessPolicy`.

Neither option protects against a hostile process already running as the same
user. Pipe names are identifiers, not credentials. The application owns pipe
name selection, operation authorization, payload sensitivity, idempotence, and
any stronger peer/session protocol. NekoLib.Pipes supplies no peer identity,
credential, challenge, replay, remote transport, or privileged-control layer.

Request and event payloads are sent verbatim to the peer, so a secret placed in
one is disclosed to whoever can open the endpoint.

## Non-goals

Pipes does not provide remote or cross-machine transport, service discovery,
peer authentication or authorization, replay protection, message persistence,
delivery guarantees, chunking, connection pooling, automatic retries, a schema or
contract registry, sender-selected CLR types, or a privileged control plane. It
emits no module instrumentation: `IPipeMetrics` remains the preferred first seam
if the frozen Inspection rollout in [`ROADMAP.md`](../../../ROADMAP.md) is ever
unfrozen, and no such producer exists today.

## Verification

```powershell
dotnet test tests\NekoLib.Pipes.Tests\Unit\NekoLib.Pipes.Tests.Unit.csproj -f net481 -m:1
dotnet test tests\NekoLib.Pipes.Tests\Unit\NekoLib.Pipes.Tests.Unit.csproj -f net9.0-windows -m:1
.\eng\verify-public-api.ps1 -PackageId NekoLib.Pipes
```

The focused suite opens real in-process named pipes, so it is both deterministic
contract coverage and IPC integration coverage. It does not establish
cross-user denial, hostile same-user security, cross-target separate-process
compatibility, non-Windows behavior, long-duration resource stability, or
throughput. The
[long-running and recovery scenario](../../../runtime_tests/Pipes/LongRunningRecovery/README.md)
adds real separate-process coverage, one target per run.

[`VALIDATION_REQUIREMENTS.md`](VALIDATION_REQUIREMENTS.md) owns the evidence
contract for this boundary and [`VALIDATIONS.md`](VALIDATIONS.md) records what
actually ran, with its gaps. Confirmed defects live in [`ISSUES.md`](ISSUES.md)
and unverified observations in [`FINDINGS.md`](FINDINGS.md); neither is
scheduled work until it is promoted to [`TODO.md`](../../../TODO.md).
