# NekoLib.Devices Validation Requirements

**Document ID:** DEV-VALIDATION-REQUIREMENTS

**Schema version:** 1

**Kind:** reference

**Lifecycle:** current

**Subject:** evidence contract for the NekoLib.Devices boundary

**Surface:** validation-requirements

**Boundary:** devices

**Authority role:** normative

**Mutation:** authored

**Indexing:** include

The [module manifest](MANIFEST.md) owns the inherited profile list. The
requirements below specialize those profiles for what this boundary actually is:
a dual-target unframed byte-stream client that owns an operating-system endpoint,
a background receive pump, and a fail-soft orchestration layer over a
device-defined protocol. They are derived from architecture and risk, not from
the tests that happen to exist; several are `NOT_RUN` in
[`VALIDATIONS.md`](VALIDATIONS.md) and say so there.

Requirement classification and evidence status are independent. A run that did
not exercise the named boundary or evidence level does not satisfy the
requirement, however green it was.

## DEV-VALREQ-001

**Classification:** REQUIRED

**Trigger:** every release candidate and every change to source, project, target, or package settings

**Category:** build

**Boundary:** in-process

**Targets:** `net481` and `net9.0`

**Acceptance criteria:** Both target assemblies build with zero errors, no new normalized warning identity, and no `CS1591` or malformed XML-comment diagnostic, and both generated XML files are produced beside their assemblies.

**Required evidence level:** build-only

**Rationale:** The module carries pre-existing nullable warning identities that must not grow, and the packaged XML asset is part of the shipped managed deliverable. `net481` and `net9.0` resolve different packages for `IAsyncDisposable` and `SerialPort`, so one target can restore and build while the other cannot.

## DEV-VALREQ-002

**Classification:** REQUIRED

**Trigger:** every release candidate and every change to a public declaration, target, nullable annotation, default value, or package dependency

**Category:** api-compatibility

**Boundary:** in-process

**Targets:** both accepted `NekoLib.Devices` manifests

**Acceptance criteria:** Release assemblies match both accepted manifests without an automatic baseline update; any delta carries an explicit compatibility disposition and a migration entry before acceptance.

**Required evidence level:** build-only

**Rationale:** `SerialConfig` publishes `System.IO.Ports` enums, so the compiled surface is bound to a package dependency that only one target resolves externally. The nullability contract on the three read methods is metadata a source review cannot prove.

## DEV-VALREQ-003

**Classification:** REQUIRED

**Trigger:** every change to engine orchestration, protocol handling, transport behaviour, or argument validation

**Category:** focused-regression

**Boundary:** in-process

**Targets:** `net481` and `net9.0`

**Acceptance criteria:** The focused suite passes on both targets with zero failures and zero skips, and every accepted behavioural disposition retains a test that fails if the behaviour is reverted.

**Required evidence level:** automated-runtime

**Rationale:** Both targets compile identical source today, but the runtime behind `SerialPort`, sockets, and the thread pool differs, and the fail-soft failure model converts most defects into a response rather than a crash. Without a regression a reverted disposition is silent.

## DEV-VALREQ-004

**Classification:** REQUIRED

**Trigger:** every release candidate and every change to package identity, dependencies, or packaged assets

**Category:** package-consumer

**Boundary:** package-feed

**Targets:** `net481` and `net9.0` consumers

**Acceptance criteria:** An isolated `PackageReference`-only consumer restores, compiles against, and runs the public surface on both targets, and receives the package-owned XML documentation asset.

**Required evidence level:** automated-runtime

**Rationale:** The `System.IO.Ports` and `Microsoft.Bcl.AsyncInterfaces` dependencies are target-conditioned. A project reference inside the solution proves nothing about what an external consumer resolves.

## DEV-VALREQ-005

**Classification:** REQUIRED

**Trigger:** every change to `StreamCommTransport` buffering, the receive pump, or any read method

**Category:** protocol

**Boundary:** network

**Targets:** `net481` and `net9.0`

