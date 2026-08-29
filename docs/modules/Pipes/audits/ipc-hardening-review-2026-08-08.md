# Pipes and Watchdog IPC Hardening Review — 2026-08-08

**Document ID:** PIPE-AUDIT-IPC-HARDENING-20260808

**Schema version:** 1

**Kind:** audit

**Lifecycle:** historical

**Subject:** code-first review of the NekoLib.Pipes transport and the Watchdog RPC/event boundary — trust model, Windows pipe security, endpoint ownership, framing, authorization, replay, backpressure, and shutdown

**Surface:** audit

**Boundary:** pipes

**Authority role:** evidence

**Mutation:** snapshot

**Indexing:** include

**Reference date:** 2026-08-08

**Reference commit:** `941e17e8224dff3b34b7495d7bd0f7cf12c8f4ed`

**Original path:** docs/audit/pipes-ipc-hardening-review-2026-08-08.md

**Last reconciliation:** 2026-08-08 — review completed; bounded dispositions accepted and promoted to `TODO.md` Phase E5

**Current state:** [`TODO.md`](../../../../TODO.md) Phase E5 is the sole authority for accepted work and implementation status; this snapshot preserves the reviewed evidence, original recommendations, and rejected alternatives

## Baseline and authority

This review covers committed `master` at the reference commit on a clean
worktree. It reviews current source and executable tests, not the findings in
the historical [`initial-audit.md`](initial-audit.md) as if they were
still current.

The review changed no product code. Creating this snapshot and indexing it are
documentation actions. A finding below is not authorization to implement a
security framework, a privileged TestControl channel, remote administration,
or a generic service bus.

## Scope

Included:

- `src/Pipes/NekoLib.Pipes/**`;
- the direct product consumer in `src/Watchdog/NekoLib.Watchdog/**`;
- the Watchdog Host bootstrap/attach handshake;
- `tests/NekoLib.Pipes.Tests/Unit/**` and the relevant Watchdog tests;
- the versioned Watchdog supervisor runtime scenario;
- Windows named-pipe security semantics relevant to the supported deployment.

Excluded:

- product-code fixes;
- Instrumentation or TestControl design;
- remote-machine IPC;
- a new identity, certificate, or secret-distribution framework;
- physical access, OS-account hardening, and malware resistance outside the
  process/IPC boundary.

## Current boundary

`NekoLib.Pipes` exposes two local channels derived from one caller-selected
name:

- `PipeServer` and `PipeClient` implement framed JSON request/response RPC;
- `PipeEventHub` and `PipeEventClient` implement one-way event delivery on
  `<name>.events`.

Pipes has no project dependency and no knowledge of Watchdog commands. The
only product use is Watchdog. Its control server maps read-only operations
(`ping`, `status`, `attach_status`, and `log_history`), ingestion operations
(`exception_notify`, `log_write`, and `log_write_batch`), and process-changing
operations (`pause`, `resume`, `restart`, and `stop`) onto the same endpoint.

The pipe name is deterministic: Watchdog lowercases the full target path,
hashes it with SHA-1, takes the first 16 hexadecimal characters, and prefixes
`NekoLib.Watchdog.`. This prevents accidental name overlap in ordinary use; it
does not authenticate either endpoint and is not a secret.

### Effective trust model observed in code

No current document declares an IPC threat model, and neither Pipes nor
Watchdog authenticates requests. The effective model is therefore:

> local cooperative processes that can open the pipe are trusted to call every
> registered operation.

That is an observation, not yet an accepted product contract. The current
implementation is not a privilege boundary against another process running as
the same Windows user. It must not be reused for privileged Instrumentation or
TestControl.

The server constructors pass no explicit `PipeSecurity` and do not request
`PipeOptions.CurrentUserOnly`. Microsoft's Windows named-pipe security
documentation states that the default descriptor grants full control to
LocalSystem, administrators, and the creator owner, plus read access to
Everyone and anonymous. This matters differently per channel: a duplex RPC
client needs write access, while an event subscriber only reads from the
server. The default descriptor therefore cannot be described as a same-user
confidentiality boundary for live events.

