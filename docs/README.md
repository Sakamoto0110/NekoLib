# Documentation Index and Authority

**Kind:** reference

**Lifecycle:** current

**Subject:** repository documentation governance

**Reference date:** 2026-08-01

**Reference commit:** working tree after `c5a152f`

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

Audit files use these stable metadata fields near the top of the file:

- `Kind`
- `Lifecycle`
- `Subject`
- `Reference date`
- `Reference commit`
- `Last reconciliation`
- `Current state`

Use `not recorded` when an old audit did not preserve its reference commit. Do
not infer or fabricate one from the file's first Git appearance.

## Authority by fact type

| Fact | Authoritative owner | Supporting evidence |
|---|---|---|
| Target frameworks, project references, build and package properties | The affected `*.csproj` and `Directory.Build.*` files | Restore/build/package validation |
| Solution membership | `NekoLib.sln` | `dotnet sln NekoLib.sln list` and solution build |
| Public API and runtime behavior | Current source code | Executable tests and runtime scenarios |
| Product purpose and concise module map | [`README.md`](../README.md) | Project files and source |
| Navigation technical contract | [`src/Navigation/NekoLib.Navigation/README.md`](../src/Navigation/NekoLib.Navigation/README.md) | Navigation source and tests |
| Open work, accepted decisions, active freezes, and completion criteria | [`TODO.md`](../TODO.md) | Current implementation and validation |
| Historical findings | The audit at its recorded date and commit | Later outcomes appear only in reconciliation sections |
| Agent workflow | [`AGENTS.md`](../AGENTS.md) and `.agents/skills/` | These files do not replace public or technical documentation |

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
| [`src/Navigation/NekoLib.Navigation/README.md`](../src/Navigation/NekoLib.Navigation/README.md) | reference | current | Navigation technical contract |
| [`docs/README.md`](README.md) | reference | current | Documentation governance and index |
| [`docs/audit/README.md`](audit/README.md) | reference | current | Audit registry and snapshot rules |
| [`docs/history/README.md`](history/README.md) | reference | current | Completed roadmap and implementation-history index |
| [`docs/history/architecture-roadmap-through-phase-d-2026-08-01.md`](history/architecture-roadmap-through-phase-d-2026-08-01.md) | roadmap/status | historical | Completed Phases A, B, and D plus the Phase C handoff snapshot |
| [`docs/history/phase-c-repository-hygiene-2026-08-01.md`](history/phase-c-repository-hygiene-2026-08-01.md) | roadmap/status | historical | Phase C completion, commit-bound validation, and residual gaps |
| [`tests/README.md`](../tests/README.md) | reference | current | Automated verification taxonomy and canonical entry points |
| [`runtime_tests/README.md`](../runtime_tests/README.md) | guide | current | Shared manual runtime-scenario contract and inventory |
| [`runtime_tests/SCENARIO_TEMPLATE.md`](../runtime_tests/SCENARIO_TEMPLATE.md) | guide | current | Required metadata and procedure template for new scenarios |
| [`runtime_tests/Devices/Com0Com/README.md`](../runtime_tests/Devices/Com0Com/README.md) | guide | current | Devices virtual-COM parity and protocol-readiness scenario |
| [`runtime_tests/Navigation/WpfSmoke/README.md`](../runtime_tests/Navigation/WpfSmoke/README.md) | guide | current | Navigation WPF interactive smoke scenario |
| [`runtime_tests/Watchdog/Supervisor481/README.md`](../runtime_tests/Watchdog/Supervisor481/README.md) | guide | current | Watchdog/Pipes interactive supervisor scenario |
| [`docs/repository-layout.md`](repository-layout.md) | reference | current | Tool source, automation, generated-artifact, and machine-local ownership |
| [`AGENTS.md`](../AGENTS.md) | guide | current | Versioned agent workflow; not public product authority |
| [`.agents/skills/nekolib/SKILL.md`](../.agents/skills/nekolib/SKILL.md) | guide | current | Repository-wide NekoLib routing |
| [`.agents/skills/nekolib-data/SKILL.md`](../.agents/skills/nekolib-data/SKILL.md) | guide | current | Data workflow |
| [`.agents/skills/nekolib-devices/SKILL.md`](../.agents/skills/nekolib-devices/SKILL.md) | guide | current | Devices workflow |
| [`.agents/skills/nekolib-navigation/SKILL.md`](../.agents/skills/nekolib-navigation/SKILL.md) | guide | current | Navigation workflow |
| [`.agents/skills/nekolib-repository-hygiene/SKILL.md`](../.agents/skills/nekolib-repository-hygiene/SKILL.md) | guide | current | Repository hygiene workflow |
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
```

To compare a full rebuild against the normalized warning-identity baseline,
capture its output and pass the log explicitly:

```powershell
.\eng\verify-docs.ps1 -BuildLogPath artifacts/validation/rebuild.log
```

`-UpdateWarningBaseline` is an intentional maintenance operation, not part of a
normal verification run.
