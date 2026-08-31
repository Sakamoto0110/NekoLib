# Documentation Index and Authority

**Kind:** reference

**Lifecycle:** current

**Subject:** repository documentation governance

**Reference date:** 2026-08-26

**Reference commit:** working tree after `0fa1a321c85c541cc3e32c39e5607de881032b5a`

This index defines where each kind of repository fact is owned. It is not a
second product overview: use the linked owner when a fact needs detail or an
update.

## Classification model

Every versioned Markdown document is classified on two independent axes:

- **Kind:** `reference`, `guide`, `roadmap/status`, or `audit`.
- **Lifecycle:** `current`, `frozen`, or `historical`.

`current` means the document is maintained with the repository. `frozen` means
the context remains live but cannot be expanded until its stated unfreeze
condition is met. `historical` means the document describes a dated snapshot
and must not be read as current state.

Module-first documents add the orthogonal metadata defined by the
[documentation schema](schemas/documentation-schema.json): stable document ID,
schema version, surface, boundary, authority role, mutation policy, and indexing
policy. The original `Kind` and `Lifecycle` vocabularies remain unchanged.

Audit files use these stable metadata fields near the top of the file:

- `Kind`
- `Lifecycle`
- `Subject`
- `Reference date`
- `Reference commit`
- `Last reconciliation`
- `Current state`

A moved audit also records `Original path` so its baseline-relative provenance
and historical links remain resolvable.

Use `not recorded` when an old audit did not preserve its reference commit. Do
not infer or fabricate one from the file's first Git appearance.

## Authority by fact type

| Fact | Authoritative owner | Supporting evidence |
|---|---|---|
| Target frameworks, project references, build and package properties | The affected `*.csproj` and `Directory.Build.*` files | Restore/build/package validation |
| Solution membership | `NekoLib.sln` | `dotnet sln NekoLib.sln list` and solution build |
| Public API and runtime behavior | Current source code | Executable tests and runtime scenarios |
| Product purpose and concise module map | [`README.md`](../README.md) | Project files and source |
| Core technical contract | [`docs/modules/Core/REFERENCE.md`](modules/Core/REFERENCE.md) | Core source, focused tests, the two accepted API manifests, and concrete capability references |
| Data gateway technical contract | [`docs/modules/Data/REFERENCE.md`](modules/Data/REFERENCE.md) | Data source, tests, compiled API manifests, and provider scenarios |
| Diagnostics and Diagnostics.Windows technical contract | [`docs/modules/Diagnostics/REFERENCE.md`](modules/Diagnostics/REFERENCE.md) | Diagnostics source, four accepted API manifests, and the dual-target Diagnostics tests |
| HTTP technical contract | [`docs/modules/Http/REFERENCE.md`](modules/Http/REFERENCE.md) | HTTP source, deterministic dual-target tests, the compiled public API manifests, and the separate TheCatAPI provider scenario |
| Logging technical contract | [`docs/modules/Logging/REFERENCE.md`](modules/Logging/REFERENCE.md) | Logging source, dual-target tests, the compiled public API manifests, and the shared Observability scenario |
| Telemetry technical contract | [`docs/modules/Telemetry/REFERENCE.md`](modules/Telemetry/REFERENCE.md) | Telemetry source, dual-target tests, the compiled public API manifests, and the shared Observability scenario |
| Inspection technical contract | [`docs/modules/Inspection/REFERENCE.md`](modules/Inspection/REFERENCE.md) | Inspection source, dual-target tests, the compiled public API manifests, and the shared Observability scenario |
| Pipes technical contract | [`docs/modules/Pipes/REFERENCE.md`](modules/Pipes/REFERENCE.md) | Pipes source, dual-target tests, and the compiled public API manifests |
| Watchdog technical contract | [`docs/modules/Watchdog/REFERENCE.md`](modules/Watchdog/REFERENCE.md) | Watchdog source, dual-target tests, and the compiled public API manifests |
| Watchdog Host deployment contract | [`docs/modules/WatchdogHost/REFERENCE.md`](modules/WatchdogHost/REFERENCE.md) | Host source, dual-target tests, package target, and package-only consumers |
| Navigation technical contract | [`docs/modules/Navigation/REFERENCE.md`](modules/Navigation/REFERENCE.md) | Navigation source, adapters, tests, and compiled public API manifests |
| Mvvm technical contract | [`docs/modules/Mvvm/REFERENCE.md`](modules/Mvvm/REFERENCE.md) | Mvvm source, focused tests, and compiled public API manifests |
| Product direction, intentions, planning horizons, guardrails, and freezes | [`ROADMAP.md`](../ROADMAP.md) | Current implementation, accepted boundaries, and owner direction |
| Formally promoted work, execution gates, and completion criteria | [`TODO.md`](../TODO.md) | Accepted decisions, current implementation, and validation |
| Unpromoted proposal records | [`docs/proposals/`](proposals/README.md) | Linked investigations, findings, audits, issues, and owner decisions |
| Historical findings | The audit at its recorded date and commit | Later outcomes appear only in reconciliation sections |
| Agent documentation authoring | [`docs/governance/agent-documentation-contract.md`](governance/agent-documentation-contract.md) and registered skills | Profiles share one output contract and do not replace public or technical documentation |
| Multi-stage execution coordination | [`docs/governance/work-campaign-policy.md`](governance/work-campaign-policy.md) | Campaign manifests consume existing authority and coordinate local stages/finalizers without becoming work or evidence authority |
| Scoped reasoning premises | [`docs/governance/premise-policy.md`](governance/premise-policy.md) and [`docs/premises/`](premises/README.md) | Premises may reduce redundant investigation only while effectively active; they never override current authority or evidence |
| Repository skill identity, adapter paths, and parity intent | [`docs/schemas/agent-skill-registry.json`](schemas/agent-skill-registry.json) | Skill entrypoints and any shared contracts own procedures and interoperable semantics |
| General agent workflow | [`AGENTS.md`](../AGENTS.md) and `.agents/skills/` | These files do not replace public or technical documentation |

Each mutable fact has one owner. Other documents may summarize it and link to
the owner, but must not maintain a competing list. In particular, current test,
warning, project, or package counts do not belong in several maintained files.
A historical count is valid only when it records its date, command, and
reference commit.

## Document registry

