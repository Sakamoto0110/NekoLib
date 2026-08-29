# NekoLib.Inspection Validation Evidence

**Document ID:** INSP-VALIDATION-EVIDENCE

**Schema version:** 1

**Kind:** reference

**Lifecycle:** current

**Subject:** executed validation evidence for the NekoLib.Inspection boundary

**Surface:** validation-evidence

**Boundary:** inspection

**Authority role:** evidence

**Mutation:** authored

**Indexing:** include

These records curate preserved evidence; they do not re-run it. The source
audit, scenario record, or package record remains the detailed owner.

The shared Observability scenario runs Logging, Telemetry, and Inspection in one
process. Only its Inspection checks are recorded here. Its Logging and Telemetry
checks belong to those boundaries and satisfy no Inspection requirement.

Requirements with no complete PASS, as of the current baseline:
`INSP-VALREQ-005` is failed intermittently by a confirmed defect, see
[`INSP-ISSUE-001`](ISSUES.md); `INSP-VALREQ-010` is covered only by three partial
runs; `INSP-VALREQ-013` has never verified the consumer-visible experimental
diagnostic through a repository probe; `INSP-VALREQ-015` is untriggered because
the freeze holds; and `INSP-VALREQ-016` has never been executed.

## INSP-VALEVID-20260809-001

**Requirement IDs:** `INSP-VALREQ-010`

**Version:** pre-1.0.0 scenario source delivered 2026-08-09

**Commit:** not recorded in the scenario verification table; each run stamps its own commit and dirty flag into `environment.json`

**Tree state:** committed scenario source

**Environment:** Windows console host; no container, service, credential, or hardware

**Targets:** `net481` and `net9.0`

**Command or scenario:** `NekoLib.Observability.RuntimeTests.LongRunningRecovery.exe --smoke` over the specified 15-minute window on each target, Inspection phase and checks only

**Execution:** automated

**Evidence level:** automated-runtime

**Result:** PARTIAL

**Artifacts:** `runtime_tests/Observability/LongRunningRecovery/README.md` verification record; 4951 checks on `net9.0` and 4591 on `net481` with 0 failed and 0 skipped across 164 and 152 full-matrix cycles; every bounded structure ended exactly at capacity; cleanup verified that the process-wide slot was back to the null recorder

**Gaps:** This mode injects no faults, so it covers the sustained-recording and retention half of `INSP-VALREQ-010` and none of its provider-failure or teardown half. The scenario deliberately references no action API, so nothing here is evidence about the experimental surface. Resource movement was asserted against a bounded allowance for threads and handles and on shape only for memory.

**Supersedes:** none

## INSP-VALEVID-20260809-002

**Requirement IDs:** `INSP-VALREQ-010`

**Version:** pre-1.0.0 scenario source

**Commit:** not recorded in the scenario verification table

**Tree state:** committed scenario source

**Environment:** Windows console host with the scenario's own seeded fault schedule

**Targets:** `net9.0`

**Command or scenario:** `--soak 15m`, in which the seeded fault schedule and the capability cycles run together through the scenario's exclusivity gate

**Execution:** automated

**Evidence level:** automated-runtime

**Result:** PARTIAL

**Artifacts:** `runtime_tests/Observability/LongRunningRecovery/README.md` verification record; exit 0, 3848 checks, 0 failed, 0 skipped, 128 cycles, all seven fault kinds fired while the cycles were running and all seven passed; schedule `fnv1a64:173b5243f59382ef`; cleanup restored the null Inspection recorder and left no process

**Gaps:** This is the only recorded run in which the Inspection faults and Inspection workload overlapped, which is what `INSP-VALREQ-010` asks for — but it ran on one target for 15 minutes. It is not long-duration soak evidence and no equivalent `net481` overlap run exists. Four of the seven fault kinds belong to Logging and Telemetry and are not Inspection evidence.

**Supersedes:** none

## INSP-VALEVID-20260810-001

**Requirement IDs:** `INSP-VALREQ-010`

**Version:** pre-1.0.0 scenario source

**Commit:** not recorded in the scenario verification table

**Tree state:** committed scenario source

**Environment:** Windows console host with the scenario's own seeded fault schedule

**Targets:** `net481` and `net9.0`