**Acceptance criteria:** Against a real peer: a response split into delayed chunks inside the quiet period is reassembled whole; `ReadLine` strips exactly its terminator and leaves the remainder buffered; `ReadExact` returns the complete length or `null` and never a partial buffer; excess bytes past a satisfied read survive for the next read.

**Required evidence level:** automated-runtime

**Rationale:** This is the boundary's core promise. Every device protocol on top of it assumes byte-for-byte framing fidelity across arbitrary chunk boundaries, and an in-memory fake cannot produce real chunking.

## DEV-VALREQ-006

**Classification:** REQUIRED

**Trigger:** every change to `HardwareEngine.ExecuteCore`, `CloseTransportOnNoResponse`, or transport open/close behaviour

**Category:** protocol

**Boundary:** network

**Targets:** `net481` and `net9.0`

**Acceptance criteria:** Against a peer that answers the first command after its budget expires and never answers the second: with the switch off, the second operation observably receives the first operation's payload; with the switch on, it does not. Both outcomes are asserted, not just the desired one.

**Required evidence level:** automated-runtime

**Rationale:** The default is a documented hazard that consumers make decisions around, so it must be pinned as deliberately as the remedy. A test that only asserted the switch-on case would let the default drift silently.

## DEV-VALREQ-007

**Classification:** REQUIRED

**Trigger:** every change to a method taking a `CancellationToken`

**Category:** focused-regression

**Boundary:** in-process

**Targets:** `net481` and `net9.0`

**Acceptance criteria:** `OperationCanceledException` propagates from `SendAsync` and from every transport read and write, is never converted into a failed `HardwareResponse`, and a token cancelled before the call fails it before any transport work begins. A pending read observes its token under both a finite and an infinite configured port timeout.

**Required evidence level:** automated-runtime

**Rationale:** Cancellation is the one failure the fail-soft model must not absorb: an unattended shell shutting down needs to distinguish "we stopped" from "the device is broken". The serial read path observes cancellation only between poll iterations, so the guarantee differs by transport and must be exercised on the real one.

## DEV-VALREQ-008

**Classification:** REQUIRED

**Trigger:** every change to open, close, reconnect, or endpoint-resolution behaviour

**Category:** runtime

**Boundary:** network

**Targets:** `net481` and `net9.0`

**Acceptance criteria:** An exchange against a departed far end ends inside its bound rather than hanging; the same endpoint serves again once the peer returns; repeated open/request/close/reopen cycles on one transport instance leak no handle; and an endpoint switch is refused while open and accepted after close.

**Required evidence level:** automated-runtime

**Rationale:** An unattended terminal is expected to survive a device power cycle without restarting the process. The endpoint is a machine-wide exclusive resource, so a leaked handle is a real operational failure rather than a tidiness concern.

## DEV-VALREQ-009

**Classification:** RECOMMENDED

**Trigger:** a consumer integrates a device that pushes unsolicited traffic, or the receive-buffer policy changes

**Category:** soak

**Boundary:** network

**Targets:** at least one target

**Acceptance criteria:** A transport left open and unread under sustained peer input records buffer growth, memory growth, and read-latency trend against a stated acceptance bound, with duration, workload, resources, and cleanup recorded per the soak evidence fields.

**Required evidence level:** automated-runtime

**Rationale:** The receive buffer is unbounded — see [`DEV-FINDING-002`](FINDINGS.md) — and the `transport` profile carries a resource-stability obligation. Classified `RECOMMENDED` because the module's intended request/response use drains the buffer every operation, so the exposure depends on a consumer's device rather than on the module.

## DEV-VALREQ-010

**Classification:** REQUIRED

**Trigger:** every change to a read path or to `ProtocolRaw` parsing

**Category:** protocol

**Boundary:** hardware

**Targets:** `net481` and `net9.0`

**Acceptance criteria:** Bytes that violate the device protocol reach the caller verbatim — neither hidden, repaired, nor silently dropped — the consumer's validator rejects them, and the next well-formed exchange succeeds on the same transport.

**Required evidence level:** automated-runtime