| Document | Kind | Lifecycle | Subject / owner |
|---|---|---|---|
| [`README.md`](../README.md) | reference | current | Product overview, compatibility, module map, and package entry points |
| [`ROADMAP.md`](../ROADMAP.md) | roadmap/status | current | Product direction, intentions, planning horizons, guardrails, freezes, and completed-milestone routing |
| [`TODO.md`](../TODO.md) | roadmap/status | current | Formally promoted work, execution order, gates, and completion criteria |
| [`CHANGELOG.md`](../CHANGELOG.md) | reference | current | Consumer-visible package, public API, compatibility, and migration changes |
| [`docs/proposals/README.md`](proposals/README.md) | reference | current | One-file unpromoted proposal index and non-exclusive promotion rule |
| [`docs/modules/Core/MANIFEST.md`](modules/Core/MANIFEST.md) | reference | current | Core identity, routing, project/package topology, API oracles, experimental marker, and evidence locations |
| [`docs/modules/Core/README.md`](modules/Core/README.md) | reference | current | Concise Core consumer introduction |
| [`docs/modules/Core/REFERENCE.md`](modules/Core/REFERENCE.md) | reference | current | Shared capability contracts, ownership, completion, snapshots, null objects, extension seams, and the Inspection provider slot |
| [`docs/modules/Core/HISTORY.md`](modules/Core/HISTORY.md) | reference | current | Append-only factual Core chronology |
| [`docs/modules/Core/CHANGELOG.md`](modules/Core/CHANGELOG.md) | reference | current | Core-specific consumer-visible evolution |
| [`docs/modules/Core/ISSUES.md`](modules/Core/ISSUES.md) | reference | current | Confirmed Core defects registry |
| [`docs/modules/Core/FINDINGS.md`](modules/Core/FINDINGS.md) | reference | current | Non-normative Core findings registry |
| [`docs/modules/Core/VALIDATION_REQUIREMENTS.md`](modules/Core/VALIDATION_REQUIREMENTS.md) | reference | current | Core evidence contract derived from the standard-library profile and boundary risks |
| [`docs/modules/Core/VALIDATIONS.md`](modules/Core/VALIDATIONS.md) | reference | current | Core executed-evidence registry |
| [`src/Core/NekoLib.Core/README.md`](../src/Core/NekoLib.Core/README.md) | reference | current | Pointer-only source portal to the canonical Core technical reference |
| [`docs/modules/Data/MANIFEST.md`](modules/Data/MANIFEST.md) | reference | current | Data identity, routing, project/package topology, API oracles, validation profiles, and evidence locations |
| [`docs/modules/Data/README.md`](modules/Data/README.md) | reference | current | Concise Data consumer introduction, composition, ownership, and target boundary |
| [`docs/modules/Data/REFERENCE.md`](modules/Data/REFERENCE.md) | reference | current | Canonical Data composition, ownership, capabilities, trusted SQL, provider extension, mapping, adaptation, sessions, events, security, and target-specific streaming |
| [`docs/modules/Data/HISTORY.md`](modules/Data/HISTORY.md) | reference | current | Append-only factual Data chronology from initial audit through XML package delivery |
| [`docs/modules/Data/CHANGELOG.md`](modules/Data/CHANGELOG.md) | reference | current | Data-specific consumer-visible stable and post-1.0.0 evolution |
| [`docs/modules/Data/ISSUES.md`](modules/Data/ISSUES.md) | reference | current | Confirmed Data defect registry and separation from promoted work or provider ideas |
| [`docs/modules/Data/FINDINGS.md`](modules/Data/FINDINGS.md) | reference | current | Current Data findings disposition and historical-evidence boundary |
| [`docs/modules/Data/VALIDATION_REQUIREMENTS.md`](modules/Data/VALIDATION_REQUIREMENTS.md) | reference | current | Data build, API, regression, security, provider, package, and XML evidence contract |
| [`docs/modules/Data/VALIDATIONS.md`](modules/Data/VALIDATIONS.md) | reference | current | Curated Data build, test, provider, recovery, API, package, and XML evidence |
| [`src/Data/NekoLib.Data/README.md`](../src/Data/NekoLib.Data/README.md) | reference | current | Pointer-only source portal to the canonical Data technical reference |
| [`docs/modules/Diagnostics/MANIFEST.md`](modules/Diagnostics/MANIFEST.md) | reference | current | Diagnostics identity, routing, project/package topology, API oracles, and evidence locations |
| [`docs/modules/Diagnostics/README.md`](modules/Diagnostics/README.md) | reference | current | Concise Diagnostics-family consumer introduction |
| [`docs/modules/Diagnostics/REFERENCE.md`](modules/Diagnostics/REFERENCE.md) | reference | current | Incident collection, handler installation and disposal, evidence budgets and bounds, redaction boundary, crash-bundle layout, and the Windows crash adapter |
| [`docs/modules/Diagnostics/HISTORY.md`](modules/Diagnostics/HISTORY.md) | reference | current | Append-only factual Diagnostics chronology |
| [`docs/modules/Diagnostics/CHANGELOG.md`](modules/Diagnostics/CHANGELOG.md) | reference | current | Diagnostics-specific consumer-visible evolution |
| [`docs/modules/Diagnostics/ISSUES.md`](modules/Diagnostics/ISSUES.md) | reference | current | Confirmed Diagnostics defects registry |
| [`docs/modules/Diagnostics/FINDINGS.md`](modules/Diagnostics/FINDINGS.md) | reference | current | Non-normative Diagnostics findings and policy-horizon registry |
| [`docs/modules/Diagnostics/VALIDATION_REQUIREMENTS.md`](modules/Diagnostics/VALIDATION_REQUIREMENTS.md) | reference | current | Diagnostics evidence contract derived from the inherited validation profiles |
| [`docs/modules/Diagnostics/VALIDATIONS.md`](modules/Diagnostics/VALIDATIONS.md) | reference | current | Diagnostics executed-evidence registry |
| [`src/Diagnostics/NekoLib.Diagnostics/README.md`](../src/Diagnostics/NekoLib.Diagnostics/README.md) | reference | current | Pointer-only source portal to the canonical Diagnostics technical reference |
| [`docs/modules/Devices/MANIFEST.md`](modules/Devices/MANIFEST.md) | reference | current | Devices identity, routing, project/package topology, API oracles, and evidence locations |
| [`docs/modules/Devices/README.md`](modules/Devices/README.md) | reference | current | Concise Devices consumer introduction |
| [`docs/modules/Devices/REFERENCE.md`](modules/Devices/REFERENCE.md) | reference | current | Hardware engine orchestration, operation boundaries, configuration ownership, transport and protocol contracts, encoding, and disposal |
| [`docs/modules/Devices/HISTORY.md`](modules/Devices/HISTORY.md) | reference | current | Append-only factual Devices chronology |
| [`docs/modules/Devices/CHANGELOG.md`](modules/Devices/CHANGELOG.md) | reference | current | Devices-specific consumer-visible evolution |
| [`docs/modules/Devices/ISSUES.md`](modules/Devices/ISSUES.md) | reference | current | Confirmed Devices defects registry |
| [`docs/modules/Devices/FINDINGS.md`](modules/Devices/FINDINGS.md) | reference | current | Non-normative Devices findings registry |
| [`docs/modules/Devices/VALIDATION_REQUIREMENTS.md`](modules/Devices/VALIDATION_REQUIREMENTS.md) | reference | current | Devices evidence contract derived from the inherited validation profiles |
| [`docs/modules/Devices/VALIDATIONS.md`](modules/Devices/VALIDATIONS.md) | reference | current | Devices executed-evidence registry |
| [`src/Devices/NekoLib.Devices/README.md`](../src/Devices/NekoLib.Devices/README.md) | reference | current | Pointer-only source portal to the canonical Devices technical reference |
| [`docs/modules/Mvvm/MANIFEST.md`](modules/Mvvm/MANIFEST.md) | reference | current | Mvvm identity, routing, project/package topology, API oracles, and evidence locations |
| [`docs/modules/Mvvm/README.md`](modules/Mvvm/README.md) | reference | current | Concise Mvvm consumer introduction |
| [`docs/modules/Mvvm/REFERENCE.md`](modules/Mvvm/REFERENCE.md) | reference | current | Binding helper and command contracts: parameter coercion, notification semantics, threading, exception behavior, and nullability |
| [`docs/modules/Mvvm/HISTORY.md`](modules/Mvvm/HISTORY.md) | reference | current | Append-only factual Mvvm chronology |
| [`docs/modules/Mvvm/CHANGELOG.md`](modules/Mvvm/CHANGELOG.md) | reference | current | Mvvm-specific consumer-visible evolution |
| [`docs/modules/Mvvm/ISSUES.md`](modules/Mvvm/ISSUES.md) | reference | current | Confirmed Mvvm defects registry |
| [`docs/modules/Mvvm/FINDINGS.md`](modules/Mvvm/FINDINGS.md) | reference | current | Non-normative Mvvm findings registry |
| [`docs/modules/Mvvm/VALIDATION_REQUIREMENTS.md`](modules/Mvvm/VALIDATION_REQUIREMENTS.md) | reference | current | Mvvm evidence contract derived from the inherited validation profile |
| [`docs/modules/Mvvm/VALIDATIONS.md`](modules/Mvvm/VALIDATIONS.md) | reference | current | Mvvm executed-evidence registry |
| [`src/Mvvm/NekoLib.Mvvm/README.md`](../src/Mvvm/NekoLib.Mvvm/README.md) | reference | current | Pointer-only source portal to the canonical Mvvm technical reference |
| [`docs/modules/Pipes/MANIFEST.md`](modules/Pipes/MANIFEST.md) | reference | current | Pipes identity, routing, project/package topology, API oracles, and evidence locations |
| [`docs/modules/Pipes/README.md`](modules/Pipes/README.md) | reference | current | Concise Pipes consumer introduction |
| [`docs/modules/Pipes/REFERENCE.md`](modules/Pipes/REFERENCE.md) | reference | current | Named-pipe RPC/events, configuration ownership, lifecycle, framing, metrics, errors, access policy, and target-specific payload contracts |
| [`docs/modules/Pipes/HISTORY.md`](modules/Pipes/HISTORY.md) | reference | current | Append-only factual Pipes chronology |
| [`docs/modules/Pipes/CHANGELOG.md`](modules/Pipes/CHANGELOG.md) | reference | current | Pipes-specific consumer-visible evolution |
| [`docs/modules/Pipes/ISSUES.md`](modules/Pipes/ISSUES.md) | reference | current | Confirmed Pipes defects registry |
| [`docs/modules/Pipes/FINDINGS.md`](modules/Pipes/FINDINGS.md) | reference | current | Non-normative Pipes findings registry |
| [`docs/modules/Pipes/VALIDATION_REQUIREMENTS.md`](modules/Pipes/VALIDATION_REQUIREMENTS.md) | reference | current | Pipes evidence contract derived from the inherited validation profiles |
| [`docs/modules/Pipes/VALIDATIONS.md`](modules/Pipes/VALIDATIONS.md) | reference | current | Pipes executed-evidence registry |
| [`src/Pipes/NekoLib.Pipes/README.md`](../src/Pipes/NekoLib.Pipes/README.md) | reference | current | Pointer-only source portal to the canonical Pipes technical reference |
| [`docs/modules/Watchdog/MANIFEST.md`](modules/Watchdog/MANIFEST.md) | reference | current | Watchdog identity, routing, project/package topology, API oracles, and evidence locations |
| [`docs/modules/Watchdog/README.md`](modules/Watchdog/README.md) | reference | current | Concise Watchdog consumer introduction |
| [`docs/modules/Watchdog/REFERENCE.md`](modules/Watchdog/REFERENCE.md) | reference | current | Watchdog application bootstrap, advanced runtime, configuration, lifecycle, process ownership, control, evidence, security, and package boundary |
| [`docs/modules/Watchdog/HISTORY.md`](modules/Watchdog/HISTORY.md) | reference | current | Append-only factual Watchdog chronology |
| [`docs/modules/Watchdog/CHANGELOG.md`](modules/Watchdog/CHANGELOG.md) | reference | current | Watchdog-specific consumer-visible evolution |
| [`docs/modules/Watchdog/ISSUES.md`](modules/Watchdog/ISSUES.md) | reference | current | Confirmed Watchdog defects registry |
| [`docs/modules/Watchdog/FINDINGS.md`](modules/Watchdog/FINDINGS.md) | reference | current | Non-normative Watchdog findings registry |
| [`docs/modules/Watchdog/VALIDATION_REQUIREMENTS.md`](modules/Watchdog/VALIDATION_REQUIREMENTS.md) | reference | current | Watchdog evidence contract derived from the inherited validation profiles |
| [`docs/modules/Watchdog/VALIDATIONS.md`](modules/Watchdog/VALIDATIONS.md) | reference | current | Watchdog executed-evidence registry |
| [`src/Watchdog/NekoLib.Watchdog/README.md`](../src/Watchdog/NekoLib.Watchdog/README.md) | reference | current | Pointer-only source portal to the canonical Watchdog technical reference |
| [`docs/modules/WatchdogHost/MANIFEST.md`](modules/WatchdogHost/MANIFEST.md) | reference | current | Watchdog Host identity, routing, project/package topology, deployment evidence locations, and the deliberate absence of an API baseline |
| [`docs/modules/WatchdogHost/README.md`](modules/WatchdogHost/README.md) | reference | current | Concise Watchdog Host consumer introduction |
| [`docs/modules/WatchdogHost/REFERENCE.md`](modules/WatchdogHost/REFERENCE.md) | reference | current | Watchdog Host deployment package, payload selection, build/publish behavior, protocol v1, fatal evidence, security, and release validation |
| [`docs/modules/WatchdogHost/HISTORY.md`](modules/WatchdogHost/HISTORY.md) | reference | current | Append-only factual Watchdog Host chronology |
| [`docs/modules/WatchdogHost/CHANGELOG.md`](modules/WatchdogHost/CHANGELOG.md) | reference | current | Watchdog Host-specific consumer-visible evolution |
| [`docs/modules/WatchdogHost/ISSUES.md`](modules/WatchdogHost/ISSUES.md) | reference | current | Confirmed Watchdog Host defects registry |
| [`docs/modules/WatchdogHost/FINDINGS.md`](modules/WatchdogHost/FINDINGS.md) | reference | current | Non-normative Watchdog Host findings registry |
| [`docs/modules/WatchdogHost/VALIDATION_REQUIREMENTS.md`](modules/WatchdogHost/VALIDATION_REQUIREMENTS.md) | reference | current | Watchdog Host evidence contract derived from the inherited deployment-package profile |
| [`docs/modules/WatchdogHost/VALIDATIONS.md`](modules/WatchdogHost/VALIDATIONS.md) | reference | current | Watchdog Host executed-evidence registry |
| [`src/Watchdog/NekoLib.Watchdog.Host/README.md`](../src/Watchdog/NekoLib.Watchdog.Host/README.md) | reference | current | Pointer-only source portal to the canonical Watchdog Host deployment reference |
| [`docs/modules/Logging/MANIFEST.md`](modules/Logging/MANIFEST.md) | reference | current | Logging identity, routing, project/package topology, API oracles, and evidence locations |
| [`docs/modules/Logging/README.md`](modules/Logging/README.md) | reference | current | Concise Logging consumer introduction |
| [`docs/modules/Logging/REFERENCE.md`](modules/Logging/REFERENCE.md) | reference | current | Logging pipeline composition, ownership, delivery ordering, snapshots, flush and disposal contracts, and the shipped sinks |
| [`docs/modules/Logging/HISTORY.md`](modules/Logging/HISTORY.md) | reference | current | Append-only factual Logging chronology |
| [`docs/modules/Logging/CHANGELOG.md`](modules/Logging/CHANGELOG.md) | reference | current | Logging-specific consumer-visible evolution |
| [`docs/modules/Logging/ISSUES.md`](modules/Logging/ISSUES.md) | reference | current | Confirmed Logging defects registry |
| [`docs/modules/Logging/FINDINGS.md`](modules/Logging/FINDINGS.md) | reference | current | Non-normative Logging findings registry |
| [`docs/modules/Logging/VALIDATION_REQUIREMENTS.md`](modules/Logging/VALIDATION_REQUIREMENTS.md) | reference | current | Logging evidence contract derived from the inherited validation profiles |
| [`docs/modules/Logging/VALIDATIONS.md`](modules/Logging/VALIDATIONS.md) | reference | current | Logging executed-evidence registry |
| [`src/Logging/NekoLib.Logging/README.md`](../src/Logging/NekoLib.Logging/README.md) | reference | current | Pointer-only source portal to the canonical Logging technical reference |
| [`docs/modules/Telemetry/MANIFEST.md`](modules/Telemetry/MANIFEST.md) | reference | current | Telemetry identity, routing, project/package topology, API oracles, and evidence locations |
| [`docs/modules/Telemetry/README.md`](modules/Telemetry/README.md) | reference | current | Concise Telemetry consumer introduction |
| [`docs/modules/Telemetry/REFERENCE.md`](modules/Telemetry/REFERENCE.md) | reference | current | Telemetry pipeline composition, ownership, operation lifecycle, time semantics, dimensions, bounded retention, snapshots, and sink dispatch |
| [`docs/modules/Telemetry/HISTORY.md`](modules/Telemetry/HISTORY.md) | reference | current | Append-only factual Telemetry chronology |
| [`docs/modules/Telemetry/CHANGELOG.md`](modules/Telemetry/CHANGELOG.md) | reference | current | Telemetry-specific consumer-visible evolution |
| [`docs/modules/Telemetry/ISSUES.md`](modules/Telemetry/ISSUES.md) | reference | current | Confirmed Telemetry defects registry |
| [`docs/modules/Telemetry/FINDINGS.md`](modules/Telemetry/FINDINGS.md) | reference | current | Non-normative Telemetry findings registry |
| [`docs/modules/Telemetry/VALIDATION_REQUIREMENTS.md`](modules/Telemetry/VALIDATION_REQUIREMENTS.md) | reference | current | Telemetry evidence contract derived from the inherited validation profiles |
| [`docs/modules/Telemetry/VALIDATIONS.md`](modules/Telemetry/VALIDATIONS.md) | reference | current | Telemetry executed-evidence registry |
| [`src/Telemetry/NekoLib.Telemetry/README.md`](../src/Telemetry/NekoLib.Telemetry/README.md) | reference | current | Pointer-only source portal to the canonical Telemetry technical reference |
| [`docs/modules/Inspection/MANIFEST.md`](modules/Inspection/MANIFEST.md) | reference | current | Inspection identity, routing, project/package topology, API oracles, experimental marker, and evidence locations |
| [`docs/modules/Inspection/README.md`](modules/Inspection/README.md) | reference | current | Concise Inspection consumer introduction |
| [`docs/modules/Inspection/REFERENCE.md`](modules/Inspection/REFERENCE.md) | reference | current | Passive Inspection composition, recording, provider identity and ordering, snapshot budgets, owner diagnostics, lifecycle, and experimental actions |
| [`docs/modules/Inspection/HISTORY.md`](modules/Inspection/HISTORY.md) | reference | current | Append-only factual Inspection chronology |
| [`docs/modules/Inspection/CHANGELOG.md`](modules/Inspection/CHANGELOG.md) | reference | current | Inspection-specific consumer-visible evolution |
| [`docs/modules/Inspection/ISSUES.md`](modules/Inspection/ISSUES.md) | reference | current | Confirmed Inspection defects registry |
| [`docs/modules/Inspection/FINDINGS.md`](modules/Inspection/FINDINGS.md) | reference | current | Non-normative Inspection findings registry |
| [`docs/modules/Inspection/VALIDATION_REQUIREMENTS.md`](modules/Inspection/VALIDATION_REQUIREMENTS.md) | reference | current | Inspection evidence contract derived from the inherited validation profiles |
| [`docs/modules/Inspection/VALIDATIONS.md`](modules/Inspection/VALIDATIONS.md) | reference | current | Inspection executed-evidence registry |
| [`src/Inspection/NekoLib.Inspection/README.md`](../src/Inspection/NekoLib.Inspection/README.md) | reference | current | Pointer-only source portal to the canonical Inspection technical reference |
| [`docs/modules/Navigation/MANIFEST.md`](modules/Navigation/MANIFEST.md) | reference | current | Navigation family identity, routing, project/package topology, API oracles, and evidence locations |
| [`docs/modules/Navigation/README.md`](modules/Navigation/README.md) | reference | current | Concise consumer introduction, package route, bootstrap entry point, and boundary non-goals |
| [`docs/modules/Navigation/REFERENCE.md`](modules/Navigation/REFERENCE.md) | reference | current | Navigation family package, lifecycle, ownership, concurrency, cancellation, platform, extension, security, diagnostics, and validation contract |
| [`docs/modules/Navigation/HISTORY.md`](modules/Navigation/HISTORY.md) | reference | current | Append-only chronology from the initial lifecycle audit through stable API and XML-package qualification |
| [`docs/modules/Navigation/CHANGELOG.md`](modules/Navigation/CHANGELOG.md) | reference | current | Navigation-specific 1.0.0 migration impact and unreleased XML-documentation delivery |
| [`docs/modules/Navigation/ISSUES.md`](modules/Navigation/ISSUES.md) | reference | current | Confirmed Navigation defects, evidence, workarounds, and promotion state |
| [`docs/modules/Navigation/FINDINGS.md`](modules/Navigation/FINDINGS.md) | reference | current | Non-normative guard ergonomics, resource-trend, and design-time observations with dispositions |
| [`docs/modules/Navigation/VALIDATION_REQUIREMENTS.md`](modules/Navigation/VALIDATION_REQUIREMENTS.md) | reference | current | Risk-derived build, API, regression, native UI, recovery, designer, package, observation, and XML-documentation qualification requirements |
| [`docs/modules/Navigation/VALIDATIONS.md`](modules/Navigation/VALIDATIONS.md) | reference | current | Curated historical interactive, designer, recovery, stable-release, API/XML, and package evidence with explicit gaps |
| [`src/Navigation/NekoLib.Navigation/README.md`](../src/Navigation/NekoLib.Navigation/README.md) | reference | current | Pointer-only source portal to the canonical Navigation technical reference |
| [`docs/modules/Http/MANIFEST.md`](modules/Http/MANIFEST.md) | reference | current | HTTP identity, routing, project/package topology, API oracles, and evidence locations |
| [`docs/modules/Http/README.md`](modules/Http/README.md) | reference | current | Concise HTTP consumer introduction |
| [`docs/modules/Http/REFERENCE.md`](modules/Http/REFERENCE.md) | reference | current | Typed HTTP endpoint catalogs, consumer ownership, relative URI construction, bounded response evidence, serialization contracts, and target differences |
| [`docs/modules/Http/HISTORY.md`](modules/Http/HISTORY.md) | reference | current | Append-only factual HTTP chronology |
| [`docs/modules/Http/CHANGELOG.md`](modules/Http/CHANGELOG.md) | reference | current | HTTP-specific consumer-visible evolution |
| [`docs/modules/Http/ISSUES.md`](modules/Http/ISSUES.md) | reference | current | Confirmed HTTP defects registry |
| [`docs/modules/Http/FINDINGS.md`](modules/Http/FINDINGS.md) | reference | current | Non-normative HTTP findings registry |
| [`docs/modules/Http/VALIDATION_REQUIREMENTS.md`](modules/Http/VALIDATION_REQUIREMENTS.md) | reference | current | HTTP evidence contract derived from the inherited validation profiles |
| [`docs/modules/Http/VALIDATIONS.md`](modules/Http/VALIDATIONS.md) | reference | current | HTTP executed-evidence registry |
| [`src/Http/NekoLib.Http/README.md`](../src/Http/NekoLib.Http/README.md) | reference | current | Pointer-only source portal to the canonical HTTP technical reference |
| [`docs/README.md`](README.md) | reference | current | Documentation governance and index |
| [`docs/governance/documentation-policy.md`](governance/documentation-policy.md) | reference | current | Module-first authority, lifecycle, conflict, migration, and indexing policy |
| [`docs/governance/agent-documentation-contract.md`](governance/agent-documentation-contract.md) | reference | current | Interoperable output contract and permitted role variation for documentation skills |
| [`docs/governance/work-campaign-policy.md`](governance/work-campaign-policy.md) | reference | current | Bounded multi-stage execution, local resumable state, and deduplicated campaign finalizers |
| [`docs/governance/premise-policy.md`](governance/premise-policy.md) | reference | current | Scoped confidence premises, effective status, contradictions, freshness, and automatic suspension |
| [`docs/governance/validation-policy.md`](governance/validation-policy.md) | reference | current | Validation taxonomy, profiles, requirement/evidence separation, and soak records |
| [`docs/schemas/README.md`](schemas/README.md) | reference | current | Deterministic documentation, agent-skill, campaign, and premise schema index |
| [`docs/premises/README.md`](premises/README.md) | reference | current | One-record-per-file registry for accepted scoped premises and their preserved lifecycle |
| [`docs/modules/README.md`](modules/README.md) | reference | current | Module-first boundary index and migration state |
| [`docs/public-api-release-policy.md`](public-api-release-policy.md) | reference | current | F1 public API classification, SemVer, compatibility, deprecation, baseline, and release rules |
| [`docs/stable-release-1.0.0.md`](stable-release-1.0.0.md) | reference | current | First stable family baseline, qualifying package provenance, hashes, validation, and distribution boundaries |
| [`docs/modules/Core/migrations/f1.md`](modules/Core/migrations/f1.md) | guide | current | Migration from the initial Core candidate surface to defensive outer snapshots and experimental action registration |
| [`docs/modules/Data/migrations/f1.md`](modules/Data/migrations/f1.md) | guide | current | Migration from the initial Data candidate surface to the accepted F1-DATA gateway contract |
| [`docs/modules/Data/migrations/querybuilder-structured-api.md`](modules/Data/migrations/querybuilder-structured-api.md) | guide | current | Migration to the canonical structured QueryBuilder API and its warning-only compatibility window |
| [`docs/modules/Data/migrations/data-type-adaptation.md`](modules/Data/migrations/data-type-adaptation.md) | guide | current | Adoption of explicit input promotion, provider decay/loss policies, schema discovery, and sanitized adaptation hooks |
| [`docs/modules/Diagnostics/migrations/f1.md`](modules/Diagnostics/migrations/f1.md) | guide | current | Migration from the initial Diagnostics candidate surface to the accepted F1-DIAG incident-collection, bundle, lifecycle, and ownership contracts |
| [`docs/modules/Http/migrations/f1.md`](modules/Http/migrations/f1.md) | guide | current | Migration from the initial HTTP candidate surface to the accepted F1-HTTP charset, response-evidence, identity, and validation contracts |
| [`docs/modules/Devices/migrations/f1.md`](modules/Devices/migrations/f1.md) | guide | current | Migration from the initial Devices candidate surface to the accepted F1-DEV operation-boundary, configuration-ownership, failure-evidence, and nullability contracts |
| [`docs/modules/Mvvm/migrations/f1.md`](modules/Mvvm/migrations/f1.md) | guide | current | Migration from the initial Mvvm candidate surface to the accepted F1-MVVM nullability contract and virtual notification funnel |
| [`docs/modules/Pipes/migrations/f1.md`](modules/Pipes/migrations/f1.md) | guide | current | Migration from the initial Pipes candidate surface to the accepted F1-PIPE ownership, lifecycle, event, metrics, error, and target contracts |
| [`docs/modules/Watchdog/migrations/f1.md`](modules/Watchdog/migrations/f1.md) | guide | current | Migration from the initial Watchdog candidate surface to the accepted F1-WDOG application, advanced-runtime, lifecycle, control, evidence, and ownership contracts |
| [`docs/modules/WatchdogHost/migrations/f1.md`](modules/WatchdogHost/migrations/f1.md) | guide | current | Migration from the initial Watchdog Host candidate package to direct-only deployment, protocol v1, fail-fast workdir validation, and bounded fatal evidence |
| [`docs/modules/Logging/migrations/f1.md`](modules/Logging/migrations/f1.md) | guide | current | Migration from the initial Logging candidate surface to the accepted F1-LOG pipeline, flush, disposal, and sink behavior |
| [`docs/modules/Inspection/migrations/f1.md`](modules/Inspection/migrations/f1.md) | guide | current | Migration from the initial Inspection candidate surface to the accepted passive runtime and experimental action boundary |
| [`docs/modules/Telemetry/migrations/f1.md`](modules/Telemetry/migrations/f1.md) | guide | current | Migration from the initial Telemetry candidate surface to the accepted F1-TEL completion, correlation, and sink-capture behavior |
| [`docs/modules/Navigation/migrations/f1.md`](modules/Navigation/migrations/f1.md) | guide | current | Migration from the initial Navigation candidate family to the accepted core facade, adapter compatibility, ownership, disposal, and nullability contracts |
| [`docs/audit/README.md`](audit/README.md) | reference | current | Audit registry and snapshot rules |
| [`docs/modules/Core/audits/public-api-review-2026-08-17.md`](modules/Core/audits/public-api-review-2026-08-17.md) | audit | historical | F1-CORE compiled-surface review, accepted decisions, and implementation reconciliation |
| [`docs/modules/Data/audits/initial-audit.md`](modules/Data/audits/initial-audit.md) | audit | historical | Data first-pass source review and later reconciliation |
| [`docs/modules/Data/audits/stabilization-review-2026-08-01.md`](modules/Data/audits/stabilization-review-2026-08-01.md) | audit | historical | Data deep stabilization review, accepted directions, and implementation reconciliation |
| [`docs/modules/Data/audits/public-api-review-2026-08-17.md`](modules/Data/audits/public-api-review-2026-08-17.md) | audit | historical | F1-DATA compiled-surface review, accepted decisions, and implementation reconciliation |
| [`docs/modules/Data/audits/type-adaptation-querybuilder-api-review-2026-08-26.md`](modules/Data/audits/type-adaptation-querybuilder-api-review-2026-08-26.md) | audit | historical | Accepted and completed Data type-adaptation policy and QueryBuilder API normalization, including DTO temporal materialization |
| [`docs/modules/Devices/audits/public-api-review-2026-08-17.md`](modules/Devices/audits/public-api-review-2026-08-17.md) | audit | historical | F1-DEV compiled-surface review, accepted implementation reconciliation, and immutable package evidence |
| [`docs/modules/Diagnostics/audits/public-api-review-2026-08-17.md`](modules/Diagnostics/audits/public-api-review-2026-08-17.md) | audit | historical | F1-DIAG compiled-surface review, accepted implementation reconciliation, final lifecycle correction, and immutable package evidence |
| [`docs/modules/Diagnostics/audits/windows-public-api-review-2026-08-17.md`](modules/Diagnostics/audits/windows-public-api-review-2026-08-17.md) | audit | historical | F1-WIN compiled-surface review, accepted implementation reconciliation, and immutable package evidence |
| [`docs/modules/Logging/audits/public-api-review-2026-08-17.md`](modules/Logging/audits/public-api-review-2026-08-17.md) | audit | historical | F1-LOG compiled-surface review, accepted dispositions, implementation reconciliation, and immutable package evidence |
| [`docs/modules/Inspection/audits/public-api-review-2026-08-17.md`](modules/Inspection/audits/public-api-review-2026-08-17.md) | audit | historical | F1-INSP compiled-surface review, accepted implementation reconciliation, and immutable package evidence |
| [`docs/modules/Http/audits/public-api-review-2026-08-17.md`](modules/Http/audits/public-api-review-2026-08-17.md) | audit | historical | F1-HTTP compiled-surface review, accepted implementation reconciliation, and immutable package evidence |
| [`docs/audit/public-api-documentation-extensibility-review-2026-08-27.md`](audit/public-api-documentation-extensibility-review-2026-08-27.md) | audit | historical | Completed managed public API documentation, extension-contract, and immutable package-delivery review |
| [`docs/modules/Mvvm/audits/public-api-review-2026-08-17.md`](modules/Mvvm/audits/public-api-review-2026-08-17.md) | audit | historical | F1-MVVM compiled-surface review, accepted implementation reconciliation, and immutable package evidence |
| [`docs/modules/Navigation/audits/public-api-review-2026-08-20.md`](modules/Navigation/audits/public-api-review-2026-08-20.md) | audit | historical | F1-NAV code-first core public API review, accepted implementation reconciliation, and residual package/runtime gaps |
| [`docs/modules/Navigation/audits/winforms-public-api-review-2026-08-21.md`](modules/Navigation/audits/winforms-public-api-review-2026-08-21.md) | audit | historical | F1-NAV-WF code-first adapter review, accepted implementation reconciliation, exact manifest changes, and residual package/runtime gaps |
| [`docs/modules/Navigation/audits/wpf-public-api-review-2026-08-21.md`](modules/Navigation/audits/wpf-public-api-review-2026-08-21.md) | audit | historical | F1-NAV-WPF code-first adapter review, accepted implementation reconciliation, exact manifest changes, and residual package/runtime gaps |
| [`docs/audit/nekomarketplace-external-consumer-evidence-intake-2026-08-26.md`](audit/nekomarketplace-external-consumer-evidence-intake-2026-08-26.md) | audit | historical | NekoMarketplace external-consumer evidence intake; F-009/F-026 are promoted, F-022 is an active roadmap decision, and remaining records stay historical |
| [`docs/audit/payments-pix-design-review-2026-08-16.md`](audit/payments-pix-design-review-2026-08-16.md) | audit | historical | Phase G2 code-first design review preserved as dated input to the unpromoted Payments/Pix proposal |
| [`docs/modules/Pipes/audits/public-api-review-2026-08-18.md`](modules/Pipes/audits/public-api-review-2026-08-18.md) | audit | historical | F1-PIPE compiled-surface review, accepted decisions, implementation reconciliation, and residual release-evidence gaps |
| [`docs/modules/Telemetry/audits/public-api-review-2026-08-17.md`](modules/Telemetry/audits/public-api-review-2026-08-17.md) | audit | historical | F1-TEL compiled-surface review, accepted dispositions, implementation reconciliation, and immutable package evidence |
| [`docs/modules/Watchdog/audits/initial-audit.md`](modules/Watchdog/audits/initial-audit.md) | audit | historical | Watchdog first-pass review preserved at its recorded baseline |
| [`docs/modules/Watchdog/audits/public-api-review-2026-08-18.md`](modules/Watchdog/audits/public-api-review-2026-08-18.md) | audit | historical | F1-WDOG compiled public surface, accepted decisions, implementation reconciliation, and residual Host/release-evidence gaps |
| [`docs/modules/WatchdogHost/audits/contract-review-2026-08-20.md`](modules/WatchdogHost/audits/contract-review-2026-08-20.md) | audit | historical | F1-WDOG-HOST deployment package and protocol review, accepted implementation reconciliation, and immutable package evidence |
| [`docs/history/README.md`](history/README.md) | reference | current | Completed roadmap and implementation-history index |
| [`docs/history/architecture-roadmap-through-phase-d-2026-08-01.md`](history/architecture-roadmap-through-phase-d-2026-08-01.md) | roadmap/status | historical | Completed Phases A, B, and D plus the Phase C handoff snapshot |
| [`docs/history/phase-c-repository-hygiene-2026-08-01.md`](history/phase-c-repository-hygiene-2026-08-01.md) | roadmap/status | historical | Phase C completion, commit-bound validation, and residual gaps |
| [`docs/history/phase-e-confidence-stabilization-2026-08-12.md`](history/phase-e-confidence-stabilization-2026-08-12.md) | roadmap/status | historical | Complete Phase E work log, outcome-first evidence boundaries, residual confidence, and final commit-bound validation |
| [`docs/history/phase-g1-http-integration-2026-08-16.md`](history/phase-g1-http-integration-2026-08-16.md) | roadmap/status | historical | Phase G1 typed HTTP catalog completion, deterministic and package evidence, and the optional provider-evidence boundary |
| [`docs/history/phase-f1-public-api-release-stability-2026-08-21.md`](history/phase-f1-public-api-release-stability-2026-08-21.md) | roadmap/status | historical | Phase F1 public API finalization and first stable coordinated package-family completion |
| [`tests/README.md`](../tests/README.md) | reference | current | Automated verification taxonomy and canonical entry points |
| [`runtime_tests/README.md`](../runtime_tests/README.md) | guide | current | Shared manual runtime-scenario contract and inventory |
| [`runtime_tests/SCENARIO_TEMPLATE.md`](../runtime_tests/SCENARIO_TEMPLATE.md) | guide | current | Required metadata and procedure template for new scenarios |
| [`runtime_tests/PHASE_E_SCENARIO_SUITE.md`](../runtime_tests/PHASE_E_SCENARIO_SUITE.md) | guide | current | Build specification and traceability for the completed Phase E long-running, recovery, and SQL Server runtime scenarios |
| [`runtime_tests/Data/FarmDatabase/README.md`](../runtime_tests/Data/FarmDatabase/README.md) | guide | current | Data SQLite/Access dual-provider and long-running simulation scenario, also covering Mvvm binding, attribute-only Navigation registration, and application-owned Logging/Telemetry measurements |
| [`runtime_tests/Http/TheCatApi/README.md`](../runtime_tests/Http/TheCatApi/README.md) | guide | current | Optional unofficial TheCatAPI provider probe for typed GET/POST/DELETE and run-owned cleanup |
| [`runtime_tests/Shared/NekoLib.RuntimeTests.Harness/README.md`](../runtime_tests/Shared/NekoLib.RuntimeTests.Harness/README.md) | guide | current | Shared runtime-scenario plumbing — exit codes, check runner, artifact layout, environment record and deterministic scheduling — and the two rules that keep it from becoming a test framework |
| [`runtime_tests/Data/SqlServer/README.md`](../runtime_tests/Data/SqlServer/README.md) | guide | current | Data against a real SQL Server engine in an adopted local container: pooling and ownership, mid-flight cancellation, transport loss and recovery, and dynamic-result schema-cap lifetime |
| [`runtime_tests/Pipes/LongRunningRecovery/README.md`](../runtime_tests/Pipes/LongRunningRecovery/README.md) | guide | current | Pipes across real processes over real named pipes: payload sizes, correlation, error contracts, frame limits, malformed peers, endpoint release and rebinding |
| [`runtime_tests/Observability/LongRunningRecovery/README.md`](../runtime_tests/Observability/LongRunningRecovery/README.md) | guide | current | Logging, Telemetry and passive Inspection under sustained load, deterministic failure and recovery: ordering, bounded snapshots, sink-failure isolation, rotation and retention, provider budgets, and process-wide Inspection lifecycle |
| [`runtime_tests/Confidence/LongRunning/README.md`](../runtime_tests/Confidence/LongRunning/README.md) | guide | current | Deterministic Phase E campaign orchestration, process ownership, aggregate exit codes, and opt-in native Navigation workers |
| [`runtime_tests/Devices/Com0Com/README.md`](../runtime_tests/Devices/Com0Com/README.md) | guide | current | Devices virtual-COM parity and protocol-readiness scenario |
| [`runtime_tests/Watchdog/CrashRecovery/README.md`](../runtime_tests/Watchdog/CrashRecovery/README.md) | guide | current | Deployed Watchdog Host crash/recovery scenario with deterministic child terminals, exact process ownership, forwarding and bundle validation |
| [`runtime_tests/Navigation/LongRunningRecovery/README.md`](../runtime_tests/Navigation/LongRunningRecovery/README.md) | guide | current | Navigation unattended long-running and recovery scenario across WinForms `net481`, WinForms `net9.0-windows`, and WPF `net9.0-windows`, with qualifying standalone smoke evidence and disabled per-combination campaign entries |
| [`runtime_tests/Navigation/WinFormsSmoke/README.md`](../runtime_tests/Navigation/WinFormsSmoke/README.md) | guide | current | Navigation WinForms interactive smoke scenario |
| [`runtime_tests/Navigation/WpfSmoke/README.md`](../runtime_tests/Navigation/WpfSmoke/README.md) | guide | current | Navigation WPF interactive smoke scenario |
| [`runtime_tests/Watchdog/Supervisor481/README.md`](../runtime_tests/Watchdog/Supervisor481/README.md) | guide | current | Watchdog/Pipes interactive supervisor scenario |
| [`docs/repository-layout.md`](repository-layout.md) | reference | current | Documentation infrastructure, agent adapters, tool source, automation, generated-artifact, and machine-local ownership |
| [`AGENTS.md`](../AGENTS.md) | guide | current | Versioned agent workflow; not public product authority |
| [`.agents/skills/nekolib/SKILL.md`](../.agents/skills/nekolib/SKILL.md) | guide | current | Repository-wide NekoLib routing |
| [`.agents/skills/nekolib-documentation/SKILL.md`](../.agents/skills/nekolib-documentation/SKILL.md) | guide | current | Codex documentation adapter with source, architecture, public API, and implementation emphasis |
| [`.agents/skills/nekolib-data/SKILL.md`](../.agents/skills/nekolib-data/SKILL.md) | guide | current | Data workflow |
| [`.agents/skills/nekolib-devices/SKILL.md`](../.agents/skills/nekolib-devices/SKILL.md) | guide | current | Devices workflow |
| [`.agents/skills/nekolib-navigation/SKILL.md`](../.agents/skills/nekolib-navigation/SKILL.md) | guide | current | Navigation workflow |
| [`.agents/skills/nekolib-repository-hygiene/SKILL.md`](../.agents/skills/nekolib-repository-hygiene/SKILL.md) | guide | current | Repository hygiene workflow |
| [`.agents/skills/nekolib-repository-inventory/SKILL.md`](../.agents/skills/nekolib-repository-inventory/SKILL.md) | guide | current | Read-only file distribution, Git state/change sets, documentation and skill topology, and compact Markdown tree views |
| [`.claude/skills/nekolib-documentation/SKILL.md`](../.claude/skills/nekolib-documentation/SKILL.md) | guide | current | Claude documentation adapter with an additional test and evidence emphasis |
| [`docs/audit/`](audit/README.md) | audit | historical or explicitly current | Dated review snapshots; each artifact is classified in the audit index |

