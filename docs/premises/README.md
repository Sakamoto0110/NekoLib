# Scoped Premise Registry

**Document ID:** GLOBAL-SCOPED-PREMISE-REGISTRY

**Schema version:** 1

**Kind:** reference

**Lifecycle:** current

**Subject:** discoverable one-record-per-file registry for accepted scoped premises

**Surface:** index

**Boundary:** global

**Authority role:** normative

**Mutation:** authored

**Indexing:** include

This directory contains shared scoped-premise records governed by the
[scoped premise policy](../governance/premise-policy.md) and validated against
the [premise schema](../schemas/premise-schema.json).

Use one `<premiseId>.json` file per premise. Copy the
[canonical example](../templates/premise.example.json), replace every example
value, obtain the required acceptance decision, and run:

```powershell
.\eng\verify-premises.ps1
```

An empty registry means no shared premise is currently declared. It does not
mean all code requires distrust, nor does it authorize an agent to infer new
premises. Broken, stale, expired, retired, and superseded records remain here so
their contradictions and state history are not erased.
