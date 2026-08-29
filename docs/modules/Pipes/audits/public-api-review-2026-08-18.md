# Pipes Public API Review — 2026-08-18

**Document ID:** PIPE-AUDIT-PUBLIC-API-20260818

**Schema version:** 1

**Kind:** audit

**Lifecycle:** historical

**Subject:** F1-PIPE compiled public surface, request/response and event contracts, ownership, lifecycle, concurrency, framing, metrics, errors, security policy, target parity, and package boundary

**Surface:** audit

**Boundary:** pipes

**Authority role:** evidence

**Mutation:** snapshot

**Indexing:** include

**Status:** all eight dispositions accepted and implemented

**Reference date:** 2026-08-18

**Reference commit:** `2db588ac6fef271851e70c04f7b8b82cd8e004a7`

**Original path:** docs/audit/pipes-public-api-review-2026-08-18.md

**Last reconciliation:** 2026-08-18

**Current state:** the [Pipes technical reference](../REFERENCE.md) owns the implemented contract; [`TODO.md`](../../../../TODO.md) records F1-PIPE as complete and retains the family release gates

## Baseline and authority

This review covers committed `HEAD` on branch
`phase-e/sqlserver-and-orchestration`. At entry, `HEAD` was exactly
`2db588ac6fef271851e70c04f7b8b82cd8e004a7`, the worktree and index were clean,
and the branch was 37 commits ahead of
`origin/phase-e/sqlserver-and-orchestration`. The recent history was:

```text
2db588a docs(f1): close five-module package gate
63785cc fix(diagnostics): serialize install with disposal
a5e4ddc test(diagnostics): use Assert.Fail in the option-capture regression
63bb269 feat(devices): finalize F1 public API
00b0f11 feat(mvvm): finalize F1 public API
ea7c476 feat(http): finalize F1 public API
291f501 feat(diagnostics-windows): finalize F1 public API
efd0a88 feat(diagnostics): finalize F1 public API
```

The review followed [the NekoLib routing
skill](../../../../.agents/skills/nekolib/SKILL.md) and the
[repository-hygiene workflow](../../../../.agents/skills/nekolib-repository-hygiene/SKILL.md).
Pipes has no specialized module skill, so the current source, project, tests,
compiled manifests, public API policy, and current repository documentation are
the operative authorities.

Authority order used here:

1. all tracked source under [`src/Pipes/NekoLib.Pipes/`](../../../../src/Pipes/NekoLib.Pipes/);
2. [`NekoLib.Pipes.csproj`](../../../../src/Pipes/NekoLib.Pipes/NekoLib.Pipes.csproj)
   and evaluated MSBuild properties/items;
3. all tests under
   [`tests/NekoLib.Pipes.Tests/Unit/`](../../../../tests/NekoLib.Pipes.Tests/Unit/);
4. the two assembly-derived manifests under
   [`eng/public-api/NekoLib.Pipes/`](../../../../eng/public-api/NekoLib.Pipes/);
5. [`README.md`](../../../../README.md),
   [`tests/README.md`](../../../../tests/README.md), and the
   [public API and release policy](../../../public-api-release-policy.md);
6. [`TODO.md`](../../../../TODO.md) F1-PIPE;
7. the historical
   [`initial-audit.md`](initial-audit.md) and
   [`ipc-hardening-review-2026-08-08.md`](ipc-hardening-review-2026-08-08.md)
   only as leads rechecked against current code.

The direct Watchdog consumer was read only where necessary to confirm how the
public payload and metrics contracts are actually consumed. Watchdog behavior
and API are outside this review.

This artifact changes no product source, test, project, manifest, accepted API
baseline, package, changelog, migration guide, or roadmap item.

## Scope

Included:

- every public type and member emitted by both supported target assemblies;
- RPC client/server construction, handler registration, dispatch, response
  correlation, errors, cancellation, timeouts, and per-call connection ownership;
- event hub/client admission, publication, ordering, subscriber isolation,
  bounded queues, overflow, reconnect, callbacks, and failure observation;
- ownership of options, cancellation sources, tasks, streams, handlers, event
  subscribers, and metrics sinks;
- concurrent connect, map, send, receive, publish, disconnect, start, shutdown,
  and disposal paths;
- frame format, byte limits, incomplete frames, malformed JSON, clean EOF, and
  oversized requests, responses, and events;
- metric extension and snapshot semantics;
- operating-system access policy, peer identity, and application-owned
  authorization;
- `net481` versus `net9.0` implementation and compiled-surface differences;
- dependency and expected future-package boundaries.

Excluded:

- implementing any recommendation;
- changing tests, target frameworks, dependencies, or API manifests;
- updating an accepted baseline;
- adding Core, Inspection, authentication, replay protection, remote IPC, or a
  service-bus abstraction;
- reviewing or changing Watchdog, Navigation, or any completed F1 module;
- running a runtime scenario, the full solution, packaging, publishing, or a
  PackageReference consumer probe.

## Project and future-package boundary

