# NekoLib.Navigation Validation Evidence

**Document ID:** NAV-VALIDATION-EVIDENCE

**Schema version:** 1

**Kind:** reference

**Lifecycle:** current

**Subject:** executed validation evidence for the NekoLib.Navigation family boundary

**Surface:** validation-evidence

**Boundary:** navigation

**Authority role:** evidence

**Mutation:** authored

**Indexing:** include

These records curate preserved evidence; they do not re-run it. The source
audit, scenario, and release record remains the detailed owner of each result.

## NAV-VALEVID-20260804-001

**Requirement IDs:** `NAV-VALREQ-004`

**Version:** pre-1.0.0

**Commit:** `7e26b878aec560081a1133ba875a609a0955703c`

**Tree state:** clean committed source for the final recorded passes

**Environment:** Windows interactive desktop with the native WinForms and WPF scenario applications

**Targets:** WinForms `net481` and `net9.0-windows`; WPF `net9.0-windows`

**Command or scenario:** Human-driven procedures in `runtime_tests/Navigation/WinFormsSmoke` and `runtime_tests/Navigation/WpfSmoke`

**Execution:** manual

**Evidence level:** interactive

**Result:** PARTIAL

**Artifacts:** `runtime_tests/Navigation/WinFormsSmoke/README.md`, `runtime_tests/Navigation/WpfSmoke/README.md`, `docs/modules/Navigation/audits/adapter-review-2026-08-03.md`

**Gaps:** No WPF `net481` interactive run; WinForms step 6 and WPF step 4 were not performable as written, and the WPF handled-child toast case remained framework-semantic rather than directly observed.

**Supersedes:** none

## NAV-VALEVID-20260806-001

**Requirement IDs:** `NAV-VALREQ-006`

**Version:** pre-1.0.0

**Commit:** `73ddbdbe50fa134ecb3793b7219c8e21672ed228`

**Tree state:** committed implementation with later documentation reconciliation at `5418cb27f8da669a060ac382fa277c59d2322769`

**Environment:** Visual Studio 2026 WinForms designer on Windows plus the dual-target Navigation test project

**Targets:** WinForms designer consumer; automated `net481` and `net9.0-windows`

**Command or scenario:** Opened the FarmDatabase `ReasonPrompt` and `ConnectionPage` in the designer; ran `dotnet test tests/NekoLib.Navigation.Tests/Unit/NekoLib.Navigation.Tests.Unit.csproj`

**Execution:** manual

**Evidence level:** interactive

**Result:** PARTIAL

**Artifacts:** `docs/modules/Navigation/audits/design-time-2026-08-06.md`, `tests/NekoLib.Navigation.Tests/Unit/SurfaceBaseDesignTimeTests.cs`, `runtime_tests/Data/FarmDatabase/README.md`

**Gaps:** The real designer observation covered WinForms; WPF loadability was protected by source shape and automated tests, not a recorded interactive WPF designer pass. Generic WinForms prompts still require a non-generic consumer shim.

**Supersedes:** none

## NAV-VALEVID-20260811-001

**Requirement IDs:** `NAV-VALREQ-005`

**Version:** pre-1.0.0

**Commit:** `897e17b5e9000af4e90a1cc3783e271216cc9b9f`, `9f8f7813dde52069b3c21d947058c95a35d934e1`

**Tree state:** clean for both qualifying records

**Environment:** Windows interactive desktop; unattended native scenario with deterministic seed `20260810`

**Targets:** 20-minute smoke on WinForms `net481`, WinForms `net9.0-windows`, and WPF `net9.0-windows`; 70-minute fourteen-fault recovery rehearsal on WinForms `net481`

**Command or scenario:** E3-NAV `--smoke` matrix and WinForms `net481 --recovery-rehearsal --rehearsal-duration 70m`

**Execution:** automated

**Evidence level:** automated-runtime

**Result:** PARTIAL

