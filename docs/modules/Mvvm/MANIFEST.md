# NekoLib.Mvvm Manifest

**Document ID:** MVVM-MANIFEST

**Schema version:** 1

**Kind:** reference

**Lifecycle:** current

**Subject:** identity and documentation routing for the NekoLib.Mvvm boundary

**Surface:** manifest

**Boundary:** mvvm

**Authority role:** normative

**Mutation:** authored

**Indexing:** include

**Projects:** `src/Mvvm/NekoLib.Mvvm/NekoLib.Mvvm.csproj`

**Packages:** `NekoLib.Mvvm`

**Targets:** `net481`, `net9.0`

**Project dependencies:** none

**Package dependencies:** none

**Solution membership:** included

**Distribution:** shipped-library

**Stability:** stable

**Experimental APIs:** none

**API baselines:** `eng/public-api/NekoLib.Mvvm/net481.approved.txt`, `eng/public-api/NekoLib.Mvvm/net9.0.approved.txt`

**Profiles:** `standard-library`

**Technical reference:** `docs/modules/Mvvm/REFERENCE.md`

**Related boundaries:** `data`

**Source:** `src/Mvvm/NekoLib.Mvvm`

**Tests:** `tests/NekoLib.Mvvm.Tests`

**Runtime scenarios:** `runtime_tests/Data/FarmDatabase/README.md`

**Package evidence:** `docs/stable-release-1.0.0.md`

This manifest routes the Mvvm bounded context. Project files and compiled
assemblies remain implementation and API truth; the accepted manifests remain
the stable public API oracle.

## Current surfaces

| Need | Owner |
|---|---|
| Consumer introduction | [`README.md`](README.md) |
| Technical contract | [`REFERENCE.md`](REFERENCE.md) |
| Chronological module history | [`HISTORY.md`](HISTORY.md) |
| Consumer-visible module evolution | [`CHANGELOG.md`](CHANGELOG.md) |
| Confirmed issues | [`ISSUES.md`](ISSUES.md) |
| Unconfirmed findings | [`FINDINGS.md`](FINDINGS.md) |
| Unpromoted ideas | [`BACKLOG.md`](BACKLOG.md) |
| Evidence requirements | [`VALIDATION_REQUIREMENTS.md`](VALIDATION_REQUIREMENTS.md) |
| Executed evidence | [`VALIDATIONS.md`](VALIDATIONS.md) |
| Historical audits | [`audits/`](audits/) |
| Consumer migrations | [`migrations/`](migrations/) |
| Accepted compiled API | [`eng/public-api/NekoLib.Mvvm/`](../../../eng/public-api/NekoLib.Mvvm/) |

## Repository evidence routes

- Project: [`NekoLib.Mvvm.csproj`](../../../src/Mvvm/NekoLib.Mvvm/NekoLib.Mvvm.csproj)
- Source: [`src/Mvvm/NekoLib.Mvvm/`](../../../src/Mvvm/NekoLib.Mvvm/)
- Focused tests: [`tests/NekoLib.Mvvm.Tests/`](../../../tests/NekoLib.Mvvm.Tests/)
- Cross-boundary consumer scenario owned by Data runtime tests:
  [`FarmDatabase`](../../../runtime_tests/Data/FarmDatabase/README.md)
- Coordinated changelog: [`CHANGELOG.md`](../../../CHANGELOG.md)
- Accepted work and freezes: [`TODO.md`](../../../TODO.md)
- Stable release provenance: [`stable-release-1.0.0.md`](../../stable-release-1.0.0.md)
- Public API and release policy: [`public-api-release-policy.md`](../../public-api-release-policy.md)

The module-first registers created by the structural pilot use explicit empty
states. They have not been semantically populated and do not erase or supersede
existing source, audit, migration, roadmap, release, test, or runtime evidence.
The `data` relationship above records ownership of consumer evidence; Mvvm still
has no project or package dependency.