**Command or scenario:** `--recovery-rehearsal --rehearsal-duration 70m` on each target; the Inspection fault kinds `inspection-provider-throws`, `inspection-provider-times-out`, and `inspection-global-teardown` with their documented terminals and post-recovery probes

**Execution:** automated

**Evidence level:** automated-runtime

**Result:** PARTIAL

**Artifacts:** `runtime_tests/Observability/LongRunningRecovery/README.md` verification record; both targets elapsed 62.3 minutes inside the specified 60-to-90-minute window with 68 checks, 0 failed and 0 skipped; the failing provider carried a thrown marker while healthy providers stayed in the same snapshot, the slow provider carried a timed-out marker with the capture returning inside its budget, disposal restored the process-wide slot to the null recorder and a fresh `EnableGlobal` succeeded, and provider and registration counts returned to baseline

**Gaps:** Covers the provider-failure and teardown half of `INSP-VALREQ-010` and none of its sustained-retention half, because this mode runs no cycle loop and takes no periodic samples. The timeout fault uses a provider that eventually returns, so `INSP-VALREQ-016` is untouched. Every injected failure is scenario-owned; no fault-injection surface exists in `NekoLib.Inspection`. Running with `--no-global-inspection` drops the teardown kind and changes the schedule hash, so a six-of-seven run is a different plan and is not this evidence.

**Supersedes:** the 2026-08-09 `net9.0` rehearsal that elapsed 52.9 minutes, below the specified lower bound

## INSP-VALEVID-20260817-001

**Requirement IDs:** `INSP-VALREQ-001`, `INSP-VALREQ-002`, `INSP-VALREQ-003`, `INSP-VALREQ-004`, `INSP-VALREQ-005`, `INSP-VALREQ-006`, `INSP-VALREQ-007`, `INSP-VALREQ-008`, `INSP-VALREQ-009`, `INSP-VALREQ-011`

**Version:** pre-1.0.0

**Commit:** reviewed at `7c4d449ec3a6854b0561c8514701a1ec31fe3c35`; accepted implementation at `9f878dfd78d997732a010c2d4996396cb0d567fa`

**Tree state:** clean tracked worktree at the recorded implementation commit

**Environment:** Windows focused build and test environment, Release configuration

**Targets:** library `net481`/`net9.0`; focused tests `net481`/`net9.0`

**Command or scenario:** project builds, the focused Inspection suite, the scoped baseline update, and the scoped public-API verification recorded in the F1-INSP audit and its package reconciliation

**Execution:** automated

**Evidence level:** automated-runtime

**Result:** PASS

**Artifacts:** `docs/modules/Inspection/audits/public-api-review-2026-08-17.md`; 40/40 focused tests on each target, up from 13; the scoped baseline update produced exactly the four accepted `ObsoleteAttribute` additions per target manifest and no other delta; both targets built with zero warnings and zero errors; scoped API verification passed 2/2; full Release solution 1,438/1,438 tests with zero failures and zero skips

**Gaps:** These are build, test, contract, and documentation claims only; no runtime scenario was executed in this campaign and the Observability scenario was built but never launched. The package gate was still open at this point. Nothing here exercised a permanently blocked provider or the consumer-visible experimental diagnostic through a package boundary. One further limit is visible only in hindsight: a single passing suite run does not establish determinism, and the budget-skip case recorded here as satisfying `INSP-VALREQ-005` was later shown to fail intermittently. See [`INSP-ISSUE-001`](ISSUES.md).

**Supersedes:** the proposal-only portions of the F1-INSP audit snapshot

## INSP-VALEVID-20260817-002

**Requirement IDs:** `INSP-VALREQ-012`, `INSP-VALREQ-017`

**Version:** `1.0.0-local.19`

**Commit:** `9f878dfd78d997732a010c2d4996396cb0d567fa`

**Tree state:** clean

**Environment:** Windows canonical local package flow with isolated package-reference probes

**Targets:** the `NekoLib.Inspection` package within the coordinated family, both target assets

**Command or scenario:** `eng\pack-local.ps1 -PackageVersion 1.0.0-local.19` from the clean tracked worktree, without `-SkipTests` or `-AllowDirty`

**Execution:** automated

**Evidence level:** automated-runtime

**Result:** PASS

