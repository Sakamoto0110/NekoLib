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
   - Navigation: `../nekolib-navigation/SKILL.md`
   - Data: `../nekolib-data/SKILL.md`
   - Devices: `../nekolib-devices/SKILL.md`
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

Route to `nekolib-repository-hygiene` for:

- repository-level `README.md`, `TODO.md`, `AGENTS.md`, `.gitignore`, or
  `NekoLib.sln` governance
- `docs/`, `runtime_tests/`, `src/Tools/`, `tools/`, `eng/`, `artifacts/`, or
  `.local/` topology and lifecycle
- documentation authority, historical audits, test taxonomy, solution or
  project membership, clean-clone reproducibility, broken paths, generated
  artifacts, or duplicate, stale, and misplaced files
- implementation or review of TODO Phase C

Load every applicable skill when a request changes a boundary between modules.
Load repository hygiene together with a module skill when structural work
requires validating module-specific claims. Do not route an ordinary internal
module refactor to repository hygiene merely because it moves files.
Never infer a new project dependency merely because multiple module skills are
active. Treat the current graph as a baseline rather than a categorical ban;
let the specialized workflow evaluate an explicitly requested dependency.