The evaluated project targets `net481;net9.0`, has `Nullable=enable`,
`ImplicitUsings=enable`, `LangVersion=latest`, and is packable as
`NekoLib.Pipes`
([`NekoLib.Pipes.csproj:3`](../../../../src/Pipes/NekoLib.Pipes/NekoLib.Pipes.csproj#L3)).
It declares `NET481` for `net481` and `NET9` for `net9.0`; it does not declare
`NEKOLIB`. It has no project reference.

The dependency graph differs by target:

| Package asset | Compiled constant | Direct package dependency | Platform qualifier |
|---|---|---|---|
| `lib/net481/NekoLib.Pipes.dll` | `NET481` | `Newtonsoft.Json 13.0.3` | .NET Framework/Windows |
| `lib/net9.0/NekoLib.Pipes.dll` | `NET9` | none | unqualified `net9.0` |

`Newtonsoft.Json` is a public, not merely implementation, dependency on
`net481`: `PipeMessage.Data` exposes `JToken?`
([`PipeMessage.cs:19`](../../../../src/Pipes/NekoLib.Pipes/PipeMessage.cs#L19)). The
modern assembly exposes `JsonElement?` instead. A future normal package is
therefore expected to contain the two library assets above, the repository root
README and license supplied by `Directory.Build.targets`, symbols in the normal
symbol package, and the one `net481` dependency group. This review produced no
package and makes no package-content claim beyond evaluated build inputs.

The unqualified modern asset is loadable outside Windows and .NET implements
named pipes through Unix-domain sockets on Unix. All verification in this
review ran on Windows, so non-Windows behavior remains a validation gap rather
than confirmed portability evidence.

## Compiled public-surface inventory by target

The two checked-in manifests contain the same 18 public type declarations.
They contain 119 public member declarations on `net481` and 122 on `net9.0`.
The three additional modern members are `DisposeAsync()` on `PipeClient`,
`PipeEventClient`, and `PipeEventHub`; those three types also implement
`IAsyncDisposable` only on the modern target. `PipeServer` implements only
`IDisposable` on both targets.

The other compiled difference is the type of `PipeMessage.Data`:
`Newtonsoft.Json.Linq.JToken?` on `net481` and
`System.Text.Json.JsonElement?` on `net9.0`. Apart from target-framework
metadata, those are the only target-specific public differences confirmed by
the manifests
([`net481.approved.txt`](../../../../eng/public-api/NekoLib.Pipes/net481.approved.txt),
[`net9.0.approved.txt`](../../../../eng/public-api/NekoLib.Pipes/net9.0.approved.txt)).

The classifications below are proposals for the decision gate, not accepted
stable declarations.

| Public type | Public members by target | Proposed classification |
|---|---:|---|
| `IPipeMetrics` | 10 / 10 | **Deliberate public extension**; keep all callbacks and nullable `Snapshot()` stable |
| `NoopPipeMetrics` | 11 / 11 | **Stable candidate**; keep `Instance`, constructor, and interface implementation |
| `PipeAccessPolicy` | 2 / 2 enum values | **Stable candidate**; security-policy selection, not authorization |
| `PipeClient` | 3 / 4 | `SendAsync` and constructor **stable candidates**; `IDisposable`, `Dispose`, and modern `IAsyncDisposable`/`DisposeAsync` are **candidates for removal** because they own nothing |
| `PipeClientOptions` | 6 / 6 | **Stable candidate**; values should be captured and validated by the client |
| `PipeError` | 3 / 3 | **Stable candidate** wire/application error DTO |
| `PipeEventClient` | 9 / 10 | **Stable candidate** plus one proposed additive error notification; modern async-disposal behavior needs correction |
| `PipeEventHub` | 7 / 8 | **Stable candidate**; standalone server-side event extension and publication surface |
| `PipeEventQueueOverflowPolicy` | 2 / 2 enum values | **Stable candidate**; explicit best-effort backpressure policy |
| `PipeMessage` | 7 / 7 | **Stable candidate with a deliberate target-specific `Data` member**, pending explicit acceptance |
| `PipeMetricsSnapshot` and four nested metric types | 31 / 31 | **Stable candidate and deliberate public extension support**; public constructors let custom metrics implementations return snapshots |
| `PipeServer` | 6 / 6 | **Stable candidate and deliberate handler-extension host**; `Map` is the public application extension point |
| `PipeServerOptions` | 11 / 11 | **Stable candidate**; values should be captured and validated by the server |
| `SimplePipeMetrics` | 11 / 11 | Concrete **stable candidate**; public inheritance is a **candidate for removal** by sealing the class |

No current type or member is proposed as experimental. No current member is
proposed for stable deprecation with a migration window. The two removal-class
proposals concern pre-stable candidate surface: the no-op `PipeClient` disposal
contract and accidental `SimplePipeMetrics` inheritance. If this family were
already stable, both would require the policy's normal deprecation/major-release
path.

## Observed contract boundaries

### Ownership and lifetime

- `PipeClient` owns one `NamedPipeClientStream` per `SendAsync`; it closes that
  stream in the call's `finally`. The instance itself retains only the caller's
  mutable options object and one metrics reference
  ([`PipeClient.cs:15`](../../../../src/Pipes/NekoLib.Pipes/PipeClient.cs#L15),
  [`PipeClient.cs:76`](../../../../src/Pipes/NekoLib.Pipes/PipeClient.cs#L76)).
- `PipeServer` owns its cancellation source, accept task, semaphore, admitted
  operations and streams, optional event hub, and the handler dictionary. A
  handler delegate remains application-owned; the server may invoke it
  concurrently for different connections.
- `PipeServer.Events` is server-owned. It is null before a successful event-hub
  start and whenever events are disabled. Disposing the server disposes the
  hub; consumers use the property to publish and must not treat it as an
  independently owned service.
- A standalone `PipeEventHub` owns its subscriber streams, one bounded FIFO per
  subscriber, one writer loop per subscriber, the accept task, and its
  cancellation source.
- `PipeEventClient` owns one reconnect/listen loop, its cancellation source,
  and the current stream. Callbacks are consumer-owned and invoked on the
  background listen-loop thread.
- Metrics objects remain consumer-owned. Pipes calls them synchronously; it
  never disposes them.

### Threading, ordering, and backpressure

- `PipeClient.SendAsync` is structurally concurrent because each request uses a
  distinct stream. Its live options alias prevents the whole operation from
  having a stable configuration snapshot today.
- `PipeServer.Map` and request lookup use a `ConcurrentDictionary`; mapping and
  dispatch may run concurrently. Re-mapping one name is last-writer-wins, and a
  request sees whichever handler the dictionary returns at lookup time
  ([`PipeServer.cs:17`](../../../../src/Pipes/NekoLib.Pipes/PipeServer.cs#L17)).
- One connection is processed sequentially. Different connections and handlers
  execute concurrently up to `MaxClients`.
- Each event subscriber has one FIFO and one writer. Sequential completed
  `PublishAsync` calls preserve enqueue order for that subscriber. Concurrent
  publishers are serialized by each queue lock, but their relative order is
  lock-acquisition order and is intentionally unspecified.
- `PublishAsync` is enqueue-only and never waits for subscriber I/O
  ([`PipeEventHub.cs:363`](../../../../src/Pipes/NekoLib.Pipes/PipeEventHub.cs#L363)).
  `DropNewest` keeps a slow subscriber and counts that attempted delivery as
  failed; `DisconnectSubscriber` removes it and fails its queued deliveries.
- `PipeEventClient` invokes `OnEvent` subscribers serially in registration
  order. A throwing callback is isolated; a slow callback delays later
  callbacks and later frame reads. That backpressure is preferable to
  unbounded callback tasks and should be documented, not silently parallelized.

### Framing, EOF, and protocol

- A frame is a native `BitConverter` four-byte signed length followed by UTF-8
  JSON. On the supported Windows targets this is little-endian. Byte-mode pipe
  writes are read with exact-length loops.
- A non-positive or over-limit incoming length is rejected before payload
  allocation. The RPC default is 1 MiB and is configurable independently on
  client and server
  ([`PipeFraming.cs:161`](../../../../src/Pipes/NekoLib.Pipes/PipeFraming.cs#L161)).
- Clean EOF before any header byte makes `TryReadAsync` return null. EOF after a
  partial header or body throws `EndOfStreamException`; it is not collapsed into
  clean closure
  ([`PipeFraming.cs:195`](../../../../src/Pipes/NekoLib.Pipes/PipeFraming.cs#L195)).
- Invalid JSON throws the target serializer's parse exception. The server closes
  a client on any read/framing failure because it may not have a trustworthy
  correlation ID.
- The client validates response ID and `Type == "res"`. A clean server close
  before a response becomes a correlated `connection_closed` failure response;
  malformed, truncated, wrong-ID, and wrong-type responses still throw.
- Handlers receive the public mutable request object. The server overwrites
  `Id`, `Type`, and `Name` on the handler's returned response object before
  writing it
  ([`PipeServer.cs:307`](../../../../src/Pipes/NekoLib.Pipes/PipeServer.cs#L307)).

### Metrics and snapshots

`IPipeMetrics` is the deliberate custom-observer seam. `SimplePipeMetrics`
maintains cumulative counts from construction; it has no reset operation.
`Snapshot()` is non-destructive. Server and client latency tuples are internally
consistent under their respective locks, while the complete snapshot is a
weakly consistent read across independent counters, not one global atomic
instant
([`SimplePipeMetrics.cs:182`](../../../../src/Pipes/NekoLib.Pipes/SimplePipeMetrics.cs#L182)).

`Events.Published` counts completed publication trackers, not subscriber
deliveries. `Delivered` and `Failed` count per-subscriber outcomes. A
publication with no subscribers increments `Published` with zero delivery
counts. A cancelled enqueue attempt counts as a failed subscriber delivery and
does not throw cancellation to the publisher. `NoopPipeMetrics.Snapshot()`
returns null through the interface.

### Security and application policy

`PlatformDefault` preserves the original OS-default descriptor and is explicitly
not an authorization boundary. On Windows, Microsoft's named-pipe documentation
states that the default descriptor grants full control to LocalSystem,
administrators, and the creator owner and grants read access to Everyone and
anonymous. `CurrentUserOnly` is opt-in. On modern .NET the runtime option checks
that client and server have the same user and, on Windows, the same elevation
level; on `net481` Pipes installs a protected ACL granting the current user SID
full control
([`PipeServerStreamFactory.cs:20`](../../../../src/Pipes/NekoLib.Pipes/PipeServerStreamFactory.cs#L20),
[`PipeServerStreamFactory.cs:78`](../../../../src/Pipes/NekoLib.Pipes/PipeServerStreamFactory.cs#L78)).

Platform references:

- [Microsoft: `PipeOptions.CurrentUserOnly`](https://learn.microsoft.com/en-us/dotnet/api/system.io.pipes.pipeoptions?view=net-9.0)
- [Microsoft: named-pipe security and default access rights](https://learn.microsoft.com/en-us/windows/win32/ipc/named-pipe-security-and-access-rights)

Neither policy authenticates a process, proves server identity, prevents name
squatting by a process with sufficient access, protects against a hostile
process already running as the same user, or authorizes an operation. The
application owns pipe-name selection, handler authorization, payload
sensitivity, idempotence, and any stronger identity/session protocol. Pipes
should not grow generic authentication, replay, credential, or privileged
TestControl infrastructure without a separate threat-model decision.

## Confirmed findings

### PIPE-01 — High reliability — constructors retain live mutable configuration and invalid values fail late or disable bounds

**Observed fact.** `PipeClient` and `PipeServer` retain the caller's options
object directly
([`PipeClient.cs:18`](../../../../src/Pipes/NekoLib.Pipes/PipeClient.cs#L18),
[`PipeServer.cs:36`](../../../../src/Pipes/NekoLib.Pipes/PipeServer.cs#L36)). One
operation reads fields at different times: a concurrent mutation can make the
stream connect to one name while metrics record another, or apply different
frame limits to its write and read. Server mutation can split the RPC and event
pipe names, change access policy between accepted instances, or make
`AccessPolicy` report a value different from the policy that created an existing
stream.

Validation is incomplete and inconsistent. `MaxClients=0` and
`MaxEventSubscribers=0` create servers that admit nothing; blank names fail only
inside stream construction; non-positive message limits make every frame fail;
invalid `ClientIdleTimeout` values are swallowed by the `CancelAfter` guard and
silently remove the intended idle bound
([`PipeServer.cs:185`](../../../../src/Pipes/NekoLib.Pipes/PipeServer.cs#L185)). Event
client timeout and delay properties are read by the background loop without
validated setters
([`PipeEventClient.cs:28`](../../../../src/Pipes/NekoLib.Pipes/PipeEventClient.cs#L28)).

**Risk to consumers.** Configuration has no stable ownership point, failures
occur far from construction, and concurrent applications can observe mixed
endpoint, limit, timeout, metrics-label, and security behavior.

**Recommended disposition.** Keep both options types public, but capture every
value at client/server construction and validate one coherent snapshot. Require
nonblank pipe names; positive client/subscriber capacities and message sizes;
finite positive connect, request, and idle timeouts; non-negative reconnect
delay; and declared enum values. Keep `PipeEventClient.AutoReconnect` as an
intentional live loop switch, but validate timeout/delay setters and snapshot
them per attempt.

**Compatibility and migration.** Signatures stay unchanged, but invalid values
fail earlier and later mutations of a supplied options instance stop affecting
an existing object. A consumer that wants different settings constructs a new
client/server rather than mutating shared options after construction.

**Rejected alternatives.** Documenting the live alias leaves races intact.
Changing all setters to init-only or introducing a common options abstraction is
a larger source break and is unnecessary. Adding Core for validation would
violate the module's independent package boundary.

**Validation if accepted.** Dual-target constructor validation, options-mutation,
concurrent-send, event-name consistency, and access-policy capture tests; API
manifests should remain unchanged.

### PIPE-02 — High lifecycle — shutdown completion is real internally but not a coherent public contract

**Observed fact.** `PipeServer` and `PipeEventHub` track admitted work and an
internal completion task, cancel and close transports, then let `Dispose()` wait
at most two seconds
([`PipeServer.cs:320`](../../../../src/Pipes/NekoLib.Pipes/PipeServer.cs#L320),
[`PipeEventHub.cs:480`](../../../../src/Pipes/NekoLib.Pipes/PipeEventHub.cs#L480)). A
handler that ignores cancellation may therefore outlive `Dispose`, as the
current tests deliberately confirm
([`PipeShutdownTests.cs:42`](../../../../tests/NekoLib.Pipes.Tests/Unit/PipeShutdownTests.cs#L42)).
The completion task is not public. `PipeServer` has no async-disposal member on
either target; `PipeEventHub.DisposeAsync` exists only on `net9.0`.

`PipeEventClient.DisposeAsync` calls synchronous `Dispose()` and immediately
returns a completed `ValueTask`, so it is not an asynchronous drain
([`PipeEventClient.cs:138`](../../../../src/Pipes/NekoLib.Pipes/PipeEventClient.cs#L138)).
It has no terminal disposed state: disposal before start does nothing, disposal
after start resets `_running`, and a later `Start()` can create another loop.
If `AutoReconnect=false` ends the loop naturally, `_running` remains true and a
new `Start()` still throws. All three `Start()` methods use check-then-set state,
so concurrent starts or start/dispose races are not atomic.

`PipeClient.Dispose()` is explicitly a no-op reserved for a hypothetical future
persistent model
([`PipeClient.cs:163`](../../../../src/Pipes/NekoLib.Pipes/PipeClient.cs#L163)). The
instance owns no long-lived resource, so its `IDisposable` and modern
`IAsyncDisposable` contracts are accidental surface today.

**Risk to consumers.** An application cannot await definitive shutdown on every
target, cannot know when an uncooperative handler released resources, may block
for two seconds when disposing from a callback running on the owned task, and
cannot rely on disposal being terminal. The no-op client disposal commits a
resource-ownership promise that the implementation does not have.

**Recommended disposition.** Add one target-neutral `Task ShutdownAsync()`
contract to each stateful type: `PipeServer`, `PipeEventHub`, and
`PipeEventClient`. It must atomically enter terminal shutdown, cancel, close,
and await all owned work; `Dispose()` initiates the same shutdown and retains a
bounded synchronous wait. Modern `DisposeAsync()` should delegate to the real
completion, and `PipeServer` should gain it on the modern target. Make `Start`
one-shot and race-safe; after terminal shutdown, `Start`, `Map`, and publish
operations should throw the appropriate disposed/lifecycle exception instead
of silently restarting or dropping work.

Remove `IDisposable`, `Dispose`, and the modern `IAsyncDisposable`/
`DisposeAsync` from stateless `PipeClient` before the first stable baseline.

**Compatibility and migration.** `ShutdownAsync` and modern server async
disposal are additive. Terminal lifecycle checks are behavioral corrections.
Removing client disposal interfaces/members is a candidate source and binary
break: remove `using`/`await using` around `PipeClient`; each `SendAsync` already
owns and closes its stream. No stable deprecation window is required before the
first stable baseline, but the break must be in the F1 migration record.

**Rejected alternatives.** Exposing only the internal completion property would
make consumers call `Dispose` and then inspect a disposed object. Blocking
`Dispose` indefinitely is unsafe for UI/process shutdown. Adding
`Microsoft.Bcl.AsyncInterfaces` solely to make net481 implement
`IAsyncDisposable` expands the package graph without improving the universal
contract. Keeping no-op client disposal for a speculative persistent model
stabilizes an implementation that does not exist.

**Validation if accepted.** Dual-target concurrent start/shutdown tests,
shutdown from handler/callback tests, cooperative and uncooperative drain tests,
post-shutdown operation tests, endpoint rebind tests, and reviewed API diffs.

### PIPE-03 — High reliability — a metrics observer can change transport outcomes

**Observed fact.** Metrics callbacks run synchronously on connect, request,
response, accept, disconnect, event-delivery, and error paths without a safety
boundary. For example, a throwing `OnClientRequest` aborts a connected request;
a throwing `OnServerRequestReceived` closes the connection before dispatch;
and a throwing event metric can fault a writer or a zero-subscriber publish
([`PipeClient.cs:47`](../../../../src/Pipes/NekoLib.Pipes/PipeClient.cs#L47),
[`PipeServer.cs:211`](../../../../src/Pipes/NekoLib.Pipes/PipeServer.cs#L211),
[`PipeEventHub.cs:89`](../../../../src/Pipes/NekoLib.Pipes/PipeEventHub.cs#L89)). A
throwing `OnError` may replace the original exception.

**Risk to consumers.** Optional observation can make an otherwise valid RPC
fail, disconnect a client or subscriber, fault detached background work, and
hide the actual transport failure.

**Recommended disposition.** Preserve `IPipeMetrics` as the deliberate public
extension, but invoke every framework-owned callback through one internal
best-effort boundary that isolates observer exceptions. Do not recursively call
the same sink to report its own failure. `Snapshot()` remains a direct
consumer-owned call and is not part of the transport path.

**Compatibility and migration.** No compiled-surface change. A metrics
implementation that intentionally throws to abort transport work would stop
doing so; that behavior is contrary to an optional metrics contract and should
not be supported.

**Rejected alternatives.** Propagation makes observability part of correctness.
Adding a second logging dependency to report metrics failures creates a cycle
of failure surfaces and a new project dependency.

**Validation if accepted.** A throwing metrics fake at connect, request,
response, handler-error, event publish, accept, and disconnect boundaries on
both targets, proving the intended transport outcome still occurs.

### PIPE-04 — Medium reliability — an oversized event disconnects healthy subscribers

**Observed fact.** RPC frame limits are configurable and oversized responses
become a structured error. Events always use the framing default of 1 MiB:
neither `PipeEventHub` nor `PipeEventClient` exposes a limit. `PublishAsync`
serializes and enqueues without preflighting framed size. Each writer later
throws `PipeFrameTooLargeException`; the generic writer catch counts failure,
removes the subscriber, and disposes its stream
([`PipeEventHub.cs:367`](../../../../src/Pipes/NekoLib.Pipes/PipeEventHub.cs#L367),
[`PipeEventHub.cs:446`](../../../../src/Pipes/NekoLib.Pipes/PipeEventHub.cs#L446)).

**Risk to consumers.** One producer mistake disconnects every current
subscriber even though none caused the failure. `PublishAsync` has already
returned successfully, so the publisher sees neither rejection nor delivery
failure.

**Recommended disposition.** Keep the event frame cap fixed at 1 MiB for this
release; preflight the serialized event before enqueue, fail the publishing call
deterministically, leave subscribers connected, and document that rejected
publishes do not increment `Published`. Do not add a configurable event limit
until a real payload requires one.

**Compatibility and migration.** Normal events are unchanged. Oversized events
change from silent asynchronous subscriber loss to a caller-visible failure.
Consumers must keep event payloads under the documented bound.

**Rejected alternatives.** Disconnecting subscribers blames the wrong party.
Chunking, lossless persistence, retry, and a new event-limit surface add protocol
and policy without a demonstrated requirement.

**Validation if accepted.** Both targets must prove that an oversized event is
rejected, no subscriber is removed, a later normal event is delivered, and
metrics remain coherent.

### PIPE-05 — Medium diagnostics — event connection notifications only partially isolate subscribers and suppress failure evidence

**Observed fact.** `OnEvent` walks the invocation list and isolates each
callback. `OnConnected` and `OnDisconnected` instead invoke the whole multicast
delegate inside one try, so the first throwing callback prevents later
callbacks from running
([`PipeEventClient.cs:119`](../../../../src/Pipes/NekoLib.Pipes/PipeEventClient.cs#L119),
[`PipeEventClient.cs:133`](../../../../src/Pipes/NekoLib.Pipes/PipeEventClient.cs#L133)).
`OnDisconnected` is raised from `finally` even when the connect attempt never
succeeded. Connect, read, framing, and parse failures are swallowed by the loop,
and the public client has no error event or failure state
([`PipeEventClient.cs:53`](../../../../src/Pipes/NekoLib.Pipes/PipeEventClient.cs#L53)).

**Risk to consumers.** Later state subscribers can miss notifications; a failed
connect is indistinguishable from a completed connection; and applications
cannot tell clean remote EOF from malformed input or local transport failure.
Troubleshooting relies on inference from reconnect timing.

**Recommended disposition.** Keep all three existing events stable. Isolate
every connected/disconnected subscriber individually, raise `OnDisconnected`
only after a connection was established, and add a minimal
`event Action<Exception> OnError` for connect/listen/framing failures. Define
callback order as `OnConnected`, zero or more ordered `OnEvent`, optional
`OnError`, then `OnDisconnected`; callbacks remain serialized on the background
loop, and callback exceptions remain isolated.

**Compatibility and migration.** The error event is additive. Consumers that
counted `OnDisconnected` for failed attempts must move that accounting to
`OnError`. No callback is moved to a UI or synchronization-context thread.

**Rejected alternatives.** Parallel callback tasks would lose deterministic
order and create unbounded work. Propagating subscriber exceptions would kill
the listen loop. A full connection-state hierarchy is unnecessary for the
current transport.

**Validation if accepted.** Multiple throwing and non-throwing state
subscribers, failed connect, clean EOF, malformed frame, remote drop, local
shutdown, callback order, and reconnect tests on both targets.

### PIPE-06 — Medium compatibility — built-in wire errors are stable behavior but have no public symbols or complete current reference

**Observed fact.** Four framework-generated response codes are visible on the
wire: `not_found`, `exception`, `response_too_large`, and `connection_closed`
([`PipeServer.cs:224`](../../../../src/Pipes/NekoLib.Pipes/PipeServer.cs#L224),
[`PipeServer.cs:253`](../../../../src/Pipes/NekoLib.Pipes/PipeServer.cs#L253),
[`PipeServer.cs:293`](../../../../src/Pipes/NekoLib.Pipes/PipeServer.cs#L293),
[`PipeClient.cs:120`](../../../../src/Pipes/NekoLib.Pipes/PipeClient.cs#L120)).
Consumers must hard-code strings because no public symbols name them. The root
README says protocol errors are stable and handler details are sanitized, but
does not publish the complete outcome matrix.

The current matrix is:

| Condition | Client outcome |
|---|---|
| unknown handler | `Ok=false`, `not_found` |
| handler throws | `Ok=false`, `exception`, sanitized message; local exception sent only to metrics |
| handler response exceeds server cap | `Ok=false`, `response_too_large` |
| clean EOF before response | `Ok=false`, `connection_closed` |
| caller cancellation or request timeout | `OperationCanceledException` |
| connect/serialization/write/parse/truncated-frame failure | exception propagates |
| wrong response ID or type | `InvalidOperationException` |
| malformed or oversized incoming request | server closes the connection; no trustworthy correlated response |

Application handlers may return any additional `PipeError.Code`; the framework
codes are not a closed enum.

**Risk to consumers.** Typos and undocumented exception/response distinctions
make error handling brittle and invite treating every `Ok=false` as the same
failure.

**Recommended disposition.** Add one public static `PipeErrorCodes` holder with
the four framework string constants, preserve application-defined codes, and
make the matrix above part of the future current Pipes technical reference.
Keep handler errors sanitized at the wire and detailed only in local metrics.

**Compatibility and migration.** Additive API only; existing string comparisons
continue to work and can migrate mechanically to named constants.

**Rejected alternatives.** A closed enum prevents application-defined codes
and changes JSON. Converting every failure to an exception breaks the deliberate
fail-response contract. Converting malformed uncorrelated input to a response
would fabricate an identity that the server cannot trust.

**Validation if accepted.** Manifest additions plus dual-target tests pinning
every built-in code, sanitized message, clean versus truncated EOF, malformed
JSON, and correlation failure.

### PIPE-07 — Medium target parity — in-flight RPC connect cancellation is not honored on `net481`

**Observed fact.** The modern client links the caller token to `ConnectTimeout`
and awaits token-aware `ConnectAsync`. The `net481` branch runs blocking
`pipe.Connect(timeout)` on a pool thread and awaits it without a cancellation
bridge
([`PipeClient.cs:82`](../../../../src/Pipes/NekoLib.Pipes/PipeClient.cs#L82)). A token
cancelled after connect begins cannot return the RPC call until the configured
connect timeout elapses. Request framing has a separate cancellation bridge and
does not share this gap.

**Risk to consumers.** The same public `SendAsync` cancellation contract has
different responsiveness by target, including during application shutdown.

**Recommended disposition.** On `net481`, race the blocking connect with caller
cancellation, observe the abandoned worker, and let `SendAsync`'s stream
disposal unblock it, preserving the independent `ConnectTimeout`. Return
`OperationCanceledException` for caller cancellation as the modern path does.

**Compatibility and migration.** Behavioral correction only; cancelled calls
return earlier. A consumer that relied on cancellation being ignored has no
supported contract to preserve.

**Rejected alternatives.** Merely documenting that `ConnectTimeout` is a bound
does not honor the method's token. Lowering the default timeout changes every
consumer and still leaves cancellation ineffective.

**Validation if accepted.** A no-server connect with a long configured timeout
and a promptly cancelled caller token on both targets, plus observation that no
worker fault or pipe endpoint remains.

### PIPE-08 — Medium compatibility — the public payload and serializer dependency are deliberately target-specific

**Observed fact.** `PipeMessage.Data` has different public types on the two
targets. The direct Watchdog consumer consequently uses conditional branches to
read request data and construct response data
([`WatchdogRuntime.cs:351`](../../../../src/Watchdog/NekoLib.Watchdog/WatchdogRuntime.cs#L351),
[`WatchdogRuntime.cs:478`](../../../../src/Watchdog/NekoLib.Watchdog/WatchdogRuntime.cs#L478)).
The wire remains JSON, but shared multi-target consumer source cannot use one
serializer-specific API without conditional code.

**Risk to consumers.** `net481` consumers acquire a public Newtonsoft contract;
modern consumers acquire a `System.Text.Json` contract. Serializer defaults,
DOM behavior, and source code can diverge. Same-target focused tests do not
prove `net481` client to `net9.0` server interoperability or the reverse.

**Recommended disposition.** Accept this as a deliberate target-specific stable
contract for the first baseline, document both types and the `net481`
dependency, and require a mixed-target wire-compatibility probe before the first
stable package-family release. Do not add a new payload abstraction during F1
without a concrete consumer that needs one.

**Compatibility and migration.** No API change. Multi-target handlers continue
to use target compilation or restrict themselves to neutral operations such as
`ToString()`.

**Rejected alternatives.** Forcing `System.Text.Json` onto `net481` adds a new
dependency graph and changes serialization behavior. Forcing Newtonsoft onto
`net9.0` adds an otherwise unnecessary dependency. Replacing `Data` with raw
JSON, `object`, or a new wrapper breaks every handler and creates a speculative
abstraction without eliminating wire-version responsibilities.

**Validation if accepted.** Separate-process `net481 -> net9.0` and
`net9.0 -> net481` request/response and event probes with representative scalar,
object, array, null, Unicode, and error payloads; package assets and exact
dependency groups recorded.

### PIPE-09 — Low API design — `SimplePipeMetrics` exposes accidental inheritance

**Observed fact.** `SimplePipeMetrics` is public and unsealed, but none of its
public methods is virtual and it exposes no protected extension hook
([`SimplePipeMetrics.cs:6`](../../../../src/Pipes/NekoLib.Pipes/SimplePipeMetrics.cs#L6)).
The real customization contract is `IPipeMetrics`. The repository has no
derived `SimplePipeMetrics`; Watchdog constructs it directly.

**Risk to consumers.** The first stable baseline would imply subclassing is a
supported extension even though derived types cannot override the metric
algorithm coherently.

**Recommended disposition.** Keep `SimplePipeMetrics` public and seal it. Keep
all metric snapshot constructors public so an `IPipeMetrics` implementation can
return the standard DTOs.

**Compatibility and migration.** Sealing is a source and binary break for an
external subclass. Before the first stable baseline, migrate such a type to
implement `IPipeMetrics` directly or compose a `SimplePipeMetrics` instance.

**Rejected alternatives.** Making all callbacks virtual expands an accidental
inheritance contract and complicates synchronization invariants. Internalizing
the concrete type removes a useful built-in and breaks the existing Watchdog
consumer. Privatizing `NoopPipeMetrics` construction has negligible value and is
not recommended.

**Validation if accepted.** Reviewed one-line API diff, both target builds, and
existing metric concurrency/snapshot tests.

### PIPE-10 — Security impact depends on use — access policy must remain an explicit application boundary

**Observed fact.** The package exposes `PlatformDefault` and `CurrentUserOnly`,
defaults server options and compatibility constructors to `PlatformDefault`,
and exposes no peer identity or operation-authorization contract
([`PipeServerOptions.cs:20`](../../../../src/Pipes/NekoLib.Pipes/PipeServerOptions.cs#L20),
[`PipeEventHub.cs:174`](../../../../src/Pipes/NekoLib.Pipes/PipeEventHub.cs#L174)).
The direct Watchdog consumer explicitly opts into `CurrentUserOnly`; generic
Pipes does not impose that policy.

**Risk to consumers.** If a consumer mistakes either policy for authentication
or uses `PlatformDefault` for sensitive events, other processes may gain access
outside the intended trust model. A hostile same-user process remains in scope
for neither policy.

**Recommended disposition.** Keep the enum, current default, and
`PipeServer.AccessPolicy` stable. State prominently that Pipes is a local
same-machine transport for cooperative processes, not an authorization
boundary. Applications own handler authorization and stronger identity/session
requirements. Do not add peer authentication, replay state, credentials, or
privileged control infrastructure to F1-PIPE.

**Compatibility and migration.** No API change. Sensitive same-user-only
applications select `CurrentUserOnly` explicitly on the server. Applications
with a hostile same-user threat require a separately designed authenticated
protocol, not a different enum value.

**Rejected alternatives.** Changing the default would silently break intended
cross-user/service topologies and contradict the already accepted opt-in E5
compatibility direction. Deterministic or secret-looking pipe names are not
credentials. Exposing peer identity without an accepted authorization use case
would widen the platform-specific surface. Replay protection is unjustified for
the generic unauthenticated transport.

**Validation if accepted.** Keep same-user success and target-specific
ACL/option tests. Before claiming an enforced Windows account boundary, add
separate cross-user and cross-elevation denial probes. No such probe ran here.

## Analyzed and rejected items

The review analyzed these directions and does not recommend them for F1-PIPE:

1. **A Core or Inspection dependency.** Pipes remains dependency-free at the
   project level; metrics already provide the opt-in seam and broad Inspection
   instrumentation remains frozen.
2. **A static/global facade.** Client, server, event hub, and metrics have clear
   instance ownership. Navigation's facade is not a repository template.
3. **Persistent RPC connections or automatic retries.** The per-call client
   gives simple correlation and ownership. Retrying non-idempotent operations
   would be application policy.
4. **Lossless events.** Bounded best-effort queues with observable failure are
   appropriate. Persistence and replay require an application protocol.
5. **Parallel event callbacks.** Serial callbacks preserve frame and subscriber
   order and keep backpressure visible.
6. **Generic authentication, peer process proof, or privileged TestControl.**
   These require a separately accepted threat model and protocol.
7. **A closed error enum.** Application-defined handler errors are intentional.
8. **A speculative target-neutral payload wrapper.** The current split is
   costly but explicit; a wrapper would break all handlers without demonstrated
   consumer value.
9. **A reset method on `IPipeMetrics`.** It would break every custom
   implementation. A fresh metrics instance or a custom resettable sink is an
   application choice.
10. **Making `PipeFraming` public.** Framing is a package protocol detail, not a
    general stream API. Its behavior is tested internally and observed through
    the public clients/servers.

## Consolidated proposal for the decision gate

Nothing below is accepted or scheduled by this review.

1. **Configuration:** keep both options types, capture values at construction,
   validate all bounds deterministically, and preserve only `AutoReconnect` as
   an intentional live event-client switch (PIPE-01).
2. **Lifecycle:** add cross-target `ShutdownAsync` to all stateful endpoints,
   make start/shutdown terminal and race-safe, correct modern async disposal,
   and remove stateless `PipeClient` disposal surface (PIPE-02).
3. **Metrics:** keep `IPipeMetrics` and the snapshot DTOs as deliberate public
   extension contracts, isolate every framework callback, keep cumulative
   non-resetting snapshots, and seal `SimplePipeMetrics` (PIPE-03, PIPE-09).
4. **Events:** retain bounded FIFO queues, `DropNewest` and
   `DisconnectSubscriber`; reject oversized events before enqueue without
   disconnecting subscribers; add minimal error observation and make every
   notification subscriber isolated (PIPE-04, PIPE-05).
5. **Errors and cancellation:** preserve the current response-versus-exception
   matrix, add public constants for the four framework codes, keep sanitized
   handler errors, and correct in-flight `net481` connect cancellation
   (PIPE-06, PIPE-07).
6. **Target/package contract:** explicitly accept serializer-specific
   `PipeMessage.Data` types and the `net481` Newtonsoft dependency for the first
   baseline, subject to mixed-target wire validation before stable release
   (PIPE-08).
7. **Security:** retain opt-in `CurrentUserOnly`, compatibility-default
   `PlatformDefault`, and application-owned authorization; add no authentication,
   replay, remote, or privileged-control framework (PIPE-10).
8. **Remaining surface:** classify every other listed type and member as stable,
   with no experimental marker and no stable deprecation window.

The recommended direction is to accept this bounded package before
implementation. It preserves the small instance-owned architecture and the E5
trust model while correcting lifecycle and observability contracts before they
become stable. The one decision that deserves explicit scrutiny is item 6: the
target-specific payload type is a real consumer cost, but the alternatives add
more dependency or abstraction cost than current evidence justifies.

## Validation

Executed on Windows from the clean reference commit, without
`-UpdateBaseline`:

| Command | Result |
|---|---|
| `git status --short --branch` | clean; expected branch; ahead 37 |
| `git rev-parse HEAD` | `2db588ac6fef271851e70c04f7b8b82cd8e004a7` |
| `dotnet test tests/NekoLib.Pipes.Tests/Unit/NekoLib.Pipes.Tests.Unit.csproj -f net481 -m:1` | **44 passed**, 0 failed, 0 skipped |
| `dotnet test tests/NekoLib.Pipes.Tests/Unit/NekoLib.Pipes.Tests.Unit.csproj -f net9.0-windows -m:1` | **44 passed**, 0 failed, 0 skipped; consumes the `net9.0` library asset |
| `.\eng\verify-public-api.ps1 -PackageId NekoLib.Pipes` | both project builds succeeded with 0 warnings and 0 errors; `net481` and `net9.0` manifests verified; 2 baselines passed |
| `.\eng\verify-docs.ps1` | passed after this audit and both index entries were added |
| `git diff --check` | passed after this audit and both index entries were added |

The focused tests are unit plus in-process integration coverage because they
open real named pipes. They cover same-target RPC, events, framing, configured
RPC limits, clean/truncated EOF, malformed JSON, correlation, sanitized handler
failure, timeout, idle lifetime, concurrent independent requests, concurrent
event publication, bounded subscriber queues, overflow policies, reconnect,
callback exception isolation, current-user same-user success, target-specific
server protection, admitted-work shutdown, and metrics.

## Residual limitations and validation gaps

- No cross-user or cross-elevation denial test was run. The current security
  evidence is same-user success plus inspection of the `net481` ACL and modern
  `PipeOptions` flag.
- No hostile same-user, server impersonation, pipe-name squatting, authorization,
  credential, or replay test was attempted. Those are outside the accepted
  generic transport boundary.
- No mixed-target or separate-process compatibility probe ran. In particular,
  `net481` client/server interoperability with `net9.0` in the opposite process
  is not established by the focused same-target tests.
- No Linux or macOS execution was performed even though the modern asset is
  unqualified `net9.0`.
- The missing contracts identified above have no tests yet: invalid and mutated
  options, lifecycle races, public shutdown awaiting, metrics-failure isolation,
  oversized-event subscriber preservation, detailed event failure observation,
  and prompt `net481` connect cancellation.
- No long-duration, saturation, high-throughput, memory-growth, or process-loss
  scenario ran. Existing historical runtime evidence was not refreshed.
- No full solution build/test ran. No package, `-AllowDirty` validation,
  PackageReference consumer, publish, or push was performed.

## Review-only declaration

F1-PIPE remains open. This review produced only this audit and its two current
index entries. It implemented no correction, accepted no proposal, changed no
public API baseline, produced no migration or changelog entry, built no package,
published nothing, and pushed nothing.

## Reconciliation — 2026-08-18: dispositions accepted and implemented

The preceding text remains the review snapshot at
`2db588ac6fef271851e70c04f7b8b82cd8e004a7`. The user subsequently accepted all
eight numbered decision-gate proposals. Acceptance was promoted to `TODO.md` in
`0086817e88bc725c92bef3dff4ea69ab12492365`; implementation, tests, current
documentation, migration guidance, changelog, and reviewed API baselines landed
in `e608dc873c78f5bcecbd79dc5931c59f5d461dcc`.

### Accepted decisions and implemented outcome

1. **Configuration (PIPE-01).** Client and server values are captured and
   validated at construction. Event-client timeouts and reconnect delay validate
   their live setters; only `AutoReconnect` remains an intentional live behavior
   switch. See [`PipeConfiguration.cs`](../../../../src/Pipes/NekoLib.Pipes/PipeConfiguration.cs)
   and the mutation regressions in
   [`PipeConfigurationTests.cs:115`](../../../../tests/NekoLib.Pipes.Tests/Unit/PipeConfigurationTests.cs#L115).
2. **Lifecycle (PIPE-02).** `PipeServer`, `PipeEventHub`, and `PipeEventClient`
   expose target-neutral `ShutdownAsync`; start and shutdown are terminal and
   race-safe; admitted operations remain represented by shutdown completion;
   modern async disposal performs real async shutdown. The stateless
   `PipeClient` no longer exposes disposal. See
   [`PipeServer.cs:357`](../../../../src/Pipes/NekoLib.Pipes/PipeServer.cs#L357),
   [`PipeEventHub.cs:488`](../../../../src/Pipes/NekoLib.Pipes/PipeEventHub.cs#L488),
   [`PipeEventClient.cs:232`](../../../../src/Pipes/NekoLib.Pipes/PipeEventClient.cs#L232),
   and [`PipeShutdownTests.cs:131`](../../../../tests/NekoLib.Pipes.Tests/Unit/PipeShutdownTests.cs#L131).
3. **Metrics (PIPE-03 and PIPE-09).** Every framework-owned metrics callback is
   isolated behind an internal guard; snapshots remain cumulative and do not
   reset; `IPipeMetrics` remains the extension seam; and `SimplePipeMetrics` is
   sealed. See [`PipeMetricsGuard.cs`](../../../../src/Pipes/NekoLib.Pipes/PipeMetricsGuard.cs)
   and [`PipeMetricsIsolationTests.cs:12`](../../../../tests/NekoLib.Pipes.Tests/Unit/PipeMetricsIsolationTests.cs#L12).
4. **Events (PIPE-04 and PIPE-05).** Bounded FIFO subscriber queues and both
   overflow policies remain. Serialized events are preflighted against the fixed
   1 MiB frame bound before enqueue, so an oversized publication fails without
   disconnecting healthy subscribers or changing publication metrics. The event
   client now exposes isolated `OnError`; connected, event, error, and
   disconnected callbacks are isolated individually and retain documented
   ordering. See [`PipeEventHub.cs:403`](../../../../src/Pipes/NekoLib.Pipes/PipeEventHub.cs#L403),
   [`PipeEventClient.cs:44`](../../../../src/Pipes/NekoLib.Pipes/PipeEventClient.cs#L44),
   [`PipeEventTests.cs:136`](../../../../tests/NekoLib.Pipes.Tests/Unit/PipeEventTests.cs#L136),
   and [`PipeEventClientTests.cs:110`](../../../../tests/NekoLib.Pipes.Tests/Unit/PipeEventClientTests.cs#L110).
5. **Errors and cancellation (PIPE-06 and PIPE-07).** `PipeErrorCodes` publishes
   the four stable framework codes while the existing response-versus-exception
   matrix and sanitized handler failure remain unchanged. The `net481` blocking
   connect path now races caller cancellation without disposing the still-running
   task prematurely. See [`PipeErrorCodes.cs`](../../../../src/Pipes/NekoLib.Pipes/PipeErrorCodes.cs),
   [`PipeTaskCancellation.cs`](../../../../src/Pipes/NekoLib.Pipes/PipeTaskCancellation.cs),
   [`PipeRpcTests.cs:339`](../../../../tests/NekoLib.Pipes.Tests/Unit/PipeRpcTests.cs#L339),
   and [`PipeClientCancellationTests.cs`](../../../../tests/NekoLib.Pipes.Tests/Unit/PipeClientCancellationTests.cs).
6. **Target and package contract (PIPE-08).** The target-specific
   `PipeMessage.Data` types and the `net481` Newtonsoft dependency are accepted
   for this baseline and documented. No new project reference or package
   dependency was added. A mixed-target separate-process wire probe remains a
   prerequisite before the first stable family release.
7. **Security (PIPE-10).** `CurrentUserOnly` remains opt-in,
   `PlatformDefault` remains the compatibility default, and applications own
   authorization. No authentication, replay, remote, privileged-control, Core,
   or Inspection infrastructure was added. The technical reference now states
   this boundary prominently.
8. **Remaining surface.** All other reviewed public types and members are stable
   for this candidate baseline. No member is marked experimental and no
   deprecation window was introduced.

### Compiled API delta

The reviewed manifests record only the accepted surface changes: removal of
`PipeClient` disposal, addition of `PipeErrorCodes`, target-neutral shutdown on
the three stateful endpoints, modern `PipeServer` async disposal, event-client
`OnError`, and sealing `SimplePipeMetrics`. Both compiled targets verify against
their baselines. Consumer sources in Watchdog and the versioned runtime-scenario
projects were migrated mechanically away from stateless client disposal; no
protocol behavior was changed by that migration.

### Implementation validation

Executed on Windows from the implementation worktree:

| Command | Result |
|---|---|
| `dotnet test tests/NekoLib.Pipes.Tests/Unit/NekoLib.Pipes.Tests.Unit.csproj -f net481 -m:1` | **74 passed**, 0 failed, 0 skipped |
| `dotnet test tests/NekoLib.Pipes.Tests/Unit/NekoLib.Pipes.Tests.Unit.csproj -f net9.0-windows -m:1` | **74 passed**, 0 failed, 0 skipped |
| `dotnet test tests/NekoLib.Watchdog.Tests/Unit/NekoLib.Watchdog.Tests.Unit.csproj -f net481 -m:1` | **84 passed**, 0 failed, 0 skipped |
| `dotnet test tests/NekoLib.Watchdog.Tests/Unit/NekoLib.Watchdog.Tests.Unit.csproj -f net9.0-windows -m:1` | **84 passed**, 0 failed, 0 skipped |
| `dotnet test NekoLib.sln -c Release -m:1 --no-restore` | **1,598 passed**, 0 failed, 0 skipped |
| `dotnet build NekoLib.sln -c Release -t:Rebuild -m:1 --no-restore` | succeeded; 464 warning occurrences, 0 errors; no new warning identity |
| `dotnet build` for Pipes LongRunningRecovery and Watchdog Supervisor481/CrashRecovery scenario projects, each supported target | succeeded with 0 warnings and 0 errors; build-only, never launched |
| `.\eng\verify-public-api.ps1 -PackageId NekoLib.Pipes` | both builds succeeded with 0 warnings and 0 errors; `net481` and `net9.0` manifests verified |
| `.\eng\verify-docs.ps1 -BuildLogPath artifacts\f1-pipes-solution-rebuild.log` | passed; 25 baseline warning identities were not emitted |
| `git diff --check` | passed |

### Residual validation limits

- No cross-user or cross-elevation denial probe was run. No hostile same-user,
  pipe-name-squatting, impersonation, authorization, credential, or replay probe
  was attempted.
- No mixed-target or separate-process wire probe ran. Same-target tests do not
  prove `net481`/`net9.0` process interoperability.
- No Linux or macOS execution was performed for the unqualified modern asset.
- No runtime, long-duration, saturation, throughput, memory-growth, abrupt
  process-loss, or recovery scenario was launched. Scenario projects above were
  compile-only evidence.
- No immutable package, PackageReference consumer campaign, publish, or push was
  produced. Package and mixed-target evidence remain family release-gate work,
  not an implicit claim of this implementation reconciliation.
