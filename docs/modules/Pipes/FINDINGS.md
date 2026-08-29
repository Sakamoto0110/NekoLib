# NekoLib.Pipes Findings

**Document ID:** PIPE-FINDINGS

**Schema version:** 1

**Kind:** reference

**Lifecycle:** current

**Subject:** unconfirmed and non-normative observations about NekoLib.Pipes

**Surface:** findings

**Boundary:** pipes

**Authority role:** non-normative

**Mutation:** authored

**Indexing:** include

Everything here is non-normative. A finding becomes a confirmed defect only
after verification, and becomes scheduled work only through explicit promotion
to [`TODO.md`](../../../TODO.md).

The historical leads in [`audits/`](audits/) remain true only for their recorded
baselines. Those reverified against current source during the 2026-08-28 module
review are summarized here; their originals stay unmodified at their recorded
baselines.

| Historical ID | Origin | Current source state |
|---|---|---|
| `IPC-04` | [`audits/ipc-hardening-review-2026-08-08.md`](audits/ipc-hardening-review-2026-08-08.md) | Superseded. Each subscriber has one bounded FIFO queue drained by one writer, so concurrent publications cannot interleave frames on a subscriber stream. |
| `IPC-05` | [`audits/ipc-hardening-review-2026-08-08.md`](audits/ipc-hardening-review-2026-08-08.md) | Superseded. `PublishAsync` returns after enqueue attempts and never awaits subscriber writes. |
| `IPC-06` | [`audits/ipc-hardening-review-2026-08-08.md`](audits/ipc-hardening-review-2026-08-08.md) | Superseded. `PipeOperationRegistry` owns admitted operations and their transports, and `ShutdownAsync` waits for them. |
| `IPC-07` | [`audits/ipc-hardening-review-2026-08-08.md`](audits/ipc-hardening-review-2026-08-08.md) | Superseded. The obsolete raw log server is absent from `src/` and `tests/`. |
| `IPC-08` (sanitization, truncated frames) | [`audits/ipc-hardening-review-2026-08-08.md`](audits/ipc-hardening-review-2026-08-08.md) | Partly superseded. Handler failures now return a sanitized message, and a body truncated after a valid prefix throws `EndOfStreamException` instead of reading as a clean close. The depth and replay portion survives as `PIPE-FINDING-004`. |
| `IPC-09` | [`audits/ipc-hardening-review-2026-08-08.md`](audits/ipc-hardening-review-2026-08-08.md) | Superseded. `PipeTaskCancellation` races the `net481` blocking connect against the caller token and observes the abandoned worker. |
| `IPC-02` | [`audits/ipc-hardening-review-2026-08-08.md`](audits/ipc-hardening-review-2026-08-08.md) | Partly superseded. `PipeAccessPolicy.CurrentUserOnly` exists on both targets; `PlatformDefault` remains the accepted compatibility default, so the default descriptor exposure is a documented accepted boundary rather than an open lead. |
| `IPC-01`, `IPC-03` | [`audits/ipc-hardening-review-2026-08-08.md`](audits/ipc-hardening-review-2026-08-08.md) | Outside this boundary. Watchdog command authorization and deterministic-name squatting concern the consumer's protocol, not the generic transport, and remain accepted risks of the cooperative-process model. |
| First-pass hardening list | [`audits/initial-audit.md`](audits/initial-audit.md) | Superseded. The per-subscriber bounded queue and drop policy, pipe ACL configuration, and graceful in-flight drain on disposal are all implemented. |

The observations that survived that reverification, plus new ones raised by the
same review, are recorded as findings below.

## PIPE-FINDING-001

**Status:** open

**Confidence:** medium

**Observation:** Neither accept loop backs off when creating the server pipe stream fails repeatedly. Each admitted operation creates its own `NamedPipeServerStream`; if creation throws, the operation ends immediately, its `finally` releases the concurrency slot, and the enclosing `while` starts another operation at once. A persistent creation failure therefore becomes an unbounded tight retry loop that raises one `OnError` per iteration for as long as the endpoint is running.

**Evidence:** `src/Pipes/NekoLib.Pipes/PipeServer.cs:176-254` and `src/Pipes/NekoLib.Pipes/PipeEventHub.cs:284-366` — `PipeServerStreamFactory.Create` is called inside the operation body, and the loop's only exits are shutdown, cancellation, and a refused `TryStart`. The failure modes are the ones named in [`audits/ipc-hardening-review-2026-08-08.md`](audits/ipc-hardening-review-2026-08-08.md) IPC-03: a name already held under an incompatible security descriptor, or an instance limit imposed by the current owner. On `net481` the `CurrentUserOnly` path adds a second source, since it throws when the current Windows user SID is unavailable.

