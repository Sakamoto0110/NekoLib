# NekoLib.Data

**Document ID:** DATA-INTRODUCTION

**Schema version:** 1

**Kind:** reference

**Lifecycle:** current

**Subject:** concise consumer introduction to the NekoLib.Data boundary

**Surface:** introduction

**Boundary:** data

**Authority role:** non-normative

**Mutation:** authored

**Indexing:** include

NekoLib.Data is an instance-scoped ADO.NET gateway for applications that need
one query, mapping, transaction, and lifecycle contract across `net481` and
`net9.0`. It provides provider-neutral query construction, provider-specific
translation, raw/DTO/dynamic result shapes, context-affine sessions, explicit
type-adaptation policy, and synchronous lifecycle evidence.

The application supplies the database provider package, credentials, connection
string, connection factory, translator, retry policy, and provider-specific
configuration. Data owns none of those application concerns, has no NekoLib
project dependency, and does not instrument itself through Core or Inspection.

Compose a `QueryExecutionContext`, retain a `DatabaseGateway` or one of its
narrower capability interfaces, and dispose the context only after every
gateway and session has finished. Streaming is a real `net9.0` surface and is
absent from the `net481` assembly.

Start with the [technical reference](REFERENCE.md) for ownership, trusted SQL,
extension, mapping, adaptation, cancellation, and provider contracts. Use the
[module manifest](MANIFEST.md) for the project, package, API baselines, evidence
requirements, audits, migrations, and runtime-scenario routes.
