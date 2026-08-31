---
name: nekolib-documentation
description: Design, migrate, populate, or validate NekoLib's module-first documentation under the shared cross-agent contract, with an additional test and evidence emphasis that remains interoperable with other authoring profiles.
---

# Maintain NekoLib Module Documentation

**Document ID:** CLAUDE-NEKOLIB-DOCUMENTATION-SKILL

**Schema version:** 1

**Kind:** guide

**Lifecycle:** current

**Subject:** Claude execution protocol for NekoLib module-first documentation

**Surface:** guide

**Boundary:** global

**Authority role:** non-normative

**Mutation:** authored

**Indexing:** include

**Documentation contract:** docs/governance/agent-documentation-contract.md

**Authoring profile:** claude

Use this skill only with an explicit mode and, for module work, an explicit
boundary. Start from the repository root, preserve the user's requested stop
point, and apply the shared
[agent documentation contract](../../../docs/governance/agent-documentation-contract.md).

## Role profile

Claude is a general documentation author, not a validation-only author. It may
produce or maintain every document surface authorized by the requested mode and
boundary. Apply an additional test and evidence lens throughout that work:
identify testable claims, target and environment matrices, negative paths,
boundary realism, traceability, reproducibility, executed evidence, and gaps.

This emphasis must not displace architecture, consumer, history, migration, or
technical-reference meaning. Read and continue documents authored through the
Codex profile without translation, provenance loss, or semantic downgrade. Do
not introduce Claude-specific vocabulary or field meanings into shared module
documents.

## Modes

- `inventory/design`: read-only discovery, authority classification, boundary
  modeling, and migration planning.
- `migrate-structure`: only an explicitly approved source-to-destination map,
  indispensable link fixes, manifests, portals, schemas, templates, indexes,
  and verifiers. Do not normalize module semantics.
- `populate-module <boundary>`: reconcile and author one module boundary. Full
  source inspection is allowed and required where the contract depends on
  lifecycle, concurrency, ownership, failure, recovery, security, wire or
  protocol behavior, platform behavior, or a source/document conflict.
- `validate-module <boundary>`: run the structural self-review and the
  proportionate repository validation without expanding the boundary.

Do not infer authorization for a later mode from completion of an earlier one.
Stop at the requested module boundary.

## Required starting checks

1. Read `AGENTS.md`, `ROADMAP.md`, `TODO.md`, `docs/README.md`, the module
   `MANIFEST.md`, the shared
   authoring contract, the
   [documentation policy](../../../docs/governance/documentation-policy.md), and
   the [validation policy](../../../docs/governance/validation-policy.md), plus
   the [shared schema](../../../docs/schemas/documentation-schema.json) and the
   [agent skill registry](../../../docs/schemas/agent-skill-registry.json).
2. Record branch, HEAD, upstream divergence, worktree/index state, and whether
   the requested review covers HEAD, the working tree, or both.
3. Preserve unrelated tracked, untracked, and ignored work. Do not inspect
   ignored or external evidence unless it is explicitly in scope.
4. Confirm the module's projects, packages, targets, dependencies, solution
   membership, and accepted API baselines from project and build sources.
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

## Module traversal

Use progressive discovery from the manifest, then apply
[authority traversal](references/authority-traversal.md). For
`populate-module`, normally inspect:

1. current reference and consumer introduction;
2. project files and accepted compiled API manifests;
3. complete relevant source where useful;
4. tests and relevant runtime scenarios;
5. audits and history;
6. migrations and global/module changelogs;
7. issues, findings, proposals, and validation evidence;
8. historical artifacts only when they are relevant and authorized.

Reuse, move, normalize, cross-link, reconcile against current source, and then
fill gaps. Do not discard sound existing documentation and generate a parallel
replacement.

## Output rules

- `MANIFEST.md` routes identity and knowledge. It does not reproduce public
  symbols.
- `README.md` stays concise; `REFERENCE.md` owns the normative technical
  contract.
- `ROADMAP.md` owns direction and intent; `TODO.md` remains the only
  accepted/promoted work scheduler.
- Findings remain non-normative, issues require evidence, and one-file
  proposals remain unpromoted. Promotion may originate elsewhere when the
  decision is formalized and accepted.
- Validation requirements are derived from architecture and risk, not from
  existing coverage alone. Evidence is recorded separately.
- Apply the Claude evidence lens to every surface where it adds useful
  traceability, but do not turn general documentation into validation-only
  documentation.
- Audits retain their baseline and body. History is factual, chronological, and
  append-only. Migration guidance remains faithful to its consumer transition.
- Use the canonical templates under
  [docs/templates](../../../docs/templates/) and the closed vocabulary
  in the [schema](../../../docs/schemas/documentation-schema.json).

Apply the [stop rules](references/stop-rules.md) immediately when triggered.
Before completion, perform the complete [self-review](references/self-review.md)
and run `eng/verify-docs.ps1` plus `git diff --check`. When a skill adapter, the
skill registry, or a shared skill contract changed, also run
`eng/verify-skills.ps1`. When an active work campaign applies, plan or execute
only its authorized phase through `eng/invoke-work-campaign.ps1`; do not
manually repeat a recorded PASS for the same finalizer and fingerprint unless
changed risk or explicit instruction justifies `-Force`. When premises were
created, changed, or consumed, run `eng/verify-premises.ps1` and report the IDs,
effective statuses, omitted investigation, retained validation, and any
contradictions. Never activate a premise from agent confidence alone. Add build,
test, API, runtime, or package validation only when the changed surface
justifies it.
