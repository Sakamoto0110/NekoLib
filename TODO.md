# NekoLib Live Roadmap

**Kind:** roadmap/status

**Lifecycle:** current

**Subject:** open work, accepted decisions, freezes, and completion criteria

Completed architecture work through Phases A, B, and D is preserved in the
[historical roadmap snapshot](docs/history/architecture-roadmap-through-phase-d-2026-08-01.md).
Audit snapshots are indexed separately under [`docs/audit/`](docs/audit/README.md).

## Frozen — deferred Inspection module rollout (B4/B5)

**Freeze reason:** the Core contracts, global Inspection runtime, Navigation
producer, and Diagnostics read-only consumer are proven, but broad module
instrumentation and state-changing actions have not yet demonstrated enough
value or a safe common contract. This is live context, not completed history.

**Implemented state:**

- Core owns independent Logging, Telemetry, and Inspection contracts plus
  non-null NO-OP defaults.
- `InspectionRuntime.EnableGlobal(...)` provides deterministic singleton
  activation and teardown. Navigation is the only feature module that records
  Inspection operations.
- Diagnostics consumes only `IInspectionSnapshotSource`; incident collection
  cannot invoke Inspection actions.
- Navigation telemetry owns the bounded page-switch timing producer accepted in
  Phase D. That work does not authorize broader Inspection recording.

**Known gaps and traps:**

- Data, Pipes, Watchdog, Devices, and Diagnostics do not record feature-module
  Inspection operations. A sample application calling `Record(...)` manually
  is application instrumentation, not module instrumentation.
- No feature module registers a real Inspection action. Navigation stays
  read-only until async execution, cancellation, timeout, and UI-marshalling
  semantics are explicitly accepted.
- Watchdog crash notification crosses IPC. Its log/crash integration must be
  designed separately from in-process module recording.

**Existing seams:**

- Data: `QueryExecutionContext`.
- Pipes: `IPipeMetrics`.
- Devices: the serialized `HardwareEngine.SendAsync` transaction.
- Watchdog and Diagnostics: their existing incident and IPC boundaries, after a
  dedicated review.

**Resume order and unfreeze conditions:**

1. Explicitly unfreeze one bounded module and define the operational question
   its data must answer.
2. Validate the smallest real producer before copying a pattern elsewhere;
   Data or Pipes are the preferred first candidates.
3. Preserve disabled/NO-OP behavior, module boundaries, and both supported
   target families.
4. Restore the broad freeze after the authorized module scope is complete.

## Completed phases

- Phase C repository documentation and organization completed on 2026-08-01.
  See the commit-bound
  [`completion and validation snapshot`](docs/history/phase-c-repository-hygiene-2026-08-01.md).

## Active architecture reviews

- [ ] Complete the remaining Diagnostics-sector boundary and naming decisions.
  - Review: [`docs/audit/diagnostics-boundaries-review-2026-07-30.md`](docs/audit/diagnostics-boundaries-review-2026-07-30.md)
  - Baseline: `1727a1cac3f66666b2df02bc618ad6ab45807a49`.
  - Phase D implemented DGN-01, CORE-01, BND-01, LOG-01, CORE-02,
    TEST-01, and the accepted DBG-01 rename.
  - Remaining review-only decisions: CRASH-01, CRASH-02, and WIN-01.
