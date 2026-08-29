# NekoLib.Pipes History

**Document ID:** PIPE-HISTORY

**Schema version:** 1

**Kind:** reference

**Lifecycle:** current

**Subject:** factual chronological history of the NekoLib.Pipes boundary

**Surface:** history

**Boundary:** pipes

**Authority role:** evidence

**Mutation:** append-only

**Indexing:** include

Entries are appended in ascending date order and are not rewritten to match
later architecture. Each entry links preserved evidence rather than restating
it.

## 2026-06-04 — PIPE-HISTORY-001 — First-pass audit and initial regression harness

**Release:** none

- The first Pipes review established the dual-channel model, added the initial
  focused test project, gave `net481` framing cancellation parity with `net9`,
  made the metrics latency statistics thread-safe, released blocked `net481`
  accept threads at shutdown, added event-client reconnect and callback
  isolation, replaced a dropped connection on an oversized response with a
  structured error, and completed the nullable annotations.
- A clean close before a response frame became an unsuccessful
  `connection_closed` response instead of a thrown `EndOfStreamException`.

**Evidence:** [`audits/initial-audit.md`](audits/initial-audit.md)

## 2026-08-08 — PIPE-HISTORY-002 — IPC hardening review and accepted dispositions

**Release:** none

- A code-first review of the transport and the Watchdog RPC/event boundary
  recorded the effective trust model, the Windows default-descriptor exposure,
  deterministic-name squatting, concurrent-publication frame integrity, slow
  subscriber backpressure, and disposal that did not own admitted work.
- The bounded disposition package was accepted: generic Pipes is a local
  cooperative-process transport and not an authorization boundary; per-subscriber
  bounded single-writer queues with an explicit drop-or-disconnect policy; owned,
  cancelled, and drained shutdown; and sanitized wire errors.

**Evidence:** [`audits/ipc-hardening-review-2026-08-08.md`](audits/ipc-hardening-review-2026-08-08.md), [`../../history/phase-e-confidence-stabilization-2026-08-12.md`](../../history/phase-e-confidence-stabilization-2026-08-12.md)

## 2026-08-11 — PIPE-HISTORY-003 — Event subscriber slots released without a publication

**Release:** none

- The first execution of the separate-process scenario found that a hub kept a
  subscriber slot after that subscriber disconnected unless an event was
  published afterwards, so the hub stopped admitting subscribers after
  `MaxEventSubscribers` lifetime connections.
- The hub now creates the event endpoint as a duplex pipe and uses a pending
  read as its liveness signal, discarding subscriber input and retaining one
  writer per subscriber. No public API, framing, access-policy, metrics, or
  overflow contract changed.

**Evidence:** [`../../../runtime_tests/Pipes/LongRunningRecovery/README.md`](../../../runtime_tests/Pipes/LongRunningRecovery/README.md)

## 2026-08-12 — PIPE-HISTORY-004 — Separate-process recovery gate completed

**Release:** none

- The long-running and recovery scenario reached its outcome-first gate on
  `net9.0` with every scheduled fault reaching its expected terminal and its
  post-recovery probe, complete cleanup, and a released endpoint.
- The three defects found while reaching that gate were all in the scenario's
  own oracle; no change was made to `src/Pipes`.

**Evidence:** [`../../../runtime_tests/Pipes/LongRunningRecovery/README.md`](../../../runtime_tests/Pipes/LongRunningRecovery/README.md), [`../../history/phase-e-confidence-stabilization-2026-08-12.md`](../../history/phase-e-confidence-stabilization-2026-08-12.md)

## 2026-08-18 — PIPE-HISTORY-005 — F1-PIPE public API finalized

**Release:** none

- The compiled-surface review produced ten findings and eight accepted
  dispositions, all implemented: construction-time option capture and
  validation, terminal race-safe lifecycle with cross-target `ShutdownAsync`,
  isolated metrics callbacks and a sealed `SimplePipeMetrics`, oversized events
  rejected at the publisher, observable event-client failures, published
  `PipeErrorCodes`, `net481` connect cancellation, and an accepted
  target-specific payload and dependency contract.
- `PipeClient` lost its no-op disposal surface. Both accepted API baselines were
  updated as part of that acceptance.

**Evidence:** [`audits/public-api-review-2026-08-18.md`](audits/public-api-review-2026-08-18.md), [`migrations/f1.md`](migrations/f1.md), [`../../history/phase-f1-public-api-release-stability-2026-08-21.md`](../../history/phase-f1-public-api-release-stability-2026-08-21.md)

## 2026-08-21 — PIPE-HISTORY-006 — First stable family baseline

**Release:** 1.0.0

- `NekoLib.Pipes` 1.0.0 entered the first stable coordinated family baseline.
  Its two accepted compiled API manifests became stable baselines under the
  public API and release policy, and the package was published with the family.

**Evidence:** [`../../stable-release-1.0.0.md`](../../stable-release-1.0.0.md)

## 2026-08-27 — PIPE-HISTORY-007 — Managed XML documentation completed

**Release:** none

- The Pipes family documentation gate closed: 94 planning-baseline `CS1591`
  diagnostics across both target compilations went to zero with no malformed or
  unresolved XML-comment warning, `IPipeMetrics.OnServerResponseSent` was
  corrected to describe handler plus response-write-attempt time, and the
  normative reference gained the custom-metrics contract and extension boundary.
- Implicit parameterless constructors were made explicit only so accepted
  manifest members could carry documentation; the compiled public API did not
  change.

**Evidence:** [`../../audit/public-api-documentation-extensibility-review-2026-08-27.md`](../../audit/public-api-documentation-extensibility-review-2026-08-27.md)

## 2026-08-28 — PIPE-HISTORY-008 — XML documentation delivered in packages

**Release:** none

- The coordinated `NEKOMKT-F009` package gate closed. Candidate
  `1.1.0-local.8` carried both `NekoLib.Pipes` target assemblies with their
  matching XML files, a permanent package-content guard was added, and isolated
  `PackageReference` consumers received the same pairs from the package rather
  than from repository build output.

**Evidence:** [`../../audit/public-api-documentation-extensibility-review-2026-08-27.md`](../../audit/public-api-documentation-extensibility-review-2026-08-27.md)

## 2026-08-28 — PIPE-HISTORY-009 — Module-first documentation migration

**Release:** none

- The boundary moved to `docs/modules/Pipes/`. The source-adjacent reference
  became the canonical `REFERENCE.md` with a pointer-only portal left in the
  source tree, and the F1 migration guide and three audits moved under the
  module with their bodies, baselines, and original paths preserved.
- One comment-only correction was applied to `IPipeMetrics`: the connect and
  disconnect callbacks document that an event hub raises them with the
  `.events` endpoint name.

**Evidence:** [`MANIFEST.md`](MANIFEST.md), [`REFERENCE.md`](REFERENCE.md), [`VALIDATIONS.md`](VALIDATIONS.md)
