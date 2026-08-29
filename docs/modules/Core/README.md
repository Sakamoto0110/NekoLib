# NekoLib.Core

**Document ID:** CORE-INTRODUCTION

**Schema version:** 1

**Kind:** reference

**Lifecycle:** current

**Subject:** concise consumer introduction to the NekoLib.Core boundary

**Surface:** introduction

**Boundary:** core

**Authority role:** non-normative

**Mutation:** authored

**Indexing:** include

`NekoLib.Core` is the dependency-free contract package shared by NekoLib's
Logging, Telemetry, Inspection, Diagnostics, Navigation, and Watchdog
capabilities. It targets `net481` and `net9.0` with one public surface.

Consumers normally receive Core transitively through a feature package. Direct
use is appropriate when an application implements a custom logger, sink,
telemetry engine, telemetry sink, Inspection recorder, or read-only snapshot
source, or when it consumes the small models and null objects at a composition
boundary.

Core deliberately supplies no concrete pipeline, persistence, serializer,
transport, platform hook, discovery system, or automatic lifetime management.
The composition root constructs and owns implementations. The only process-wide
slot is the explicitly installed `InspectionProvider`; it does not own the
recorder. `IInspectionRecorder.RegisterAction` remains experimental as
`NEKOEXP0001` and is not an authorization or remote-control boundary.

Start with the [technical reference](REFERENCE.md) for ownership, completion,
snapshot, callback, mutability, security, and extension contracts. Use the
[module manifest](MANIFEST.md) for API baselines, evidence, audits, migrations,
source, tests, and related boundaries.
