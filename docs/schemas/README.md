# Documentation Schemas

**Document ID:** GLOBAL-DOCUMENTATION-SCHEMAS

**Schema version:** 1

**Kind:** reference

**Lifecycle:** current

**Subject:** deterministic schemas for documentation, agent skills, work campaigns, and scoped premises

**Surface:** index

**Boundary:** global

**Authority role:** normative

**Mutation:** authored

**Indexing:** include

[`documentation-schema.json`](documentation-schema.json) is the structural
contract for document metadata, module manifests, stable record IDs, validation
taxonomy, validation profiles, soak evidence, and the one-record-per-file
proposal directory. It identifies the logical documentation skill but does not
duplicate its adapter paths.

[`agent-skill-registry.json`](agent-skill-registry.json) owns repository skill
identity, Codex and Claude adapter paths, and intended parity. A
`single-profile` skill requires no counterpart; `contract-equivalent` skills
share externally observable contracts; `near-mirror` skills additionally share
common modes and core workflow semantics while retaining explicitly allowed
agent-specific differences.

Shared documentation-role semantics remain in the
[agent documentation contract](../governance/agent-documentation-contract.md).

[`work-campaign-schema.json`](work-campaign-schema.json) defines the closed
manifest structure for already-authorized multi-stage repository work. Its
semantics remain in the
[work campaign policy](../governance/work-campaign-policy.md), and the canonical
shape is demonstrated by
[`docs/templates/work-campaign.example.json`](../templates/work-campaign.example.json).
The manifest and its local state coordinate execution; they do not schedule,
authorize, or prove the work.

[`premise-schema.json`](premise-schema.json) defines one shared scoped-premise
record, including its falsifiable statement, acceptance, path/boundary/campaign
scope, permitted shortcuts, evidence anchor, freshness rules, contradictions,
status history, and supersession. Its semantics remain in the
[scoped premise policy](../governance/premise-policy.md), and
[`docs/templates/premise.example.json`](../templates/premise.example.json) is
the canonical starting shape. Premise records never become technical truth or
executed evidence.

None of these schemas duplicates authored knowledge or acts as an
implementation, product, roadmap, public API, or evidence oracle.

Schema-backed metadata uses one physical line per value. The manifest owns each
boundary's validation-profile list; module requirements derive from it without
creating a second owner.

The canonical documentation verifier is
[`eng/verify-docs.ps1`](../../eng/verify-docs.ps1). It resolves the registered
documentation adapters through the skill registry. The canonical skill-topology
verifier is [`eng/verify-skills.ps1`](../../eng/verify-skills.ps1); it enforces
registry coverage, path and frontmatter identity, parity-policy structure, and
declared common-mode presence. Neither verifier infers semantic parity or
authorizes edits. The work-campaign validator and runner is
[`eng/invoke-work-campaign.ps1`](../../eng/invoke-work-campaign.ps1); planning is
its default and execution requires an active manifest under ignored `.local/`.
The premise evaluator is
[`eng/verify-premises.ps1`](../../eng/verify-premises.ps1); it validates shared
records and derives a checkout-specific effective status without activating a
premise or proving its statement.

Generated catalogs, link graphs, chunks, or local indexes are derived output
and must remain non-authoritative.
