# Agent Documentation Contract

**Document ID:** GLOBAL-AGENT-DOCUMENTATION-CONTRACT

**Schema version:** 1

**Kind:** reference

**Lifecycle:** current

**Subject:** interoperable documentation authoring contract for agent-specific skills

**Surface:** policy

**Boundary:** global

**Authority role:** normative

**Mutation:** authored

**Indexing:** include

This contract lets different agent roles author, read, review, and continue the
same documentation without creating model-specific knowledge silos. It extends
the [documentation policy](documentation-policy.md), uses the shared
[documentation schema](../schemas/documentation-schema.json), resolves its
agent adapters through the
[skill registry](../schemas/agent-skill-registry.json), and preserves the
[validation policy](validation-policy.md).

## Contract at rest, profiles during authoring

Repository documents have one shared contract at rest. Agent-specific skills
are authoring adapters over that contract. A profile may change investigation
emphasis, sequencing, depth, examples, and evidence density, but it does not
change the meaning or authority of the resulting document.

Every authorized profile may produce or maintain every document surface needed
by its requested mode and boundary. A profile emphasis is never an exclusive
ownership rule.

## Shared invariants

Every agent-authored document must:

- use the canonical path, metadata, vocabulary, stable IDs, and template for
  its surface;
- preserve the authority, lifecycle, mutation, provenance, conflict, and
  promotion rules defined by repository governance;
- remain self-contained and interpretable without private prompts, agent
  memory, hidden state, or knowledge of which model authored it;
- distinguish implemented behavior, intended contract, historical rationale,
  findings, accepted work, requirements, and executed evidence;
- keep `ROADMAP.md` direction, `TODO.md` promoted work, and concise
  `docs/proposals/` ideas distinct, without requiring a proposal file as the
  origin of every promotion;
- preserve source, build, test, API, runtime, interactive, package, and release
  evidence as separate claims;
- expose uncertainty, exclusions, conflicts, and validation gaps explicitly;
- remain readable and maintainable by humans and by every registered authoring
  profile.

The originating agent is not an authority category. Documents must not assign
extra weight to a claim because Codex, Claude, or another tool authored it.

## Work-campaign interoperability

When an explicit work campaign is active, every profile reads the
[work campaign policy](work-campaign-policy.md) and the same local manifest
before acting. The manifest coordinates already-authorized stages and
finalizers; it does not replace the requested mode, module boundary, `TODO.md`,
or any product, API, validation, package, release, commit, or push gate.

Profiles must preserve the manifest baseline, scope, pre-existing-path boundary,
finalizer IDs, phases, and change-set fingerprint semantics. A recorded PASS may
prevent redundant execution of the same finalizer for the same fingerprint, but
it is not durable evidence and must not suppress a newly relevant focused check.
Cross-profile handoff therefore needs only the shared repository documents plus
the explicitly transported local campaign manifest/state; no model-private
interpretation is allowed.

## Scoped-premise interoperability

Every profile applies the same
[scoped premise policy](premise-policy.md). A premise may remove a redundant
investigation step only when the shared record is effectively `active`, covers
the current path, boundary, and campaign, and explicitly permits that shortcut.
No profile may use it to weaken validation, evidence, authorization, conflict,
or stop rules.

Profiles stop relying on the premise at the first qualifying contradiction and
record the same contradiction identity, classification, severity, context, and
evidence fields. The evaluator's effective status controls eligibility even
when the durable status has not yet been reconciled. Agent-specific confidence,
memory, or prose cannot activate, preserve, break, or restore a premise.

## Permitted profile variation

Profiles may differ in:

- the order in which they inspect source, tests, evidence, and historical
  context, subject to the common authority order;
- how deeply they explain architecture, implementation, tests, edge cases,
  traceability, or operational evidence;
- which risks and gaps they surface first;
- how they organize prose inside a template when the surface contract allows
  it.

Profiles must not introduce private field meanings, alternate status values,
competing document locations, hidden cross-document conventions, or weaker
acceptance criteria. They must not turn emphasis into exclusivity: a
test-oriented profile may author a complete technical reference, and a
source-oriented profile remains responsible for proportionate validation.

## Registered profiles

### Codex

Codex is a general documentation author with a source-, architecture-, public
API-, and implementation-oriented lens. It normally gives additional attention
to ownership, boundaries, invariants, dependency direction, compatibility, and
the relationship between documented contracts and current implementation. It
still derives validation requirements, inspects relevant tests, and reports
evidence limits.

### Claude

Claude is a general documentation author with an additional test- and
evidence-oriented lens. It normally gives additional attention to testable
claims, target and environment matrices, negative paths, boundary realism,
traceability, reproducibility, executed evidence, and residual gaps. It is not
restricted to validation documents and must preserve complete architecture,
consumer, history, migration, and technical-reference meaning when those
surfaces are in scope.

## Skill adapter requirements

Every registered documentation skill must:

1. declare `Documentation contract` with this repository-relative path;
2. declare one registered `Authoring profile`;
3. read this contract, the documentation and validation policies, the schema,
   and the affected manifest before authoring;
4. expose the common modes `inventory/design`, `migrate-structure`,
   `populate-module <boundary>`, and `validate-module <boundary>`;
5. preserve explicit authorization, scope, stop points, and conflict rules;
6. treat canonical templates as shared output contracts rather than
   agent-specific formats;
7. consume documents from another profile using only repository metadata,
   authority, and evidence rules, without translation or semantic downgrade;
8. keep role-specific procedures inside the skill instead of embedding them in
   the authored module documents.

The documentation skill is registered with `near-mirror` parity. Its registered
adapters are
[Codex](../../.agents/skills/nekolib-documentation/SKILL.md) and
[Claude](../../.claude/skills/nekolib-documentation/SKILL.md).

Both adapters expose the common modes and preserve equivalent scope, authority,
authorization, safety, stop conditions, output contract, and validation
semantics. Registered authoring emphasis, tool-specific metadata, procedure and
reference decomposition, and presentation within the shared output contract may
differ. Those differences must not weaken any requirement above.

## Cross-profile handoff

When continuing another profile's work, start from the boundary manifest and
the requested document surface. Reconfirm its baseline and owners, then retain
valid content regardless of author. Correct unsupported claims through the
normal evidence and conflict workflow; do not rewrite prose merely to make it
look as though the current profile authored it.

The documentation verifier resolves this logical skill through the shared skill
registry while validating authored document structure. The skill verifier
checks complete adapter registration, path and declared-name identity,
parity-policy structure, and literal common-mode presence. Semantic parity and
technical quality remain subject to source-first review and the module
self-review; neither verifier infers truth from the producing agent.
