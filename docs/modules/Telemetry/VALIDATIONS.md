# NekoLib.Telemetry Validation Evidence

**Document ID:** TEL-VALIDATION-EVIDENCE

**Schema version:** 1

**Kind:** reference

**Lifecycle:** current

**Subject:** executed validation evidence for the NekoLib.Telemetry boundary

**Surface:** validation-evidence

**Boundary:** telemetry

**Authority role:** evidence

**Mutation:** authored

**Indexing:** include

These records curate preserved evidence; they do not re-run it. The source
audit, scenario record, or package record remains the detailed owner.

The shared Observability scenario runs Logging, Telemetry, and Inspection in one
process. Only its Telemetry checks are recorded here. Its Logging and Inspection
checks belong to those boundaries and satisfy no Telemetry requirement.

Requirements with no complete PASS, as of the current baseline:
`TEL-VALREQ-008` is covered only by three partial runs, `TEL-VALREQ-013` has
never been executed, and `TEL-VALREQ-014` has never been executed.

## TEL-VALEVID-20260809-001

**Requirement IDs:** `TEL-VALREQ-008`

**Version:** pre-1.0.0 scenario source delivered 2026-08-09

**Commit:** not recorded in the scenario verification table; each run stamps its own commit and dirty flag into `environment.json`

**Tree state:** committed scenario source

**Environment:** Windows console host; no container, service, credential, or hardware

**Targets:** `net481` and `net9.0`

**Command or scenario:** `NekoLib.Observability.RuntimeTests.LongRunningRecovery.exe --smoke` over the specified 15-minute window on each target, Telemetry phase and checks only

**Execution:** automated

**Evidence level:** automated-runtime

**Result:** PARTIAL

**Artifacts:** `runtime_tests/Observability/LongRunningRecovery/README.md` verification record; 4951 checks on `net9.0` and 4591 on `net481` with 0 failed and 0 skipped across 164 and 152 full-matrix cycles; every bounded structure ended exactly at capacity; the `abandoned-scope` check confirmed that an operation started and never completed produced no sink write, no error, and nothing in the snapshot, including after a forced collection

**Gaps:** This mode injects no faults, so it covers the sustained-retention and abandonment half of `TEL-VALREQ-008` and none of its sink-failure half. Resource movement was asserted against a bounded allowance for threads and handles and on shape only for memory. No checkpoint-growth measurement was taken, so `TEL-VALREQ-013` is untouched.

**Supersedes:** none

## TEL-VALEVID-20260809-002

**Requirement IDs:** `TEL-VALREQ-008`

**Version:** pre-1.0.0 scenario source

**Commit:** not recorded in the scenario verification table

**Tree state:** committed scenario source

**Environment:** Windows console host with the scenario's own seeded fault schedule

**Targets:** `net9.0`

**Command or scenario:** `--soak 15m`, in which the seeded fault schedule and the capability cycles run together through the scenario's exclusivity gate

**Execution:** automated

**Evidence level:** automated-runtime

**Result:** PARTIAL

**Artifacts:** `runtime_tests/Observability/LongRunningRecovery/README.md` verification record; exit 0, 3848 checks, 0 failed, 0 skipped, 128 cycles, all seven fault kinds fired while the cycles were running and all seven passed; schedule `fnv1a64:173b5243f59382ef`

**Gaps:** This is the only recorded run in which the Telemetry fault and Telemetry workload overlapped, which is what `TEL-VALREQ-008` asks for — but it ran on one target for 15 minutes. It is not long-duration soak evidence and no equivalent `net481` overlap run exists. Six of the seven fault kinds belong to Logging and Inspection and are not Telemetry evidence.

**Supersedes:** none

## TEL-VALEVID-20260810-001

**Requirement IDs:** `TEL-VALREQ-008`

**Version:** pre-1.0.0 scenario source

**Commit:** not recorded in the scenario verification table

**Tree state:** committed scenario source

**Environment:** Windows console host with the scenario's own seeded fault schedule

**Targets:** `net481` and `net9.0`

**Command or scenario:** `--recovery-rehearsal --rehearsal-duration 70m` on each target; the Telemetry fault kind `telemetry-sink-throws` with its documented terminal and post-recovery probe

**Execution:** automated

**Evidence level:** automated-runtime

**Result:** PARTIAL

**Artifacts:** `runtime_tests/Observability/LongRunningRecovery/README.md` verification record; both targets elapsed 62.3 minutes inside the specified 60-to-90-minute window with 68 checks, 0 failed and 0 skipped; the pipeline kept recording with bounded retention unaffected and the next operation recorded normally; the two runs produced the same schedule hash and byte-identical workload counters

