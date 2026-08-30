# NekoLib.Data Validation Evidence

**Document ID:** DATA-VALIDATIONS

**Schema version:** 1

**Kind:** reference

**Lifecycle:** current

**Subject:** executed evidence for the NekoLib.Data boundary

**Surface:** validation-evidence

**Boundary:** data

**Authority role:** evidence

**Mutation:** authored

**Indexing:** include

These records curate preserved evidence; they do not re-run it. The source
audit, scenario, package, or release record remains the detailed owner of each
result. The current documentation campaign will add its own command results only
after the separately authorized validation stage executes them.

## DATA-VALEVID-20260802-001

**Requirement IDs:** `DATA-VALREQ-001`, `DATA-VALREQ-003`, `DATA-VALREQ-004`, `DATA-VALREQ-005`, `DATA-VALREQ-006`, `DATA-VALREQ-008`, `DATA-VALREQ-011`

**Version:** pre-1.0.0

**Commit:** inclusive implementation range `b6a49d6` through `adf9ade`

**Tree state:** committed implementation with reconciliation in the dated stabilization audit

**Environment:** Windows dual-target build and deterministic fake-provider test environment

**Targets:** `NekoLib.Data` and focused tests on `net481` and `net9.0`; complete solution

**Command or scenario:** dual-target Data builds; complete Data suite; `dotnet test NekoLib.sln --no-restore --nologo`; full solution rebuild with documentation verification

**Execution:** automated

**Evidence level:** automated-runtime

**Result:** PASS

**Artifacts:** `docs/modules/Data/audits/stabilization-review-2026-08-01.md`

**Gaps:** Data passed 108 tests on `net481` and 117 on `net9.0`, with warning-free module builds; real SQLite, Access/OleDb, and server-provider execution was explicitly deferred and is not claimed by this record.

**Supersedes:** the initial audit's pre-stabilization build and test observations

## DATA-VALEVID-20260808-001

**Requirement IDs:** `DATA-VALREQ-004`, `DATA-VALREQ-005`, `DATA-VALREQ-009`

**Version:** pre-1.0.0

**Commit:** scenario source `4186e48`; Data fix `865d90f`

**Tree state:** committed-source revalidation

**Environment:** Windows x64 with restored SQLite packages and the x64 ACE OleDb provider

**Targets:** FarmDatabase `--builder` on `net481` and `net9.0-windows` against SQLite and Access

**Command or scenario:** versioned FarmDatabase builder procedure

**Execution:** automated

**Evidence level:** automated-runtime

**Result:** PASS

**Artifacts:** `runtime_tests/Data/FarmDatabase/README.md`

**Gaps:** This was the non-interactive builder/provider matrix, not the full WinForms walkthrough, package-consumer validation, or SQL Server evidence.

**Supersedes:** earlier partial provider walkthroughs for the corrected builder behavior

## DATA-VALEVID-20260808-002

**Requirement IDs:** `DATA-VALREQ-005`, `DATA-VALREQ-006`, `DATA-VALREQ-008`, `DATA-VALREQ-010`

**Version:** pre-1.0.0

**Commit:** recorded by the E4-SQL scenario artifacts and Phase E completion record

**Tree state:** committed scenario source; each run recorded its environment and cleanup result

**Environment:** Windows x64, SQL Server 16.0.4265.3 Developer Edition in the adopted pinned container, `Microsoft.Data.SqlClient` 6.1.6

**Targets:** smoke on `net481` and `net9.0`; recovery rehearsal on `net9.0` plus a short-window `net481` data point

**Command or scenario:** versioned SQL Server smoke and recovery-rehearsal procedures

**Execution:** automated

**Evidence level:** automated-runtime

**Result:** PARTIAL

**Artifacts:** `runtime_tests/Data/SqlServer/README.md`, `docs/history/phase-e-confidence-stabilization-2026-08-12.md`

**Gaps:** Both smoke targets passed; the qualifying 82-minute rehearsal covered all seven faults only on `net9.0`. The `net481` recovery run was below the required duration and intentionally omitted the streaming fault because that API is absent. Host-port exposure and credential prerequisites remain consumer/environment concerns recorded by the scenario.

**Supersedes:** the ten-minute `net9.0` rehearsal as qualifying duration evidence; that short run remains a useful fault-handler data point

## DATA-VALEVID-20260817-001

**Requirement IDs:** `DATA-VALREQ-001`, `DATA-VALREQ-002`, `DATA-VALREQ-003`, `DATA-VALREQ-011`, `DATA-VALREQ-012`, `DATA-VALREQ-013`

**Version:** `1.0.0-local.14`

**Commit:** `3e58df2` after accepted implementation `59d1faf` and package refinement `bced326`

**Tree state:** committed package candidate source

