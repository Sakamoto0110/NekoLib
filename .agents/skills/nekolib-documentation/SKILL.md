---
name: nekolib-documentation
description: Design, migrate, populate, or validate NekoLib's module-first documentation under the shared cross-agent contract, with a source, architecture, public API, and implementation emphasis that remains interoperable with other authoring profiles.
---

# Maintain NekoLib Module Documentation

**Document ID:** CODEX-NEKOLIB-DOCUMENTATION-SKILL

**Schema version:** 1

**Kind:** guide

**Lifecycle:** current

**Subject:** Codex execution protocol for interoperable NekoLib module-first documentation

**Surface:** guide

**Boundary:** global

**Authority role:** non-normative

**Mutation:** authored

**Indexing:** include

**Documentation contract:** docs/governance/agent-documentation-contract.md

**Authoring profile:** codex

Use this skill only with an explicit mode and, for module work, an explicit
boundary. Start from the repository root, preserve the user's requested stop
point, and apply the shared
[agent documentation contract](../../../docs/governance/agent-documentation-contract.md).

## Role profile

Codex is a general documentation author. Apply an additional source,
architecture, public API, and implementation lens without restricting the
document surfaces Codex may author. Give proportionate attention to ownership,
dependencies, invariants, compatibility, failure behavior, and the relationship
between the current implementation and its documented contract.

This emphasis does not weaken validation responsibilities. Inspect relevant
tests and evidence, derive risk-based validation requirements, and state every
unexecuted evidence layer. Read and continue documents authored through the
Claude profile without translation, provenance loss, or semantic downgrade.

## Modes

- `inventory/design`: read-only discovery, authority classification, boundary
  modeling, and migration planning. Use the repository-inventory skill when the
  request benefits from physical-file counts, repeated `README.md` counts,
  documentation/skill topology, Git-state classification, or a compact Markdown
  tree; do not infer semantic completeness from those observations.
- `migrate-structure`: only an explicitly approved source-to-destination map,
  indispensable link fixes, manifests, portals, schemas, templates, indexes,
  skills, and verifiers. Do not normalize module semantics.
- `populate-module <boundary>`: reconcile and author one module boundary. Read
  the complete source needed to establish contracts, ownership, lifecycle,
  concurrency, failure, recovery, security, platform, wire, and compatibility
  behavior.
- `validate-module <boundary>`: run structural self-review and proportionate
  repository validation without expanding the boundary.

Do not infer authorization for a later mode from completion of an earlier one.
Stop at the requested module boundary.

## Required starting checks

1. Read `../../../AGENTS.md`, `../../../ROADMAP.md`, `../../../TODO.md`,
   `../../../docs/README.md`, the affected
   `MANIFEST.md`, the shared authoring contract, the
   [documentation policy](../../../docs/governance/documentation-policy.md),
   the [validation policy](../../../docs/governance/validation-policy.md), and
   the [schema](../../../docs/schemas/documentation-schema.json), plus the
   [agent skill registry](../../../docs/schemas/agent-skill-registry.json).
2. Record branch, HEAD, upstream divergence, worktree/index state, and whether
   the requested work covers HEAD, the working tree, or both.
3. Preserve unrelated tracked, untracked, and ignored work. Do not inspect
   ignored or external evidence unless it is explicitly in scope.
4. Confirm project, package, target, dependency, solution, and accepted API
   baseline facts from their authoritative sources.
5. If an explicit work-campaign manifest is in scope, read the
   [work campaign policy](../../../docs/governance/work-campaign-policy.md) and
   that manifest. Reconfirm its authority, branch, baseline, scope, and
   pre-existing paths; the campaign coordinates execution but does not authorize
   a mode, mutation, commit, push, package, or release.
6. If a scoped premise is offered or applicable, read the
   [premise policy](../../../docs/governance/premise-policy.md), the complete
   premise record, and run `eng/verify-premises.ps1`. Use only effective
   `active` for the exact permitted shortcut; never weaken validation or a stop
   rule, and suspend use at the first qualifying contradiction.
7. An existing local documentation index may accelerate inventory and retrieval
   only when `eng/search-docs.ps1 -Status` reports the current checkout. Read the
   [index policy](../../../docs/governance/documentation-index-policy.md), retain
   result provenance, and verify every claim against its normal authority. A
   missing or stale index falls back to direct repository traversal.

## Authority traversal

Use each source only for the facts it owns:

1. source and project files for implementation and topology;
2. compiled assemblies and accepted manifests for actual and reviewed API;
3. current policies and the module reference for documented contract;
4. `ROADMAP.md` for direction, intentions, guardrails, freezes, and planning
   horizons;
5. `TODO.md` for formally promoted work, execution order, gates, and completion;
6. tests, scenarios, package probes, and evidence records for bounded evidence;
7. migrations and changelogs for consumer transition;
8. audits and history for baseline-bound rationale;
9. findings, proposals, and historical agent guidance only as non-normative
   leads.

If current source and a normative reference conflict, record the exact conflict
and stop for disposition. If compiled API and an accepted manifest conflict,
stop as an API mismatch. Never update a baseline automatically.

## Output rules

- Use the canonical templates under
  [docs/templates](../../../docs/templates/) and their shared field meanings.
- `MANIFEST.md` routes identity and knowledge; it does not reproduce public
  symbols.
- `README.md` stays concise; `REFERENCE.md` owns the normative technical
  contract.
- `ROADMAP.md` owns direction and intent; `TODO.md` remains the only accepted
  and promoted scheduler.
- Findings remain non-normative, issues require evidence, and one-file
  proposals remain unpromoted. Promotion may originate elsewhere when the
  decision is formalized and accepted.
- Requirements are derived from architecture and risk; evidence records only
  what actually ran.
- Historical documents retain their baseline, body, chronology, and original
  provenance.
- Physical file counts and inventory trees are baseline-bound discovery
  snapshots. They do not establish canonical ownership, documentation
  completeness, or migration readiness.
- Do not add Codex-specific vocabulary or assumptions to shared module
  documents.

## Stop and self-review

Stop when the requested outcome requires a product or public API change, crosses
a freeze, promotes work automatically, exceeds the approved boundary, overlaps
pre-existing work, lacks claim-grade evidence, would rewrite a historical
snapshot, or when project, package, target, or boundary ownership cannot be
established.

Before completion, confirm that another registered profile can interpret every
changed document from its metadata, authority, links, and evidence alone. Run
`eng/verify-docs.ps1` and `git diff --check`. When a skill adapter, the skill
registry, or a shared skill contract changed, also run `eng/verify-skills.ps1`.
When an active work campaign applies, plan or execute only its authorized phase
through `eng/invoke-work-campaign.ps1`; do not manually repeat a recorded PASS
for the same finalizer and fingerprint unless changed risk or explicit
instruction justifies `-Force`.
When premises were created, changed, or consumed, run
`eng/verify-premises.ps1` and report the IDs, effective statuses, omitted
investigation, retained validation, and any contradictions. Never activate a
premise from agent confidence alone.
Add build, test, API, runtime, or package validation only when the changed
surface justifies it. Report source, build, test, API, runtime, interactive,
package, and release evidence separately, including every layer not run.