**Gaps:** Covers the sink-failure and recovery half of `TEL-VALREQ-008` and none of its sustained-retention half, because this mode runs no cycle loop and takes no periodic samples. Exactly one of the seven fault kinds is Telemetry's. Every injected failure is scenario-owned; no fault-injection surface exists in `NekoLib.Telemetry`.

**Supersedes:** the 2026-08-09 `net9.0` rehearsal that elapsed 52.9 minutes, below the specified lower bound

## TEL-VALEVID-20260817-001

**Requirement IDs:** `TEL-VALREQ-001`, `TEL-VALREQ-002`, `TEL-VALREQ-003`, `TEL-VALREQ-004`, `TEL-VALREQ-005`, `TEL-VALREQ-006`, `TEL-VALREQ-007`, `TEL-VALREQ-009`

**Version:** pre-1.0.0

**Commit:** reviewed at `6480c9e57a42af3490eeda55b0f66400e75782cd`; accepted implementation at `518c078abc9bd9b340fbb7200470de47cde93452`

**Tree state:** clean tracked worktree at the recorded implementation commit

**Environment:** Windows focused build and test environment, Release configuration

**Targets:** library `net481`/`net9.0`; focused tests `net481`/`net9.0`

**Command or scenario:** project builds, the focused Telemetry suite, and the scoped public-API comparison recorded in the F1-TEL audit and its package reconciliation

**Execution:** automated

**Evidence level:** automated-runtime

**Result:** PASS

**Artifacts:** `docs/modules/Telemetry/audits/public-api-review-2026-08-17.md`; 22/22 focused tests on each target, up from 6; both accepted API manifests verified unchanged with no baseline update; full solution 1,384/1,384 tests with no failures or skips

**Gaps:** The ordering evidence is one run of 4,000 operations across 8 threads and 200 completion races on one machine — strong evidence that the dispatch gate serializes correctly, not proof that no rare interleaving exists. The blocking-sink observations used an artificially blocking sink; the width of the window in a real application was not measured. The unbounded checkpoint growth is a code fact plus one 50,000-checkpoint observation with no memory measurement. No cross-process, persistence, or export behavior exists in this module, so none was evaluated. No long-running, package, or consumer claim is included.

**Supersedes:** the proposal-only portions of the F1-TEL audit snapshot

## TEL-VALEVID-20260817-002

**Requirement IDs:** `TEL-VALREQ-010`, `TEL-VALREQ-011`, `TEL-VALREQ-015`

**Version:** `1.0.0-local.18`

**Commit:** `518c078abc9bd9b340fbb7200470de47cde93452`

**Tree state:** clean

**Environment:** Windows canonical local package flow with isolated package-reference probes

**Targets:** the `NekoLib.Telemetry` package within the coordinated family, both target assets

**Command or scenario:** the canonical `eng/pack-local.ps1` campaign recorded in the F1-TEL package reconciliation

**Execution:** automated

**Evidence level:** automated-runtime

**Result:** PASS

**Artifacts:** immutable `NekoLib.Telemetry.1.0.0-local.18.nupkg` with both target assemblies, an aligned `NekoLib.Core` dependency, recorded source-commit provenance, and SHA-256 `8B3DDABA5B16A91D0518258B8246022688E3E8E042BF03BC079A7D4AFE9BA185`; WinForms, WPF, multitarget, structure, deployment, stale-payload, publish, and clean probes

**Gaps:** The consumer probes restored, built, and ran, but none of them started or completed an operation, exercised a sink, or read a snapshot through the package boundary. XML package content was not a gate at this version, so this record does not satisfy `TEL-VALREQ-012`.

**Supersedes:** pre-package F1-TEL qualification

## TEL-VALEVID-20260821-001

**Requirement IDs:** `TEL-VALREQ-001`, `TEL-VALREQ-002`, `TEL-VALREQ-003`, `TEL-VALREQ-010`, `TEL-VALREQ-011`, `TEL-VALREQ-015`

**Version:** `1.0.0` qualified by `1.0.0-local.22`

**Commit:** `7090e40eed7c6b888ce8da732f21cbe10f1a936c`

**Tree state:** clean qualifying candidate and separately materialized stable packages

**Environment:** Windows canonical package flow and approved release-asset publication

**Targets:** the `NekoLib.Telemetry` package and both target assets within the stable coordinated family

**Command or scenario:** `eng/pack-local.ps1 -PackageVersion 1.0.0-local.22` plus the recorded stable materialization and publication flow

