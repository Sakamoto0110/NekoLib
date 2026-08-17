# F1-INSP Candidate Migration

**Kind:** guide

**Lifecycle:** current

**Subject:** migration from the initial Inspection candidate surface to the
accepted F1-INSP passive runtime and experimental action boundary

This guide covers the pre-stable `NekoLib.Inspection` correction accepted on
2026-08-17. The rationale, evidence, and rejected alternatives are preserved in
the [Inspection public API review](../audit/inspection-public-api-review-2026-08-17.md).

No public type, member, signature, nullability annotation, default value,
namespace, target, or project reference was removed or changed. The only
compiled API changes are four intentional `ObsoleteAttribute` markers carrying
`NEKOEXP0001`. The remaining changes tighten pre-stable behavior.

## Concrete action members are explicitly experimental

These members now emit `CS0618` with the exact marker
`Experimental API NEKOEXP0001: compatibility is not guaranteed.`:

- `InspectionRuntime.RegisterAction`;
- `InspectionRuntime.TryInvokeAction`;
- `InspectionRuntime.ActionKeys`;
- `InspectionRuntimeDiagnostics.ActionCount`.

Code that deliberately uses the in-process experiment may opt in narrowly:

```csharp
#pragma warning disable CS0618 // Deliberate NEKOEXP0001 use; authorization is application-owned.
using var registration = inspection.RegisterAction(
    "Maintenance",
    "refresh_cache",
    argument => RefreshCache(argument));
#pragma warning restore CS0618
```

The marker does not authorize a caller. Applications own authentication,
authorization, lifetime, and thread/UI policy. No stable async, cancellation,
timeout, discovery, remote, or marshalling contract is promised. Passive
consumers that only asserted zero action counts should remove that dependency
instead of opting into an experiment they do not use.

## Identifiers must be non-ambiguous

`Record`, `RegisterStateProvider`, `RegisterAction`, and `TryInvokeAction` now
reject blank identity components. Modules and provider/action components also
reject the reserved `::` delimiter. Null inputs continue to throw
`ArgumentNullException` with the original parameter names.

Valid public identities remain `module::key`, compared with ordinal
case-sensitive semantics. Rename invalid inputs rather than escaping them:

```csharp
// Invalid: the module contains the reserved delimiter.
// inspection.RegisterStateProvider("Checkout::Primary", "queue", CaptureQueue);

// Valid and unchanged in snapshots.
inspection.RegisterStateProvider("Checkout.Primary", "queue", CaptureQueue);
// Key: Checkout.Primary::queue
```

Operation names must be non-blank but may contain `::`, because they are not
flattened into a registration identity.

## Provider and key order follows registration

Budgeted providers now run in registration order. `StateKeys()` and the
experimental `ActionKeys()` return the same deterministic order. If a consumer
accidentally depended on dictionary enumeration, register essential providers
first and update the expectation to the explicit registration order.

## Timed-out provider work is single-flight

Each state-provider registration now has at most one outstanding budgeted
invocation. Repeated or overlapping `CaptureSnapshot` calls share that work
until it completes. A later capture can start a fresh invocation only after the
prior one has finished.

The timeout still bounds caller completion only and never cancels provider code.
Timeout, null, and thrown markers are unchanged. Exceptions arriving after a
caller timed out are captured and observed rather than left on an abandoned
faulted task. `CaptureState()` remains synchronous and unbudgeted.

Consumers should not use repeated snapshots as a cancellation mechanism. Fix a
provider that can remain blocked, or keep it behind an application-owned bound.

## Capacity errors identify the property

Constructing a runtime with `Capacity < 1` still throws
`ArgumentOutOfRangeException`, but `ParamName` is now `Capacity` instead of
`options`. Code that asserted the old diagnostic metadata must update that
expectation. The default remains `1024`, and options are still read once.

## Clear is inert after disposal

While enabled, every explicit `ClearOperations()` call still increments
`ClearCount`, including an empty clear, and preserves lifetime totals,
evictions, and sequence. After disposal, clear is now inert and cannot mutate
diagnostics.

## Unchanged passive contracts

- direct construction is immediately enabled and caller-owned;
- `EnableGlobal` admits one owner, rolls back failed publication, and restores
  `NullInspection.Instance` when disposed;
- valid post-disposal writes and registrations are inert, while argument
  validation still runs;
- payloads execute outside the operation lock; failures use a type-only marker;
  null remains null; values remain shallow;
- sequence is the concurrency order, while timestamps are wall-clock
  annotations;
- operation retention is chronological and newest-bounded;
- duplicate registrations do not replace their owner, and stale handles cannot
  remove a later owner;
- copied delegates may finish after unregistration or disposal;
- one shared provider budget returns partial evidence and never cancels code;
  operations are copied before provider state;
- `CapturedUtc` is construction completion, not an atomic evidence timestamp;
- `GetDiagnostics()` is coherent within each lock domain and best-effort across
  domains;
- Core protects snapshot outer collections, while payload/state values remain
  shallow;
- Inspection does not persist, transmit, redact, truncate, authorize, or
  remotely expose evidence;
- no broad module instrumentation, IPC, reflection, plugin loading, test-control
  surface, facade, or additional dependency was added.
