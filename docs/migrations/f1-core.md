# F1-CORE Candidate Migration

**Kind:** guide

**Lifecycle:** current

**Subject:** migration from the initial Core candidate surface to the accepted
F1-CORE contract

This guide covers the pre-stable `NekoLib.Core` correction accepted on
2026-08-17. The rationale is preserved in the
[Core public API review](../audit/core-public-api-review-2026-08-17.md).

## Collection-bearing models

`TelemetryCheckpoint`, `TelemetryOperation`, and `InspectionSnapshot` keep
their existing constructors and public property types. Their outer collections
are now defensive read-only snapshots.

Before this correction, a caller could retain a supplied `List` or `Dictionary`
and mutate the published model later. It could also cast exposed collection
objects back to arrays or dictionaries and mutate them directly. That aliasing
was accidental.

Ordinary construction needs no source change:

```csharp
var dimensions = new Dictionary<string, object>
{
    ["target"] = "Catalog"
};

var checkpoint = new TelemetryCheckpoint(
    "ready",
    TimeSpan.FromMilliseconds(12),
    dimensions);
```

If code intentionally relied on later collection mutation, construct a new
model with the new values instead. Values stored inside dimension/state
dictionaries remain shallow references; clone application values explicitly
when a deep capture is required.

## Experimental action registration

`IInspectionRecorder.RegisterAction` now carries:

```csharp
[Obsolete(
    "Experimental API NEKOEXP0001: compatibility is not guaranteed.",
    error: false)]
```

The method was not removed and its runtime behavior did not change. Code that
calls it through `IInspectionRecorder` now receives an experimental warning.
There is no stable replacement in F1-CORE because action authorization,
invocation, async/cancellation/timeout, UI marshalling, and module adoption are
deliberately deferred.

Do not introduce a module action merely to silence the warning. Existing
deliberate application use may suppress `CS0618` in the narrowest possible
scope while accepting `NEKOEXP0001`. Each dependent module will classify its
own concrete action surface during its future F1 review.

## Unchanged contracts

- package ID, namespaces, targets, type names, constructor signatures, and
  collection property types are unchanged;
- Logging and Telemetry remain instance-owned with no global provider;
- Inspection passive recording, state providers, snapshots, null behavior, and
  singleton-capable provider lifecycle remain stable candidates;
- exception, dimension, payload, and state values remain shallow references;
- no dependency, serializer, privileged control surface, or broad Inspection
  producer was added.
