---
name: repo-file-inventory
description: Provide read-only, Git-aware repository topology for NekoLib workflows. Use to count physical files and repeated basenames such as README.md, map distribution by area, inspect clean-clone or change-set state, inventory module-documentation surfaces and registered skill adapters, or render a compact Markdown tree. Do not use inventory observations as semantic, product, roadmap, or cleanup authority.
---

# Inventory NekoLib Repository Files

**Document ID:** CODEX-NEKOLIB-REPOSITORY-INVENTORY-SKILL

**Schema version:** 1

**Kind:** guide

**Lifecycle:** current

**Subject:** read-only physical-file and repeated-basename inventory for the NekoLib repository

**Surface:** guide

**Boundary:** global

**Authority role:** non-normative

**Mutation:** authored

**Indexing:** include

Measure the repository's physical file shape without changing it. The main use
case is counting meaningful files and repeated basenames across different
locations, including questions such as how many `README.md` files exist.

An inventory is a baseline-bound observation. It does not decide which document
is canonical, whether two same-named files duplicate meaning, or whether a file
should move or be removed.

## Capabilities

Select the smallest capability set that answers the request. Capabilities
produce one shared inventory model; `compact` and `tree` below are renderers,
not separate enumeration rules.

- `files` counts eligible physical files by category and Git state.
- `distribution <basename-or-extension>` counts a basename such as
  `README.md`, or an extension such as `.md`, and may group the result by major
  repository area.
- `topology [scope]` summarizes the physical shape of the whole repository or
  an explicit path/boundary without dumping every file.
- `git-state` distinguishes tracked, staged, unstaged, untracked, and included
  ignored context, and states what would be absent from a clean clone.
- `changes <ref>` summarizes added, moved, deleted, and modified eligible files
  between a named Git reference and the current index/working tree. Preserve
  staged and unstaged layers instead of flattening them.
- `documentation-topology [boundary|all]` inventories physical module surfaces,
  manifests, portals, audits, migrations, templates, governance, and schemas.
  Use the shared schema for expected surface names, but leave correctness and
  semantic completeness to the documentation skill and verifier.
- `skill-topology` inventories repository-owned `.agents/skills/`, registered
  `.claude/skills/`, their UI metadata, and their Git state. It must not inspect
  Claude runtime state or infer that an unregistered local path is repository
  guidance.

Capabilities may be combined. For example, repository hygiene may request
`changes HEAD` plus `distribution README.md`, while documentation
`inventory/design` may request `documentation-topology mvvm` plus
`skill-topology`.

## Output modes

Use one of two outputs over the same Git-aware enumeration.

### `compact` — default

Return category totals followed by repeated basenames. This preserves the
original inventory contract and is the right mode for questions such as
"quantos `README.md` existem?".

For non-file capabilities, return the equivalent smallest structured summary:
counts first, then only the affected areas or states needed to interpret them.

For a focused basename question, answer only what is needed:

```text
README.md: 34x
Tracked: 31 | Untracked: 3 | Included ignored context: 0
Baseline: master @ 806f0bd (dirty working tree)
```

For a general compact inventory, use this order:

```text
Code files: 124
Project/build files: 18
Documentation files: 41
Configuration files: 7
Script files: 3

README.md: 34x
AssemblyInfo.cs: 3x
TODO.md: 2x
```

Print only non-zero categories. Print only basenames occurring more than once
unless the user asks for one exact basename. Sort repeated names by count
descending, then alphabetically case-insensitively.

Do not show paths, directory names, a tree, unique filenames, or explanatory
prose in the ordinary compact result beyond the short baseline/Git-state note
needed to interpret the counts.

### `tree` — Markdown structural view

Use when the user asks for a Markdown report, tree view, structural overview,
or a human-friendly artifact for discussion. Return Markdown with:

1. baseline, scope, and tracked/untracked/included-ignored totals;
2. category totals;
3. a fenced `text` tree grouped by major repository area;
4. physical file counts and, when relevant, repeated-basename counts by area;
5. capability-specific annotations such as Git state, document surface, or
   registered adapter status;
6. a short clean-clone/local-context note.

Keep the tree compact. Show major areas and at most one useful child level; do
not dump every physical path. The module-first documentation tree may be shown
like this:

```text
docs/
|-- governance/             3 files
|-- schemas/                2 files
|-- templates/             10 files
|-- modules/
|   `-- Mvvm/              12 files | README.md: 1x
|-- audit/                 31 files
`-- history/                6 files
```

Other useful major areas include `src`, `tests`, `runtime_tests`, `eng`, agent
workflows, and root files. Aggregate sensitive or machine-local context rather
than showing its internal names.

Return the Markdown in the response. This read-only skill does not create a
`.md` file. If the user explicitly requests a saved generated report, hand the
write to `nekolib-repository-hygiene`; its default generated destination is
`artifacts/documentation/`, and the report must remain non-authoritative.

## Baseline and scope

1. Resolve the repository root with Git, even when invoked below it.
2. Record branch, full HEAD commit, requested scope, and whether the index or
   working tree is dirty.
3. Default to the entire repository. Restrict the scan only when the user names
   a path or boundary.
4. Enumerate tracked, untracked, and relevant ignored candidates separately.
   Do not rely only on `git ls-files`, and do not use an unrestricted recursive
   scan that floods the result with generated content.
5. Keep Git-state counts separate. Untracked and ignored files are not
   clean-clone evidence.
6. For `changes <ref>`, resolve the exact ref without inventing it and distinguish
   committed, staged, unstaged, and untracked observations.

