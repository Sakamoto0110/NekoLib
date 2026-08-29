# NekoLib.Devices History

**Document ID:** DEV-HISTORY

**Schema version:** 1

**Kind:** reference

**Lifecycle:** current

**Subject:** factual chronological history of the NekoLib.Devices boundary

**Surface:** history

**Boundary:** devices

**Authority role:** evidence

**Mutation:** append-only

**Indexing:** include

Entries are appended in ascending date order and are not rewritten to match
later architecture. Each entry links preserved evidence rather than restating
it.

## 2026-06-10 — DEV-HISTORY-001 — First-pass audit and initial regression harness

**Release:** none

- The first Devices review took the module from a serial-only abstraction with
  unvalidated inputs to a defensible unit-level baseline: `SerialConfig.PortName`
  was actually applied to the port, the `SerialPort` default port name stopped
  being treated as an explicit choice, `Open(string)` validated and opened under
  one lock, reopening on a different port failed clearly, `"\n"` survived as a
  newline, write/read/timeout arguments were validated, and the internal
  semaphore was disposed.
- `HardwareEngine` gained constructor and argument validation, kept cancellation
  as cancellation instead of converting it to a failed response, assigned the
  originating operation to a parsed response, treated a null protocol response as
  an error, and refused a transport already open on another port.
- `ProtocolRaw` validated null operations and arguments and rejected wrongly
  typed `RawBytes`/`RawText` values.
- Four review items were left open: nullable `ReadLine` timeout behaviour,
  explicit `SerialConfig` validation, `ThrowIfDisposed`, and a decision on
  ASCII-only `RawText`.

**Evidence:** [`audits/initial-audit.md`](audits/initial-audit.md)

## 2026-07-01 — DEV-HISTORY-002 — Remaining first-pass items closed

**Release:** none

- Commit `d352fa8` closed all four open items from the first pass: `ReadLine`
  returns `null` rather than an empty string on timeout, `ValidateSerialConfig`
  checks baud rate, data bits, stop bits, handshake and both timeouts,
  `ThrowIfDisposed` fails consistently with `ObjectDisposedException`, and the
  ASCII text boundary became a documented decision rather than an accident.

**Evidence:** [`audits/initial-audit.md`](audits/initial-audit.md)

## 2026-08-01 — DEV-HISTORY-003 — Stream transports and the virtual-COM oracle pass

**Release:** none

- Commit `ddd09d3` added `StreamCommTransport` with its background receive pump
  and the `TcpCommTransport` and `NamedPipeCommTransport` implementations,
  serialized complete `HardwareEngine` operations behind one gate, and let
  `ProtocolRaw` callers select a text encoding while keeping ASCII as the
  default. The module stopped being serial-only.
- The versioned com0com scenario closed the virtual-COM execution gap on both
  `net481` and `net9.0`, validating PCB-A text and PCB-B binary protocols against
  an independent emulator oracle. Physical UART and electrical behaviour remained
  outside that evidence and still are.

**Evidence:** [`audits/initial-audit.md`](audits/initial-audit.md), [`runtime_tests/Devices/Com0Com/README.md`](../../../runtime_tests/Devices/Com0Com/README.md)

## 2026-08-12 — DEV-HISTORY-004 — Automated com0com fault modes

**Release:** none

- The com0com scenario gained scenario-owned peers on the ports the emulator had
  held, with five injectable faults — delay, silence, malformed frame,
  disconnect, restart — none of which exists in `NekoLib.Devices`. The module
  gained no fault-injection or control API.
- A compact `net9.0` recovery sweep passed 33/33 checks with all five faults
  reaching their expected terminal and a clean request after each. It records
  `belowSpecifiedWindow: true` and is not nominal-window evidence, and runtime
  fault coverage exists on `net9.0` only.
- One suite assertion was deliberately weakened and documented: a serial line
  carries no correlation of its own, so "a late reply does not survive a reopen"
  is asserted instead of "a late reply vanishes".

**Evidence:** [`runtime_tests/Devices/Com0Com/README.md`](../../../runtime_tests/Devices/Com0Com/README.md), [`docs/history/phase-e-confidence-stabilization-2026-08-12.md`](../../history/phase-e-confidence-stabilization-2026-08-12.md)

## 2026-08-18 — DEV-HISTORY-005 — F1-DEV public API finalization

**Release:** `1.0.0`

- The compiled surface was reviewed in full — 18 public types, 121 public and
  protected member declarations — and fifteen dispositions were accepted.
  `Protocols.HardwareProtocol` was removed as a base class that supplied nothing.
- The late-reply hazard was made addressable rather than invisible: the accepted
  remedy became the opt-in `HardwareEngine.CloseTransportOnNoResponse` after
  impact analysis withdrew an earlier pre-write drain, which would have narrowed
  the window without closing it and would have cost the most public surface.
  `SerialCommTransport` began discarding the port input buffer on open so the
  close/reopen boundary is symmetric.
- The engine stopped handing the protocol's live configuration to the transport,
  and neither transport writes a resolved endpoint back into a caller-owned
  object. `HardwareResponse.Failure` preserved the real exception behind a
  fail-soft response. The three read methods, `ParseResponse`, `Log`, and the
  `SerialCommTransport` constructor were annotated for the nulls they always
  produced. `SerialCommTransport.Dispose` began taking the transport gate, and
  both `Checksum` methods began rejecting null consistently.
- Ten focused dual-target regressions landed, two of them driving a real loopback
  peer that answers 600 ms after a 200 ms budget. No serial port was opened for
  this work and the com0com scenario was built but not run.

**Evidence:** [`audits/public-api-review-2026-08-17.md`](audits/public-api-review-2026-08-17.md), [`migrations/f1.md`](migrations/f1.md)

## 2026-08-28 — DEV-HISTORY-006 — Managed API documentation and packaged XML

**Release:** none

- The family-wide managed documentation campaign completed the module's public
  XML comments and extended the technical reference with the supported extension
  seams. The residual `CS1591` count recorded for `NekoLib.Devices` in the review
  snapshot predates that work.
- Documentation-enabled builds ship the XML asset beside the packaged assembly.
  No compiled signature, target, dependency, or runtime behaviour changed.

**Evidence:** [`docs/audit/public-api-documentation-extensibility-review-2026-08-27.md`](../../audit/public-api-documentation-extensibility-review-2026-08-27.md)

## 2026-08-29 — DEV-HISTORY-007 — Module-first documentation migration

**Release:** none

- The boundary moved to `docs/modules/Devices/`. The source-adjacent README
  became a pointer-only portal, the technical reference became
  `REFERENCE.md`, and the two audits and the F1 migration guide were moved with
  their bodies, baselines, chronology, and original paths preserved.
- The full documentation review reverified every `DEV-01` through `DEV-15`
  disposition and the first-pass item list against current source, confirmed no
  open defect, and recorded four new non-normative findings plus the module's
  first risk-derived validation requirements and curated evidence coverage.
- Four public XML comments were corrected where they misdescribed current
  behaviour: the `SendAsync` receive budget bounds only the reply read, and the
  stream transports validate, store, and report `SerialConfig`'s timeout fields
  without applying them. `HardwareResponse.RawText` now states the encoding it
  carries and that it is lossy for binary payloads. No public signature, runtime
  behaviour, API baseline, target, dependency, or package topology changed.

**Evidence:** [`VALIDATIONS.md`](VALIDATIONS.md), [`FINDINGS.md`](FINDINGS.md)
