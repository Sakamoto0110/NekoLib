# NekoLib.Telemetry

**Document ID:** TEL-INTRODUCTION

**Schema version:** 1

**Kind:** reference

**Lifecycle:** current

**Subject:** concise consumer introduction to the NekoLib.Telemetry boundary

**Surface:** introduction

**Boundary:** telemetry

**Authority role:** non-normative

**Mutation:** authored

**Indexing:** include

NekoLib.Telemetry answers "how long did that take, and how did it end" for
desktop and unattended applications, without an agent, an exporter, or a metrics
backend. It records correlated operations with checkpoints, a terminal outcome,
string dimensions and numeric measurements, and keeps the newest completed ones
in a bounded in-memory window that an incident collector can read.

The composition root constructs `TelemetryPipeline` with the sinks it wants and
hands feature modules the Core `ITelemetry` contract. Navigation is the only
in-repository producer: it reports one correlated page-switch operation per
navigation. Export and aggregation are an `ITelemetrySink` implementation, not a
subclass — `TelemetryPipeline` is sealed.

Three boundaries are worth knowing before adopting it. **The caller owns exactly
one explicit terminal**: `ITelemetryOperation` is not `IDisposable`, so an
operation that is started and never completed is simply never recorded — no
sink write, no error, nothing in the snapshot. **Dispatch is synchronous and
inline**, so a slow sink blocks the completing thread and, on the Navigation
path, becomes a UI stall. And **version 1 keeps raw completed operations in
bounded memory only** — it does not persist, aggregate, export, or sample them,
so nothing here survives a restart.

The pipeline is deliberately not `IDisposable`: it owns no handle, no buffer
that outlives a call, and no background worker, so there is nothing to flush.

Telemetry is a capability separate from Logging and Inspection. They share Core
contracts and a composition root, not an implementation or an authority.

Start with the [technical reference](REFERENCE.md) for the lifecycle, time, and
dispatch contracts. Use the [module manifest](MANIFEST.md) for package, API,
evidence, audit, and migration routes.
