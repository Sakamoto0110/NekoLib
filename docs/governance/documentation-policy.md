# Documentation Policy

**Document ID:** GLOBAL-DOCUMENTATION-POLICY

**Schema version:** 1

**Kind:** reference

**Lifecycle:** current

**Subject:** authority, lifecycle, ownership, migration, and indexing rules for NekoLib documentation

**Surface:** policy

**Boundary:** global

**Authority role:** normative

**Mutation:** authored

**Indexing:** include

This policy defines how the module-first knowledge base is organized and how it
coexists with implementation, build, release, roadmap, and historical evidence.
It extends the classification model in the [documentation index](../README.md).

## Authority

| Fact | Owner |
|---|---|
| Implemented behavior | Current source code |
| Targets, references, build, and package properties | Project and `Directory.Build.*` files |
| Solution membership | `NekoLib.sln` |
| Actual public API | Compiled assemblies |
| Accepted public API baseline | Assembly-derived manifests under `eng/public-api/` |
| Current module technical contract | `docs/modules/<Boundary>/REFERENCE.md` |
| Module routing and identity | `docs/modules/<Boundary>/MANIFEST.md` |
| Product direction, intentions, planning horizons, guardrails, and freezes | Root `ROADMAP.md` |
| Formally promoted work, execution order, gates, and completion criteria | Root `TODO.md` |
| Scoped reasoning premises and their preserved lifecycle | `docs/governance/premise-policy.md` plus accepted records under `docs/premises/`; never implementation or evidence truth |
| Confirmed defects | Module `ISSUES.md`; scheduling still requires explicit promotion to `TODO.md` |
| Unconfirmed observations | Module `FINDINGS.md` |
| Unpromoted proposals | One concise record per file under `docs/proposals/` |
| Evidence requirements | Module `VALIDATION_REQUIREMENTS.md` |
| Executed evidence | Module `VALIDATIONS.md`, tests, scenarios, and artifacts |
| Historical rationale | Dated audits at their recorded baseline |
| Coordinated release provenance | Global release records |

Supporting documents may summarize and link to an owner. They must not maintain
a competing mutable list.

`ROADMAP.md` states direction but does not authorize implementation. `TODO.md`
is the sole promoted-work scheduler. A proposal is a lightweight non-normative
idea record, not a required promotion stage: formalized findings, issues,
audits, external evidence, or direct owner decisions may also be promoted.

## Module surfaces

- `MANIFEST.md` routes identity, projects, packages, dependencies, API oracles,
  documentation surfaces, evidence locations, and related boundaries.
- `README.md` is a concise consumer introduction.
- `REFERENCE.md` is the normative current technical contract.
- `INTERNALS.md` is optional and limited to maintainer-facing implementation
  invariants that do not belong in the public contract.
- `HISTORY.md` is a factual chronological append-only timeline.
- `CHANGELOG.md` owns detailed consumer-visible module evolution once populated;
  the root changelog remains the coordinated family summary.
- `ISSUES.md` and `FINDINGS.md` keep confirmed defects and uncertain knowledge
  separate. Cross-repository proposal files carry a `Boundary` so a module can
  discover relevant unpromoted ideas without maintaining a duplicate backlog.
- `VALIDATION_REQUIREMENTS.md` defines the evidence contract;
  `VALIDATIONS.md` records executed evidence.
- `audits/` and `migrations/` preserve module rationale and consumer transition.

The manifest is the sole owner of the boundary's validation-profile list.
`VALIDATION_REQUIREMENTS.md` derives concrete requirements from those profiles
without repeating the mutable list. Migrations are non-normative consumer
guidance; they link to the current technical reference rather than owning its
contract.

Sub-boundaries represent package, platform, adapter, protocol, or deployment
surfaces inside a bounded context. They are not automatically independent domain
modules.

`Related boundaries` records documentation, consumer-evidence, adapter, or
deployment relationships that help traversal. It does not create or imply a
project or package dependency; those remain separate manifest fields.

## Agent authoring interoperability

Agent-specific skills are adapters over one shared documentation contract. The
[agent documentation contract](agent-documentation-contract.md) defines the
common invariants, registered profiles, permitted role variation, and
cross-profile handoff rules. A profile may add an architecture, implementation,
test, or evidence lens, but it does not own a separate document format or
authority model.

Documents remain interpretable without knowing which agent authored them.
Role-specific procedures stay in skills; canonical documents use only the
shared schema, templates, paths, vocabulary, and evidence meanings.

## Metadata and retrieval

Indexable documents in the module-first tree use the bold-label metadata defined
by the [documentation schema](../schemas/documentation-schema.json). `Kind` and
`Lifecycle` retain their existing meanings. `Surface`, `Boundary`, `Authority
role`, `Mutation`, and `Indexing` are independent dimensions.

Every metadata value occupies one physical line. Long prose belongs in the body
rather than an implicit continuation that another parser may truncate.
`Canonical` paths are always repository-relative, even when the value resembles
a filename beside the current document.

- `current` is maintained with the repository.
- `frozen` remains live but cannot expand without its recorded unfreeze.
- `historical` is a dated snapshot and never current-state authority.
- A historical audit uses `Mutation: snapshot`: its recorded body and baseline
  are preserved, while narrowly identified metadata/link corrections and later
  reconciliation sections may be appended.
- `Surface: portal`, `Authority role: portal`, `Indexing: pointer-only`, and
  `Canonical` form one combined contract. Such portals contain only routing
  metadata and one canonical link.
- Empty registries state their baseline explicitly. An empty scaffold is not
  evidence that no issue, finding, history, proposal, or validation exists.

Document IDs and record IDs are stable. Existing identifiers such as `F1-NAV`,
`DATA-016`, or `NEKOEXP0001` remain valid aliases and are never renumbered.

## Conflict handling

- If source and a current normative reference disagree, record the conflict and
  stop for disposition; do not silently rewrite the contract.
- If an assembly and its accepted API manifest disagree, treat it as an API
  mismatch; never update the baseline automatically.
- Audits remain true only for their recorded baseline.
- Evidence demonstrates claims but does not define behavior.
- A finding becomes an issue only after verification.
- A proposal, issue, finding, audit, external-evidence record, or direct owner
  decision becomes scheduled work only through explicit, formalized promotion
  to `TODO.md`. `docs/proposals/` is not an exclusive source queue.

## Structural migration

Move an existing authority with `git mv`, make only indispensable link and path
corrections, and add metadata or portals in a later logical change. Never copy a
current reference into a second maintained authority. A source-adjacent README
may remain as a minimal `pointer-only` portal whose `Canonical` field names the
module reference.

Moved audits retain their body, recorded baseline, original path, and snapshot
mutation. Relative links may be corrected for the new location; historical
claims must not be rewritten as if the audit knew later outcomes.

Keep runtime instructions with their executable scenarios and keep accepted API
manifests under `eng/public-api/`. Generated indexes belong under
`artifacts/documentation/` or ignored local storage and are never authority.
