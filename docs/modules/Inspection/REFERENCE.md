# NekoLib.Inspection

**Document ID:** INSP-REFERENCE

**Schema version:** 1

**Kind:** reference

**Lifecycle:** current

**Subject:** passive in-process inspection composition, bounded operation retention, state-provider identity and ordering, snapshot budgets, owner diagnostics, lifecycle, and the experimental action boundary

**Surface:** technical-reference

**Boundary:** inspection

**Authority role:** normative

**Mutation:** authored

**Indexing:** include

`NekoLib.Inspection` is the concrete opt-in runtime behind the Core Inspection
contracts. It targets `net481` and `net9.0` with one public surface and
references only [`NekoLib.Core`](../Core/REFERENCE.md).

The stable product is passive: modules record bounded operation evidence and
register pull-based state providers; readers capture that evidence through the
read-only `IInspectionSnapshotSource`. Inspection does not persist, transmit,
redact, truncate, authorize, or remotely expose the captured values.

## Composition and ownership

A composition root may construct a local runtime directly:

```csharp
using var inspection = new InspectionRuntime(
    new InspectionOptions { Capacity = 512 });

myModule.UseInspection(inspection);
var snapshot = inspection.CaptureSnapshot(
    maxOperations: 100,
    timeout: TimeSpan.FromMilliseconds(250));
```

Direct construction is immediately enabled. The caller owns the runtime and
must dispose it.

An application that uses the process-wide Core provider can instead install one
global runtime:

```csharp
using var inspection = InspectionRuntime.EnableGlobal();
```

Only one enabled global owner is admitted. A second installation throws
`InvalidOperationException` without replacing the current owner. Two narrower
failures also exist and both roll the installation back rather than leaving a
disabled recorder in the slot: a runtime disposed while activation is completing
throws `ObjectDisposedException`, and a recorder that reports itself disabled
during installation surfaces `ArgumentException` from Core.

Disposing the owning runtime restores `InspectionProvider.Current` to
`NullInspection.Instance`. The provider remains non-null, so modules can depend
on the Core recorder without a separate enabled branch.

`InspectionOptions` is read once at construction. `Capacity` defaults to `1024`
and must be at least 1. Mutating the options instance later cannot change a live
runtime.

## Passive operation recording

`Record(module, operation, payload)` requires non-blank module and operation
names. A module cannot contain the reserved `::` delimiter. The operation name
is deliberately not constrained beyond being non-blank: it is descriptive text,
not an identity component, so it may contain `::` and is never composed into a
registration key. Valid calls made after disposal are inert and do not evaluate
the payload delegate; argument validation still runs.

Payload evaluation happens before the operation lock:

- a successful value is retained as a shallow reference;
- a null result remains null;
- an exception becomes `<payload threw: TypeName>` without its message or
  stack. This uses the **thrown** exception's type name, whereas a failing state
  provider is reported by its **innermost** exception type — the two markers do
  not use the same rule;
- a slow payload does not hold the queue lock or stop another record from
  committing first.

Sequence is the concurrency-order authority. The timestamp is a wall-clock
annotation read when the record commits after payload evaluation. A call that
began first may therefore receive a later sequence and timestamp.

Retention is chronological and bounded to `Capacity`. When the queue is full,
the oldest record is evicted. `GetOperations()` returns a detached collection in
sequence order. The contained `InspectionOperation` models protect their outer
shape through Core, but the payload objects remain shallow.

## Provider identity, ownership, and ordering

`RegisterStateProvider(module, key, snapshot)` creates the public identity
`module::key`. Both components must be non-blank and neither may contain `::`.
Identity comparison is ordinal and case-sensitive.

A duplicate is rejected without replacing its current owner. The returned
handle is idempotent and conditionally unregisters only the registration that
created it; a stale handle cannot remove a later owner that reused the same
identity.

Providers are invoked in registration order. `StateKeys()` returns that same
order, allowing a composition root to register essential evidence before less
important evidence. Registration and invocation use separate lock phases, so
application delegates never run under the registry lock. A provider already
copied by a capture may finish after its handle or runtime is disposed.

## Budgeted snapshots

`CaptureSnapshot(maxOperations, timeout)` implements the
`IInspectionSnapshotSource` boundary used by Diagnostics:

