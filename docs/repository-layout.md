# Repository Operational Layout

**Document ID:** GLOBAL-REPOSITORY-LAYOUT

**Schema version:** 1

**Kind:** reference

**Lifecycle:** current

**Subject:** repository-owned documentation, tools, automation, generated artifacts, and machine-local state

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
| `docs/governance/` | Shared documentation authority, lifecycle, validation, campaign, premise, and retrieval policies | yes |
| `docs/schemas/` | Closed structural vocabulary for documentation, campaigns, and premises | yes |
| `docs/templates/` | Canonical shared document structures; examples are not repository evidence | yes |
| `docs/premises/` | Accepted scoped-premise records, contradiction history, and lifecycle; not truth or evidence authority | yes |
| `docs/proposals/` | Concise one-file unpromoted ideas; non-normative and not an exclusive promotion source | yes |
| `docs/modules/` | Reviewed module-first boundaries and their manifests, current references, registers, audits, and migrations | yes |
| `docs/development.md` | Human-readable development, compatibility, validation, review, and Git workflow | yes |
| `src/Tools/` | Source code for executables maintained by this repository | yes |
| `tools/` | Locally restored or copied executable payloads; never source authority | no |
| `eng/` | Build, validation, packaging, and repository-maintenance automation | yes |
| `.github/workflows/` | Manual remote publication transport; no automatic build or validation trigger | yes |
| `artifacts/` | Generated, disposable build/package/tool output | no |
| `.local/` | Machine-only experiments, active work-campaign manifests/state, the generated documentation search database, configuration, private prerequisites, and scratch data | no |

## Documentation infrastructure

The [documentation index](README.md) registers authored knowledge. The
[documentation policy](governance/documentation-policy.md) owns authority and
lifecycle meanings, the [schema](schemas/documentation-schema.json) owns closed
structural vocabulary, and each migrated boundary manifest owns its
module-documentation routing. The
[development guide](development.md) collects repository-wide contributor
workflow without becoming product or implementation authority. This layout
document owns only the repository path and lifecycle distinction between those
areas.

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

## Local documentation search index

The [local documentation index policy](governance/documentation-index-policy.md)
owns the retrieval boundary. Versioned maintenance lives in
[`eng/documentation_index.py`](../eng/documentation_index.py),
[`eng/refresh-documentation-index.ps1`](../eng/refresh-documentation-index.ps1),
and [`eng/search-docs.ps1`](../eng/search-docs.ps1). The generated SQLite/FTS
database remains under ignored `.local/documentation-migration/` and is never
clean-clone evidence or documentation authority.

One clean post-commit refresh indexes authored Markdown/Text plus generated XML
members for each accepted managed package/target baseline. Search is read-only,
reports commit/tree freshness and source authority, and rejects stale snapshots
unless the caller explicitly requests diagnostic stale access. This retrieval
index is distinct from the physical repository inventory below: inventory maps
files and Git state, while the local index provides persisted lexical search and
API-member lookup.

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
