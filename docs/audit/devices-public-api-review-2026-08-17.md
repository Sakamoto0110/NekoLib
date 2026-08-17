# Devices Public API Review — 2026-08-17

**Kind:** audit

**Lifecycle:** current

**Subject:** F1-DEV compiled public surface, `HardwareEngine` orchestration and
operation boundaries, transport and protocol extension contracts, configuration
ownership, timeout and cancellation semantics, raw-byte and encoding
boundaries, disposal, target parity, and documentation ownership

**Status:** review complete; dispositions proposed and awaiting the consolidated
F1 decision gate

**Reference date:** 2026-08-17

**Reference commit:** `a6af985245180bf1d5aa4581dbeb3352fee3e885`

**Last reconciliation:** 2026-08-17 — DEV-01 remedy revised at the decision gate

**Current state:** [`TODO.md`](../../TODO.md) F1-DEV

## Baseline and authority

This review covers committed `HEAD` on branch
`phase-e/sqlserver-and-orchestration`. The reviewed product source is unchanged
from `89f05b667be10104e8ef966ac9bebba7b7f13a23`; the four commits in between
added the F1-DIAG, F1-WIN, F1-HTTP, and F1-MVVM review artifacts and their index
entries. The worktree and index were clean before this artifact was added, the
branch was 27 commits ahead of `origin/phase-e/sqlserver-and-orchestration`, and
nothing was pushed.

The review followed
[`.agents/skills/nekolib-devices/SKILL.md`](../../.agents/skills/nekolib-devices/SKILL.md)
throughout. The reviewed authority is the `NekoLib.Devices` project, all eight of
its source files, its project file, the two assembly-derived manifests under
[`eng/public-api/NekoLib.Devices/`](../../eng/public-api/NekoLib.Devices), the
dual-target tests, the
[public API and release policy](../public-api-release-policy.md), and the
[com0com scenario](../../runtime_tests/Devices/Com0Com/README.md) as source only.

[`devices-first-pass.md`](devices-first-pass.md) is materially stale, as the
campaign brief states. Every one of its four remaining review items was
reverified against current source and all four are closed — see the
reverification section below.

This review changes no product source, test, API baseline, package, changelog,
migration guide, or roadmap item.

## Scope

Included:

- all 18 compiled public type declarations and their 121 public and protected
  member declarations, on both target frameworks;
- `HardwareEngine` orchestration, operation serialization, and failure model;
- initialization, open/close, timeout, cancellation, and disposal lifecycle;
- transport mutation and configuration ownership while operations are active;
- `ICommTransport` ownership and the viability of custom implementations;
- `IHardwareProtocol` ownership and protocol-specific semantics;
- raw byte preservation, response parsing, and the failure model;
- logging delegates without a Logging dependency;
- `SerialConfig` validation and defaults;
- `SerialCommTransport` ownership, `ReadLine`, quiet period, and timeouts;
- stream reads, partial chunks, late responses, and operation boundaries;
- `TcpCommTransport` and `NamedPipeCommTransport` endpoint ownership;
- `StreamCommTransport` disposal and reconnect behavior;
- public versus internal helpers, model mutability, nullable annotations, and
  target parity.

Excluded:

- implementing any recommendation, editing product source or tests, or updating
  an accepted API manifest;
- adding `NekoLib.Core`, `NekoLib.Pipes`, or any other project reference;
- unfreezing Inspection or broad observability;
- creating a general forwarding facade;
- modifying any external `PcbEmulator`;
- launching the com0com scenario. It was **not** built and **not** run, and no
  real UART, electrical, or physical-serial claim appears anywhere below.

## Package, dependency, and ownership boundary

`NekoLib.Devices` targets `net481;net9.0`, enables `Nullable`, enables
`ImplicitUsings`, declares `NETFRAMEWORK`/`NET_9` **but not `NEKOLIB`**, and has
no project reference
([`NekoLib.Devices.csproj`](../../src/Devices/NekoLib.Devices/NekoLib.Devices.csproj)).
It takes `System.IO.Ports 9.0.0` on `net9.0` and
`Microsoft.Bcl.AsyncInterfaces 10.0.1` on `net481`. There is **no conditional
compilation anywhere**, so the two declared constants are currently unused and
both targets compile the same text.

`System.IO.Ports` is a **public** dependency on `net9.0`, not an implementation
detail: `SerialConfig` exposes `Parity`, `StopBits`, and `Handshake` fields, so
the package's compiled surface is bound to that package. That is a deliberate
consequence of keeping one `SerialConfig` shape for every transport, and this
review recommends keeping it — but it belongs in documentation.

The intended ownership split, verified against source, is:

- the **consumer** owns transport construction, endpoint selection, the
  `HardwareEngine` lifetime, and transport disposal;
- **`HardwareEngine`** owns only orchestration: applying configuration, opening
  on demand, serializing complete transactions, writing, reading, delegating
  parsing, and logging;
- **protocols** own frame construction and response interpretation;
- **transports** own connection, buffering, read, write, timeout, and disposal
  behavior.

That split is respected by the code. The findings below are about contracts
inside it, not about the split itself.

## Compiled-surface inventory and recommended classification

**18 public types across four namespaces and 121 public or protected member
declarations, including 11 enum values.** Both manifests are identical apart
from the `TargetFramework` assembly attribute.

