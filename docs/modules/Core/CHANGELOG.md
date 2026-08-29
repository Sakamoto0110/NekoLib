# NekoLib.Core Changelog

**Document ID:** CORE-CHANGELOG

**Schema version:** 1

**Kind:** reference

**Lifecycle:** current

**Subject:** consumer-visible evolution of the NekoLib.Core boundary

**Surface:** changelog

**Boundary:** core

**Authority role:** non-normative

**Mutation:** authored

**Indexing:** include

The [coordinated family changelog](../../../CHANGELOG.md) remains the release
summary. This file records Core-specific consumer impact without duplicating
package hashes or release provenance.

## Unreleased

**Packages:** `NekoLib.Core`

**Compatibility class:** additive

**Consumer impact:** Package-owned XML documentation now describes every public member and the supported custom implementation seams. Compiled signatures and runtime behavior are unchanged.

**Migration:** none

- Both target assemblies generate matching XML documentation for the 109 public
  member entries reviewed in the documentation campaign.
- The contract text now distinguishes explicit composition from discovery,
  outer collection protection from deep immutability, and Core interfaces from
  the behavior of the supplied concrete capability packages.
- This documentation work does not make `NEKOEXP0001` stable and does not
  unfreeze broad Inspection instrumentation.

## 1.0.0

**Packages:** `NekoLib.Core`

**Compatibility class:** behavioral

**Consumer impact:** The pre-stable candidate stopped exposing caller-owned mutable outer collections through published models, and action registration became explicitly experimental. No public type, member, signature, namespace, target, dependency, or null-object behavior was removed.

**Migration:** `docs/modules/Core/migrations/f1.md`

- `TelemetryCheckpoint`, `TelemetryOperation`, and `InspectionSnapshot`
  defensively copy and read-only-wrap their outer collections. Contained
  checkpoint, operation, exception, payload, and state objects remain shallow
  references.
- `IInspectionRecorder.RegisterAction` carries the warning-only experimental
  marker `NEKOEXP0001`. Authorization, invocation, async work, cancellation,
  timeout, UI marshalling, remote control, and feature-module adoption are not
  stable contracts.
- The Logging, Telemetry, passive Inspection, null-object, target, package, and
  dependency boundaries otherwise remain unchanged.