## Eligible physical files

Use this case-insensitive whitelist and assign every eligible file to exactly
one category:

| Category | Extensions or exact names |
|---|---|
| Code | `.cs` |
| Project/build | `.sln`, `.slnx`, `.csproj`, `.props`, `.targets`, `global.json`, `NuGet.Config`, `Directory.Build.props`, `Directory.Build.targets`, `Directory.Packages.props` |
| Documentation | `.md`, `.txt` |
| Configuration | `.json`, `.yml`, `.yaml`, `.toml`, `.config`, `.editorconfig`, `.gitignore`, `.gitattributes`, `.gitmodules` |
| Scripts | `.ps1`, `.cmd`, `.bat`, `.sh` |

Category totals count physical files, not unique basenames. A `README.md` in
each module is therefore counted once per physical location, then aggregated as
one repeated basename group.

## Repeated-basename semantics

Group basenames case-insensitively and ignore their directories for the count.
Prefer the most common casing; on a tie, use the first stable enumeration
occurrence.

This aggregation answers distribution questions. It does not mean the files
have equal content or authority. In the module-first system, several legitimate
`README.md` roles can coexist:

- the root product overview;
- global and area indexes;
- concise module introductions under `docs/modules/<Boundary>/`;
- source-adjacent `pointer-only` portals;
- runtime-scenario or tool instructions.

Do not hash, compare, merge, or label same-named files as redundant unless the
user explicitly requests duplicate-content analysis. Route semantic ownership,
movement, or cleanup decisions to `nekolib-repository-hygiene` and use the
documentation policy and boundary manifests as authority.

## Module-first documentation awareness

For a documentation-oriented tree or interpretation, read:

- `docs/README.md` for the global documentation registry;
- `docs/repository-layout.md` for path lifecycle;
- `docs/modules/README.md` for migrated boundary status;
- `docs/schemas/documentation-schema.json` for registered documentation
  structure and vocabulary;
- `docs/schemas/agent-skill-registry.json` for repository skill identity,
  adapter paths, and parity intent.

Recognize these distinct areas without changing their meanings:

- `docs/governance/` — shared policies;
- `docs/schemas/` — deterministic structure and registered profiles;
- `docs/templates/` — shared authoring templates;
- `docs/modules/` — reviewed module-first boundaries;
- `.agents/skills/` — repository-owned Codex workflows;
- registry-listed `.claude/skills/` — repository-owned Claude adapters.

For `documentation-topology`, use `module.requiredSurfaces` and
`module.requiredDirectories` from the shared schema as structural expectations.
Report present, absent, tracked, and untracked candidates without claiming that
a present surface is populated correctly. `eng/verify-docs.ps1` remains the
contract verifier.

For `skill-topology`, use the agent skill registry for the complete
repository-owned adapter set and parity intent. Presence and Git state are
inventory facts; role semantics come from each shared contract. Do not report a
missing counterpart for a `single-profile` skill. For `contract-equivalent` or
`near-mirror`, report only structural presence and registration here; semantic
parity remains a review concern. `eng/verify-skills.ps1` owns deterministic
registry coverage, path identity, frontmatter identity, and declared
common-mode checks; inventory does not duplicate its pass/fail role.

Treat `.claude/` path by path. Registered skills are versioned candidates;
permissions, locks, worktrees, checkpoints, mailboxes, and other specifically
ignored runtime state remain local. Do not classify the entire directory as
tracked or ignored.

## Ignored, generated, and sensitive content

Exclude generated or transient content under `bin/`, `obj/`, `.vs/`,
`TestResults/`, `coverage/`, `artifacts/`, `packages/`, and
`BenchmarkDotNet.Artifacts/`, plus compiler output, generated NuGet content,
coverage, IDE state, temporary files, and caches.

Do not exclude tracked designer or `AssemblyInfo.cs` files merely because their
names often appear in generated output. Preserve them when Git and repository
evidence show they are maintained source. Apply generated-name heuristics more
aggressively to ignored and untracked candidates.

An ignored file may be included only when it passes the whitelist, is clearly
human-maintained and relevant, and is neither generated, transient,
machine-specific, nor secret-bearing. Never expose credential-, token-, key-,
cookie-, or private-user filenames. Report included ignored files as a separate
local-context count, never as shared evidence.

Do not inspect `experiments/` or secret-bearing local state unless the user
explicitly places that exact area in scope and repository safety rules allow it.

## Integration boundary

- `nekolib-repository-hygiene` may consume `files`, `distribution`, `topology`,
  `git-state`, and `changes` for discovery, then independently establish
  ownership and decide changes.
- `nekolib-documentation` may consume `documentation-topology`,
  `skill-topology`, and a tree during `inventory/design`, but presence and file
  counts do not establish semantic completeness, canonical authority, or
  migration readiness.
- `nekolib` routes measurement-only requests here and adds repository hygiene
  when the user asks for interpretation, movement, cleanup, or ownership.
- A historical inventory remains interpretable only with its commit, tree
  state, scope, filters, and ignored-context boundary.

Do not copy mutable repository counts into multiple maintained documents. A
saved inventory is generated evidence, not a live index.

## Safety and completion

This skill is strictly read-only. Do not create, edit, delete, rename, stage, or
commit files; alter ignore rules; run formatters; restore packages; build; or
execute tests.

Before responding, reconcile category totals with the eligible physical-file
total, reconcile tracked/untracked/included-ignored layers, and ensure every
displayed repeated-basename count comes from the same enumerated inventory.