**Hypothesis:** In that state the endpoint consumes CPU and floods the metrics sink instead of failing observably or backing off. No test or scenario places an endpoint in it, so neither the loop rate nor the practical reachability is measured.

**Disposition:** Record only. Adding a delay, a failure budget, or a terminal transition would change documented lifecycle behavior and requires an accepted decision; the review that produced this finding was not authorized to make one. Reproduce it with a squatted endpoint before proposing any change.

**Outcome link:** none

## PIPE-FINDING-002

**Status:** confirmed

**Confidence:** high

**Observation:** `NoopPipeMetrics` implements `Snapshot()` as an explicit interface implementation while `SimplePipeMetrics` exposes it publicly. A consumer holding a `NoopPipeMetrics` reference therefore cannot call `Snapshot()` without casting to `IPipeMetrics`, even though both types satisfy the same contract.

**Evidence:** `src/Pipes/NekoLib.Pipes/IPipeMetrics.cs` declares `PipeMetricsSnapshot? IPipeMetrics.Snapshot() => null;` while `src/Pipes/NekoLib.Pipes/SimplePipeMetrics.cs` declares `public PipeMetricsSnapshot Snapshot()`. Both accepted baselines record the asymmetry: the `NoopPipeMetrics` block has no `Snapshot` member and the `SimplePipeMetrics` block does. `SimplePipeMetricsTests.NoopMetrics_SnapshotIsNull` pins the behavior and casts through the interface to reach it.

**Hypothesis:** The asymmetry is an ergonomic wrinkle rather than a defect; the null-returning contract was a deliberate first-pass decision recorded as L5 in [`audits/initial-audit.md`](audits/initial-audit.md).

**Disposition:** Documented in [`REFERENCE.md`](REFERENCE.md) so consumers call it through the interface. Making it public would change both accepted API baselines and requires a public API decision under the [public API and release policy](../../public-api-release-policy.md). None is scheduled.

**Outcome link:** [`REFERENCE.md`](REFERENCE.md)

## PIPE-FINDING-003

**Status:** confirmed

**Confidence:** high

**Observation:** One event publication serializes the same envelope more than once. `PublishAsync` serializes it to measure the frame size against the fixed 1 MiB bound, and each subscriber's writer serializes it again inside `PipeFraming.WriteAsync`, so a publication observed by `N` subscribers performs `N + 1` serializations of identical content.

**Evidence:** `src/Pipes/NekoLib.Pipes/PipeEventHub.cs:465` calls `PipeFraming.ValidateMessageSize`, which serializes; `src/Pipes/NekoLib.Pipes/PipeEventHub.cs:512` calls `PipeFraming.WriteAsync` per queued delivery, and both target implementations of that method serialize before writing.

**Hypothesis:** The cost is invisible at low fan-out and small payloads, and could become measurable with many subscribers or payloads near the frame bound. It is a throughput and allocation question, not a correctness one: delivery, ordering, overflow accounting, and metrics are unaffected.

**Disposition:** Record only. No measured throughput or allocation budget exists for this boundary; deriving one is part of the unpromoted [performance and resource budgets proposal](../../proposals/performance-resource-budgets.md). The runtime scenario samples counters without asserting a trend, so it would not detect a regression here either.

**Outcome link:** none

## PIPE-FINDING-004

**Status:** confirmed

**Confidence:** high

**Observation:** The wire protocol relies on serializer defaults. No explicit maximum depth, schema, or duplicate-identifier policy is configured on either target, and request identifiers correlate a response without being retained or deduplicated, so a raw peer may reuse an identifier or repeat a non-idempotent operation.

**Evidence:** `src/Pipes/NekoLib.Pipes/PipeFraming.cs` serializes and deserializes with no options or settings object on either target; `src/Pipes/NekoLib.Pipes/PipeClient.cs:174-181` validates only that the response identifier equals the request identifier and that the type is `res`. Originally raised as IPC-08 in [`audits/ipc-hardening-review-2026-08-08.md`](audits/ipc-hardening-review-2026-08-08.md) and reverified as still current on 2026-08-28.

**Hypothesis:** Under the accepted cooperative-process trust model this is a non-issue, because a peer that can open the endpoint can already call every mapped operation directly. It would become relevant only if untrusted callers or automatic client retries entered the threat model.

**Disposition:** Accepted as an application concern and stated in [`REFERENCE.md`](REFERENCE.md). The byte-size cap is the retained protection. Replay infrastructure was explicitly rejected in the source review and remains rejected; reopening it requires an accepted threat-model change.

**Outcome link:** [`REFERENCE.md`](REFERENCE.md)
