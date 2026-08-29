# NekoLib.Logging

**Document ID:** LOG-INTRODUCTION

**Schema version:** 1

**Kind:** reference

**Lifecycle:** current

**Subject:** concise consumer introduction to the NekoLib.Logging boundary

**Surface:** introduction

**Boundary:** logging

**Authority role:** non-normative

**Mutation:** authored

**Indexing:** include

NekoLib.Logging is the concrete logging pipeline for desktop and unattended
applications that need ordered, severity-filtered entries, a bounded in-memory
window an incident collector can read, and bounded rolling-file persistence,
without a logging framework, a provider model, or a process-wide facade.

The composition root constructs `Logger` with the sinks it wants, hands feature
modules the Core `ILogger` contract, and owns the logger's lifetime. Two sinks
ship: `DebugLogSink` for the process trace channel and `RollingFileLogSink` for
size-bounded files with archive retention. Custom persistence is an `ILogSink`
or `IFlushableLogSink` implementation, not a subclass — `Logger` is sealed.

The boundary worth knowing before adopting it: **dispatch is synchronous and
inline on the calling thread**. That is what makes delivery ordering,
crash-time completeness, and the bounded flush meaningful, and it is also why a
slow sink slows its callers. There is no background queue, no sampling, and no
transport. Logging never redacts, truncates, or filters caller content; a sink
that persists or transmits entries owns that.

Logging is a capability separate from Telemetry and Inspection. They share
Core contracts and a composition root, not an implementation or an authority.

Start with the [technical reference](REFERENCE.md) for the delivery, flush, and
disposal contracts. Use the [module manifest](MANIFEST.md) for package, API,
evidence, audit, and migration routes.