Local ignored guidance files are outside this registry. They may help a local
tool, but they cannot own repository facts because a clean clone does not
contain them.

## Change flow

Durable architecture work follows this sequence:

```text
proposal, finding, issue, audit, external evidence, or owner decision
    -> investigation and formalized accepted decision
    -> TODO.md
    -> implementation
    -> current technical documentation and evidence
    -> history when complete
```

`ROADMAP.md` supplies direction and guardrails around this flow but does not
authorize implementation. `docs/proposals/` is one possible input, not a
mandatory queue: any listed source may be promoted after formalization and
owner acceptance.

When an audit is complete, preserve its original evidence and mark it
historical. Record later outcomes in a short reconciliation section or in the
audit index. A finding becomes live work only after it is verified, a direction
is accepted, and it is added to `TODO.md`.

## Verification

Run the repository documentation and topology checks from the root:

```powershell
.\eng\verify-docs.ps1
.\eng\verify-skills.ps1
```

The documentation verifier checks authored document structure and links. The
skill verifier separately checks repository-owned adapter coverage and the
deterministic portions of the registered parity contract; semantic parity still
requires a scoped review of the affected logical skill.

Build the library family and compare every assembly-derived package/target API
against the accepted candidate snapshots:

```powershell
.\eng\verify-public-api.ps1
```

Baseline updates are accepted API decisions, not normal verification. The
scoped `-PackageId` and `-UpdateBaseline` workflow is defined by the
[`public API and release policy`](public-api-release-policy.md).

To compare a full rebuild against the normalized warning-identity baseline,
capture its output and pass the log explicitly:

```powershell
.\eng\verify-docs.ps1 -BuildLogPath artifacts/validation/rebuild.log
```

`-UpdateWarningBaseline` is an intentional maintenance operation, not part of a
normal verification run.
