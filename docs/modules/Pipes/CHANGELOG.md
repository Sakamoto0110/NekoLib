# NekoLib.Pipes Changelog

**Document ID:** PIPE-CHANGELOG

**Schema version:** 1

**Kind:** reference

**Lifecycle:** current

**Subject:** consumer-visible evolution of NekoLib.Pipes

**Surface:** changelog

**Boundary:** pipes

**Authority role:** non-normative

**Mutation:** authored

**Indexing:** include

The [coordinated family changelog](../../../CHANGELOG.md) remains the release
summary. This file records Pipes-specific consumer impact without duplicating
package hashes or release provenance.

## 1.1.0

**Packages:** `NekoLib.Pipes`

**Compatibility class:** additive

**Consumer impact:** Package candidates produced through the corrected flow deliver XML member documentation for the accepted public API; compiled signatures and runtime behavior are unchanged.

**Migration:** none

- Documentation-enabled builds produce XML assets for both target assemblies.
  Immutable candidate `1.1.0-local.9` is the qualifying package evidence that
  the managed package contains its package-owned XML file and that isolated
  `PackageReference` consumers receive it.
- `IPipeMetrics.OnServerClientConnected` and
  `IPipeMetrics.OnServerClientDisconnected` now document that an event hub
  raises them with the `.events` endpoint name, so a sink shared with the RPC
  server aggregates both peer kinds. This corrects the documentation only; the
  behavior is unchanged and predates this entry.

## 1.0.0

**Packages:** `NekoLib.Pipes`

**Compatibility class:** mixed

**Consumer impact:** The pre-stable candidate surface was corrected before the first stable contract; consumers upgrading from an earlier candidate may require source changes and recompilation.

**Migration:** `docs/modules/Pipes/migrations/f1.md`

- `PipeClient` is no longer disposable and `SimplePipeMetrics` is sealed.
- `PipeServer`, `PipeEventHub`, and `PipeEventClient` gained cross-target
  `ShutdownAsync`, terminal race-safe start and shutdown, corrected modern async
  disposal, and modern server async disposal.
- Constructors capture and validate options instead of retaining live mutable
  configuration; `PipeEventClient` validates its live timeout and reconnect-delay
  setters, and only `AutoReconnect` remains a live switch.
- Metrics callback exceptions are isolated from transport outcomes.
- Oversized events are rejected before enqueue without disconnecting
  subscribers or incrementing `Published`.
- `PipeEventClient` gained isolated `OnError` observation and raises
  `OnDisconnected` only for an established connection.
- `PipeErrorCodes` publishes the four framework wire codes, and in-flight
  `net481` connect now observes cancellation.
- The target-specific `JToken?`/`JsonElement?` payload contract, the `net481`
  Newtonsoft.Json dependency, access-policy defaults, bounded event policies,
  application-defined error codes, and application-owned authorization are
  unchanged.
