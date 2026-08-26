# Documentation Schemas

**Document ID:** GLOBAL-DOCUMENTATION-SCHEMAS

**Schema version:** 1

**Kind:** reference

**Lifecycle:** current

**Subject:** deterministic schemas for the module-first documentation tree

**Surface:** index

**Boundary:** global

**Authority role:** normative

**Mutation:** authored

**Indexing:** include

[`documentation-schema.json`](documentation-schema.json) is the structural
contract for document metadata, module manifests, stable record IDs, validation
taxonomy, validation profiles, and soak evidence. It identifies the logical
documentation skill but does not duplicate its adapter paths.

[`agent-skill-registry.json`](agent-skill-registry.json) owns repository skill
identity, Codex and Claude adapter paths, and intended parity. A
`single-profile` skill requires no counterpart; `contract-equivalent` skills
share externally observable contracts; `near-mirror` skills additionally share
common modes and core workflow semantics while retaining explicitly allowed
agent-specific differences.

Shared documentation-role semantics remain in the
[agent documentation contract](../governance/agent-documentation-contract.md).
Neither schema duplicates authored knowledge or acts as an implementation,
product, roadmap, or public API oracle.

Schema-backed metadata uses one physical line per value. The manifest owns each
boundary's validation-profile list; module requirements derive from it without
creating a second owner.

The canonical documentation verifier is
[`eng/verify-docs.ps1`](../../eng/verify-docs.ps1). It resolves the registered
documentation adapters through the skill registry. The canonical skill-topology
verifier is [`eng/verify-skills.ps1`](../../eng/verify-skills.ps1); it enforces
registry coverage, path and frontmatter identity, parity-policy structure, and
declared common-mode presence. Neither verifier infers semantic parity or
authorizes edits.

Generated catalogs, link graphs, chunks, or local indexes are derived output
and must remain non-authoritative.