**Rationale:** The module deliberately owns no framing. Its correctness claim is that it does not interpret, so a transport that quietly repaired or discarded malformed input would break every protocol built on it while looking healthier.

## DEV-VALREQ-011

**Classification:** CONDITIONAL

**Trigger:** any change to `SerialCommTransport`, to the engine's read or open path, or to the documented serial contract

**Category:** runtime

**Boundary:** hardware

**Targets:** `net481` and `net9.0`

**Acceptance criteria:** The com0com scenario passes on a Windows machine with the configured virtual pairs, on **both** targets, covering configuration round-trip, the three read methods, close/reopen, cancellation under finite and infinite port timeouts, and the five peer faults.

**Required evidence level:** automated-runtime

**Rationale:** The focused suite exercises real sockets and pipes but never a serial port, so `SerialCommTransport` — the module's original and most-used transport — is otherwise covered only by configuration tests that open nothing. A virtual pair is the cheapest evidence that exercises the real Windows serial API and driver.

## DEV-VALREQ-012

**Classification:** CONDITIONAL

**Trigger:** any documentation, release note, or consumer claim about physical UART, baud, parity, framing, flow control, cabling, USB adapters, or electrical behaviour

**Category:** runtime

**Boundary:** hardware

**Targets:** `net481` and `net9.0`

**Acceptance criteria:** The claim is demonstrated against physical hardware with the wiring, adapter, and line settings recorded; a virtual pair does not satisfy it.

**Required evidence level:** automated-runtime

**Rationale:** com0com implements none of these — it carries bytes between two handles — so a passing virtual run is silent about them. This requirement exists to keep that silence explicit: today no such claim is made anywhere in this boundary, and no physical evidence exists.

## DEV-VALREQ-013

**Classification:** REQUIRED

**Trigger:** every change to either `Dispose` or `DisposeAsync`, or to gate acquisition in any transport

**Category:** focused-regression

**Boundary:** in-process

**Targets:** `net481` and `net9.0`

**Acceptance criteria:** Disposal with a read in flight returns inside a bound, ends that read, is idempotent, refuses `Close` and `Open` afterwards with `ObjectDisposedException`, and releases the endpoint so a replacement transport binds it. Covered for each shipped transport, not only for one.

**Required evidence level:** automated-runtime

**Rationale:** `SerialCommTransport.Dispose` was corrected to take the gate on source symmetry with `StreamCommTransport`, and that correction has never been reproduced against a serial read on `net481`. The disposal race is the failure mode most likely to appear only under load.

## DEV-VALREQ-014

**Classification:** REQUIRED

**Trigger:** every change to a text path, `ProtocolRaw.TextEncoding`, or `Write(string)`

**Category:** protocol

**Boundary:** hardware

**Targets:** `net481` and `net9.0`

**Acceptance criteria:** A binary payload round-trips byte-for-byte through `RawBytes`; a single-byte non-ASCII payload sent as bytes returns intact; the same text through `Write(string)` is observably coerced to ASCII. Any encoding the documentation claims support for is exercised, not merely accepted by a constructor.

**Required evidence level:** automated-runtime

**Rationale:** Raw-byte preservation is the invariant every binary protocol depends on, and the text paths are ASCII in places a consumer cannot configure. The acceptance criterion is written to stop a constructor that accepts an `Encoding` from being read as evidence that the encoding works end to end.

## DEV-VALREQ-015

**Classification:** REQUIRED

**Trigger:** every change to the public surface or to a transport's connection code

**Category:** security

**Boundary:** in-process

**Targets:** both accepted `NekoLib.Devices` manifests

**Acceptance criteria:** No shipped transport introduces authentication, encryption, credential handling, or access control, and neither accepted manifest gains a credential, certificate, or ACL member without an explicit accepted decision.

**Required evidence level:** build-only

**Rationale:** The boundary's security posture is that it has none: TCP connects in the clear to whatever host it is given, and the named-pipe client takes platform defaults. That is defensible for a device on a terminal's own bus or LAN, but only while it stays explicit. Silently gaining a half-implemented security surface would be worse than having none.