| Namespace / type | Kind | Recommended class |
|---|---|---|
| `Abstractions.Checksum` | static helper | Stable candidate; null handling corrected |
| `Abstractions.ControllerModel` | enum, 7 values | Stable candidate |
| `Abstractions.HardwareLogHandler` | delegate | Stable candidate |
| `Abstractions.HardwareOperation` | class, 2 public fields | Stable candidate |
| `Abstractions.HardwareResponse` | class, 7 public fields | Stable candidate + additive |
| `Abstractions.IHardwareProtocol` | interface, 4 members | Stable candidate |
| `Abstractions.IProtocolWithLogging` | interface, 1 member | Stable candidate |
| `Abstractions.LogLevel` | enum, 4 values | Stable candidate |
| `Abstractions.LogUtil` | static helper | Stable candidate |
| `Abstractions.SerialConfig` | class, 11 public fields | Stable candidate |
| `Engine.HardwareEngine` | sealed | Stable candidate |
| `Protocols.HardwareProtocol` | abstract, 1 member | **Proposed removal** |
| `Protocols.ProtocolRaw` | sealed | Stable candidate |
| `Transport.ICommTransport` | interface, 13 members | Stable candidate; nullability corrected |
| `Transport.StreamCommTransport` | abstract, real extension seam | Stable candidate |
| `Transport.SerialCommTransport` | sealed | Stable candidate |
| `Transport.TcpCommTransport` | sealed | Stable candidate |
| `Transport.NamedPipeCommTransport` | sealed | Stable candidate |

The genuine extension seams are `ICommTransport`, `IHardwareProtocol`,
`IProtocolWithLogging`, and `StreamCommTransport`'s two `protected abstract`
members. Unlike the HTTP endpoint hierarchy, `StreamCommTransport` is a **real**
public extension point: an external assembly can implement `NormalizeEndpoint`
and `ConnectStream` and inherit the whole buffered-pump implementation. That is
worth stating explicitly and preserving.

Nothing is recommended for the experimental class. One removal and one additive
field are proposed; everything else is behavioral, annotation, or documentary.

## Downstream usage

- `tests/NekoLib.Devices.Tests/Unit/` — 40 tests per target, using in-memory
  fakes plus **real loopback TCP and in-process named pipes**.
- `runtime_tests/Devices/Com0Com/` — the versioned virtual-COM scenario, read as
  source only.

`Checksum.Sum` and `Checksum.Xor` have **no** caller anywhere in the repository.
`HardwareProtocol` has no derived type anywhere. Per the release policy neither
fact alone justifies removal; the `HardwareProtocol` recommendation below rests
on the type being non-functional, not on being unused.

## Observed facts, risks, and recommended dispositions

Findings marked *probe-confirmed* were reproduced with a disposable dual-target
console probe built against the `NekoLib.Devices` project reference and run on
**both** `net481` and `net9.0`, using a real loopback TCP listener and an
in-memory fake transport. Every result was identical on the two targets. **No
serial port, virtual COM port, or physical device was involved.**

### DEV-01 — A late reply to a timed-out operation is delivered as the next operation's success

**Confirmed, probe-confirmed on both targets. This is the most serious finding in
the module.**

