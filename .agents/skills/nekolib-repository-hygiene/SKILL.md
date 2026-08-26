---
name: nekolib-repository-hygiene
description: Audit, reorganize, document, and validate NekoLib's cross-cutting repository topology. Use for README, TODO, AGENTS.md, docs governance, agent skill registry and adapter topology, solution or project membership, test taxonomy, runtime_tests, src/Tools, tools, eng, artifacts, .local, duplicate or misplaced files, broken paths, clean-clone reproducibility, and TODO Phase C work. Do not use for an ordinary internal refactor already scoped to one feature module.
---

# Maintain NekoLib Repository Hygiene

Keep this skill procedural. Read current repository sources instead of copying
the Phase C checklist or mutable project facts into this skill.

## Establish scope and authority

1. Run `git status --short` and inspect relevant diffs before drawing
   conclusions or changing files. Preserve unrelated tracked, untracked, and
   ignored work.
2. Read `../../../AGENTS.md`, the relevant section of `../../../TODO.md`, and
   the affected README, `.gitignore`, solution, project, packaging, or
   documentation files. Read all of Phase C when the request invokes it.
3. Determine authority by the kind of fact:
   - use project and `Directory.Build.*` files for targets, references, and
     build or package properties;
   - use `NekoLib.sln` for solution membership;
   - use source for implemented behavior and public surface, with executable
     tests as evidence;
   - use the live roadmap for open work, decisions, and freezes;
   - treat audits as evidence fixed to their audited commit;
   - use `docs/README.md` for documentation registration,
     `docs/governance/` for shared documentation policy,
     `docs/schemas/` for structural vocabulary, and each migrated boundary
     manifest for module-documentation topology.
4. Load the matching sibling module skill when the task verifies or changes
   module-specific technical claims, test placement, or documentation:
   `nekolib-navigation`, `nekolib-data`, or `nekolib-devices`.
5. Load `../nekolib-documentation/SKILL.md` when the task designs, migrates,
   populates, or validates the module-first documentation contract. Repository
   hygiene owns topology mechanics; the documentation skill owns authoring
   semantics and cross-agent interoperability.
6. Load `../nekolib-repository-inventory/SKILL.md` when the task needs broad
   physical-file counts, repeated-basename counts such as `README.md`, Git-state
   or change-set measurement, skill/documentation topology, or a compact
   Markdown tree before interpreting repository topology. Inventory is read-only
   discovery; this skill remains responsible for ownership and change decisions.

Do not turn `AGENTS.md`, the skill, or a historical audit into a competing
public source of truth.

## Follow the requested action

- For review, audit, explanation, or planning requests, remain read-only and
  report evidence-backed findings.
- For explicit organize, move, archive, reconcile, clean, or Phase C
  implementation requests, make the scoped changes without adding a redundant
  approval gate.
- Stop for direction only when different unresolved choices would materially
  change the result or when deletion or movement targets remain ambiguous after
  inspection.

## Inventory before reorganizing

1. Inventory tracked files with `git ls-files` and search text or paths with
   `rg`. Inspect ignored and untracked paths separately with `git status`,
   `git check-ignore`, or a narrowly scoped filesystem listing.
   Use the repository-inventory skill when a reusable compact or tree-shaped
   snapshot is part of the requested outcome.
2. Classify each relevant item as current source, current documentation,
   repository governance, agent workflow adapter, automated verification,
   shared manual scenario, historical record, reproducible tool payload,
   generated artifact, or machine-local scratch.
3. Treat clean-clone availability as a Git fact. A file merely present on the
   current machine is not shared repository evidence.
4. Resolve incoming references, solution or project inclusion, packaging
   behavior, and generated-output ownership before moving or removing a file.
5. Search for identical tracked files first. Distinguish necessary per-assembly
   boilerplate from abandoned copies and inspect ignored outputs separately.
6. Treat `.claude/` as a mixed-ownership tree, not one ignore category.
   Registry-listed skills under `.claude/skills/` are repository-owned
   workflow adapters; runtime state, permissions, locks, worktrees, checkpoints,
   and mailboxes remain local according to `.gitignore`. Confirm each path with
   `git ls-files`, `git status`, and `git check-ignore` before classifying it.

## Apply structural changes

1. Establish the intended `source -> destination` mapping and its affected
   links, namespaces, project items, solution membership, packaging, commands,
   and documentation.
2. Use `git mv` for tracked moves when practical, then update all affected
   references in the same change.
3. Preserve historical audit bodies. Put later reconciliation in clearly
   separated metadata, indexes, or appended sections.
4. Preserve every live frozen block and its resumption context. An explicitly
   authorized temporary unfreeze applies to its stated scope without requiring
   a second equivalent authorization.
5. Classify runtime scenarios before changing ignore rules. Version source and
   instructions for shared evidence; keep machine-only experiments under
   `.local/` and never cite them as repository coverage.
6. Keep repository-owned tool source under `src/Tools/`, maintenance
   orchestration under `eng/`, reproducible local payloads under `tools/`, and
   generated output under `artifacts/`.
7. For module-first documentation topology, keep `docs/README.md`,
   `docs/modules/README.md`, the shared schema, registered adapter paths, and
   affected portals synchronized. Load the documentation skill for field
   meanings, authority, templates, and cross-profile handoff semantics.
8. Treat `.agents/skills/` and registry-listed `.claude/skills/` as versioned
   procedure surfaces, never as product or technical-documentation authority.
   Registration changes must update
   `docs/schemas/agent-skill-registry.json` and any affected shared contract or
   domain schema in the same coherent change. Do not infer one-to-one parity;
   follow the registered parity policy for each logical skill.

Do not:

- impose one internal folder template on every module;
- change feature behavior, cross-project dependencies, or observability freezes
  merely to make the tree look uniform;
- delete ignored or untracked local content unless the request explicitly
  includes that exact scope;
- stage, commit, publish, or overwrite immutable local package versions unless
  the user asks;
- implement the optional LLM code cataloger as incidental hygiene work.

## Validate proportionally

- For documentation and path-only changes, search for old paths and stale
  claims, run the repository documentation verifier when it exists, and run
  `git diff --check`.
- When a repository-owned skill adapter, shared skill contract, or
  `docs/schemas/agent-skill-registry.json` changes, run
  `eng/verify-skills.ps1`. Treat its result as deterministic topology evidence,
  not as proof of semantic parity or technical truth.
- When changing the documentation parser or its path classifications, validate
  every supported PowerShell host affected by the change and confirm that
  untracked candidates are not reported as clean-clone evidence.
- For solution, project, source, or test topology changes, inspect
  `dotnet sln NekoLib.sln list` and run the narrowest relevant build or tests.
- For full Phase C completion, run the final validation declared by the current
  TODO and compare warning identities rather than counts alone.
- When packaging inputs or documentation change, use the canonical pack flow
  with a unique disposable version and never overwrite the local feed.
- Do not report ignored runtime scenarios, local fixtures, or cached executables
  as validation unless their reproducible setup is part of the shared repo.

## Report the result

Summarize the resulting topology, moved or removed files, updated references,
validation performed, and any deliberately unresolved decision. Mention local
or ignored evidence explicitly when it limits confidence.