**Artifacts:** immutable `NekoLib.Inspection.1.0.0-local.19.nupkg` containing `lib/net481` and `lib/net9.0` assemblies, both dependency groups declaring `NekoLib.Core` at the aligned version, recorded source-commit provenance, and SHA-256 `8B553A3B7DCC605CB6470E495EAF21E15BD646927C2EB9F5B8BE15E45750E7AF`; 16 coordinated packages published; 1,438/1,438 tests; WinForms loaded 19 package surfaces and WPF loaded 4

**Gaps:** The consumer probes restored, built, and ran, but none of them recorded an operation, registered a provider, captured a snapshot, or referenced a marked member through the package boundary, so `INSP-VALREQ-013` is only partly addressed and its experimental-diagnostic criterion is not met here. XML package content was not a gate at this version.

**Supersedes:** pre-package F1-INSP qualification

## INSP-VALEVID-20260821-001

**Requirement IDs:** `INSP-VALREQ-001`, `INSP-VALREQ-002`, `INSP-VALREQ-003`, `INSP-VALREQ-009`, `INSP-VALREQ-012`, `INSP-VALREQ-017`

**Version:** `1.0.0` qualified by `1.0.0-local.22`

**Commit:** `7090e40eed7c6b888ce8da732f21cbe10f1a936c`

**Tree state:** clean qualifying candidate and separately materialized stable packages

**Environment:** Windows canonical package flow and approved release-asset publication

**Targets:** the `NekoLib.Inspection` package and both target assets within the stable coordinated family

**Command or scenario:** `eng/pack-local.ps1 -PackageVersion 1.0.0-local.22` plus the recorded stable materialization and publication flow

**Execution:** automated

**Evidence level:** automated-runtime

**Result:** PASS

**Artifacts:** `docs/stable-release-1.0.0.md`, which records that `IInspectionRecorder.RegisterAction` remains explicitly experimental under `NEKOEXP0001` while the other accepted surfaces are stable; stable `NekoLib.Inspection.1.0.0.nupkg` SHA-256 `18664A0D3523CA2AB1DAEE5DE43F6F9E7EADC72B14E283BC564A33DB633CE9B3`; qualifying `1.0.0-local.22` SHA-256 `5D6D8667FCC292DC2F505FEB55BE15B5457204ECD069837EB8436348A651194C`

**Gaps:** The release campaign executed no long-running Inspection scenario, no blocked-provider characterization, and no XML package-content gate.

**Supersedes:** `1.0.0-local.19` as coordinated stable qualification, while preserving its Inspection-specific package evidence

## INSP-VALEVID-20260826-001

**Requirement IDs:** `INSP-VALREQ-013`

**Version:** published `1.0.0`

**Commit:** `0fa1a321c85c541cc3e32c39e5607de881032b5a` review baseline

**Tree state:** committed repository plus the external NekoMarketplace evidence corpus

**Environment:** an external consumer application building against the published package family

**Targets:** the consumer's own build

**Command or scenario:** external consumer build observing the compiler diagnostic on the experimental action members, recorded as `F-018` in the evidence intake

**Execution:** manual

**Evidence level:** interactive

**Result:** PARTIAL

**Artifacts:** `docs/audit/nekomarketplace-external-consumer-evidence-intake-2026-08-26.md`

**Gaps:** This establishes that the experimental status survives packaging and reaches a real consumer's build, which is the part of `INSP-VALREQ-013` the repository's own probes do not cover. It is external, single-consumer, and manually reported, and it does not constitute a repeatable isolated package-consumer probe.

**Supersedes:** none

## INSP-VALEVID-20260828-001

**Requirement IDs:** `INSP-VALREQ-001`, `INSP-VALREQ-002`, `INSP-VALREQ-012`, `INSP-VALREQ-014`, `INSP-VALREQ-017`

**Version:** `1.1.0-local.8`

**Commit:** `d6f2efdbe99f4a827293cdf4e8ed27c4096d134a`

**Tree state:** clean

**Environment:** Windows documentation-enabled Release builds and the canonical local package flow with isolated consumers

**Targets:** both `NekoLib.Inspection` target assemblies

**Command or scenario:** the family documentation campaign builds and `eng/pack-local.ps1 -PackageVersion 1.1.0-local.8`

