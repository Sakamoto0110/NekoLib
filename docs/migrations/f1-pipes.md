# F1-PIPE Migration — Pipes

**Kind:** guide

**Lifecycle:** current

**Subject:** migration from the initial Pipes candidate surface to the accepted
F1-PIPE ownership, lifecycle, event, metrics, error, and target contracts

**Reference date:** 2026-08-18

The complete contracts are owned by the
[Pipes reference](../../src/Pipes/NekoLib.Pipes/README.md).

Two public candidate contracts were removed or closed before the first stable
baseline. The remaining surface changes are additive or behavioral corrections.

## Breaking: `PipeClient` is no longer disposable

`PipeClient` creates and closes a stream inside every `SendAsync`. The instance
owns no persistent resource, so its no-op `Dispose`, `IDisposable`, and modern
`IAsyncDisposable` surface was removed.

```csharp
// before
using (var client = new PipeClient(options))
{
    var response = await client.SendAsync("ping");
}

// after
var client = new PipeClient(options);
var response = await client.SendAsync("ping");
```

Remove `using` or `await using`. Reuse the client freely; concurrent sends own
independent streams.

## Breaking: `SimplePipeMetrics` is sealed

The concrete collector had no virtual member or protected extension seam.
Subclassing could not replace its algorithms coherently and is no longer a
supported contract.

```csharp
// before
public sealed class MyMetrics : SimplePipeMetrics { }

// after
public sealed class MyMetrics : IPipeMetrics
{
    // implement the observer and Snapshot contract
}
```

Composition over a `SimplePipeMetrics` instance is also valid. All standard
snapshot constructors remain public for custom implementations.

## Additive: definitive cross-target shutdown

`PipeServer`, `PipeEventHub`, and `PipeEventClient` now expose
`Task ShutdownAsync()` on both targets. `PipeServer` also gains
`IAsyncDisposable`/`DisposeAsync` on `net9.0`; the other modern endpoints keep
their async-disposal surface but now await real completion.

Use `ShutdownAsync` when application shutdown, endpoint rebinding, or test
isolation requires every admitted operation to finish. Synchronous `Dispose`
still waits for at most two seconds and may return while a handler that ignores
cancellation is running.

Start is now one-shot and terminal. Starting after shutdown, mapping a handler
after server shutdown, or publishing after hub shutdown throws
`ObjectDisposedException` instead of silently restarting or dropping work.

## Behavioral: options are captured and validated at construction

Mutating a `PipeClientOptions` or `PipeServerOptions` object after construction
no longer changes an existing endpoint. Construct a new endpoint to apply new
settings.

Blank names, non-positive capacities/sizes, non-positive timeouts, timeouts
above `Int32.MaxValue` milliseconds, and invalid enum values now fail at
construction. `PipeEventClient` validates its timeout and reconnect-delay
setters. Only `AutoReconnect` is intentionally live.

## Behavioral: metrics cannot control transport outcomes

Every framework-owned `IPipeMetrics` callback is now best effort and exception
isolated. A metrics sink that threw to abort a request, publication, or
connection will no longer do so. Observation is not a transport control plane.

`Snapshot()` remains a direct consumer call. `SimplePipeMetrics` remains
cumulative and has no reset; use a new instance for a new measurement window.

## Behavioral: oversized events fail at the publisher

Events keep their fixed 1 MiB maximum. Previously an oversized event entered
every subscriber queue, then each writer failed and disconnected a healthy
subscriber after `PublishAsync` had already succeeded.

The event is now serialized and checked before enqueue. `PublishAsync` throws an
`InvalidOperationException`, subscribers remain connected, later normal events
are delivered, and `Published` is not incremented.

## Additive and behavioral: event failures are observable

`PipeEventClient` adds:

```csharp
client.OnError += error => log.Error(error);
```

Connection, framing, parsing, and listen failures raise `OnError`. A failed
connection attempt no longer raises `OnDisconnected`; move failed-attempt
accounting to `OnError`. Established connections retain this serialized order:

```text
OnConnected -> zero or more OnEvent -> optional OnError -> OnDisconnected
```

Each connected, disconnected, error, and event subscriber is isolated
individually. Callbacks still run on the background listen loop, not a UI thread.

## Additive: framework error constants

Replace hard-coded framework strings mechanically:

| String | Constant |
|---|---|
| `"not_found"` | `PipeErrorCodes.NotFound` |
| `"exception"` | `PipeErrorCodes.Exception` |
| `"response_too_large"` | `PipeErrorCodes.ResponseTooLarge` |
| `"connection_closed"` | `PipeErrorCodes.ConnectionClosed` |

Application-defined `PipeError.Code` values remain supported; the constants are
not a closed enum and do not change JSON.

## Behavioral: `net481` connect observes cancellation

A caller token cancelled after blocking `NamedPipeClientStream.Connect` began
used to wait until `ConnectTimeout` on `net481`. The await now returns promptly
with `OperationCanceledException`, observes the abandoned worker, and closes the
owned stream to unblock it. `ConnectTimeout` remains an independent bound.

## Unchanged target and security contracts

`PipeMessage.Data` remains `JToken?` with Newtonsoft.Json 13.0.3 on `net481` and
`JsonElement?` with no direct package dependency on `net9.0`. Multi-target code
still needs target-specific DOM handling. Cross-target separate-process wire
validation remains a gate before the first stable release.

`PlatformDefault` remains the compatibility default and `CurrentUserOnly`
remains opt-in. Neither is authentication against a hostile same-user process;
applications still own operation authorization and any stronger session
protocol.

RPC response-versus-exception behavior, per-subscriber bounded FIFO queues,
`DropNewest`, `DisconnectSubscriber`, handler error sanitization, application
error codes, target frameworks, and the no-project-reference graph are
unchanged.