`StreamCommTransport` runs a background receive pump into a shared
`_receiveBuffer`
([`StreamCommTransport.cs:449`](../../src/Devices/NekoLib.Devices/Core/Transport/StreamCommTransport.cs#L449)).
That correctly prevents an orphaned stream read from lingering — the design
intent recorded in the class comment. But the buffer is only cleared on `Open`
and `StopConnection`
([`:432`](../../src/Devices/NekoLib.Devices/Core/Transport/StreamCommTransport.cs#L432),
[`:514`](../../src/Devices/NekoLib.Devices/Core/Transport/StreamCommTransport.cs#L514)),
and `HardwareEngine.ExecuteCore` never drains it before writing a new command
([`HardwareEngine.cs:183`](../../src/Devices/NekoLib.Devices/Core/Engine/HardwareEngine.cs#L183)).

Probe, over a real loopback TCP connection: operation 1 is given a 200 ms budget
and the peer answers after 600 ms; operation 2 is then issued and is never
answered.

```text
op1 (200 ms budget)  -> Success=False Status=NoResponse
op2 (never answered) -> Success=True  Status=Ok RawText='LATE-REPLY-TO-OP1'
op2 received op1's late reply: True
```

Operation 2 reports **success** carrying operation 1's payload. The transport
kept its promise; the *data* still crossed the operation boundary, and the engine
had no way to tell. For a locker or dispensing controller this is a
wrong-command-succeeded hazard, not a cosmetic issue.

`SerialCommTransport` has the same exposure by a different route: unread bytes
stay in the `SerialPort` receive buffer and the next `ReadAll` returns them
([`SerialCommTransport.cs:257`](../../src/Devices/NekoLib.Devices/Core/Transport/SerialCommTransport.cs#L257)).
That path was **not** probed, because it needs a real or virtual serial port.

**Recommended disposition:** make the operation boundary explicit and
enforceable, without changing the transport-neutral contract:

1. document the current contract precisely — a timed-out operation leaves the
   transport in an indeterminate receive state, and close/reopen is the only
   guaranteed clean boundary today (`Open` clears the buffer);
2. give `HardwareEngine` a documented, **opt-in** pre-write drain so a
   request/response consumer can require a clean boundary per operation, and
   leave it off by default so protocols that receive unsolicited data are not
   silently broken.

The opt-in switch is the one place a new option is justified in this module. If
the decision gate prefers zero new surface, documentation plus the close/reopen
remedy is the minimum acceptable outcome — but then the hazard remains real and
undefended.

**Rejected alternative:** draining unconditionally. It would silently discard
push/notification traffic that a custom protocol may legitimately expect, and
nothing in the repository establishes that every device is strictly half-duplex
request/response.

### DEV-02 — `HardwareEngine` mutates the protocol's own configuration object

**Confirmed, probe-confirmed on both targets.** `ProtocolRaw.PortConfig` returns
the **live** internal instance
([`ProtocolRaw.cs:92`](../../src/Devices/NekoLib.Devices/Core/Protocols/ProtocolRaw.cs#L92)),
`ExecuteCore` passes it straight to `_transport.Configure(cfg)`
([`HardwareEngine.cs:155`](../../src/Devices/NekoLib.Devices/Core/Engine/HardwareEngine.cs#L155)),
and both shipped transports **write back** into the caller's object when the
config has no endpoint
([`SerialCommTransport.cs:115`](../../src/Devices/NekoLib.Devices/Core/Transport/SerialCommTransport.cs#L115),
[`StreamCommTransport.cs:121`](../../src/Devices/NekoLib.Devices/Core/Transport/StreamCommTransport.cs#L121)).

Probe, a `ProtocolRaw` constructed with no `PortName`:

```text
protocol.PortConfig.PortName before='<null>' after='tcp://127.0.0.1:9'
transport.Configure received the protocol's live instance: True
```

A single `SendAsync` permanently rewrote the protocol's configuration. A protocol
shared between two transports, or reused after an explicit-endpoint call, is
therefore not the object its author constructed. `StreamCommTransport` does clone
the config into its own state, but only *after* writing back into the caller's
instance.

**Recommended disposition:** stop writing into a caller-owned object. Transports
should read the supplied config and report their resolved endpoint through
`PortInfo`/`PortName` — both of which already exist for exactly this purpose —
instead of mutating the argument. `HardwareEngine` should pass a copy of
`_protocol.PortConfig` to `Configure`. `IHardwareProtocol.PortConfig` should be
documented as protocol-owned and treated as read-only by everything else.

This is a behavioral change with no compiled-surface change. A consumer that
today relies on reading its endpoint back out of the config object must read
`transport.PortName` instead; that is the migration note.

### DEV-03 — Every non-cancellation failure collapses into an indistinguishable failed response

**Confirmed, probe-confirmed on both targets.** `ExecuteCore` catches everything
except `OperationCanceledException` and returns
`new HardwareResponse { Success = false, Status = ex.Message }`
([`HardwareEngine.cs:213`](../../src/Devices/NekoLib.Devices/Core/Engine/HardwareEngine.cs#L213)).

```text
ObjectDisposedException     -> Success=False Status='Cannot access a disposed object...'
ArgumentOutOfRangeException -> Success=False Status='Specified argument was out of the range...'
TimeoutException            -> Success=False Status='device silent'
```

Cancellation is correctly preserved and rethrown — the skill's invariant holds.
Everything else loses its type, its stack, and its inner exception; only
`ex.Message` survives, in the same `Status` field a protocol uses for `"Ok"`,
`"NoResponse"`, and its own vocabulary. A disposed transport, a caller bug, and a
silent device are all one outcome.

The fail-soft direction is right for an unattended terminal: a device fault must
not take down the shell. The evidence loss is not.

**Recommended disposition:** keep fail-soft, restore the evidence. Add an
additive public `Exception Failure` field to `HardwareResponse` — additive is
consistent with the type's existing public-field shape and breaks nothing — set
it on the engine's catch path, and document that `Status` remains the
protocol-facing string while `Failure` carries the transport or engine
exception when one occurred.

**Rejected alternative:** rethrowing programming errors such as
`ObjectDisposedException` and `ArgumentException`. It is defensible in the
abstract, but it converts a currently-recoverable path into a process-visible
throw for every existing consumer, which is a behavioral break with no requester.

### DEV-04 — The central transport interface lies about nullability

**Confirmed, probe-confirmed on both targets.** The project is
`Nullable=enable`, yet `ICommTransport.ReadLine`, `ReadExact`, and `ReadAll`
declare non-nullable returns
([`ISerialCommTransport.cs:80`](../../src/Devices/NekoLib.Devices/Core/Transport/ISerialCommTransport.cs#L80))
while their own XML documentation says they return `null` on timeout — and both
shipped implementations do exactly that, using `return null!;` to silence the
compiler
([`StreamCommTransport.cs:273`](../../src/Devices/NekoLib.Devices/Core/Transport/StreamCommTransport.cs#L273),
[`:317`](../../src/Devices/NekoLib.Devices/Core/Transport/StreamCommTransport.cs#L317),
[`:378`](../../src/Devices/NekoLib.Devices/Core/Transport/StreamCommTransport.cs#L378)).

```text
ICommTransport.ReadLine return type 'Task`1', nullable metadata: <none>
  — yet both implementations return null on timeout
```

Timeout is the *normal* outcome these methods are designed to express, and it is
the one the annotation denies. A nullable-enabled consumer gets no warning and no
null check, on the exact path a device is silent.

The module is also annotated inconsistently: `ProtocolRaw(SerialConfig?,
Encoding?)` carries annotations while `SerialCommTransport(string portName =
null)` does not, and `StreamCommTransport.Log` is initialized to `null!`
([`StreamCommTransport.cs:36`](../../src/Devices/NekoLib.Devices/Core/Transport/StreamCommTransport.cs#L36)).

**Recommended disposition:** annotate the three read methods as `Task<string?>`
and `Task<byte[]?>` on the interface and both implementations, annotate the
`Log` properties and the optional `portName` parameter, and remove the `null!`
suppressions. Both manifests change; the change is binary-compatible and
source-compatible in the safe direction — it only adds warnings where a consumer
was already at risk of a null reference. This is the same class of correction as
the F1-MVVM nullability finding.

### DEV-05 — `HardwareProtocol` is a public abstract type with no function

**Confirmed.** `HardwareProtocol` declares one member,
`public virtual string Template { get; protected set; }`
([`ProtocolRaw.cs:12`](../../src/Devices/NekoLib.Devices/Core/Protocols/ProtocolRaw.cs#L12)).
Nothing derives from it, `ProtocolRaw` implements the interfaces directly, and
`Template` is never read, written, or referenced anywhere in the repository. It
participates in no contract: `HardwareEngine` accepts `IHardwareProtocol`, not
this base.

**Recommended disposition:** remove it. Protocol authors implement
`IHardwareProtocol` and optionally `IProtocolWithLogging`; a base class that
supplies nothing is a false seam that a stable 1.0 would be committing to
forever. This is the only proposed removal in F1-DEV and is source-breaking only
for an external type that derives from it — which would gain nothing today.
Migration: implement `IHardwareProtocol` directly.

**Rejected alternative:** keeping it and documenting `Template`. There is nothing
to document; the property has no defined meaning.

### DEV-06 — `HardwareEngine` silently takes over transport and protocol logging

**Confirmed by construction.** The constructor overwrites `_transport.Log` and,
for a protocol implementing `IProtocolWithLogging`, `p.Log`
([`HardwareEngine.cs:70`](../../src/Devices/NekoLib.Devices/Core/Engine/HardwareEngine.cs#L70)),
and the `Log` setter overwrites both again
([`:43`](../../src/Devices/NekoLib.Devices/Core/Engine/HardwareEngine.cs#L43)).

A consumer who wired `transport.Log` before constructing an engine loses it
without any signal, and a consumer who sets `transport.Log` after construction
has it silently replaced the next time `engine.Log` is assigned.

The design goal — one logging delegate for the whole stack, with no Logging
dependency — is right, and the `HardwareLogHandler` delegate correctly keeps
`NekoLib.Core` out of the graph. Only the silent takeover is undocumented.

**Recommended disposition:** document that constructing a `HardwareEngine`
transfers ownership of `ICommTransport.Log` and `IProtocolWithLogging.Log` to the
engine, and that a consumer wanting separate transport logging must not route it
through an engine-owned transport. No code change: making the takeover
conditional would produce two competing logging paths.

### DEV-07 — Disposal is asymmetric between the two shipped transports

**Confirmed.** `StreamCommTransport.Dispose` acquires `_gate` before tearing down
([`StreamCommTransport.cs:399`](../../src/Devices/NekoLib.Devices/Core/Transport/StreamCommTransport.cs#L399)),
so it cannot race a read or write in flight. `SerialCommTransport.Dispose` does
**not** take `_gate`
([`SerialCommTransport.cs:409`](../../src/Devices/NekoLib.Devices/Core/Transport/SerialCommTransport.cs#L409));
it closes and disposes the `SerialPort` while another thread may be inside the
`Task.Run` polling loop reading `_port.BytesToRead`, which then throws
`ObjectDisposedException` from a background task.

Both types also dispose `_gate` immediately after releasing it, so a thread
already waiting on the semaphore observes `ObjectDisposedException` rather than a
clean failure.

**Recommended disposition:** make `SerialCommTransport.Dispose` acquire `_gate`
the way `StreamCommTransport.Dispose` does, so disposal under load is consistent
across transports, and document that disposal is terminal and must not race
in-flight operations. No compiled-surface change. This was **not** probed —
reproducing it needs a real or virtual serial port — and rests on source
symmetry with the stream transport, which was probed indirectly through the
existing dual-target tests.

### DEV-08 — `SerialCommTransport.PortName` is settable while the interface's is not

**Confirmed.** `ICommTransport.PortName` is get-only
([`ISerialCommTransport.cs:44`](../../src/Devices/NekoLib.Devices/Core/Transport/ISerialCommTransport.cs#L44))
and `StreamCommTransport.PortName` is get-only, but `SerialCommTransport.PortName`
adds a public setter that also latches `_hasExplicitPortName`
([`SerialCommTransport.cs:31`](../../src/Devices/NekoLib.Devices/Core/Transport/SerialCommTransport.cs#L31)).
Three implementations of one contract therefore disagree on mutability, and code
written against `ICommTransport` cannot use the setter at all.

**Recommended disposition:** document it as a serial-specific convenience that
mirrors `SerialPort.PortName`, and keep it. Removing it is a breaking change for
a member that has a coherent meaning on that one transport; promoting it to the
interface would force every custom transport to accept mid-life retargeting,
which the stream transports deliberately reject.

### DEV-09 — `Checksum` handles null inconsistently and has no caller

**Confirmed, probe-confirmed on both targets.**

```text
Checksum.Sum(null) -> ArgumentNullException     (from LINQ)
Checksum.Xor(null) -> NullReferenceException    (from foreach)
```

Two sibling helpers with `params byte[]` signatures fail differently on the same
input, and neither validates deliberately
([`ControllerModel.cs:97`](../../src/Devices/NekoLib.Devices/Core/Abstractions/ControllerModel.cs#L97)).

Neither is called anywhere in the repository. They are, however, exactly the kind
of helper a third-party protocol implementation needs, which is a legitimate
reason to keep them public.

**Recommended disposition:** keep both, make them throw
`ArgumentNullException(nameof(bytes))` consistently, and document them as
protocol-authoring helpers. No compiled-surface change.

### DEV-10 — Raw bytes are preserved; text decoding on the binary path is lossy

**Confirmed.** `ProtocolRaw.ParseResponse` returns the reply array unchanged as
`RawBytes` — the skill's raw-byte invariant holds — and also sets
`RawText = _textEncoding.GetString(reply)`
([`ProtocolRaw.cs:166`](../../src/Devices/NekoLib.Devices/Core/Protocols/ProtocolRaw.cs#L166)).
With the ASCII default, every byte above `0x7F` decodes to `?`, so `RawText` on a
binary reply is lossy and must never be used to reconstruct the payload.

Two related behaviors are also undocumented: `RawBytes` takes precedence when an
operation supplies both arguments
([`ProtocolRaw.cs:121`](../../src/Devices/NekoLib.Devices/Core/Protocols/ProtocolRaw.cs#L121)),
and `ICommTransport.Write(string)` is hardcoded to ASCII on both transports
regardless of `ProtocolRaw.TextEncoding` — which is harmless through the engine,
because the engine only ever calls `Write(byte[])`, but is a trap for direct
transport users.

**Recommended disposition:** document all three. No code change: `RawText` is
useful for ASCII devices, and `Write(string)` is documented as the ASCII
convenience path.

### DEV-11 — Public mutable fields are the module's model shape

**Confirmed.** `SerialConfig`, `HardwareOperation`, and `HardwareResponse` expose
public mutable **fields**, not properties
([`ControllerModel.cs:120`](../../src/Devices/NekoLib.Devices/Core/Abstractions/ControllerModel.cs#L120)).
`HardwareOperation.Args` is a mutable dictionary initialized inline and never
copied.

Converting fields to properties is binary-breaking for every compiled consumer
and buys little here: these are transfer objects, not invariant-bearing types.

**Recommended disposition:** keep the fields, and document that all three models
are mutable, uncopied, caller-owned data carriers — combined with DEV-02, which
removes the module's own habit of writing into them. Do **not** convert to
properties in F1.

### DEV-12 — Timeout, cancellation, and quiet-period semantics are sound and worth pinning

**Confirmed; recorded as positives so the dispositions above do not disturb
them.**

- `OperationCanceledException` is rethrown, never converted into a failed
  response
  ([`HardwareEngine.cs:208`](../../src/Devices/NekoLib.Devices/Core/Engine/HardwareEngine.cs#L208)) —
  the skill's cancellation invariant.
- Complete transactions are serialized through a `SemaphoreSlim`, and the gate is
  released on every failure path
  ([`:130`](../../src/Devices/NekoLib.Devices/Core/Engine/HardwareEngine.cs#L130)).
- `ReadAll`'s quiet period only starts after the first byte arrives, so a slow
  device is not cut off before it answers
  ([`StreamCommTransport.cs:361`](../../src/Devices/NekoLib.Devices/Core/Transport/StreamCommTransport.cs#L361)).
- `ReadExact` returns null rather than a partial buffer.
- `TcpCommTransport` and `NamedPipeCommTransport` canonicalize and validate their
  endpoints before connecting, reject malformed forms with clear messages, and
  dispose the half-built client on every failure path.
- `StreamCommTransport` bounds receive-pump shutdown and logs when the pump
  outlives it rather than hanging.
- The engine refuses to run against a transport already open on a different
  endpoint.

**Recommended disposition:** state each of these in the new module reference and
keep the existing regressions.

### DEV-13 — `SerialCommTransport` reads occupy a pool thread for the whole budget

**Confirmed by construction.** All three serial reads run a polling loop inside
`Task.Run` with `Thread.Sleep(5)`
([`SerialCommTransport.cs:248`](../../src/Devices/NekoLib.Devices/Core/Transport/SerialCommTransport.cs#L248)),
so a `SendAsync(op, 2000)` holds a thread-pool thread for up to two seconds and
cancellation is observed with up to ~5 ms latency. `StreamCommTransport` avoids
this with its pump.

For one or two devices per terminal this is unremarkable; documenting it lets a
consumer with many devices make an informed choice.

**Recommended disposition:** document the difference between the polling serial
implementation and the pumped stream implementation. No code change: rewriting
the serial path onto `SerialPort.BaseStream` is a substantial behavioral change
that only real-hardware evidence should justify, and this campaign has none.

### DEV-14 — Devices has no documentation owner

**Confirmed.** There is no `src/Devices/NekoLib.Devices/README.md` and
[`docs/README.md`](../README.md) registers no owner. Every contract this review
had to derive from source — the operation boundary, configuration ownership, the
failure model, logging ownership, encoding boundaries, disposal, endpoint forms,
and the extension seams — has no owner anywhere.

This is the largest undocumented public surface among the five modules in this
campaign: 18 types and 121 member declarations, against Mvvm's 3 and 15.

**Recommended disposition:** add `src/Devices/NekoLib.Devices/README.md` and
register it in the documentation index and the `AGENTS.md` navigation table. It
must state plainly that unit and loopback coverage is not serial evidence, and
point at the com0com scenario for that.

### DEV-15 — Coverage is strong on transports and thin on the engine's contracts

**Confirmed.** 40 tests per target cover engine construction, ordering,
explicit-port routing, reopen rules, the failed-response path, cancellation,
serialization of concurrent operations, `ProtocolRaw` build/parse, full
`SerialConfig` validation, disposed-object behavior, and — over **real loopback
TCP and in-process named pipes** — fragmented exchanges, quiet-period behavior,
read timeouts without closing the connection, and endpoint validation.

Nothing covers a late reply crossing an operation boundary (DEV-01),
configuration write-back (DEV-02), failure-evidence preservation (DEV-03), the
nullability contract (DEV-04), `Checksum` null handling (DEV-09), or disposal
racing an in-flight serial read (DEV-07).

**Recommended disposition:** add focused dual-target regressions for every
accepted disposition. DEV-01 and DEV-02 are both reproducible with the existing
loopback-TCP fixture style, so they need no new infrastructure and no hardware.

## Reverification of the historical audit

[`devices-first-pass.md`](devices-first-pass.md) lists four remaining review
items. All four are **closed** in current source:

1. **`ReadLine` timeout behavior** — both transports return `null`, not an empty
   string. Closed. DEV-04 is about annotating that null, not about restoring it.
2. **Explicit `SerialConfig` validation** — `ValidateSerialConfig` checks baud
   rate, data bits, stop bits, handshake, and both timeouts
   ([`SerialCommTransport.cs:473`](../../src/Devices/NekoLib.Devices/Core/Transport/SerialCommTransport.cs#L473));
   `ValidateStreamConfig` checks the timeout fields that apply to a stream.
   Closed.
3. **`ThrowIfDisposed()`** — present in both transports and exercised by
   dual-target tests. Closed.
4. **`ProtocolRaw.RawText` encoding** — ASCII remains the default and an explicit
   `Encoding` overload exists. Closed; DEV-10 only adds the missing documentation.

The audit's 2026-08-01 reconciliation already recorded these outcomes. Nothing in
it remains open, and none of the findings above descends from it.

## Target parity

Both manifests are identical apart from the `TargetFramework` assembly
attribute, and the source contains no conditional compilation, so both targets
compile the same text. Every probe in this review produced identical output on
`net481` and `net9.0`, including the late-reply hazard, the configuration
write-back, the failure model, and the `Checksum` asymmetry.

The two target-specific project inputs are `System.IO.Ports 9.0.0` on `net9.0`
and `Microsoft.Bcl.AsyncInterfaces 10.0.1` on `net481` — packaging requirements
that produce the same API rather than target differences. The declared
`NETFRAMEWORK` and `NET_9` constants are currently unused; that is worth noting
in documentation but is not a defect.

## Likely migration cost

| Disposition | Compiled surface | Behavior | Consumer action |
|---|---|---|---|
| DEV-01 opt-in drain | **additive** | off by default | opt in for a guaranteed clean boundary |
| DEV-02 stop mutating caller config | none | config object no longer rewritten | read `transport.PortName` instead of the config |
| DEV-03 `HardwareResponse.Failure` | **additive field** | none | opt in to the new evidence |
| DEV-04 nullability | **both manifests change**; binary-compatible | none | add null checks the contract now demands |
| DEV-05 remove `HardwareProtocol` | **breaking** | none | implement `IHardwareProtocol` directly |
| DEV-07 gate serial disposal | none | disposal waits for in-flight work | none |
| DEV-09 `Checksum` null | none | consistent `ArgumentNullException` | none |
| DEV-06, DEV-08, DEV-10 to DEV-15 | none | none | none |

`docs/migrations/f1-devices.md` is required: DEV-05 removes a public type and
DEV-02/DEV-04 change documented behavior and annotations.

## Core, dependency, and freeze conflicts

**None.** No recommendation adds `NekoLib.Core`, `NekoLib.Pipes`, or any other
project reference; the no-project-reference graph is preserved exactly. Logging
stays on the `HardwareLogHandler` delegate, so no Logging or Inspection contract
is needed and the B4 Inspection freeze is untouched. No frozen module is
modified, and no new NuGet dependency is proposed.

The `HardwareEngine.SendAsync` transaction is listed in `TODO.md` as an existing
Inspection seam. This review deliberately does **not** instrument it.

## Rejected alternatives

- **Adding `NekoLib.Core` for logging convenience.** Rejected: the delegate
  already satisfies the requirement, and the skill is explicit that convenience
  is not a reason.
- **Adding `NekoLib.Pipes` for the named-pipe transport.** Rejected:
  `System.IO.Pipes` gives a raw byte stream, which is exactly what the transport
  needs; the Pipes RPC/pub-sub protocol is a different thing.
- **A general forwarding facade over the engine, transports, and protocols.**
  Out of bounds by the campaign and it would hide the ownership boundaries the
  module depends on.
- **Draining the receive buffer unconditionally before every write.** Rejected in
  favour of the opt-in in DEV-01, because it would silently discard unsolicited
  device traffic.
- **Rethrowing programming errors from `HardwareEngine`.** Rejected: a behavioral
  break that removes the fail-soft property an unattended terminal wants.
- **Converting the public model fields to properties.** Rejected: binary-breaking
  for transfer objects that carry no invariants.
- **Promoting `PortName`'s setter to `ICommTransport`, or removing it from
  `SerialCommTransport`.** Rejected in both directions: the first forces
  mid-life retargeting onto every custom transport, the second breaks a coherent
  serial-specific member.
- **Making `ICommTransport` extend `IDisposable`.** Rejected: it would break
  every existing custom transport implementation, which is precisely what the
  skill says to protect. Lifetime ownership is documented instead.
- **Rewriting `SerialCommTransport` onto `SerialPort.BaseStream` with a receive
  pump.** Deferred: a substantial behavioral change that only real-hardware
  evidence should justify, and this campaign produced none.
- **Instrumenting the `SendAsync` transaction with Inspection.** Out of bounds;
  the freeze stands.
- **Treating the fake and loopback probes as serial evidence.** Explicitly
  refused throughout.

## Proposed implementation block after acceptance

If the dispositions are accepted, one narrow commit should:

1. record the accepted decisions in `TODO.md` F1-DEV with package-pending
   evidence and leave the checkbox unchecked;
2. implement DEV-01 (opt-in drain), DEV-02, DEV-03, DEV-04, DEV-05, DEV-07, and
   DEV-09 in `src/Devices/NekoLib.Devices/`;
3. add the focused dual-target regressions described in DEV-15, using the
   existing loopback-TCP fixture style and no hardware;
4. add `src/Devices/NekoLib.Devices/README.md` covering DEV-06, DEV-08,
   DEV-10 to DEV-14 and the DEV-12 positives;
5. add `docs/migrations/f1-devices.md`;
6. update `CHANGELOG.md`, `docs/README.md`, and the `AGENTS.md` navigation
   table;
7. update both `NekoLib.Devices` manifests through a scoped
   `verify-public-api.ps1 -UpdateBaseline -PackageId NekoLib.Devices`;
8. **build** the com0com scenario if the accepted changes touch anything it
   compiles against, and report that as build-only evidence;
9. append a reconciliation section here without rewriting the snapshot above.

## Review validation

Commands run on Windows at the reference commit:

```text
dotnet test tests/NekoLib.Devices.Tests/Unit/NekoLib.Devices.Tests.Unit.csproj
  net481:  40 passed, 0 failed, 0 skipped
  net9.0:  40 passed, 0 failed, 0 skipped

diff eng/public-api/NekoLib.Devices/net481.approved.txt
     eng/public-api/NekoLib.Devices/net9.0.approved.txt
  TargetFramework assembly attribute only

git grep '#if|#else|#endif' -- src/Devices
  no match (no conditional compilation on either target)

git grep 'Checksum\.|HardwareProtocol' -- src tests runtime_tests
  no caller for Checksum; no type derives from HardwareProtocol
```

A disposable dual-target console probe was built against the `NekoLib.Devices`
project reference and run on both `net481` and `net9.0` outside the repository,
then deleted. It used a real loopback `TcpListener` and an in-memory fake
transport to reproduce DEV-01, DEV-02, DEV-03, DEV-04, and DEV-09; every result
was identical on both targets. No repository file changed.

## Residual validation limits

- **No serial port was opened.** No physical UART, no virtual COM pair, no
  com0com scenario. The com0com scenario was **not built and not run**, as the
  campaign requires. Nothing in this review is serial, electrical, or
  hardware evidence, and the fake and loopback probes are explicitly not a
  substitute.
- DEV-01 was reproduced over TCP. The equivalent `SerialCommTransport` exposure
  is asserted from source and is **unverified**.
- DEV-07 was **not** reproduced; it rests on the source asymmetry between the two
  `Dispose` implementations.
- DEV-13's thread-occupancy claim is a source reading, not a measurement.
- No package was produced and no package-consumer probe was run.
- The full solution was not rebuilt or tested for this review.
- The `PcbEmulator` was not inspected, run, or modified.

## Decision gate

DEV-01 through DEV-05, DEV-07, and DEV-09 are recommended as accepted work.
DEV-05 is the only removal and DEV-01/DEV-03 the only additions. DEV-06,
DEV-08, and DEV-10 through DEV-14 are recommended as documentation-only, with a
new module reference. DEV-15 is recommended as test-only. The historical audit is
fully reconciled and closed. Nothing here may be implemented until the
consolidated F1 decision gate accepts or modifies these dispositions.

## Reconciliation — 2026-08-17: DEV-01 remedy revised

The observed facts, evidence, and probe results recorded above are unchanged and
remain the snapshot. This section records a revision to **the recommended remedy
for DEV-01 only**, made during the consolidated F1 decision gate after an impact
analysis that the original disposition had not performed. Nothing else in this
review is affected, and no product code was changed by this reconciliation.

### Why the original remedy was withdrawn

The original DEV-01 disposition recommended an opt-in pre-write drain in
`HardwareEngine`. Impact analysis found two defects in that proposal:

1. **It does not close the hazard.** The sequence would be
   `drain → write → read`. A late reply from operation *N-1* arriving between the
   drain and the read still satisfies operation *N*. The drain narrows the window;
   it never eliminates it. The original text claimed a stronger outcome than the
   mechanism delivers.
2. **It costs the most public surface of any option.** `ICommTransport` has no
   discard member, and adding one would break every custom transport
   implementation — which the Devices skill explicitly protects. Reusing the
   existing interface is not viable either: `ReadAll(0, 0)` drains
   `StreamCommTransport`, because `Remaining(0, …)` returns `0` after one
   `TakeAvailable`, but is a silent no-op on `SerialCommTransport`, whose loop
   condition `sw.ElapsedMilliseconds < timeoutMs` never executes at zero. A drain
   would therefore require a new optional interface plus an engine switch, and
   would still be inert for any transport that did not adopt it.

### Revised remedy — close the transport when no bytes were received

`HardwareEngine` already has the trigger and the mechanism it needs, with no new
interface:

- the engine sees `rspBytes == null` directly, which is the transport-level
  signal that nothing arrived within the budget
  ([`HardwareEngine.cs:186`](../../src/Devices/NekoLib.Devices/Core/Engine/HardwareEngine.cs#L186));
- `ICommTransport.Close()` already exists
  ([`ISerialCommTransport.cs:64`](../../src/Devices/NekoLib.Devices/Core/Transport/ISerialCommTransport.cs#L64));
- the next `SendAsync` reopens, because `ExecuteCore` opens whenever
  `!_transport.IsOpen`
  ([`HardwareEngine.cs:169`](../../src/Devices/NekoLib.Devices/Core/Engine/HardwareEngine.cs#L169));
- `StreamCommTransport.OpenCore` clears `_receiveBuffer` on every open
  ([`StreamCommTransport.cs:432`](../../src/Devices/NekoLib.Devices/Core/Transport/StreamCommTransport.cs#L432)),
  so the reopened transport starts from a verified clean boundary.

The accepted remedy is therefore a single opt-in `HardwareEngine` property,
default **off**, named for its trigger rather than for a timeout — `ReadAll`
returns `null` both when the budget expires and when the connection closed with
nothing buffered, and `"NoResponse"` is already the vocabulary `ProtocolRaw` uses
for that outcome
([`ProtocolRaw.cs:160`](../../src/Devices/NekoLib.Devices/Core/Protocols/ProtocolRaw.cs#L160)).
When enabled, the engine closes the transport after an operation that received no
bytes, so the next operation cannot inherit a late reply.

**Serial half.** `SerialCommTransport.OpenCore` only calls `_port.Open()`
([`SerialCommTransport.cs:439`](../../src/Devices/NekoLib.Devices/Core/Transport/SerialCommTransport.cs#L439));
unlike the stream transport it performs no explicit discard. To make the
close/reopen boundary symmetric, the implementation should also call
`SerialPort.DiscardInBuffer()` after a successful open. A freshly opened port
carrying no stale data is the defensible contract, and this makes the guarantee
explicit rather than dependent on OS handle semantics.

### Comparison recorded for the decision

| | Withdrawn: pre-write drain | Accepted: close on no response |
|---|---|---|
| New public surface | optional interface + member + engine property | one engine property |
| Risk to custom transports | inert unless they adopt the interface | none |
| Closes the hazard | no — narrows the window | yes, on reopen |
| Runtime cost | negligible | one reconnect per empty response |
| Unsolicited/push traffic | discarded when enabled | preserved |
| Serial coverage | needs `DiscardInBuffer` in the new interface | needs `DiscardInBuffer` on open |

### Consequences to carry into implementation

- **Default off, so the default behavior is unchanged.** Every existing consumer,
  test, and the com0com scenario are unaffected unless they opt in.
- **Enabled, a protocol with legitimate fire-and-forget commands reconnects after
  every unanswered operation.** That is the reason the switch is opt-in and must
  be documented as such.
- **Enabled, a failed reconnection changes the next operation's failure mode**
  from "no response" to the transport's connect error, surfaced through the
  DEV-03 failure evidence. This must be stated in the module reference.
- The `TcpCommTransport` and `NamedPipeCommTransport` defaults of
  `ConnectTimeout = 5000` bound that reconnection.
- No deadlock is introduced: `ReadAll` releases the transport gate before
  `ExecuteCore` would call `Close()`, and the engine's own operation gate is a
  different primitive.

### What this supersedes and what it does not

This section supersedes the DEV-01 recommended disposition, its row in the
"Likely migration cost" table, and step 2 of the proposed implementation block,
in each case only as they concern DEV-01. The DEV-01 evidence, the probe output,
the `SerialCommTransport` exposure recorded as unverified, and every other
finding stand exactly as written.

The residual limits also stand: the revised remedy's clean-boundary guarantee is
**verified only for stream transports**, where `OpenCore` clears the buffer
explicitly. The serial equivalent depends on handle semantics plus the proposed
`DiscardInBuffer` call and remains unverified, because no serial port was opened
by this review.

The rejected alternatives list gains one entry: **the opt-in pre-write drain**,
rejected for the two reasons above. Documentation-only — stating that a timed-out
operation leaves the transport in an indeterminate receive state and that
`Close()` is the caller's remedy — remains the fallback if the decision gate
declines any new public surface in this module; the hazard then stays real but
stops being invisible.
