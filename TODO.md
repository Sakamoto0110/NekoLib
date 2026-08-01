# NekoLib Live Roadmap

**Kind:** roadmap/status

**Lifecycle:** current

**Subject:** open work, accepted decisions, freezes, and completion criteria

Completed architecture work through Phases A, B, and D is preserved in the
[historical roadmap snapshot](docs/history/architecture-roadmap-through-phase-d-2026-08-01.md).
Audit snapshots are indexed separately under [`docs/audit/`](docs/audit/README.md).

## Frozen — deferred Inspection module rollout (B4/B5)

**Freeze reason:** the Core contracts, global Inspection runtime, Navigation
producer, and Diagnostics read-only consumer are proven, but broad module
instrumentation and state-changing actions have not yet demonstrated enough
value or a safe common contract. This is live context, not completed history.

**Implemented state:**

- Core owns independent Logging, Telemetry, and Inspection contracts plus
  non-null NO-OP defaults.
- `InspectionRuntime.EnableGlobal(...)` provides deterministic singleton
  activation and teardown. Navigation is the only feature module that records
  Inspection operations.
- Diagnostics consumes only `IInspectionSnapshotSource`; incident collection
  cannot invoke Inspection actions.
- Navigation telemetry owns the bounded page-switch timing producer accepted in
  Phase D. That work does not authorize broader Inspection recording.

**Known gaps and traps:**

- Data, Pipes, Watchdog, Devices, and Diagnostics do not record feature-module
  Inspection operations. A sample application calling `Record(...)` manually
  is application instrumentation, not module instrumentation.
- No feature module registers a real Inspection action. Navigation stays
  read-only until async execution, cancellation, timeout, and UI-marshalling
  semantics are explicitly accepted.
- Watchdog crash notification crosses IPC. Its log/crash integration must be
  designed separately from in-process module recording.

**Existing seams:**

- Data: `QueryExecutionContext`.
- Pipes: `IPipeMetrics`.
- Devices: the serialized `HardwareEngine.SendAsync` transaction.
- Watchdog and Diagnostics: their existing incident and IPC boundaries, after a
  dedicated review.

**Resume order and unfreeze conditions:**

1. Explicitly unfreeze one bounded module and define the operational question
   its data must answer.
2. Validate the smallest real producer before copying a pattern elsewhere;
   Data or Pipes are the preferred first candidates.
3. Preserve disabled/NO-OP behavior, module boundaries, and both supported
   target families.
4. Restore the broad freeze after the authorized module scope is complete.

## Phase C — repository documentation and organization

Phase C is structural. It does not authorize product behavior changes, project
dependency changes, or broad Inspection instrumentation.

### Completed foundation

- [x] C1 — Define documentation authority, classification, lifecycle, and
  stable audit metadata in [`docs/README.md`](docs/README.md).
- [x] C2 — Reconcile current README and agent guidance against project files,
  solution membership, packaging, source, and tracked test assets.
- [x] C3 — Index audits as snapshots, move the Data audit, and reconcile the
  named historical divergences without creating competing work lists.
- [x] C4 — Archive completed phase logs while preserving this full Inspection
  freeze and the live Diagnostics review.

### C5 — Formalize the test taxonomy

- [x] Define `tests/` as automated verification, not as a synonym for unit
  tests.
- [x] Classify verification independently by execution mode, scope
  (unit/integration/functional/package probe), prerequisites, and entry point.
  A semantic classification does not require moving a test by itself.
- [x] Keep unit tests under `tests/NekoLib.{Module}.Tests/Unit/`.
- [x] Document that `tests/NekoLib.PackageConsumers/` contains package probes,
  stays outside `NekoLib.sln`, and runs through the packaging workflow.
- [x] Classify automated tests that use processes, IPC, a real database, or OS
  resources as integration/functional. Split them physically when their cost,
  prerequisites, commands, or isolation justify a separate suite.
- [x] Document canonical commands for the solution, a project, a TFM, one test,
  and package-consumer probes.

### C6 — Give `runtime_tests/` an operational contract

Shared evidence must be versioned. Machine-only experiments belong under
`.local/` and cannot be cited as repository coverage.

- [x] Inventory each current ignored scenario and classify it as rebuild for
  shared evidence, retain as a local experiment, archive, or remove. Do not
  treat the outdated scenarios as current behavioral evidence.
- [x] Decide each scenario before changing `.gitignore` or shared docs.
- [x] For shared scenarios, version `runtime_tests/README.md`, a minimal
  template, source, and instructions; ignore only outputs and temporary data.
