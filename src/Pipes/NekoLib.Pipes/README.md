# NekoLib.Pipes

**Kind:** reference

**Lifecycle:** current

**Subject:** named-pipe RPC and events, ownership, lifecycle, framing, errors,
metrics, access policy, and target-specific payload contracts

**Reference date:** 2026-08-18

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
cross-target wire compatibility; a separate-process `net481`/`net9.0` matrix is
required before the first stable family release.

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

`PipeEventClient` captures its base pipe name at construction. Its
`ConnectTimeout` must be positive, and `ReconnectDelay` must be non-negative;
both also have the `Int32.MaxValue`-millisecond ceiling. Their values are
captured separately for each connection attempt or reconnect wait.
`AutoReconnect` is the one intentional live switch and may be changed while the
background loop is running.

The RPC server name is the configured base name. The event endpoint always uses
the same captured base plus `.events`.

## Ownership and lifecycle

| Type | Ownership contract |
|---|---|
| `PipeClient` | Stateless between calls. Each `SendAsync` creates, owns, and closes its stream. The client has no disposal contract. Concurrent calls are independent. |
| `PipeServer` | Owns its cancellation source, accept loop, admitted RPC operations, streams, limiter, and optional `Events` hub. |
| `PipeEventHub` | Owns its cancellation source, accept loop, subscriber operations, streams, per-subscriber queues, and writers. |
| `PipeEventClient` | Owns one reconnect/listen loop, its cancellation source, and the current stream. |

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
for all work already admitted by that endpoint. A mapped handler that initiates
its own server's shutdown must not await the returned task from inside that same
handler: the task includes that handler. Initiate it, return, and await it from
the owner.

The same self-wait rule applies to `PipeEventClient` callbacks. A callback may
initiate `ShutdownAsync`, but must not await that task or `DisposeAsync` before
returning because completion includes the callback loop. Synchronous `Dispose`
detects this case, initiates shutdown, and returns immediately.

`Dispose()` initiates the same shutdown but waits synchronously for at most two
seconds; uncooperative user work may therefore outlive the call. Use
`ShutdownAsync()` whenever definitive completion matters. On `net9.0`, all
three stateful types also implement `IAsyncDisposable`, and `DisposeAsync()`
awaits the real shutdown. No async-disposal dependency is added to `net481`.

`PipeEventClient` remains started after an `AutoReconnect=false` loop ends; it
cannot be restarted. Call `ShutdownAsync` or dispose it to enter the terminal
state and release its cancellation source.

## RPC, cancellation, and errors

`PipeClient.SendAsync` validates a nonblank operation name, opens one duplex
connection, sends one request, reads one response, validates its ID and `res`
type, and closes the stream.

`ConnectTimeout` bounds only connection establishment. `RequestTimeout` starts
after connection and bounds request write plus response read. The caller token
applies to both phases. Timeout or caller cancellation surfaces as
`OperationCanceledException`; the library does not convert it to a
`PipeMessage`. On `net481`, the blocking connect and framing workers are raced
against cancellation, observed if abandoned, and unblocked when the owned
stream closes.

The server's `ClientIdleTimeout` measures inactivity while waiting for the next
request. It is paused during handler execution and response writing, so it does
not impose a handler deadline. Server shutdown cancels the token passed to a
mapped handler; application handlers are responsible for observing it.

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

## Framing and limits

Every frame is a four-byte length prefix followed by one UTF-8 JSON document.
RPC requests and responses default to a 1 MiB maximum and use the captured
`MaxMessageBytes` on each side. A writer validates the serialized size before it
emits any bytes. A declared length less than one or greater than the receiving
limit is invalid.

EOF before any byte of a new length prefix is a clean close. EOF after a partial
prefix or payload is a truncated frame and throws `EndOfStreamException`.
Malformed JSON propagates its serializer parse exception.

Events have a fixed 1 MiB frame maximum in this release. `PublishAsync`
serializes and preflights the event before inspecting subscriber queues. An
oversized event throws an `InvalidOperationException`, changes no queue, does not
disconnect subscribers, and does not increment the publication metric. There is
no chunking, persistence, or retry protocol.

## Events, backpressure, and callback order

Each subscriber has one bounded FIFO queue and one serialized writer. One slow
subscriber never blocks another subscriber or the publisher.

`EventSubscriberQueueCapacity` controls the per-subscriber bound:

- `DropNewest` marks that subscriber's delivery failed and keeps it connected;
- `DisconnectSubscriber` marks queued deliveries failed, closes the slow
  subscriber, and leaves other subscribers alone.

`PublishAsync` confirms the enqueue attempt only; it does not wait for pipe I/O
or promise delivery. A cancelled publish token marks each current subscriber's
attempt failed instead of throwing. With no subscribers, publication succeeds
and records one publication with zero deliveries.

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

`SimplePipeMetrics.Snapshot()` is cumulative and has no reset operation:

- server requests and response attempts, success/failure, connected RPC and
  event clients, and response latency;
- client connection attempts, request outcomes, and total latency;
- event publications plus successful and failed subscriber deliveries;
- total locally observed transport/handler errors.

`Published` advances when all current subscriber delivery trackers complete,
which may be after `PublishAsync` returns. Rejected oversized events do not
advance it. Snapshots are new DTOs over current counters; a fresh metrics
instance is the supported reset boundary. `NoopPipeMetrics` records nothing and
returns null through `IPipeMetrics.Snapshot()`.

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
  created it. On `net481` this is an explicit Windows pipe ACL; on `net9.0` it is
  `PipeOptions.CurrentUserOnly`.

Neither option protects against a hostile process already running as the same
user. Pipe names are identifiers, not credentials. The application owns pipe
name selection, operation authorization, payload sensitivity, idempotence, and
any stronger peer/session protocol. NekoLib.Pipes supplies no peer identity,
credential, challenge, replay, remote transport, or privileged-control layer.

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
throughput.
