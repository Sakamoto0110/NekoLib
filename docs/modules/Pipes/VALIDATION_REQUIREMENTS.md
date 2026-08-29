# NekoLib.Pipes Validation Requirements

**Document ID:** PIPE-VALIDATION-REQUIREMENTS

**Schema version:** 1

**Kind:** reference

**Lifecycle:** current

**Subject:** evidence contract for qualifying the NekoLib.Pipes boundary

**Surface:** validation-requirements

**Boundary:** pipes

**Authority role:** normative

**Mutation:** authored

**Indexing:** include

The [module manifest](MANIFEST.md) owns the inherited profile list. The
requirements below specialize those profiles for what this boundary actually is:
a dual-target framed byte-stream transport that owns operating-system endpoints,
background accept loops, bounded per-subscriber queues, and two different JSON
serializers. They are derived from architecture and risk, not from the tests
that happen to exist; several are `NOT_RUN` in
[`VALIDATIONS.md`](VALIDATIONS.md) and say so there.

## PIPE-VALREQ-001

**Classification:** REQUIRED

**Trigger:** every release candidate and every change to source, project, target, or package settings

**Category:** build

**Boundary:** in-process

**Targets:** `net481` and `net9.0`

**Acceptance criteria:** Both target assemblies build with zero errors, no new normalized warning identity, and no `CS1591` or malformed XML-comment diagnostic, and both generated XML files are produced beside their assemblies.

**Required evidence level:** build-only

**Rationale:** The two targets compile different framing, cancellation, access-policy, and payload code behind `NET481`/`NET9`, so one target can compile while the other does not. XML documentation is part of the shipped managed asset.

## PIPE-VALREQ-002

**Classification:** REQUIRED

**Trigger:** every release candidate and every change to a public declaration, target, nullable annotation, default value, or package dependency

**Category:** api-compatibility

**Boundary:** in-process

**Targets:** both accepted `NekoLib.Pipes` manifests

**Acceptance criteria:** Release assemblies match both accepted manifests without an automatic baseline update; any delta carries an explicit compatibility disposition and a migration entry before acceptance.

**Required evidence level:** build-only

**Rationale:** The public payload type and async-disposal surface differ by target on purpose, so a change can be invisible on one manifest and breaking on the other. Source review cannot prove compiled metadata parity.

## PIPE-VALREQ-003

**Classification:** REQUIRED

**Trigger:** every implementation or contract change and every release candidate

**Category:** focused-regression

**Boundary:** ipc

**Targets:** `net481` and `net9.0-windows` test targets

**Acceptance criteria:** The complete focused suite passes with zero failed or skipped tests on both targets and preserves construction-time option capture and validation, correlation, the response-versus-exception matrix, sanitized handler failure, request and idle timeouts, configured frame limits, clean and truncated EOF, malformed JSON, one-shot terminal lifecycle, admitted-work drain, bounded event queues and both overflow policies, callback ordering and isolation, reconnect, metrics failure isolation, and same-user `CurrentUserOnly` success.

**Required evidence level:** automated-runtime

**Rationale:** The suite opens real named pipes in process, so it is simultaneously the contract oracle and the only routinely executed IPC coverage. Lifecycle, cancellation, and queue behavior are unreachable by compilation.

## PIPE-VALREQ-004

**Classification:** REQUIRED

**Trigger:** every change to framing, limits, dispatch, correlation, error codes, or the accept and shutdown paths

**Category:** protocol

**Boundary:** process

**Targets:** at least one supported target per campaign

**Acceptance criteria:** Across real separate processes on a real named pipe: payloads round-trip byte-for-byte at multiple sizes, concurrent clients never receive another caller's response, an unmapped operation and a throwing handler return their documented codes and leave the connection usable, an over-limit request is refused and an over-limit response reports `response_too_large`, and a peer that lies about a frame length or closes mid-frame does not disturb other traffic.

**Required evidence level:** automated-runtime

**Rationale:** In-process tests cannot prove that a real peer, a real kernel object, and a real process boundary behave as the framing contract claims, and the malformed-peer cases require a raw peer the library does not provide.

## PIPE-VALREQ-005

**Classification:** REQUIRED

**Trigger:** every change to lifecycle, shutdown, disposal, the operation registry, or endpoint creation

**Category:** runtime

**Boundary:** process

**Targets:** at least one supported target per campaign

**Acceptance criteria:** A server killed or disposed while a request is in flight fails that request rather than hanging, releases its endpoint within a bounded wait, and allows the name to be bound and served again; a client killed abruptly is shed without stopping the server; and a run leaves no bound endpoint, orphaned process, or leaked worker.

**Required evidence level:** automated-runtime

**Rationale:** A named pipe is a machine-wide resource. Ownership defects surface as a leaked endpoint that blocks the next run rather than as a failing assertion, so cleanup must be asserted rather than assumed.

## PIPE-VALREQ-006

