# Phase C Repository Hygiene Completion

**Kind:** roadmap/status

**Lifecycle:** historical

**Subject:** completed repository documentation and organization phase

**Reference date:** 2026-08-01

**Reference commit:** `abe2b3d3c717c494ad9e0df4c6f925792433d39d`

**Current state:** [`TODO.md`](../../TODO.md)

This snapshot records the completed Phase C baseline. It covers the clean
`HEAD` named above; ignored generated validation logs and package outputs are
supporting local evidence, not versioned repository state. The archival commit
that adds this file is documentation-only and is intentionally outside the
validated baseline.

## Completed scope

- **C1 -- documentation authority:** established document kind, lifecycle,
  ownership, and audit metadata in [`docs/README.md`](../README.md).
- **C2 -- current-fact reconciliation:** checked the product map, project graph,
  target frameworks, packages, tests, and agent guidance against source and
  project files.
- **C3 -- audit governance:** indexed dated audits, moved the Data audit under
  `docs/audit/`, and recorded later outcomes without rewriting historical
  findings into current facts.
- **C4 -- roadmap separation:** archived the prior architecture roadmap while
  preserving the complete Inspection freeze and live Diagnostics decisions in
  the current [`TODO.md`](../../TODO.md).
- **C5 -- test taxonomy:** documented execution mode, scope, prerequisites, and
  entry point independently in [`tests/README.md`](../../tests/README.md).
- **C6 -- runtime scenarios:** promoted the WPF Navigation smoke scenario and
  the net481 Watchdog supervisor as shared, versioned, buildable scenarios.
  Four obsolete experiments were retained locally under the ignored
  `.local/runtime-tests/legacy/` directory; they are recoverable local data and
  are not repository coverage.
- **C7 -- tool ownership:** assigned source to `src/Tools/`, automation to
  `eng/`, generated output to `artifacts/`, and machine-local state to `.local/`.
  BundlerTool now has a reproducible publish entry point and a provenance
  manifest.
- **C8 -- duplication:** removed redundant Navigation copies of the root
  license and attributes files. The remaining identical Watchdog
  `AssemblyInfo.cs` pair is intentional per-assembly input.
- **C9 -- verification:** added deterministic documentation, topology, link,
  and normalized-warning checks in [`eng/verify-docs.ps1`](../../eng/verify-docs.ps1).

## Validation

All commands below ran on Windows against the clean reference commit.

| Command | Result |
|---|---|
| `.\eng\verify-docs.ps1 -BuildLogPath artifacts/phase-c-validation/rebuild-abe2b3d.log` | Passed; 176 normalized warning identities matched the versioned baseline. |
| `dotnet sln NekoLib.sln list` | Passed; solution membership matched the documented source-project map. |
| `dotnet build NekoLib.sln -t:Rebuild -m:1 -v:minimal -nologo` | Passed with 0 errors and 529 warning occurrences. Occurrence count is recorded only in this snapshot; warning identity is the regression gate. |
| `dotnet test NekoLib.sln -m:1 -v:minimal -nologo` | Passed 898 executions across 22 project/target runs; 0 failed and 0 skipped. |
| `.\eng\pack-local.ps1 -PackageVersion 1.0.0-local.6` | Passed package-consumer probes and published 15 immutable packages to the ignored local feed. |
| `.\eng\build-bundler.ps1` | Passed; the manifest records the reference commit, a clean tree, and project/executable SHA-256 hashes. |
| WPF Navigation and net481 Watchdog shared scenario builds | Both passed with 0 warnings and 0 errors. |
| `git diff --check` | Passed. |

The first full-test invocation was limited externally to 120 seconds after all
completed runs had passed. A clean rerun with a sufficient window completed in
100.6 seconds and produced the result above; the earlier timeout was not
counted as a product failure.

## Residual validation gaps

- The WPF Navigation and Watchdog supervisor scenario procedures were built but
  were not launched interactively during this phase. Their READMEs identify
  the manual steps and expected results; build success is not represented as a
  completed interactive probe.
- The four legacy local runtime experiments do not compile against the current
  APIs and remain outside shared coverage unless separately rebuilt and
  promoted.
- SQLite was considered as a repository-inventory cache. It was not introduced:
  the current Git/MSBuild-derived checks complete quickly, and a database would
  add cache invalidation without becoming an authoritative source. It remains a
  valid future optimization if measured history or incremental-query costs
  justify it.

## Completion decision

Phase C is complete. A clean clone can discover current references, historical
artifacts, validation entry points, shared runtime scenarios, and reproducible
tool builds without relying on ignored files. Open work remains authoritative
only in [`TODO.md`](../../TODO.md).
