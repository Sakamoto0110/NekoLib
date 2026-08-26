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
| Data gateway technical contract | [`src/Data/NekoLib.Data/README.md`](../src/Data/NekoLib.Data/README.md) | Data source, tests, and provider scenarios |
| Diagnostics and Diagnostics.Windows technical contract | [`src/Diagnostics/NekoLib.Diagnostics/README.md`](../src/Diagnostics/NekoLib.Diagnostics/README.md) | Diagnostics source and the dual-target Diagnostics tests |
| Logging pipeline technical contract | [`src/Logging/NekoLib.Logging/README.md`](../src/Logging/NekoLib.Logging/README.md) | Logging source, tests, and the Observability scenario |
| Telemetry pipeline technical contract | [`src/Telemetry/NekoLib.Telemetry/README.md`](../src/Telemetry/NekoLib.Telemetry/README.md) | Telemetry source, tests, and the Observability scenario |
| Inspection runtime technical contract | [`src/Inspection/NekoLib.Inspection/README.md`](../src/Inspection/NekoLib.Inspection/README.md) | Inspection source, tests, and the Observability scenario |
| Pipes technical contract | [`src/Pipes/NekoLib.Pipes/README.md`](../src/Pipes/NekoLib.Pipes/README.md) | Pipes source, dual-target tests, and the compiled public API manifests |
| Watchdog technical contract | [`src/Watchdog/NekoLib.Watchdog/README.md`](../src/Watchdog/NekoLib.Watchdog/README.md) | Watchdog source, dual-target tests, and the compiled public API manifests |
| Watchdog Host deployment contract | [`src/Watchdog/NekoLib.Watchdog.Host/README.md`](../src/Watchdog/NekoLib.Watchdog.Host/README.md) | Host source, dual-target tests, package target, and package-only consumers |
| Navigation technical contract | [`src/Navigation/NekoLib.Navigation/README.md`](../src/Navigation/NekoLib.Navigation/README.md) | Navigation source and tests |
| Mvvm technical contract | [`docs/modules/Mvvm/REFERENCE.md`](modules/Mvvm/REFERENCE.md) | Mvvm source, focused tests, and compiled public API manifests |
| Open work, accepted decisions, active freezes, and completion criteria | [`TODO.md`](../TODO.md) | Current implementation and validation |
| Historical findings | The audit at its recorded date and commit | Later outcomes appear only in reconciliation sections |
| Agent documentation authoring | [`docs/governance/agent-documentation-contract.md`](governance/agent-documentation-contract.md) and registered skills | Profiles share one output contract and do not replace public or technical documentation |
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
| [`TODO.md`](../TODO.md) | roadmap/status | current | Open work, accepted decisions, freezes, and completion criteria |
| [`CHANGELOG.md`](../CHANGELOG.md) | reference | current | Consumer-visible package, public API, compatibility, and migration changes |
| [`src/Core/NekoLib.Core/README.md`](../src/Core/NekoLib.Core/README.md) | reference | current | Core capability contracts, ownership, snapshots, null objects, and experimental action marker |
| [`src/Data/NekoLib.Data/README.md`](../src/Data/NekoLib.Data/README.md) | reference | current | Data gateway composition, ownership, capabilities, mapping, sessions, events, and target-specific streaming |
| [`src/Diagnostics/NekoLib.Diagnostics/README.md`](../src/Diagnostics/NekoLib.Diagnostics/README.md) | reference | current | Incident collection, handler installation and disposal, evidence budgets and bounds, redaction boundary, crash-bundle layout, and the Windows crash adapter |
| [`src/Devices/NekoLib.Devices/README.md`](../src/Devices/NekoLib.Devices/README.md) | reference | current | Hardware engine orchestration, operation boundaries, configuration ownership, transport and protocol contracts, encoding, and disposal |
| [`docs/modules/Mvvm/MANIFEST.md`](modules/Mvvm/MANIFEST.md) | reference | current | Mvvm identity, routing, project/package topology, API oracles, and evidence locations |
| [`docs/modules/Mvvm/README.md`](modules/Mvvm/README.md) | reference | current | Concise Mvvm consumer introduction |
| [`docs/modules/Mvvm/REFERENCE.md`](modules/Mvvm/REFERENCE.md) | reference | current | Binding helper and command contracts: parameter coercion, notification semantics, threading, exception behavior, and nullability |
| [`docs/modules/Mvvm/HISTORY.md`](modules/Mvvm/HISTORY.md) | reference | current | Append-only factual Mvvm chronology; structural empty state in the pilot |
| [`docs/modules/Mvvm/CHANGELOG.md`](modules/Mvvm/CHANGELOG.md) | reference | current | Module changelog scaffold routing to the coordinated family changelog until populated |
| [`docs/modules/Mvvm/ISSUES.md`](modules/Mvvm/ISSUES.md) | reference | current | Confirmed Mvvm issues registry; structural empty state in the pilot |
| [`docs/modules/Mvvm/FINDINGS.md`](modules/Mvvm/FINDINGS.md) | reference | current | Non-normative Mvvm findings registry; structural empty state in the pilot |
| [`docs/modules/Mvvm/BACKLOG.md`](modules/Mvvm/BACKLOG.md) | roadmap/status | current | Unpromoted Mvvm ideas; structural empty state in the pilot |
| [`docs/modules/Mvvm/VALIDATION_REQUIREMENTS.md`](modules/Mvvm/VALIDATION_REQUIREMENTS.md) | reference | current | Mvvm evidence-contract scaffold and inherited validation profile |
| [`docs/modules/Mvvm/VALIDATIONS.md`](modules/Mvvm/VALIDATIONS.md) | reference | current | Mvvm executed-evidence registry; structural empty state in the pilot |
| [`src/Mvvm/NekoLib.Mvvm/README.md`](../src/Mvvm/NekoLib.Mvvm/README.md) | reference | current | Pointer-only source portal to the canonical Mvvm technical reference |
| [`src/Pipes/NekoLib.Pipes/README.md`](../src/Pipes/NekoLib.Pipes/README.md) | reference | current | Named-pipe RPC/events, configuration ownership, lifecycle, framing, metrics, errors, access policy, and target-specific payload contracts |
| [`src/Watchdog/NekoLib.Watchdog/README.md`](../src/Watchdog/NekoLib.Watchdog/README.md) | reference | current | Watchdog application bootstrap, advanced runtime, configuration, lifecycle, process ownership, control, evidence, security, and package boundary |
| [`src/Watchdog/NekoLib.Watchdog.Host/README.md`](../src/Watchdog/NekoLib.Watchdog.Host/README.md) | reference | current | Watchdog Host deployment package, payload selection, build/publish behavior, protocol v1, fatal evidence, security, and release validation |
| [`src/Logging/NekoLib.Logging/README.md`](../src/Logging/NekoLib.Logging/README.md) | reference | current | Logging pipeline composition, ownership, delivery ordering, snapshots, flush and disposal contracts, and the shipped sinks |
| [`src/Telemetry/NekoLib.Telemetry/README.md`](../src/Telemetry/NekoLib.Telemetry/README.md) | reference | current | Telemetry pipeline composition, ownership, operation lifecycle, time semantics, dimensions, bounded retention, snapshots, and sink dispatch |
| [`src/Inspection/NekoLib.Inspection/README.md`](../src/Inspection/NekoLib.Inspection/README.md) | reference | current | Passive Inspection composition, recording, provider identity and ordering, snapshot budgets, owner diagnostics, lifecycle, and experimental actions |
| [`src/Navigation/NekoLib.Navigation/README.md`](../src/Navigation/NekoLib.Navigation/README.md) | reference | current | Navigation technical contract |
| [`src/Http/NekoLib.Http/README.md`](../src/Http/NekoLib.Http/README.md) | reference | current | Typed HTTP endpoint catalogs, consumer ownership, response bounds, and explicit non-goals |
| [`docs/README.md`](README.md) | reference | current | Documentation governance and index |
| [`docs/governance/documentation-policy.md`](governance/documentation-policy.md) | reference | current | Module-first authority, lifecycle, conflict, migration, and indexing policy |
| [`docs/governance/agent-documentation-contract.md`](governance/agent-documentation-contract.md) | reference | current | Interoperable output contract and permitted role variation for documentation skills |
| [`docs/governance/validation-policy.md`](governance/validation-policy.md) | reference | current | Validation taxonomy, profiles, requirement/evidence separation, and soak records |
| [`docs/schemas/README.md`](schemas/README.md) | reference | current | Deterministic documentation and agent-skill schema index |
| [`docs/modules/README.md`](modules/README.md) | reference | current | Module-first boundary index and migration state |
| [`docs/public-api-release-policy.md`](public-api-release-policy.md) | reference | current | F1 public API classification, SemVer, compatibility, deprecation, baseline, and release rules |
| [`docs/stable-release-1.0.0.md`](stable-release-1.0.0.md) | reference | current | First stable family baseline, qualifying package provenance, hashes, validation, and distribution boundaries |
| [`docs/migrations/f1-core.md`](migrations/f1-core.md) | guide | current | Migration from the initial Core candidate surface to defensive outer snapshots and experimental action registration |
| [`docs/migrations/f1-data.md`](migrations/f1-data.md) | guide | current | Migration from the initial Data candidate surface to the accepted F1-DATA gateway contract |
| [`docs/migrations/f1-diagnostics.md`](migrations/f1-diagnostics.md) | guide | current | Migration from the initial Diagnostics candidate surface to the accepted F1-DIAG incident-collection, bundle, lifecycle, and ownership contracts |
| [`docs/migrations/f1-http.md`](migrations/f1-http.md) | guide | current | Migration from the initial HTTP candidate surface to the accepted F1-HTTP charset, response-evidence, identity, and validation contracts |
| [`docs/migrations/f1-devices.md`](migrations/f1-devices.md) | guide | current | Migration from the initial Devices candidate surface to the accepted F1-DEV operation-boundary, configuration-ownership, failure-evidence, and nullability contracts |
| [`docs/modules/Mvvm/migrations/f1.md`](modules/Mvvm/migrations/f1.md) | guide | current | Migration from the initial Mvvm candidate surface to the accepted F1-MVVM nullability contract and virtual notification funnel |
| [`docs/migrations/f1-pipes.md`](migrations/f1-pipes.md) | guide | current | Migration from the initial Pipes candidate surface to the accepted F1-PIPE ownership, lifecycle, event, metrics, error, and target contracts |
| [`docs/migrations/f1-watchdog.md`](migrations/f1-watchdog.md) | guide | current | Migration from the initial Watchdog candidate surface to the accepted F1-WDOG application, advanced-runtime, lifecycle, control, evidence, and ownership contracts |
| [`docs/migrations/f1-watchdog-host.md`](migrations/f1-watchdog-host.md) | guide | current | Migration from the initial Watchdog Host candidate package to direct-only deployment, protocol v1, fail-fast workdir validation, and bounded fatal evidence |
| [`docs/migrations/f1-logging.md`](migrations/f1-logging.md) | guide | current | Migration from the initial Logging candidate surface to the accepted F1-LOG pipeline, flush, disposal, and sink behavior |
| [`docs/migrations/f1-inspection.md`](migrations/f1-inspection.md) | guide | current | Migration from the initial Inspection candidate surface to the accepted passive runtime and experimental action boundary |
| [`docs/migrations/f1-telemetry.md`](migrations/f1-telemetry.md) | guide | current | Migration from the initial Telemetry candidate surface to the accepted F1-TEL completion, correlation, and sink-capture behavior |
| [`docs/migrations/f1-navigation.md`](migrations/f1-navigation.md) | guide | current | Migration from the initial Navigation candidate family to the accepted core facade, adapter compatibility, ownership, disposal, and nullability contracts |
| [`docs/audit/README.md`](audit/README.md) | reference | current | Audit registry and snapshot rules |
| [`docs/audit/core-public-api-review-2026-08-17.md`](audit/core-public-api-review-2026-08-17.md) | audit | historical | F1-CORE compiled-surface review, accepted decisions, and implementation reconciliation |
| [`docs/audit/data-public-api-review-2026-08-17.md`](audit/data-public-api-review-2026-08-17.md) | audit | historical | F1-DATA compiled-surface review, accepted decisions, and implementation reconciliation |
| [`docs/audit/devices-public-api-review-2026-08-17.md`](audit/devices-public-api-review-2026-08-17.md) | audit | historical | F1-DEV compiled-surface review, accepted implementation reconciliation, and immutable package evidence |
| [`docs/audit/diagnostics-public-api-review-2026-08-17.md`](audit/diagnostics-public-api-review-2026-08-17.md) | audit | historical | F1-DIAG compiled-surface review, accepted implementation reconciliation, final lifecycle correction, and immutable package evidence |
| [`docs/audit/diagnostics-windows-public-api-review-2026-08-17.md`](audit/diagnostics-windows-public-api-review-2026-08-17.md) | audit | historical | F1-WIN compiled-surface review, accepted implementation reconciliation, and immutable package evidence |
| [`docs/audit/logging-public-api-review-2026-08-17.md`](audit/logging-public-api-review-2026-08-17.md) | audit | historical | F1-LOG compiled-surface review, accepted dispositions, implementation reconciliation, and immutable package evidence |
| [`docs/audit/inspection-public-api-review-2026-08-17.md`](audit/inspection-public-api-review-2026-08-17.md) | audit | historical | F1-INSP compiled-surface review, accepted implementation reconciliation, and immutable package evidence |
| [`docs/audit/http-public-api-review-2026-08-17.md`](audit/http-public-api-review-2026-08-17.md) | audit | historical | F1-HTTP compiled-surface review, accepted implementation reconciliation, and immutable package evidence |
| [`docs/modules/Mvvm/audits/public-api-review-2026-08-17.md`](modules/Mvvm/audits/public-api-review-2026-08-17.md) | audit | historical | F1-MVVM compiled-surface review, accepted implementation reconciliation, and immutable package evidence |
| [`docs/audit/navigation-public-api-review-2026-08-20.md`](audit/navigation-public-api-review-2026-08-20.md) | audit | historical | F1-NAV code-first core public API review, accepted implementation reconciliation, and residual package/runtime gaps |
| [`docs/audit/navigation-winforms-public-api-review-2026-08-21.md`](audit/navigation-winforms-public-api-review-2026-08-21.md) | audit | historical | F1-NAV-WF code-first adapter review, accepted implementation reconciliation, exact manifest changes, and residual package/runtime gaps |
| [`docs/audit/navigation-wpf-public-api-review-2026-08-21.md`](audit/navigation-wpf-public-api-review-2026-08-21.md) | audit | historical | F1-NAV-WPF code-first adapter review, accepted implementation reconciliation, exact manifest changes, and residual package/runtime gaps |
| [`docs/audit/nekomarketplace-external-consumer-evidence-intake-2026-08-26.md`](audit/nekomarketplace-external-consumer-evidence-intake-2026-08-26.md) | audit | historical | NekoMarketplace external-consumer evidence intake for published NekoLib 1.0.0 packages; current reconciliation remains gated |
| [`docs/audit/payments-pix-design-review-2026-08-16.md`](audit/payments-pix-design-review-2026-08-16.md) | audit | current | Phase G2 code-first design review for a bounded Pix payment module and Efí sandbox model; implementation remains gated |
| [`docs/audit/pipes-public-api-review-2026-08-18.md`](audit/pipes-public-api-review-2026-08-18.md) | audit | historical | F1-PIPE compiled-surface review, accepted decisions, implementation reconciliation, and residual release-evidence gaps |
| [`docs/audit/telemetry-public-api-review-2026-08-17.md`](audit/telemetry-public-api-review-2026-08-17.md) | audit | historical | F1-TEL compiled-surface review, accepted dispositions, implementation reconciliation, and immutable package evidence |
| [`docs/audit/watchdog-public-api-review-2026-08-18.md`](audit/watchdog-public-api-review-2026-08-18.md) | audit | historical | F1-WDOG compiled public surface, accepted decisions, implementation reconciliation, and residual Host/release-evidence gaps |
| [`docs/audit/watchdog-host-contract-review-2026-08-20.md`](audit/watchdog-host-contract-review-2026-08-20.md) | audit | historical | F1-WDOG-HOST deployment package and protocol review, accepted implementation reconciliation, and immutable package evidence |
| [`docs/history/README.md`](history/README.md) | reference | current | Completed roadmap and implementation-history index |
| [`docs/history/architecture-roadmap-through-phase-d-2026-08-01.md`](history/architecture-roadmap-through-phase-d-2026-08-01.md) | roadmap/status | historical | Completed Phases A, B, and D plus the Phase C handoff snapshot |
| [`docs/history/phase-c-repository-hygiene-2026-08-01.md`](history/phase-c-repository-hygiene-2026-08-01.md) | roadmap/status | historical | Phase C completion, commit-bound validation, and residual gaps |
| [`docs/history/phase-e-confidence-stabilization-2026-08-12.md`](history/phase-e-confidence-stabilization-2026-08-12.md) | roadmap/status | historical | Complete Phase E work log, outcome-first evidence boundaries, residual confidence, and final commit-bound validation |
| [`docs/history/phase-g1-http-integration-2026-08-16.md`](history/phase-g1-http-integration-2026-08-16.md) | roadmap/status | historical | Phase G1 typed HTTP catalog completion, deterministic and package evidence, and the optional provider-evidence boundary |
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
review or audit -> accepted decision -> TODO.md -> implementation -> current technical documentation
```

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
