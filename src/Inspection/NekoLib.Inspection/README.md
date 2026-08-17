# NekoLib.Inspection

**Kind:** reference

**Lifecycle:** current

**Subject:** passive in-process inspection composition, bounded operation
retention, state-provider identity and ordering, snapshot budgets, owner
diagnostics, lifecycle, and the experimental action boundary

`NekoLib.Inspection` is the concrete opt-in runtime behind the Core Inspection
contracts. It targets `net481` and `net9.0` with one public surface and
references only [`NekoLib.Core`](../../Core/NekoLib.Core/README.md).

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

Only one enabled global owner is admitted. A second installation throws without
replacing the current owner. Disposing the owning runtime restores
`InspectionProvider.Current` to `NullInspection.Instance`. The provider remains
non-null, so modules can depend on the Core recorder without a separate enabled
branch.

`InspectionOptions` is read once at construction. `Capacity` defaults to `1024`
and must be at least 1. Mutating the options instance later cannot change a live
runtime.

## Passive operation recording

`Record(module, operation, payload)` requires non-blank module and operation
names. A module cannot contain the reserved `::` delimiter. Valid calls made
after disposal are inert and do not evaluate the payload delegate; argument
validation still runs.

Payload evaluation happens before the operation lock:

- a successful value is retained as a shallow reference;
- a null result remains null;
- an exception becomes `<payload threw: TypeName>` without its message or
  stack;
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

The timeout bounds caller completion only. It cannot cancel application code.
Each provider registration has at most one outstanding budgeted invocation;
overlapping or repeated captures share that in-flight task instead of starting
unbounded duplicate work. A provider exception that arrives after a caller has
timed out is captured as task data, so it is observed. After the task completes,
a later capture may start a fresh invocation.

Operations and state do not form one atomic instant: operations are copied
before providers run, and `CapturedUtc` describes snapshot-construction
completion.

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
fleet management, persistent storage, or test-control bypasses. The broader
Inspection rollout remains frozen in [`TODO.md`](../../../TODO.md).

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