**Artifacts:** `runtime_tests/Navigation/LongRunningRecovery/README.md`, `artifacts/validation/phase-e/` as recorded by the scenario

**Gaps:** No WPF `net481` scenario; the complete recovery fault matrix ran only on WinForms `net481`; WPF private-byte movement remains an unclassified short-window observation; pixel, focus, accessibility, DPI, first-paint, package, crash, and physical-input behavior were outside this evidence.

**Supersedes:** earlier two-minute development probes and build-only schedule previews recorded in the scenario README

## NAV-VALEVID-20260821-001

**Requirement IDs:** `NAV-VALREQ-001`, `NAV-VALREQ-002`, `NAV-VALREQ-003`

**Version:** `1.0.0-local.22`

**Commit:** `7090e40eed7c6b888ce8da732f21cbe10f1a936c`

**Tree state:** clean

**Environment:** Windows canonical local package flow

**Targets:** all six Navigation target assemblies within the full coordinated solution and package family

**Command or scenario:** `eng/pack-local.ps1 -PackageVersion 1.0.0-local.22`

**Execution:** automated

**Evidence level:** automated-runtime

**Result:** PASS

**Artifacts:** `docs/stable-release-1.0.0.md`, six accepted manifests under `eng/public-api/NekoLib.Navigation*`

**Gaps:** The canonical package flow proved build, tests, APIs, packages, consumers, and Host probes; it did not execute the Navigation interactive or long-running native scenarios. The 1.0.0 packages predated the later XML-package-content requirement.

**Supersedes:** pre-stable F1 validation recorded in the three public-API audits

## NAV-VALEVID-20260828-001

**Requirement IDs:** `NAV-VALREQ-001`, `NAV-VALREQ-002`, `NAV-VALREQ-003`, `NAV-VALREQ-009`

**Version:** post-1.0.0 documentation source, before package closure

**Commit:** `8eea9440391c96a4537edb569d46ef350dc83c37`

**Tree state:** committed documentation-only source

**Environment:** Windows Release documentation-enabled builds and focused tests

**Targets:** all six Navigation target assemblies; tests on `net481` and `net9.0-windows`

**Command or scenario:** Documentation-enabled Release builds, six scoped public-API comparisons, and the focused Navigation suite with `-m:1`

**Execution:** automated

**Evidence level:** automated-runtime

**Result:** PASS

**Artifacts:** `docs/audit/public-api-documentation-extensibility-review-2026-08-27.md`, generated XML build outputs, six accepted manifests

**Gaps:** 292 tests passed on each test target and XML diagnostics were zero, but no package, PackageReference consumer, interactive UI, or runtime soak ran in this record.

**Supersedes:** the Navigation `CS1591` planning baseline and targeted-extension-only documentation pass recorded earlier in the audit

## NAV-VALEVID-20260828-002

**Requirement IDs:** `NAV-VALREQ-007`

**Version:** `1.1.0-local.8`

**Commit:** `d6f2efdbe99f4a827293cdf4e8ed27c4096d134a`

**Tree state:** clean

**Environment:** Windows canonical local package flow with isolated PackageReference consumers

**Targets:** all three Navigation packages and both package-owned target assets per package within the coordinated managed family

**Command or scenario:** `eng/pack-local.ps1 -PackageVersion 1.1.0-local.8`

**Execution:** automated

**Evidence level:** build-only

**Result:** PASS

**Artifacts:** `docs/audit/public-api-documentation-extensibility-review-2026-08-27.md`; immutable local-feed packages with aggregate SHA-256 `b56451a1ee8eb7ef4de0d32de143f9488b09f00b25fc300572bcbeb2ee34e9f2`

**Gaps:** This was immutable local package and consumer evidence, not a public stable release, native interactive run, or long-running recovery run.

**Supersedes:** `1.1.0-local.7`, which is retained as negative immutable evidence because its managed packages omitted XML files