On .NET 9, `PipeOptions.CurrentUserOnly` is available and verifies both user
account and elevation level on Windows. The net481 `PipeOptions` surface does
not contain that flag; equivalent restriction requires an explicit
`PipeSecurity`/ACL or an application-layer check. Current code uses neither.

References:

- [Windows named-pipe security and default ACLs](https://learn.microsoft.com/en-us/windows/win32/ipc/named-pipe-security-and-access-rights)
- [.NET `PipeOptions`, including `CurrentUserOnly`](https://learn.microsoft.com/en-us/dotnet/api/system.io.pipes.pipeoptions?view=net-9.0#fields)

## Confirmed protections

The review confirmed useful hardening already present:

- every frame is length-prefixed and rejected before payload allocation when
  it is non-positive or exceeds `MaxMessageBytes`; RPC defaults to 1 MiB;
- server clients and event subscribers have independent concurrency caps;
- request responses must match the request ID and response type;
- connect, request, and idle waits are bounded by configuration;
- net481 blocking framing work is observed through cancellation and the owning
  call disposes the pipe after cancellation;
- event callback exceptions are isolated and event clients reconnect;
- Watchdog serializes its own event publication through one event thread, uses
  a bounded producer queue, counts drops, and gives each publish a one-second
  cancellation budget;
- Watchdog log forwarding uses a separate bounded queue and bounded batches.

These controls limit accidental failure. They do not authenticate a caller or
prove server identity.

## Findings

Severity describes impact within the stated condition. Security findings whose
reach depends on the unresolved threat model say so explicitly.

### IPC-01 — High impact if same-user processes are not fully trusted — Watchdog control is unauthenticated

`PipeServer` dispatches solely by caller-supplied command name. Watchdog maps
`restart` to killing the supervised child and maps `stop` to terminating the
host. No transport identity, request credential, allowlist by caller, or
per-command authorization is checked.

Evidence:

- [`PipeServer.Dispatch`](../../../../src/Pipes/NekoLib.Pipes/PipeServer.cs#L265)
  looks up only `request.Name`;
- [`WatchdogRuntime.RegisterRpcHandlers`](../../../../src/Watchdog/NekoLib.Watchdog/WatchdogRuntime.cs#L236)
  exposes the command surface;
- `restart` calls `TryKill` at lines 300-312 and `stop` queues `Stop(true)` at
  lines 315-327.

Any process with duplex access can pause supervision, kill/restart the child,
stop the Host, inject crash/log records, or read status/history. Under an
explicit same-user-trusted model this is an accepted trust assumption. Under a
hostile same-user, cross-elevation, or privileged-control model it is a direct
authorization defect.

The attach token does not solve this. `attach_status` returns the token over
the same unauthenticated endpoint, so it is a bootstrap correlation value, not
a reusable authentication secret.

### IPC-02 — Medium — default pipe security does not establish the intended local boundary

RPC and event servers use `PipeOptions.Asynchronous` with no explicit security
descriptor. The event client needs read access only, and live Watchdog events
contain log/telemetry payloads. The documented Windows default grants read
access more broadly than creator-owner.

The net9 implementation could request `CurrentUserOnly`; net481 needs a
separate ACL path. Neither exists, and there is no test that starts a client as
a different user or at a different elevation level.

Evidence:

- [`PipeServer`](../../../../src/Pipes/NekoLib.Pipes/PipeServer.cs#L95) creates the
  duplex server;
- [`PipeEventHub`](../../../../src/Pipes/NekoLib.Pipes/PipeEventHub.cs#L81) creates
  the outbound event server;
- [`PipeClient`](../../../../src/Pipes/NekoLib.Pipes/PipeClient.cs#L39) and
  [`PipeEventClient`](../../../../src/Pipes/NekoLib.Pipes/PipeEventClient.cs#L80)
  likewise omit an identity restriction.

### IPC-03 — Medium — deterministic names permit same-user endpoint squatting and impersonation

The target path makes the Watchdog pipe name reproducible. Neither endpoint
verifies the peer process, and the server does not request a first-instance
guarantee. A same-user process can create the expected name before the real
Host, impersonate it to a client, occupy connection/subscriber slots, or race
normal startup.

The named Watchdog semaphore prevents cooperative duplicate Hosts; it does not
prove that the process answering the pipe owns that semaphore. The existing
PID/token bootstrap handshake detects many accidental mismatches but cannot
authenticate a hostile server that can observe or obtain the correlation token.

Evidence:

- [`WatchdogController.ResolvePipeNameForTarget`](../../../../src/Watchdog/NekoLib.Watchdog/WatchdogController.cs#L46);
- [`WatchdogRuntime.Start`](../../../../src/Watchdog/NekoLib.Watchdog/WatchdogRuntime.cs#L101).

### IPC-04 — Medium correctness — concurrent event publications have no single-writer boundary

One `PublishAsync` call writes to different subscribers concurrently, which
fixes cross-subscriber head-of-line blocking. However, two callers can invoke
`PublishAsync` concurrently. Both then call `PipeFraming.WriteAsync` on the
same subscriber stream without a per-subscriber lock or writer queue.

Framing emits the four-byte length and JSON payload as separate writes. The
pair is not made atomic across two publications, so concurrent publishers can
interleave frames or fault/remove the subscriber. Existing tests cover fan-out
from one publisher, not concurrent publication.

Watchdog does not currently trigger this path because its `EventLoop` is the
single publisher. The generic public Pipes API still exposes the race.

Evidence:

- [`PipeEventHub.PublishAsync`](../../../../src/Pipes/NekoLib.Pipes/PipeEventHub.cs#L142)
  snapshots subscribers and awaits `Task.WhenAll`;
- [`PipeEventHub.SendToSubscriber`](../../../../src/Pipes/NekoLib.Pipes/PipeEventHub.cs#L202)
  writes directly to the shared stream;
- [`PipeFraming.WriteCore`](../../../../src/Pipes/NekoLib.Pipes/PipeFraming.cs#L149)
  writes length, payload, and flush separately.

### IPC-05 — Medium reliability — slow subscribers still backpressure each publisher

Parallel fan-out prevents one subscriber from delaying delivery to another,
but `PublishAsync` awaits all writes. It has no per-subscriber bounded queue,
single writer, delivery timeout, or explicit drop/disconnect policy of its own.
A caller without a cancellation token can therefore remain blocked on a slow
subscriber.

Watchdog contains this with its own bounded queue and one-second cancellation;
other consumers do not receive that policy from Pipes. A slow Watchdog
subscriber is removed after the cancelled write, so delivery is best-effort
rather than buffered.

### IPC-06 — Medium reliability — server disposal does not own or drain active client work

`PipeServer` launches every accept/client path as a detached task and retains
only `_acceptTask`. `Dispose` cancels the shared token, waits up to two seconds
for the accept loop, then disposes the semaphore and token source. It does not
track active client tasks or connected streams.

A cooperative handler observes cancellation and exits. A handler that ignores
the token can outlive `Dispose`; its detached task later reaches cleanup and
releases a semaphore that may already be disposed. Shutdown is therefore
bounded only for the accept loop, not for admitted work. No test disposes a
server while a handler is active.

Evidence:

- detached task creation at [`PipeServer.cs:88`](../../../../src/Pipes/NekoLib.Pipes/PipeServer.cs#L88);
- handler execution at [`PipeServer.cs:200`](../../../../src/Pipes/NekoLib.Pipes/PipeServer.cs#L200);
- disposal at [`PipeServer.cs:296`](../../../../src/Pipes/NekoLib.Pipes/PipeServer.cs#L296).

`PipeEventHub` has the same detached-accept ownership shape, although it does
dispose all registered subscriber streams during shutdown.

### IPC-07 — Medium, legacy surface — obsolete WatchdogLogPipeServer cannot cancel a pending accept

`WatchdogLogPipeServer` is obsolete and has no repository consumer, but it is
still public and packaged. Its accept thread blocks in `WaitForConnection` on a
method-local stream. `Dispose` sets `_exiting` and joins for 800 ms but cannot
dispose that pending stream, so the background thread can survive disposal and
accept a later client into an object whose dispatcher has stopped.

This is not the live Watchdog log path. The live path uses `PipeServer` events.
The decision is whether to remove the obsolete public type in a breaking
release or make its shutdown truthful while it remains shipped.

### IPC-08 — Low/medium — protocol failure disclosure and replay policy are implicit

Handler exceptions return `ex.Message` to the caller. Malformed frames are
silently treated as disconnects by the server, and a body truncated after a
valid length prefix is represented by `TryReadAsync` as clean EOF. Serializer
depth uses library defaults rather than an explicit protocol setting.

Request IDs correlate responses but are not retained or deduplicated. A raw
client may replay an ID or repeat `restart`/`stop`; no current client retries
automatically, so this is primarily relevant if untrusted callers or automatic
retries enter the threat model.

The byte-size cap is real and should be retained. The review does not recommend
inventing replay infrastructure for read-only or explicitly idempotent calls.

### IPC-09 — Low, target-specific — net481 cannot cancel an in-progress client connect by token

On net9, `PipeClient` creates a linked cancellation source and calls the native
token-aware `ConnectAsync`. On net481 it runs `pipe.Connect(timeout)` on a pool
thread; the caller token is not passed to that work. The operation remains
bounded by `ConnectTimeout`, but caller cancellation cannot interrupt a connect
already in progress.

The framing/read path has a separate net481 cancellation bridge and is covered
by timeout tests. The connect distinction is not covered.

## Coverage gaps confirmed by the review

The current tests cover RPC round trips, not-found and handler errors, response
size, configured frame caps, correlation, request timeout, idle timeout,
concurrent independent RPC clients, event fan-out, reconnect, callback
isolation, and metrics.

They do not cover:

- cross-user or cross-elevation connection attempts;
- server identity or name squatting;
- authorization of Watchdog mutating commands;
- concurrent `PublishAsync` calls to one subscriber;
- a non-reading/slow subscriber without caller cancellation;
- dispose while a handler ignores cancellation;
- malformed JSON, explicit depth, duplicate request IDs, or replay;
- net481 caller cancellation during connect;
- disposal of the obsolete raw log server while it is waiting for a client.

## Decisions requiring acceptance

The review recommends the following bounded disposition package. None is
promoted as implementation until accepted.

1. **Adopt an explicit generic Pipes contract:** local, same-machine,
   cooperative processes. `NekoLib.Pipes` is not an authorization boundary and
   does not support hostile same-user clients, remote administration, or
   privileged TestControl.
2. **Protect the actual Watchdog boundary:** restrict both RPC and event pipes
   to the intended user on both targets. On net9 use the platform
   `CurrentUserOnly` behavior; on net481 use an explicit ACL. Preserve generic
   Pipes compatibility through an opt-in policy if changing its default would
   be breaking.
3. **Choose whether same-user hostile processes are in scope for Watchdog.** If
   yes, add application-layer session authentication and server-ownership
   proof before mutating commands. Do not treat the name hash or the current
   attach token as a secret. If no, record the accepted risk explicitly and do
   not claim privilege separation.
4. **Accept correctness/reliability hardening independent of the security
   model:** one bounded single-writer queue per event subscriber with an
   explicit drop-or-disconnect policy; tracked active RPC work and connected
   streams; bounded disposal that cancels, closes, and then drains.
5. **Retire or repair the obsolete raw log server.** Removal belongs to an
   explicitly scheduled breaking release; truthful disposal is the compatible
   alternative.
6. **Sanitize generic handler errors at the wire boundary** while retaining
   detailed local metrics/logging. Add explicit protocol depth/schema limits
   only where a concrete payload requires them. Add replay protection only for
   authenticated, non-idempotent commands if that threat is accepted.

## Rejected alternatives

- Treating the deterministic pipe name or truncated SHA-1 as a credential.
- Reusing `AttachToken` while returning it from unauthenticated
  `attach_status`.
- Adding sender-selected CLR types, assembly-qualified names, runtime objects,
  or reflection activation to the wire protocol.
- Turning generic Pipes into an authorization server, service bus, remote
  administration layer, or Instrumentation/TestControl host.
- Claiming all events must be lossless. The Watchdog event stream is telemetry
  and logs; a bounded best-effort policy with observable drops is preferable to
  blocking supervision.

## Validation

Executed on Windows from the clean reference commit:

| Command | Result |
|---|---|
| `dotnet test tests/NekoLib.Pipes.Tests/Unit/NekoLib.Pipes.Tests.Unit.csproj -f net481 --no-restore -m:1` | 25 passed, 0 failed, 0 skipped |
| `dotnet test tests/NekoLib.Pipes.Tests/Unit/NekoLib.Pipes.Tests.Unit.csproj -f net9.0-windows --no-restore -m:1` | 25 passed, 0 failed, 0 skipped |
| `dotnet test tests/NekoLib.Watchdog.Tests/Unit/NekoLib.Watchdog.Tests.Unit.csproj -f net481 --no-restore -m:1` | 81 passed, 0 failed, 0 skipped |
| `dotnet test tests/NekoLib.Watchdog.Tests/Unit/NekoLib.Watchdog.Tests.Unit.csproj -f net9.0-windows --no-restore -m:1` | passed; the same reference commit's full solution run recorded 81 tests on this target |

The source build for this reference commit previously completed with zero
errors and no new normalized warning identity. No cross-user account was
created and no ACL mutation was performed during this review. Security reach
is therefore derived from the constructors plus Microsoft's documented Windows
default descriptor; it is not presented as a separate penetration test.

## Review disposition

The historical hardening leads are current, but the review sharpened them:

- ACL/security is not merely defense in depth because the event channel is
  read-only to clients and the Windows default grants broad read access;
- bounded subscriber queues are also the natural single-writer fix for
  concurrent frame integrity;
- graceful drain requires ownership of tasks and streams, not a longer sleep;
- Watchdog authorization cannot be decided inside generic Pipes until the
  same-user threat model is accepted or rejected.

The E5 re-verification itself is complete. Implementation remains gated on the
decisions above.

## Reconciliation — 2026-08-08

The proposed bounded disposition package was accepted and promoted to
[`TODO.md`](../../../../TODO.md) Phase E5. The accepted contract and implementation
direction are:

- generic Pipes is a local, same-machine transport for cooperative callers and
  is not an authorization boundary;
- Watchdog RPC and event endpoints will enforce a current-Windows-user boundary
  on both targets, while hostile processes already running as that same user
  remain outside the Phase E threat model;
- event delivery will use bounded per-subscriber single-writer queues with an
  explicit, observable drop-or-disconnect policy;
- server shutdown will own admitted tasks and streams, cancel and close them,
  and perform a bounded drain; the obsolete raw log server must have truthful
  shutdown while it remains public;
- wire errors will be sanitized and focused malformed-frame coverage added.

This decision does not claim privilege separation from deterministic names,
the attach token, or current-user restriction. It does not authorize session
authentication, replay infrastructure, remote administration, sender-selected
types, Instrumentation, or TestControl. Those alternatives remain rejected for
Phase E and require a separately accepted threat-model change.
