# NekoLib.Mvvm Validation Evidence

**Document ID:** MVVM-VALIDATION-EVIDENCE

**Schema version:** 1

**Kind:** reference

**Lifecycle:** current

**Subject:** executed validation evidence for the NekoLib.Mvvm boundary

**Surface:** validation-evidence

**Boundary:** mvvm

**Authority role:** evidence

**Mutation:** authored

**Indexing:** include

These records curate evidence that actually ran. The source audit, package
record, or scenario record remains the detailed owner of each.

Evidence layers are kept separate and none substitutes for another: source
inspection, build, focused tests, compiled API verification, cross-boundary
consumer runs, interactive UI, package, release, and documentation validation.

Requirements with no complete PASS, as of the current baseline:
`MVVM-VALREQ-009` has never been executed, and `MVVM-VALREQ-010` has no evidence
owned by this boundary — the only interactive binding evidence is cross-boundary
and belongs to Data.

## MVVM-VALEVID-20260817-001

**Requirement IDs:** `MVVM-VALREQ-001`, `MVVM-VALREQ-003`, `MVVM-VALREQ-004`, `MVVM-VALREQ-005`, `MVVM-VALREQ-006`, `MVVM-VALREQ-007`, `MVVM-VALREQ-008`

**Version:** pre-1.0.0

**Commit:** reviewed at `c9c4321e9fe67c0aeadcb7afda36347368fce457`; accepted implementation at `00b0f11b4d5980c03d2c32af2df7760ed749bd9b`

**Tree state:** clean tracked worktree at the recorded implementation commit

**Environment:** Windows focused build and test environment

**Targets:** library `net481`/`net9.0`; focused tests `net481`/`net9.0-windows`

**Command or scenario:** dual-target `-t:Rebuild`, the focused suite, and the scoped public-API comparison recorded in the F1-MVVM audit and its reconciliation

**Execution:** automated

**Evidence level:** automated-runtime

**Result:** PASS

**Artifacts:** `docs/modules/Mvvm/audits/public-api-review-2026-08-17.md`; both targets rebuilt with 0 warnings and 0 errors, down from 20 nullable warnings; 34/34 focused tests on each test target; the API delta was exactly the accepted one — nullability annotations across all three types plus `virtual` on `OnPropertyChanged` — after which the baselines were updated and re-verified; twelve regressions added covering coercion rejection, `Execute` ignoring `CanExecute` on both command types, subscriber and delegate exception propagation, reentrancy, `NaN` equality, in-place mutation, the null property name, and the virtual funnel

**Gaps:** No WinForms or WPF binding pipeline was driven, so the threading guidance rests on documented framework behavior and source. Concurrent `Execute` from multiple threads was not measured. The FarmDatabase scenario was neither built nor run in this block. No full-solution build or test run was performed, so the effect of removing 20 warnings on the solution baseline was an expectation rather than a measurement. No package was produced and no consumer probe was run.

**Supersedes:** the proposal-only portions of the F1-MVVM audit snapshot

## MVVM-VALEVID-20260818-001

**Requirement IDs:** `MVVM-VALREQ-011`

**Version:** `1.0.0-local.20`

**Commit:** implementation `00b0f11b4d5980c03d2c32af2df7760ed749bd9b`; package source `63785cc8bb801f1d4a90ade6cffb7f0b42c6bc1b`

**Tree state:** clean

**Environment:** Windows canonical local package flow with isolated package-reference probes

**Targets:** the `NekoLib.Mvvm` package within the coordinated family, both target assets

**Command or scenario:** the coordinated clean `1.0.0-local.20` campaign recorded in the F1-MVVM package reconciliation

**Execution:** automated

**Evidence level:** automated-runtime

**Result:** PASS