**Execution:** automated

**Evidence level:** automated-runtime

**Result:** PASS

**Artifacts:** `docs/stable-release-1.0.0.md`; stable `NekoLib.Telemetry.1.0.0.nupkg` SHA-256 `CC9720739B4974B26FC1AF6A6B7488BEBC0A328D132ECBB4EA2424A4536158E8`; qualifying `1.0.0-local.22` SHA-256 `0EEA2A42800F1FEB78EF8AFAEF946B6789574BEB02A191D9332B0BC9F6617DF8`

**Gaps:** The release campaign executed no long-running Telemetry scenario, no checkpoint-growth measurement, and no XML package-content gate.

**Supersedes:** `1.0.0-local.18` as coordinated stable qualification, while preserving its Telemetry-specific package evidence

## TEL-VALEVID-20260828-001

**Requirement IDs:** `TEL-VALREQ-001`, `TEL-VALREQ-002`, `TEL-VALREQ-010`, `TEL-VALREQ-011`, `TEL-VALREQ-012`, `TEL-VALREQ-015`

**Version:** `1.1.0-local.8`

**Commit:** `d6f2efdbe99f4a827293cdf4e8ed27c4096d134a`

**Tree state:** clean

**Environment:** Windows documentation-enabled Release builds and the canonical local package flow with isolated consumers

**Targets:** both `NekoLib.Telemetry` target assemblies

**Command or scenario:** the family documentation campaign builds and `eng/pack-local.ps1 -PackageVersion 1.1.0-local.8`

**Execution:** automated

**Evidence level:** automated-runtime

**Result:** PASS

**Artifacts:** `docs/audit/public-api-documentation-extensibility-review-2026-08-27.md`; all 1,787 solution tests passed across their targets with zero failures and zero skips; every managed package carried both target assemblies and both matching XML files; immutable local-feed packages with aggregate SHA-256 `b56451a1ee8eb7ef4de0d32de143f9488b09f00b25fc300572bcbeb2ee34e9f2`

**Gaps:** This is a local candidate, not a stable release. The campaign's mid-flight scan recorded three residual `CS1591` diagnostics for this assembly; the closing state was not separately measured at that time, so this record proves package XML delivery rather than complete member coverage.

**Supersedes:** `1.1.0-local.7`, retained as negative immutable evidence because its managed packages omitted the XML files
## TEL-VALEVID-20260829-001

**Requirement IDs:** `TEL-VALREQ-001`, `TEL-VALREQ-002`, `TEL-VALREQ-003`, `TEL-VALREQ-004`, `TEL-VALREQ-005`, `TEL-VALREQ-006`, `TEL-VALREQ-007`, `TEL-VALREQ-012`

**Version:** unreleased documentation source

**Commit:** working tree based on `1cf2bd57ee9d5c8bb0e094b8cbe86253e887ff05`

**Tree state:** authorized module-first documentation working tree before its final commit; no Telemetry source change is carried

**Environment:** Windows local checkout, Release configuration

**Targets:** library `net481`/`net9.0`; focused tests `net481`/`net9.0`

**Command or scenario:** forced `-t:Rebuild` on each target with `CS1591` live, including `-p:NoWarn=1701;1702` on `net481` to unsuppress it; two ordinary Release target builds; `eng/verify-public-api.ps1 -PackageId NekoLib.Telemetry -NoBuild`; focused Release tests per target with `-m:1`; `eng/verify-docs.ps1`; `git diff --check`

**Execution:** automated

**Evidence level:** automated-runtime

**Result:** PASS

**Artifacts:** 0 warnings and 0 errors on every build, including both forced rebuilds with `CS1591` unsuppressed, so the assembly currently carries no undocumented public member on either target; both generated `NekoLib.Telemetry.xml` assets byte-identical across targets at 5,184 bytes; 22/22 tests passed with 0 failed and 0 skipped on each test target; both accepted API manifests verified unchanged; documentation verification and diff check passed

**Gaps:** No package candidate, PackageReference consumer, runtime scenario, checkpoint-growth measurement, reentrancy characterization, or full-solution regression was run in this pass, so `TEL-VALREQ-008`, `TEL-VALREQ-010`, `TEL-VALREQ-011`, and `TEL-VALREQ-013` through `TEL-VALREQ-015` are untouched by it. This run measures member coverage in the working tree only; it is not package-delivery evidence.

**Supersedes:** the mid-campaign residual-`CS1591` count recorded in `TEL-VALEVID-20260828-001`, which remains valid as package-delivery evidence