1. it copies the newest requested operation window first, in sequence order;
2. it copies current provider registrations;
3. it invokes providers sequentially in registration order under one shared
   completion budget;
4. it records `CapturedUtc` after provider processing finishes or the budget is
   exhausted.

Negative limits or timeouts throw. Zero operations and a zero timeout are
valid. Provider outcomes are isolated into values:

| Provider outcome | Snapshot value |
|---|---|
| returns null | `<null>` |
| throws | `<snapshot threw: RootType>` |
| exceeds or is skipped after the shared budget | `<snapshot timed out>` |

**Known deviation.** The skip is not yet deterministic. When a provider exhausts
the budget by a sub-millisecond margin the next provider can still be admitted
and invoked, so it returns its value instead of the timeout marker. The contract
above is the intended one and is what the focused regression asserts; the
implementation does not meet it in that narrow race. See
[`INSP-ISSUE-001`](ISSUES.md).

The timeout bounds caller completion only. It cannot cancel application code.
Each provider registration has at most one outstanding budgeted invocation;
overlapping or repeated captures share that in-flight task instead of starting
unbounded duplicate work. A provider exception that arrives after a caller has
timed out is captured as task data, so it is observed. After the task completes,
a later capture may start a fresh invocation.

The returned `InspectionSnapshot` also carries the configured `Capacity` and
the lifetime `TotalRecorded` and `EvictedCount` counters, so a consumer reading
a bounded window can tell whether evidence was dropped before it looked.

Operations and state do not form one atomic instant: operations are copied
before providers run, and `CapturedUtc` describes snapshot-construction
completion.

**Every marker is an ordinary string in the same value space as provider
results.** Nothing types or wraps them, so a provider that legitimately returns
the text `<null>` or `<snapshot timed out>` is indistinguishable from the
runtime's own marker. A consumer that must tell them apart owns that
disambiguation; see [`INSP-FINDING-001`](FINDINGS.md).

## Unbudgeted owner state

`CaptureState()` is a synchronous local-owner convenience. It copies the
provider list, invokes every provider in registration order without a timeout,
and uses the same null/exception markers. It does not use the budgeted
single-flight path and can block for as long as a provider blocks.

Diagnostics and other bounded evidence collectors must use
`IInspectionSnapshotSource.CaptureSnapshot`, not `CaptureState()`.

## Diagnostics and clearing

`GetDiagnostics()` reports enabled state, configured capacity, retained count,
lifetime totals, lifetime capacity evictions, enabled clear count, oldest and
newest retained sequence, and current registration counts.

The operation fields are coherent with each other, as are the registry counts,
but the two groups and lifecycle flag are read under separate locks. Under
concurrency the result is a best-effort cross-domain observation rather than a
global atomic snapshot.

`ClearOperations()` removes retained operations and increments `ClearCount`
while enabled, including when the queue is already empty. It preserves total
recorded count, eviction count, and the next lifetime sequence. After disposal
it is inert.

Counter overflow is outside the supported operational envelope; Inspection has
no counter-reset or rollover protocol.

## Experimental action boundary

The following concrete members carry the exact experimental marker
`NEKOEXP0001`:

- `InspectionRuntime.RegisterAction`;
- `InspectionRuntime.TryInvokeAction`;
- `InspectionRuntime.ActionKeys`;
- `InspectionRuntimeDiagnostics.ActionCount`.

They remain only for coherent pre-stable compatibility with Core's experimental
registration seam. The compiler marker is release signaling, not a security
boundary. In-process access does not provide authentication or authorization.

Current invocation is synchronous, executes outside the registry lock, and
surfaces action exceptions. It has no stable async, cancellation, timeout, UI
marshalling, discovery, permission, or remote-execution contract. No NekoLib
feature module registers an action. Deliberate consumers must opt into the
warning narrowly and must supply their own authorization boundary.

## Disposal and concurrency

`Dispose()` is idempotent and one-way. It first disables the runtime, then
removes a global installation if owned, clears provider/action registrations,
and clears retained operations. Lifetime totals, evictions, clears, capacity,
and sequence state remain diagnostic history.

After completed disposal, valid writes and registrations are inert; operations,
state, and key lists are empty; budgeted snapshots are empty; diagnostics report
disabled state and zero live counts; and experimental action lookup returns
false. Reads racing with disposal may observe either their copied pre-cleanup
input or the empty post-cleanup state. Delegates already copied are not
cancelled.

