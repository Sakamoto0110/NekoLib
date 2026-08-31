# Repository Operational Layout

**Document ID:** GLOBAL-REPOSITORY-LAYOUT

**Schema version:** 1

**Kind:** reference

**Lifecycle:** current

**Subject:** repository-owned documentation infrastructure, agent adapters, tools, automation, generated artifacts, and machine-local state

**Surface:** guide

**Boundary:** global

**Authority role:** normative

**Mutation:** authored

**Indexing:** include

These directories have distinct ownership. A file's location determines
its default role, but Git state and the governing index determine whether the
current file is actually clean-clone evidence.

| Path | Owner and lifecycle | Versioned? |
|---|---|---|
| `ROADMAP.md` | Current product direction, intentions, guardrails, freezes, and planning horizons; not implementation authorization | yes |
| `TODO.md` | Formally promoted work, execution gates, ordering, and completion criteria | yes |
| `docs/governance/` | Shared documentation authority, lifecycle, validation, and cross-agent authoring policies | yes |
| `docs/schemas/` | Closed structural vocabulary plus repository skill identity, adapter topology, and parity intent | yes |
| `docs/templates/` | Canonical shared document structures; examples are not repository evidence | yes |
| `docs/premises/` | Accepted scoped-premise records, contradiction history, and lifecycle; not truth or evidence authority | yes |
| `docs/proposals/` | Concise one-file unpromoted ideas; non-normative and not an exclusive promotion source | yes |
| `docs/modules/` | Reviewed module-first boundaries and their manifests, current references, registers, audits, and migrations | yes |
| `.agents/skills/` | Repository-owned Codex workflow adapters and routing; procedural, not product authority | yes |
| `.claude/skills/` | Repository-owned Claude workflow adapters registered by the shared skill registry; procedural, not product authority | yes |
| `src/Tools/` | Source code for executables maintained by this repository | yes |
| `tools/` | Locally restored or copied executable payloads; never source authority | no |
| `eng/` | Build, validation, packaging, and repository-maintenance automation | yes |
| `.github/workflows/` | Manual remote publication transport; no automatic build or validation trigger | yes |
| `artifacts/` | Generated, disposable build/package/tool output | no |
| `.local/` | Machine-only experiments, active work-campaign manifests/state, configuration, private prerequisites, and scratch data | no |
| Selected `.claude/` runtime paths | Machine permissions, locks, routines state, worktrees, checkpoints, mailboxes, and other paths explicitly listed in `.gitignore` | no |

## Documentation and agent adapters

The [documentation index](README.md) registers authored knowledge. The
[documentation policy](governance/documentation-policy.md) owns authority and
lifecycle meanings, the [schema](schemas/documentation-schema.json) owns closed
structural vocabulary, the
[skill registry](schemas/agent-skill-registry.json) owns repository skill
identity, adapter paths, and parity intent, and each migrated boundary manifest
owns its module-documentation routing. This layout document owns only the
repository path and lifecycle distinction between those areas.

Agent skills are authoring procedures over that shared contract. They do not
become product, implementation, roadmap, or evidence authority. Codex and
Claude adapters may emphasize different aspects, but their repository outputs
remain interoperable under the
[agent documentation contract](governance/agent-documentation-contract.md).

`.claude/` is deliberately mixed ownership. Registered skills and any explicit
repository guidance are versioned candidates; machine state is ignored by
specific `.gitignore` rules. Never infer that the entire directory is versioned
or ignored. Confirm the exact path through `git ls-files`, `git status`, and
`git check-ignore`. An untracked, non-ignored adapter is part of the current
working-tree review but is not available in a clean clone until committed.

## Work campaigns

The [work campaign policy](governance/work-campaign-policy.md) owns the shared
execution-coordination contract. Its schema and example are versioned under
`docs/schemas/` and `docs/templates/`; the safe plan/execute entry point is
[`eng/invoke-work-campaign.ps1`](../eng/invoke-work-campaign.ps1). An active
`campaign.json` and mutable `state.json` live under ignored
`.local/work-campaigns/<campaign-id>/` and are machine-local coordination, not
clean-clone authority or evidence.

This repository-wide work-campaign contract is distinct from the Phase E
runtime scenario `campaign.json`, whose process orchestration remains owned by
the relevant `runtime_tests/` scenario. Durable campaign outcomes stay in their
existing source, documentation, validation, audit, history, package, or release
owners.