- [x] Record purpose, module, OS/TFM, prerequisites, build, executable, manual
  steps, expected result, cleanup, and last verified date/commit for each active
  shared scenario.
- [x] Organize new scenarios first by validated module/capability; keep UI and
  TFM as metadata or a secondary level.
- [x] Keep scenarios outside `NekoLib.sln` by default. Run them through an
  explicit build and executable launch, never `dotnet test`.
- [x] Move machine-only experiments to `.local/runtime-tests/` and remove
  shared-document references to them.
- [x] Update `.gitignore` after classification so source and instructions for
  active shared scenarios remain visible while outputs stay ignored.

### C7 — Separate tools, automation, and artifacts

- [x] Establish these owners:
  - `src/Tools/`: versioned source for repository-owned executables;
  - `tools/`: restored/local executable payloads, never source authority;
  - `eng/`: versioned build, validation, and maintenance automation;
  - `artifacts/`: disposable generated output;
  - `.local/`: machine-only experiments, configuration, and scratch data.
- [x] No test may depend on an opaque, manually copied executable. A
  repository-owned executable needs a reproducible build or restore, version,
  and hash; OS binaries are declared prerequisites, not vendored payloads.
- [x] Keep `src/Tools/BundlerTool/` as the canonical BundlerTool source and
  provide a reproducible build whose output goes to `artifacts/`.
  `tools/BundlerTool.exe` may exist only as an ignored local cache.
- [x] Do not create a broad Git-invisible `internal_tools/` tree. Version useful
  source; ignore only outputs, caches, credentials, and scratch data.

An LLM-oriented code catalog is outside Phase C. If separately authorized, it
must reuse or extract BundlerTool's Roslyn scanner, generate deterministic
output under `artifacts/`, attach commit/source evidence, mark inferred intent,
and never inject inferred comments into source code.

### C8 — Remove physical duplication and logical divergence

- [x] Remove the identical
  `src/Navigation/NekoLib.Navigation/LICENSE.txt` and `.gitattributes` only
  after references and packaging confirm that the root copies are authoritative.
- [x] Preserve assembly-required duplicates such as the two Watchdog
  `AssemblyInfo.cs` files even when their content is identical.
- [x] Replace repeated explanations with links to the authoritative owner;
  summaries must not carry a second list of current state or open work.
- [x] Before completing the phase, scan tracked files for identical content and
  divergent facts. Classify ignored files separately and distinguish required
  boilerplate from abandoned copies.

### C9 — Automate documentation verification

- [ ] Add an `eng/` check for Markdown links, absent paths, references to
  ignored files, and required classification/audit metadata.
- [ ] When practical, compare the documented project map with
  `dotnet sln NekoLib.sln list` and targets/references from `*.csproj`.
- [ ] Fail when a current document cites an absent path. Historical audits may
  cite removed paths only when their lifecycle and reference baseline are
  explicit.
- [ ] Compare warnings by normalized identity rather than count so the baseline
  cannot gain new warning identities silently.
- [ ] When packaging files or docs change, use a fresh disposable local package
  version and never overwrite an existing feed version.
- [ ] Run the final validation:

```powershell
.\eng\verify-docs.ps1
dotnet sln NekoLib.sln list
dotnet build NekoLib.sln -t:Rebuild
dotnet test NekoLib.sln
git diff --check
```

- [ ] Record final validation in a dated commit-bound snapshot. Do not copy its
  counts into several maintained documents.

### Phase C completion criteria

- [ ] A clean clone can find current reference material, validation entry
  points, and historical artifacts from `README.md` and `docs/README.md`.
- [ ] No open item has two authoritative work lists.
- [ ] No documented scenario or tool depends on invisible local files without a
  reproducible procedure.
- [ ] No current document depends on an ignored or absent path without declaring
  it as a local prerequisite.
- [ ] Build and tests pass for both target families without a new warning
  identity.

## Active architecture reviews

- [ ] Complete the remaining Diagnostics-sector boundary and naming decisions.
  - Review: [`docs/audit/diagnostics-boundaries-review-2026-07-30.md`](docs/audit/diagnostics-boundaries-review-2026-07-30.md)
  - Baseline: `1727a1cac3f66666b2df02bc618ad6ab45807a49`.
  - Phase D implemented DGN-01, CORE-01, BND-01, LOG-01, CORE-02,
    TEST-01, and the accepted DBG-01 rename.
  - Remaining review-only decisions: CRASH-01, CRASH-02, and WIN-01.
