---
name: nekolib
description: Route ambiguous, explicitly invoked, cross-module, or repository-wide NekoLib work to the appropriate specialized skills. Use when the user invokes $nekolib, refers to NekoLib without making the affected area clear, requests work spanning multiple modules, or asks for repository organization and documentation governance. Do not use when a request is already clearly scoped to one module that has its own NekoLib skill.
---

# Route NekoLib Work

Act only as a lightweight router. Do not duplicate module rules or API
documentation in this skill.

## Route the request

1. Inspect the requested paths, projects, types, and APIs. Use repository
   structure and source evidence; do not classify from vocabulary alone.
2. Read every matching sibling skill completely:
   - Module-first documentation:
     `../nekolib-documentation/SKILL.md`
   - Navigation: `../nekolib-navigation/SKILL.md`
   - Data: `../nekolib-data/SKILL.md`
   - Devices: `../nekolib-devices/SKILL.md`
   - Repository inventory:
     `../nekolib-repository-inventory/SKILL.md`
   - Repository hygiene:
     `../nekolib-repository-hygiene/SKILL.md`
3. Follow all selected workflows. When a task spans modules, reconcile their
   validation requirements and inspect the affected project references.
4. If no specialized skill matches, follow `../../../AGENTS.md` and the relevant
   project documentation directly.
5. Briefly state which workflow or workflows were selected, then
   continue the user's task.

## Routing signals

Route to `nekolib-navigation` for:

- `src/Navigation/` or `tests/NekoLib.Navigation.Tests/`
- `NekoLib.Navigation`, `.WinForms`, or `.Wpf`
- `NavigationService`, `NavigationRuntime`, `NavigationContext`,
  `PageNavBootstrap`, pages, guards, history, idle behavior, overlays,
  navigation diagnostics, or platform adapters

Route to `nekolib-data` for:

- `src/Data/` or `tests/NekoLib.Data.Tests/`
- `NekoLib.Data`
- `QueryBuilder`, `DatabaseGateway`, `QueryExecutionContext`, `DbSession`,
  query translators, SQL generation, mapping, streaming, database providers,
  or transactions

Route to `nekolib-devices` for:

- `src/Devices/` or `tests/NekoLib.Devices.Tests/`
- `NekoLib.Devices`
- `HardwareEngine`, `ICommTransport`, hardware protocols, serial ports, byte
  streams, TCP transports, named-pipe transports, endpoint configuration, or
  device communication

Route to `repo-file-inventory` for:

- physical file counts or repeated-basename counts such as the number of
  `README.md` files across the repository
- compact category inventories or human-friendly Markdown tree views
- tracked, untracked, and included ignored-context counts
- structural change-set summaries against an explicit Git ref
- physical documentation-surface or repository-skill topology
- read-only file-distribution discovery before a repository discussion

Load repository hygiene as well when the request asks what should move, merge,
become canonical, or be removed. Inventory measures topology; hygiene interprets
and changes it.

Route to `nekolib-repository-hygiene` for:

- repository-level `README.md`, `TODO.md`, `AGENTS.md`, `.gitignore`, or
  `NekoLib.sln` governance
- `.agents/skills/`, `.claude/skills/`, the agent skill registry, adapter
  registration, or cross-agent parity intent
- `docs/`, `runtime_tests/`, `src/Tools/`, `tools/`, `eng/`, `artifacts/`, or
  `.local/` topology and lifecycle
- documentation authority, historical audits, test taxonomy, solution or
  project membership, clean-clone reproducibility, broken paths, generated
  artifacts, or duplicate, stale, and misplaced files
- implementation or review of TODO Phase C

Route to `nekolib-documentation` for:

- `docs/modules/`, `docs/governance/`, `docs/schemas/`, or `docs/templates/`
- module-first documentation design, migration, population, or validation
- documentation manifests, canonical references, portals, stable document
  records, validation records, or the cross-agent authoring contract
- documentation skill interoperability between Codex, Claude, or another
  registered profile

Load every applicable skill when a request changes a boundary between modules.
Load repository hygiene together with the documentation skill for structural
documentation work, and with a module skill when structural work requires
validating module-specific claims. Do not route an ordinary internal module
refactor to repository hygiene merely because it moves files.
Never infer a new project dependency merely because multiple module skills are
active. Treat the current graph as a baseline rather than a categorical ban;
let the specialized workflow evaluate an explicitly requested dependency.