## Scoped premises

The [scoped premise policy](governance/premise-policy.md) owns premise
eligibility and lifecycle. Shared records live under `docs/premises/`, their
closed structure is versioned under `docs/schemas/`, and
[`eng/verify-premises.ps1`](../eng/verify-premises.ps1) derives checkout-specific
effective status. Optional evaluator caches belong under ignored
`.local/premises/` and never replace the durable record.

Premise records may name work-campaign IDs, but they remain distinct from
campaign manifests and state. Premises optimize investigation while campaigns
coordinate execution; neither becomes source, validation, task, or release
authority.

## Inventory snapshots

The repository-inventory skill provides two read-only views over one Git-aware
physical-file enumeration: a compact count grouped by category and repeated
basename, and a human-friendly Markdown tree grouped by major area. The compact
view deliberately answers distribution questions such as how many `README.md`
files exist without implying that same-named documents duplicate meaning.

Over that shared enumeration it can also report scoped topology, clean-clone
and staged/unstaged state, structural changes from an explicit Git ref,
module-documentation surface presence, and repository skill/adapter presence.
Those are inventory observations: `eng/verify-docs.ps1` validates the
documentation contract, `eng/verify-skills.ps1` validates registry coverage and
other deterministic skill-topology invariants, and repository hygiene still
owns change decisions.

The skill registry distinguishes `single-profile`, `contract-equivalent`, and
`near-mirror` skills. It never requires an undeclared one-to-one Codex/Claude
mapping. When parity is declared, reviewers compare only the commonality
required by that policy and preserve its listed agent-specific differences.
The skill verifier checks declared adapter coverage and literal common-mode
presence; it does not claim that role-specific procedures are semantically
equivalent.

Inventory output is baseline-bound discovery, not a live documentation index.
If a user explicitly requests a saved report, it belongs under
`artifacts/documentation/` as generated, non-authoritative output and must record
commit, tree state, scope, and the tracked/untracked/ignored boundary.

Tests and shared documentation must not depend on an opaque executable copied
manually into `tools/`. A repository-owned executable needs versioned source and
a reproducible build or restore. An operating-system binary is a declared
prerequisite and must be isolated behind the scenario that uses it; it is not a
vendored repository payload.

## BundlerTool

[`src/Tools/BundlerTool/`](../src/Tools/BundlerTool/) is the only source
authority for BundlerTool. It is deliberately outside `NekoLib.sln` and builds
through:

```powershell
.\eng\build-bundler.ps1
```

The script publishes the net481 executable to
`artifacts/tools/BundlerTool/Release/`, replacing only that disposable output
directory. It emits `build-manifest.json` with the assembly version, source
commit/tree state, project hash, and executable hash. An ignored
`tools/BundlerTool.exe` is at most a local cache and is never proof of the
versioned source's behavior.

The packaging workflow remains separate:
[`eng/pack-local.ps1`](../eng/pack-local.ps1) owns library and Watchdog Host
packages under `artifacts/`.

Remote NuGet.org publication is deliberately narrower than packaging.
`.github/workflows/publish-nuget.yml` runs only through an explicit manual
dispatch from `master`. It downloads the approved assets attached to the GitHub
release, verifies their recorded aggregate SHA-256, and then uses NuGet.org
trusted publishing to obtain a short-lived OIDC credential. It does not rebuild,
retest, or silently replace the canonical local package evidence.

## Public API tool

[`src/Tools/NekoLib.PublicApiTool/`](../src/Tools/NekoLib.PublicApiTool/) is the
source authority for the small dual-target assembly reflector used by F1. It is
deliberately outside `NekoLib.sln` and is built and invoked through:

```powershell
.\eng\verify-public-api.ps1
```

The script discovers the 15 packable library projects, builds the source tree,
runs the tool on each target-specific DLL, and compares the received manifests
under `artifacts/public-api/` with the versioned snapshots under
`eng/public-api/`. `-UpdateBaseline` changes those snapshots and therefore
requires an accepted API decision under the public API release policy. The
Watchdog Host deployment package is reviewed as a payload/protocol contract and
is intentionally not treated as a library assembly.

## Generated catalogs

An LLM-oriented code catalog is not part of Phase C. If separately authorized,
it must reuse or extract BundlerTool's existing Roslyn scanner, produce
deterministic output under `artifacts/`, attach source evidence, distinguish
inference from authored documentation, and never write inferred comments into
product source.