**Classification:** REQUIRED

**Trigger:** any claim that `CurrentUserOnly` restricts access, and every change to `PipeServerStreamFactory`

**Category:** security

**Boundary:** process

**Targets:** `net481` and `net9.0`

**Acceptance criteria:** A same-user peer connects successfully under both policies, and a peer running as a different operating-system user or at a different elevation level is denied by a `CurrentUserOnly` server on both targets. Denial must be observed, not inferred from the constructor arguments.

**Required evidence level:** automated-runtime

**Rationale:** The two targets implement the boundary by different mechanisms — an explicit Windows ACL on `net481` and a platform pipe option on `net9.0` — so one can silently fail to restrict while the other works. The reference makes an access-control claim that only a denial observation supports.

## PIPE-VALREQ-007

**Classification:** REQUIRED

**Trigger:** every release candidate and every change to `PipeMessage`, framing, or either serializer path

**Category:** protocol

**Boundary:** process

**Targets:** a `net481` process paired with a `net9.0` process, in both role directions

**Acceptance criteria:** A `net481` client interoperates with a `net9.0` server and the reverse, for requests, responses, errors, and events, including a populated `Data` payload and a correlated identifier.

**Required evidence level:** automated-runtime

**Rationale:** The wire is produced by Newtonsoft.Json on one target and System.Text.Json on the other. Member naming and value shapes agree by construction in source, but the two serializers are independent implementations and no executed run has paired them. Same-target scenarios cannot close this by design, since each scenario run uses one binary in every role.

## PIPE-VALREQ-008

**Classification:** REQUIRED

**Trigger:** every change to admission, the concurrency limiters, or the accept loops

**Category:** protocol

**Boundary:** process

**Targets:** at least one supported target per campaign

**Acceptance criteria:** With `MaxClients` established RPC clients and `MaxEventSubscribers` established subscribers, a further peer's connect is refused by its own timeout rather than admitted, established peers keep working, and a slot released by a disconnect admits the waiting peer.

**Required evidence level:** automated-runtime

**Rationale:** `REFERENCE.md` states an admission bound in which one slot may be a pending listener. That behavior is emergent from the semaphore and the accept loop and is untested at saturation, which is also the state in which `PIPE-FINDING-001` would matter.

## PIPE-VALREQ-009

**Classification:** REQUIRED

**Trigger:** every release candidate and every change to package identity, targets, dependencies, or documentation generation

**Category:** package-consumer

**Boundary:** package-feed

**Targets:** `lib/net481` and `lib/net9.0` package assets

**Acceptance criteria:** An immutable package contains both target assemblies with their matching XML files, declares `Newtonsoft.Json` only for `net481`, and an isolated `PackageReference` consumer restores and receives those exact assets from the package rather than from repository build output.

**Required evidence level:** automated-runtime

**Rationale:** The dependency is conditioned on one target and the XML asset has already been shipped missing once for the whole family; repository build output cannot prove either.

## PIPE-VALREQ-010

**Classification:** CONDITIONAL

**Trigger:** an unattended deployment that keeps an endpoint live for extended periods, or any change to the event queues, metrics counters, or resource cleanup

**Category:** recovery-soak

**Boundary:** process

**Targets:** at least one supported target

**Acceptance criteria:** A sustained run over its stated nominal window records duration, workload, concurrency, operations, injected faults, expected and actual recovery, and resource measurements, and shows no crash, deadlock, leaked process or endpoint, unrecovered state, or unexplained resource growth.

**Required evidence level:** automated-runtime

**Rationale:** Watchdog keeps a Pipes endpoint live for the life of a supervised application. The existing recovery evidence is a short window that records itself as below the nominal one, and counter trends are sampled without being asserted.

## PIPE-VALREQ-011

**Classification:** CONDITIONAL

**Trigger:** supporting, claiming, or documenting non-Windows use of the unqualified `net9.0` asset

**Category:** runtime

**Boundary:** process

**Targets:** `net9.0` on a non-Windows operating system

**Acceptance criteria:** RPC, events, framing, lifecycle, and the access-policy contract behave as documented, or the differences are documented before the claim is made.

**Required evidence level:** automated-runtime

**Rationale:** The `net9.0` asset carries no platform qualifier, but the implementation assumes Windows named-pipe semantics and the reference makes no non-Windows claim. The requirement exists so that adding such a claim cannot skip its evidence.

## PIPE-VALREQ-012

**Classification:** NOT_APPLICABLE

**Trigger:** none

**Category:** security

**Boundary:** process

**Targets:** none

**Acceptance criteria:** none

**Required evidence level:** build-only

**Rationale:** Resistance to a hostile process already running as the same user — impersonation, endpoint squatting, credential or replay attack — is explicitly outside the accepted transport boundary stated in `REFERENCE.md`. It is recorded here so its absence reads as an accepted scope decision rather than an untested requirement. Admitting it requires an accepted threat-model change, not a new test.
