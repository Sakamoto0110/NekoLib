# NekoLib.Devices Changelog

**Document ID:** DEV-CHANGELOG

**Schema version:** 1

**Kind:** reference

**Lifecycle:** current

**Subject:** consumer-visible evolution of NekoLib.Devices

**Surface:** changelog

**Boundary:** devices

**Authority role:** non-normative

**Mutation:** authored

**Indexing:** include

The [coordinated family changelog](../../../CHANGELOG.md) remains the release
summary and owns package hashes and release provenance. This file records
Devices-specific consumer impact.

## 1.1.0

**Packages:** `NekoLib.Devices`

**Compatibility class:** additive

**Consumer impact:** Package candidates produced through the corrected flow deliver XML member documentation for the accepted public API; compiled signatures, targets, dependencies, and runtime behaviour are unchanged.

**Migration:** none

- Documentation-enabled builds produce the XML asset for both target assemblies,
  so an IDE consuming the package sees member documentation for the public
  surface. This is a packaging and documentation change only.

## 1.0.0

**Packages:** `NekoLib.Devices`

**Compatibility class:** mixed

**Consumer impact:** One public type was removed, two documented behaviours changed, three additions are opt-in, and the read methods now declare the null they always returned.

**Migration:** [`migrations/f1.md`](migrations/f1.md)

- **Removed** `Protocols.HardwareProtocol`. It was a public abstract class whose
  single `Template` property nothing read, wrote, or derived from, and which
  participated in no contract. Implement `IHardwareProtocol` directly, and
  `IProtocolWithLogging` if you want the engine to inject its logger.
- **Added** `HardwareEngine.CloseTransportOnNoResponse`, default off. A timed-out
  operation leaves the transport in an indeterminate receive state, and by
  default a late reply can still be returned as the next operation's successful
  response. Enabling the switch closes the transport after an operation that
  received no bytes so the next one reopens onto a cleared buffer.
- **Added** `HardwareResponse.Failure`, so a fail-soft response carries the real
  exception instead of only `ex.Message` in the same `Status` field a protocol
  uses for `"Ok"`. A disposed transport, a caller bug, and a silent device were
  previously indistinguishable.
- **Changed** configuration ownership: the engine hands the transport a copy of
  `IHardwareProtocol.PortConfig`, and neither shipped transport writes the
  resolved endpoint back into a caller-owned config. A single send no longer
  rewrites the protocol's own configuration. Read `ICommTransport.PortName` or
  `PortInfo` instead.
- **Changed** `SerialCommTransport` to discard the port input buffer on open,
  matching the stream transports, and to take the transport gate in `Dispose` so
  disposal cannot race an in-flight read.
- **Changed** `Checksum.Sum` and `Checksum.Xor` to reject null consistently with
  `ArgumentNullException`; `Xor` previously threw `NullReferenceException`.
- **Annotated** `ReadLine`, `ReadExact`, and `ReadAll` as returning null on
  timeout, `ParseResponse` as taking `byte[]?`, `Log` as nullable across the
  contracts, and the `SerialCommTransport` constructor parameter as nullable. The
  change is binary-compatible; a nullable-enabled consumer may see new warnings
  exactly where a device can be silent.