**Artifacts:** 1,538/1,538 full-solution tests; rebuild with 464 warnings, zero errors, and no new warning identity; `NekoLib.Mvvm.1.0.0-local.20.nupkg` containing `lib/net481/NekoLib.Mvvm.dll` and `lib/net9.0/NekoLib.Mvvm.dll` with **empty dependency groups for both targets**, recording its source commit, SHA-256 `4A44B2EC7D519EB658619A37F82EB8463112D360C4BF21F2AFAD86BAB56CBBBE`; all PackageReference-only consumer, multitarget, package, deployment, publish, and clean probes passed

**Gaps:** The consumer probes restored, built, and ran, but none of them bound a view-model or executed a command through the package boundary. No WinForms or WPF binding pipeline and no interactive runtime scenario was driven. XML package content was not a gate at this version.

**Supersedes:** none

## MVVM-VALEVID-20260821-001

**Requirement IDs:** `MVVM-VALREQ-001`, `MVVM-VALREQ-003`, `MVVM-VALREQ-011`

**Version:** `1.0.0` qualified by `1.0.0-local.22`

**Commit:** `7090e40eed7c6b888ce8da732f21cbe10f1a936c`

**Tree state:** clean qualifying candidate and separately materialized stable packages

**Environment:** Windows canonical package flow and approved release-asset publication

**Targets:** the `NekoLib.Mvvm` package and both target assets within the stable coordinated family

**Command or scenario:** `eng/pack-local.ps1 -PackageVersion 1.0.0-local.22` plus the recorded stable materialization and publication flow

**Execution:** automated

**Evidence level:** automated-runtime

**Result:** PASS

**Artifacts:** `docs/stable-release-1.0.0.md`; stable `NekoLib.Mvvm.1.0.0.nupkg` SHA-256 `AD76F8F1A6F7C09B4F5947643FFC58F7D772EF453CD12C1E1430DF4F5C9B9496`; qualifying `1.0.0-local.22` SHA-256 `BBDD741CD2CF25892DA5A0F677E2E37AA5984317121D110BC05CA7F9E3F17D2D`

**Gaps:** The release campaign drove no binding pipeline, no interactive scenario, and applied no XML package-content gate.

**Supersedes:** `1.0.0-local.20` as coordinated stable qualification, while preserving its Mvvm-specific package evidence

## MVVM-VALEVID-20260828-001

**Requirement IDs:** `MVVM-VALREQ-001`, `MVVM-VALREQ-003`, `MVVM-VALREQ-011`, `MVVM-VALREQ-012`

**Version:** `1.1.0-local.8`

**Commit:** `d6f2efdbe99f4a827293cdf4e8ed27c4096d134a`

**Tree state:** clean

**Environment:** Windows documentation-enabled Release builds and the canonical local package flow with isolated consumers

**Targets:** both `NekoLib.Mvvm` target assemblies

**Command or scenario:** the family documentation campaign builds and `eng/pack-local.ps1 -PackageVersion 1.1.0-local.8`

**Execution:** automated

**Evidence level:** automated-runtime

**Result:** PASS

**Artifacts:** `docs/audit/public-api-documentation-extensibility-review-2026-08-27.md`; all 1,787 solution tests passed with zero failures and zero skips; every managed package carried both target assemblies and both matching XML files; immutable local-feed packages with aggregate SHA-256 `b56451a1ee8eb7ef4de0d32de143f9488b09f00b25fc300572bcbeb2ee34e9f2`

**Gaps:** This is a local candidate, not a stable release. The campaign's mid-flight scan recorded 8 residual `CS1591` diagnostics for this assembly; the closing state was not separately measured at that time, so this record proves package XML delivery rather than complete member coverage. It also predates the two XML corrections made on 2026-08-29.

**Supersedes:** `1.1.0-local.7`, retained as negative immutable evidence because its managed packages omitted the XML files

## MVVM-VALEVID-20260806-001

**Requirement IDs:** none

**Version:** scenario source at the dates recorded below

**Commit:** not recorded per run by the scenario verification table; the 2026-08-06 re-run is recorded against Navigation change `378663a`