**Execution:** automated

**Evidence level:** automated-runtime

**Result:** PASS

**Artifacts:** `docs/audit/public-api-documentation-extensibility-review-2026-08-27.md`; all 1,787 solution tests passed across their targets with zero failures and zero skips; every managed package carried both target assemblies and both matching XML files; immutable local-feed packages with aggregate SHA-256 `b56451a1ee8eb7ef4de0d32de143f9488b09f00b25fc300572bcbeb2ee34e9f2`

**Gaps:** This is a local candidate, not a stable release. The campaign's mid-flight scan recorded 27 residual `CS1591` diagnostics for this assembly; the closing state was not separately measured at that time, so this record proves package XML delivery rather than complete member coverage.

**Supersedes:** `1.1.0-local.7`, retained as negative immutable evidence because its managed packages omitted the XML files
## INSP-VALEVID-20260829-001

**Requirement IDs:** `INSP-VALREQ-001`, `INSP-VALREQ-002`, `INSP-VALREQ-009`, `INSP-VALREQ-014`

**Version:** unreleased documentation source

**Commit:** working tree based on `ac3da3dccb7be57230bae3de377109c617552af8`

**Tree state:** authorized module-first documentation working tree before its final commit, carrying two XML comment changes in `InspectionRuntime`

**Environment:** Windows local checkout, Release configuration

**Targets:** `net481` and `net9.0`

**Command or scenario:** two ordinary Release target builds; forced `-t:Rebuild` on each target with `CS1591` live, including `-p:NoWarn=1701;1702` on `net481` to unsuppress it; `eng/verify-public-api.ps1 -PackageId NekoLib.Inspection -NoBuild`; `eng/verify-docs.ps1`; `git diff --check`

**Execution:** automated

**Evidence level:** build-only

**Result:** PASS

**Artifacts:** 0 warnings and 0 errors on every build, including both forced rebuilds with `CS1591` unsuppressed, so the assembly currently carries no undocumented public member on either target; both accepted API manifests verified unchanged, confirming that the two comment changes altered no compiled surface and that the four experimental attributes, the friend declaration, and the per-target framework attribute are intact; documentation verification and diff check passed

**Gaps:** This record makes no test, package, consumer, or runtime claim. It measures member coverage in the working tree only and is not package-delivery evidence.

**Supersedes:** the mid-campaign residual-`CS1591` count recorded in `INSP-VALEVID-20260828-001`, which remains valid as package-delivery evidence

## INSP-VALEVID-20260829-002

**Requirement IDs:** `INSP-VALREQ-003`, `INSP-VALREQ-004`, `INSP-VALREQ-005`, `INSP-VALREQ-006`, `INSP-VALREQ-007`, `INSP-VALREQ-008`

**Version:** unreleased documentation source

**Commit:** working tree based on `ac3da3dccb7be57230bae3de377109c617552af8`

**Tree state:** authorized module-first documentation working tree before its final commit

**Environment:** Windows local checkout, Release configuration, tests run with `-m:1`

**Targets:** focused tests on `net481` and `net9.0`

**Command or scenario:** the focused Inspection suite repeated 35 times across both targets, including a TRX-logged loop to capture a failure by name

**Execution:** automated

**Evidence level:** automated-runtime

**Result:** PARTIAL

**Artifacts:** 32 of 35 runs passed 40/40 with zero skips. Three runs failed with one test each — two on `net481` and one on `net9.0`. One was captured by TRX and positively identified as `CaptureSnapshot_SharedBudgetExpires_SkipsLaterProviders` with `Expected: <snapshot timed out>, Actual: 2`; the other two showed the same 39-passed/1-failed signature but were not captured by name.

**Gaps:** The suite is not deterministic on this host, so it does not establish `INSP-VALREQ-005`, whose acceptance criteria include that the shared budget skips later providers once exhausted. That failure is a confirmed product defect recorded as [`INSP-ISSUE-001`](ISSUES.md), not a test-authoring error, and it was neither fixed nor promoted in this read-only documentation pass. The remaining requirements listed here were exercised by every run including the failing ones, since only one test failed in each. No package, consumer, runtime scenario, blocked-provider, or full-solution claim is made.

**Supersedes:** none