**Environment:** Windows canonical local package flow with isolated PackageReference consumers

**Targets:** Data on both targets within the coordinated solution and package family

**Command or scenario:** `eng/pack-local.ps1 -PackageVersion 1.0.0-local.14`

**Execution:** automated

**Evidence level:** automated-runtime

**Result:** PASS

**Artifacts:** `docs/modules/Data/audits/public-api-review-2026-08-17.md`, `docs/modules/Data/migrations/f1.md`, both accepted Data API manifests

**Gaps:** Data passed 111 `net481` and 119 `net9.0` tests and the solution passed 1,280 tests; provider scenarios were build-validated but were not executed for this API-shape block. XML package delivery was not yet a release requirement.

**Supersedes:** the pre-F1 candidate public surface

## DATA-VALEVID-20260821-001

**Requirement IDs:** `DATA-VALREQ-001`, `DATA-VALREQ-002`, `DATA-VALREQ-003`, `DATA-VALREQ-011`, `DATA-VALREQ-012`, `DATA-VALREQ-013`

**Version:** `1.0.0-local.22` qualifying candidate; materialized stable `1.0.0`

**Commit:** clean qualifying source `7090e40eed7c6b888ce8da732f21cbe10f1a936c`; materialized release source `db63529cafce11690a18a595e4abc6c0610b9b8e`

**Tree state:** clean qualifying and materialized package source

**Environment:** Windows canonical local package flow plus later approved public release transport

**Targets:** Data on both targets within the complete coordinated package family

**Command or scenario:** `eng/pack-local.ps1 -PackageVersion 1.0.0-local.22`, stable materialization, and recorded public-feed verification

**Execution:** automated

**Evidence level:** automated-runtime

**Result:** PASS

**Artifacts:** `docs/stable-release-1.0.0.md`, `NekoLib.Data.1.0.0-local.22.nupkg` SHA-256 `56E66DAC1026C97E610929C0B07EAC8B6668DBE32DCDAA1E31184352A4BD4DCF`, both accepted Data API manifests

**Gaps:** The qualifying flow passed 1,670 solution tests, APIs, packages, and PackageReference consumers but did not re-run Data provider scenarios. The stable package predates the later package XML-content requirement.

**Supersedes:** pre-stable package candidates as the supported 1.0.0 baseline

## DATA-VALEVID-20260827-001

**Requirement IDs:** `DATA-VALREQ-001`, `DATA-VALREQ-002`, `DATA-VALREQ-003`, `DATA-VALREQ-004`, `DATA-VALREQ-006`, `DATA-VALREQ-007`, `DATA-VALREQ-009`, `DATA-VALREQ-010`

**Version:** post-1.0.0 additive source; disposable package candidates through `1.1.0-local.4`

**Commit:** review baseline `fc10319a439edc4943a1226fc66d0cf4ee2d2e2a`; final validation tree was intentionally dirty and its exact commit was not recorded

**Tree state:** authorized implementation working tree with preserved validation reconciliation; disposable packages used `-AllowDirty`

**Environment:** Windows dual-target focused tests; SQLite and x64 ACE through FarmDatabase; SQL Server 16.0.4265.3 and `Microsoft.Data.SqlClient` 6.1.6 through the adopted container

**Targets:** Data `net481` and `net9.0`; FarmDatabase both targets/providers; SQL Server smoke both targets

**Command or scenario:** complete Data suite, scoped API verification, FarmDatabase `--builder`, SQL Server `--smoke`, and disposable local package flow

**Execution:** automated

**Evidence level:** automated-runtime

**Result:** PASS

**Artifacts:** `docs/modules/Data/audits/type-adaptation-querybuilder-api-review-2026-08-26.md`, `runtime_tests/Data/FarmDatabase/README.md`, `runtime_tests/Data/SqlServer/README.md`

**Gaps:** The final suite passed 177 tests on `net481` and 186 on `net9.0`; FarmDatabase passed all four target/provider runs; SQL Server passed 31/31 on `net9.0` and 30/30 on `net481` with the expected streaming absence. The package candidates are implementation evidence, not clean release provenance, and no new long-running recovery rehearsal ran for this slice.

**Supersedes:** the earlier QueryBuilder-only and write-adaptation-only validation slices in the same audit

## DATA-VALEVID-20260828-001

**Requirement IDs:** `DATA-VALREQ-001`, `DATA-VALREQ-002`, `DATA-VALREQ-003`, `DATA-VALREQ-014`

**Version:** post-1.0.0 documentation source, before package closure

**Commit:** `8eea9440391c96a4537edb569d46ef350dc83c37`

**Tree state:** committed XML-documentation source

**Environment:** Windows documentation-enabled Release builds and focused tests

**Targets:** Data on `net481` and `net9.0`

