# Phase C Repository Hygiene Readiness Review

**Kind:** audit

**Lifecycle:** historical

**Subject:** Phase C repository-hygiene readiness

**Status:** complete; Phase C is ready to start

**Reference date:** 2026-08-01

**Reference ref:** `master` / `HEAD`

**Reference commit:** `88b07d83f037db6bf13eab34ac3f5abafba787b1`

**Last reconciliation:** 2026-08-01

**Current state:** [`TODO.md`](../../TODO.md)

**Coverage:** committed `HEAD` plus a read-only inventory of ignored local
`runtime_tests/`, `tools/`, and `artifacts/`

**Open-work authority:** [`TODO.md`](../../TODO.md)

## Baseline

The worktree was clean after the Phase D, Devices transport, and repository
skill commits. This review did not modify product code, project references,
solution membership, ignored runtime scenarios, local tools, or generated
artifacts.

The current Phase C roadmap remains structurally sound: C1-C9 cover authority,
current-document reconciliation, audit lifecycle, roadmap cleanup, verification
taxonomy, runtime scenarios, tools and artifacts, duplication, and automated
verification. No architecture decision blocks C1 from starting.

## Observed facts

### Documentation authority is not implemented yet

- `docs/README.md`, `docs/audit/README.md`, `docs/history/`, and
  `eng/verify-docs.ps1` do not exist.
- The repository contains 15 tracked Markdown files. A simple relative-link
  scan found 29 missing targets, all inside the Diagnostics boundary review and
  all pointing to source removed by Phase D. C1/C3 must classify such
  baseline-relative audit evidence before C9 decides when a removed path is an
  error.
- Audit headers are inconsistent. Examples include `Latest`, `Status
  (current)`, branch-only baselines, and the Diagnostics review's still-active
  decision status. The original audit bodies must remain snapshots.

### Two current roadmap premises have drifted

- The package list in [`eng/pack-local.ps1`](../../eng/pack-local.ps1) contains
  14 library packages plus `NekoLib.Watchdog.Host`; the root README still says
  13 libraries plus the sidecar.
- `tests/NekoLib.Data.Tests/Shared/Pods.db` and `PodsDB` are tracked files even
  though their directory also matches a `.gitignore` rule. Current versioned
  Data tests do not reference either fixture by name. C2 must reconcile both
  facts instead of describing the fixtures simply as absent or ignored.

### Ignored runtime scenarios are local evidence only

Six local scenario projects exist under the fully ignored `runtime_tests/`
tree: `DiagnosticsPipesWatchdog`, `IntegrationDemo_481`, `LoginFlow_481`,
`Supervisor_481`, `WinForms_481`, and `Wpf_Smoke`.

At least `IntegrationDemo_481` and `WinForms_481` still reference APIs or
project paths removed by Phase D, including `NekoLib.Logger`,
`NekoLib.DebugUtils`, `UseDiagnostics`, or `UseDebugUtils`. None of the six may
be treated as shared current coverage until C6 classifies it and versions the
required source and instructions. This review does not decide that every local
scenario should be retained.

### Tool and duplicate ownership is already discoverable

- `src/Tools/BundlerTool/` is tracked source; `tools/BundlerTool.exe` is an
  ignored 396,800-byte local payload; `artifacts/` contains generated package
  and smoke-test output.
- A SHA-256 pass over tracked files found exactly three duplicate groups:
  root/navigation `.gitattributes`, root/navigation `LICENSE.txt`, and the two
  Watchdog `AssemblyInfo.cs` files. Project and packaging inspection confirms
  that package licensing comes from the root through `Directory.Build.targets`;
  the Navigation copies are not package inputs. The Watchdog pair remains
  legitimate per-assembly metadata.

## Accepted preparation decisions

- B4 and B5 are remarks, not active Phase C implementation work. The remarks
  retain the current Inspection freeze, remaining gaps, usable seams, resume
  order, and validation expectations. Superseded DebugUtils-era work logs do
  not need to remain inline in the live roadmap.
- Phase C does not unfreeze module instrumentation, change product behavior, or
  alter the dependency graph.
- Historical audit bodies remain unchanged. C3 adds classification and
  reconciliation around them rather than rewriting old evidence.
- Ignored runtime content is inventoried but is not moved, deleted, or promoted
  until each scenario receives an explicit C6 classification.
- The optional LLM cataloger remains outside the Phase C completion criteria.

## Recommended execution order

1. **C1 + C3 foundation:** create the documentation and audit indexes, define
   machine-checkable metadata, and classify every current audit snapshot.
2. **C2 + C4 reconciliation:** correct current README/AGENTS facts, archive
   completed Phase A/B/D work logs, and leave the live roadmap with only open
   work, decisions, criteria, and concise B4/B5 remarks.
3. **C5 taxonomy:** document automated tests, package probes, and manual/runtime
   verification without forcing cosmetic folder moves.
4. **C6 classification:** decide the fate of each of the six ignored scenarios;
   version only the scenarios that will be maintained as shared evidence.
5. **C7 + C8 topology:** formalize tool/output ownership, make BundlerTool output
   reproducible under `artifacts/`, and remove only the two verified redundant
   Navigation copies.
6. **C9 enforcement:** add the verifier after metadata and lifecycle rules are
   stable, then run the full Windows build, tests, link/path checks, warning
   identity comparison, and final validation snapshot.

This ordering keeps C9 from encoding rules that C1/C3 have not defined and
keeps C6's local-content decisions out of the initial documentation foundation.

## Rejected alternatives

- Do not make `AGENTS.md` a public documentation index.
- Do not treat every ignored runtime project as repository coverage merely
  because it exists on this machine.
- Do not move all tests or modules into a uniform folder template.
- Do not create a second BundlerTool scanner or a generic ignored
  `internal_tools/` source tree.
- Do not rewrite historical audit bodies to repair their old links or claims.

## Validation performed

```text
git status --short
git ls-files docs tests src/Tools eng tools runtime_tests .local
git status --short --ignored -- docs runtime_tests tools artifacts .local
dotnet sln NekoLib.sln list
git check-ignore -v --no-index <fixture/runtime/tool samples>
SHA-256 comparison of every tracked file
read-only Markdown relative-link scan
```

No Phase C completion criterion was marked complete by this readiness review.