## Data and security boundary

Inspection values can contain sensitive or mutable application data. The
runtime deliberately performs no deep clone, serialization, redaction,
truncation, persistence, or transport. Producers should emit bounded diagnostic
projections. A consumer that persists or transmits evidence owns access control,
redaction, size limits, and retention. `NekoLib.Diagnostics` accepts only the
read-only snapshot interface and applies its own evidence safeguards; it cannot
invoke actions.

## Explicit non-goals

This package does not add broad feature-module instrumentation, a public static
facade, a second registry, IPC, reflection loading, plugins, remote control,
fleet management, persistent storage, or test-control bypasses.

The broader Inspection rollout remains **frozen**. That freeze, its guardrails,
and the conditions for lifting it are owned by
[`ROADMAP.md`](../../../ROADMAP.md); [`TODO.md`](../../../TODO.md) is the
promoted-work scheduler and carries no freeze. Navigation is the only feature
module that records, Diagnostics consumes only `IInspectionSnapshotSource`, no
feature module registers an action, and application code calling `Record(...)`
by hand is not evidence that a module emits.

## Validation

```powershell
dotnet test tests\NekoLib.Inspection.Tests\Unit\NekoLib.Inspection.Tests.Unit.csproj -c Release -f net481
dotnet test tests\NekoLib.Inspection.Tests\Unit\NekoLib.Inspection.Tests.Unit.csproj -c Release -f net9.0
.\eng\verify-public-api.ps1 -PackageId NekoLib.Inspection
```

The tests are pure unit scope with no external prerequisites; see
[`tests/README.md`](../../../tests/README.md). The accepted manifests under
`eng/public-api/NekoLib.Inspection/` are the compiled dual-target compatibility
oracle. Package-backed claims require the canonical immutable package flow and
PackageReference-only consumers.

Both targets compile from one source set with no conditional compilation, so
every behavior above applies equally to `net481` and `net9.0`. Two properties of
the accepted manifests are specific to this package and are protected
deliberately: they carry the `InternalsVisibleTo("NekoLib.Inspection.Tests.Unit")`
friend declaration, and they carry a per-target `TargetFrameworkAttribute`
because this project does not suppress its generation the way the sibling
capability packages do. Those two lines are the only difference between the two
manifests. One asymmetry matters when validating documentation: the project
suppresses `CS1591` on `net481` only, so an ordinary `net481` build cannot
detect a missing XML comment and `net9.0` is the target that reports one.

Sustained and fault-driven behavior comes from the shared Observability scenario
at
[`runtime_tests/Observability/LongRunningRecovery/`](../../../runtime_tests/Observability/LongRunningRecovery/README.md).
That scenario also drives Logging and Telemetry; only its Inspection checks are
Inspection evidence, and it deliberately references no action API.

[`VALIDATION_REQUIREMENTS.md`](VALIDATION_REQUIREMENTS.md) owns the qualifying
evidence contract and [`VALIDATIONS.md`](VALIDATIONS.md) records what actually
ran, with its gaps.

## Related surfaces

| Need | Owner |
|---|---|
| Identity, packages, targets, API oracles, evidence routes | [`MANIFEST.md`](MANIFEST.md) |
| Consumer introduction | [`README.md`](README.md) |
| Consumer-visible evolution | [`CHANGELOG.md`](CHANGELOG.md) |
| Chronology | [`HISTORY.md`](HISTORY.md) |
| Confirmed defects | [`ISSUES.md`](ISSUES.md) |
| Unconfirmed observations | [`FINDINGS.md`](FINDINGS.md) |
| Candidate-to-stable transition | [`migrations/f1.md`](migrations/f1.md) |
| Historical F1 review | [`audits/public-api-review-2026-08-17.md`](audits/public-api-review-2026-08-17.md) |
| Instrumentation and action freeze | [`ROADMAP.md`](../../../ROADMAP.md) |
| Only in-repository producer | [`../Navigation/REFERENCE.md`](../Navigation/REFERENCE.md) |
| Read-only evidence consumer | [`../Diagnostics/REFERENCE.md`](../Diagnostics/REFERENCE.md) |