**Tree state:** committed scenario source, owned by the Data boundary

**Environment:** Windows, Visual Studio WinForms designer and a real WinForms message loop, SQLite and Access providers

**Targets:** `net9.0-windows`

**Command or scenario:** the Data-owned FarmDatabase scenario, whose Core project references `NekoLib.Mvvm` and whose view-models derive from `ViewModelBase`

**Execution:** manual

**Evidence level:** interactive

**Result:** PARTIAL

**Artifacts:** `runtime_tests/Data/FarmDatabase/README.md` verification record, which states that this is the only place in the repository where `NekoLib.Mvvm` is exercised through a real binding surface; seven view-model types derive from `ViewModelBase` through the scenario's own `FarmViewModelBase`

**Gaps:** This scenario belongs to Data and exists to validate the Data gateway; it is cross-boundary consumer evidence for Mvvm and nothing more. Its recorded outcomes are about database, transaction, provider, and Navigation behavior — none of its documented checks asserts an Mvvm contract, so it demonstrates that binding works in a real WinForms application without establishing any specific notification, coercion, or exception claim. It does not satisfy `MVVM-VALREQ-010`, which requires evidence owned by and asserted for this boundary. Last driven 2026-08-06; `net481` for that scenario is build-only.

**Supersedes:** none

## MVVM-VALEVID-20260829-001

**Requirement IDs:** `MVVM-VALREQ-001`, `MVVM-VALREQ-002`, `MVVM-VALREQ-003`, `MVVM-VALREQ-004`, `MVVM-VALREQ-005`, `MVVM-VALREQ-006`, `MVVM-VALREQ-007`, `MVVM-VALREQ-008`, `MVVM-VALREQ-012`

**Version:** unreleased documentation source

**Commit:** working tree based on `418c6a4cab8f20c10fa8ff7df6589f74bd8af5ea`

**Tree state:** authorized module-first documentation working tree before its final commit, carrying two XML comment changes

**Environment:** Windows local checkout; Debug for the mandated builds and Release for the rebuild, API verification, and re-run suite

**Targets:** library `net481`/`net9.0`; focused tests `net481`/`net9.0-windows`

**Command or scenario:** `dotnet build … -f net481` and `-f net9.0`; forced Release `-t:Rebuild` on each target after the comment changes, including `-p:NoWarn=1701;1702` on `net481` to unsuppress `CS1591`; `dotnet test …` in Debug and again in Release; `eng/verify-public-api.ps1 -PackageId NekoLib.Mvvm -NoBuild`; `eng/verify-docs.ps1`; `git diff --check`

**Execution:** automated

**Evidence level:** automated-runtime

**Result:** PASS

**Artifacts:** 0 warnings and 0 errors on every build, including the `net481` rebuild with `CS1591` unsuppressed, so neither target carries an undocumented public member; both generated `NekoLib.Mvvm.xml` assets byte-identical across targets at 11,231 bytes; 34/34 tests passed with 0 failed and 0 skipped on each test target in both configurations; both accepted API manifests verified unchanged, confirming the comment changes altered no compiled surface; documentation verification and diff check passed

**Gaps:** Source inspection covered both source files, both manifests, all 34 tests, the F1 audit, the migration guide, and the release records. No package candidate, PackageReference consumer, interactive binding pipeline, concurrency characterization, or full-solution regression was run, so `MVVM-VALREQ-009`, `MVVM-VALREQ-010`, and `MVVM-VALREQ-011` are untouched by this pass. Per `MVVM-VALREQ-002`, the `net9.0` suite ran on `net9.0-windows`; the shipped `net9.0` assembly was built but not exercised on its own target framework. Member coverage was measured in the working tree and is not package-delivery evidence.

**Supersedes:** the mid-campaign residual-`CS1591` count recorded in `MVVM-VALEVID-20260828-001`, which remains valid as package-delivery evidence
