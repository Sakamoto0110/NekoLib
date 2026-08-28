# NekoLib.Navigation Manifest

**Document ID:** NAV-MANIFEST

**Schema version:** 1

**Kind:** reference

**Lifecycle:** current

**Subject:** identity and documentation routing for the NekoLib.Navigation family boundary

**Surface:** manifest

**Boundary:** navigation

**Authority role:** normative

**Mutation:** authored

**Indexing:** include

**Projects:** `src/Navigation/NekoLib.Navigation/NekoLib.Navigation.csproj`, `src/Navigation/NekoLib.Navigation.WinForms/NekoLib.Navigation.WinForms.csproj`, `src/Navigation/NekoLib.Navigation.Wpf/NekoLib.Navigation.Wpf.csproj`

**Packages:** `NekoLib.Navigation`, `NekoLib.Navigation.WinForms`, `NekoLib.Navigation.Wpf`

**Targets:** `net481`, `net9.0`, `net9.0-windows`

**Project dependencies:** `NekoLib.Core`, `NekoLib.Navigation`

**Package dependencies:** `Microsoft.Bcl.AsyncInterfaces`

**Solution membership:** included

**Distribution:** shipped-library

**Stability:** stable

**Experimental APIs:** none

**API baselines:** `eng/public-api/NekoLib.Navigation/net481.approved.txt`, `eng/public-api/NekoLib.Navigation/net9.0.approved.txt`, `eng/public-api/NekoLib.Navigation.WinForms/net481.approved.txt`, `eng/public-api/NekoLib.Navigation.WinForms/net9.0-windows.approved.txt`, `eng/public-api/NekoLib.Navigation.Wpf/net481.approved.txt`, `eng/public-api/NekoLib.Navigation.Wpf/net9.0-windows.approved.txt`

**Profiles:** `standard-library`, `stateful-runtime`, `ui-runtime`, `platform-adapter`

**Technical reference:** `docs/modules/Navigation/REFERENCE.md`

**Related boundaries:** `core`, `inspection`, `logging`, `telemetry`

**Source:** `src/Navigation/NekoLib.Navigation`, `src/Navigation/NekoLib.Navigation.WinForms`, `src/Navigation/NekoLib.Navigation.Wpf`

**Tests:** `tests/NekoLib.Navigation.Tests`

**Runtime scenarios:** `runtime_tests/Navigation/LongRunningRecovery/README.md`, `runtime_tests/Navigation/WinFormsSmoke/README.md`, `runtime_tests/Navigation/WpfSmoke/README.md`

**Package evidence:** `docs/stable-release-1.0.0.md`, `docs/audit/public-api-documentation-extensibility-review-2026-08-27.md`

This manifest routes the Navigation family boundary. Project files and compiled
assemblies remain implementation and API truth; accepted manifests remain the
stable public API oracle.

## Current surfaces

| Need | Owner |
|---|---|
| Consumer introduction | [`README.md`](README.md) |
| Technical contract | [`REFERENCE.md`](REFERENCE.md) |
| Chronological module history | [`HISTORY.md`](HISTORY.md) |
| Consumer-visible module evolution | [`CHANGELOG.md`](CHANGELOG.md) |
| Confirmed issues | [`ISSUES.md`](ISSUES.md) |
| Unconfirmed findings | [`FINDINGS.md`](FINDINGS.md) |
| Unpromoted ideas | [`docs/proposals/`](../../proposals/README.md), filtered by `Boundary: navigation` |
| Evidence requirements | [`VALIDATION_REQUIREMENTS.md`](VALIDATION_REQUIREMENTS.md) |
| Executed evidence | [`VALIDATIONS.md`](VALIDATIONS.md) |
| Historical audits | [`audits/`](audits/) |
| Consumer migrations | [`migrations/`](migrations/) |
| Accepted compiled API | [`eng/public-api/NekoLib.Navigation*/`](../../../eng/public-api/) |

## Repository evidence routes

- Projects: [`src/Navigation/`](../../../src/Navigation/)
- Focused tests: [`tests/NekoLib.Navigation.Tests/`](../../../tests/NekoLib.Navigation.Tests/)
- Runtime scenarios: [`runtime_tests/Navigation/`](../../../runtime_tests/Navigation/)
- Coordinated changelog: [`CHANGELOG.md`](../../../CHANGELOG.md)
- Product direction and freezes: [`ROADMAP.md`](../../../ROADMAP.md)
- Promoted work: [`TODO.md`](../../../TODO.md)
- Stable release provenance: [`stable-release-1.0.0.md`](../../stable-release-1.0.0.md)
- Public API and release policy: [`public-api-release-policy.md`](../../public-api-release-policy.md)

The module registers reconcile current source and accepted API with preserved
audit, migration, release, test, runtime, and package evidence. Requirements
describe the qualification contract; validation records identify only evidence
that actually ran and retain their recorded gaps.