**Command or scenario:** documentation-enabled Release builds, both scoped public-API comparisons, and the focused Data suite with `-m:1`

**Execution:** automated

**Evidence level:** automated-runtime

**Result:** PASS

**Artifacts:** `docs/audit/public-api-documentation-extensibility-review-2026-08-27.md`, generated target XML files, both accepted Data manifests

**Gaps:** The builds reported zero XML or other diagnostics, APIs matched, and tests passed 177 on `net481` and 186 on `net9.0`; no package, PackageReference consumer, or provider scenario ran in this record.

**Supersedes:** the Data `CS1591` planning baseline and targeted-extension-only documentation pass

## DATA-VALEVID-20260828-002

**Requirement IDs:** `DATA-VALREQ-011`, `DATA-VALREQ-012`, `DATA-VALREQ-013`, `DATA-VALREQ-014`

**Version:** `1.1.0-local.8`

**Commit:** `d6f2efdbe99f4a827293cdf4e8ed27c4096d134a`

**Tree state:** clean

**Environment:** Windows canonical local package flow with isolated PackageReference consumers

**Targets:** `NekoLib.Data` and both package-owned target assets within the coordinated managed family

**Command or scenario:** `eng/pack-local.ps1 -PackageVersion 1.1.0-local.8`

**Execution:** automated

**Evidence level:** automated-runtime

**Result:** PASS

**Artifacts:** `docs/audit/public-api-documentation-extensibility-review-2026-08-27.md`; immutable local-feed artifacts with aggregate SHA-256 `b56451a1ee8eb7ef4de0d32de143f9488b09f00b25fc300572bcbeb2ee34e9f2`

**Gaps:** All 1,787 solution tests passed and every managed package delivered both XML target assets to isolated consumers. This was local package evidence, not a Data provider run, public stable release, or Git push.

**Supersedes:** `1.1.0-local.7`, retained as negative immutable evidence because the managed packages omitted XML files

## DATA-VALEVID-20260830-001

**Requirement IDs:** `DATA-VALREQ-001`, `DATA-VALREQ-002`, `DATA-VALREQ-003`, `DATA-VALREQ-004`, `DATA-VALREQ-005`, `DATA-VALREQ-006`, `DATA-VALREQ-007`, `DATA-VALREQ-008`, `DATA-VALREQ-014`

**Version:** uncommitted 2026-08-30 module-first documentation campaign on post-1.0.0 source

**Commit:** clean campaign baseline `7796c20b304df26791b2d472a094ee92dc465b58`

**Tree state:** authorized documentation working tree with eight staged relocations, seventeen modified tracked paths, and nine untracked campaign paths before and after validation

**Environment:** Windows NT 10.0.26200.0 x64; .NET SDK 10.0.400; PowerShell 7.6.4; x64 process

**Targets:** `NekoLib.Data` build and API on `net481` and `net9.0`; complete focused test project on both targets; repository documentation and skill topology

**Command or scenario:** `dotnet build src/Data/NekoLib.Data/NekoLib.Data.csproj -f net481`; `dotnet build src/Data/NekoLib.Data/NekoLib.Data.csproj -f net9.0`; `dotnet test tests/NekoLib.Data.Tests/Unit/NekoLib.Data.Tests.Unit.csproj`; `eng/verify-public-api.ps1 -PackageId NekoLib.Data`; `eng/verify-docs.ps1`; `eng/verify-skills.ps1`; `git diff --check`

**Execution:** automated

**Evidence level:** automated-runtime

**Result:** PASS

**Artifacts:** local Debug assemblies and XML files under `src/Data/NekoLib.Data/bin/Debug`; local Release assemblies and XML files under `src/Data/NekoLib.Data/bin/Release`; accepted manifests under `eng/public-api/NekoLib.Data`; this committed-candidate evidence record

**Gaps:** Both direct builds passed with 0 warnings and 0 errors; focused tests passed 177/177 on `net481` and 186/186 on `net9.0`, with no failures or skips; API verification rebuilt both Release targets with 0 warnings/errors and matched both manifests; documentation and skill verification passed; `git diff --check` passed with informational line-ending notices. No full-solution test/rebuild, package, PackageReference consumer, SQLite, Access/OleDb, SQL Server, Docker, interactive UI, performance, soak, recovery, publish, or push action ran. Documentation verification received no build log, so it did not compare the repository-wide warning baseline.

**Supersedes:** none

### Current campaign boundary

The 2026-08-30 `populate-module data` and `validate-module data` stages inspected
the versioned FarmDatabase and SQL Server procedures but did not launch SQLite,
Access, SQL Server, Docker, or an interactive WinForms scenario. The validation
stage executed only the commands recorded in `DATA-VALEVID-20260830-001`. It did
not use the tracked `Pods.db` or `PodsDB` fixtures, which remain non-coverage.
