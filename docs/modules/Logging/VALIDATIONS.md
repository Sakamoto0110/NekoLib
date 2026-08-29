# NekoLib.Logging Validation Evidence

**Document ID:** LOG-VALIDATION-EVIDENCE

**Schema version:** 1

**Kind:** reference

**Lifecycle:** current

**Subject:** executed validation evidence for the NekoLib.Logging boundary

**Surface:** validation-evidence

**Boundary:** logging

**Authority role:** evidence

**Mutation:** authored

**Indexing:** include

These records curate preserved evidence; they do not re-run it. The source
audit, scenario record, or package record remains the detailed owner.

The shared Observability scenario runs Logging, Telemetry, and Inspection in one
process. Only its Logging checks are recorded here. Its Telemetry and Inspection
checks belong to those boundaries and satisfy no Logging requirement.

Requirements with no complete PASS, as of the current baseline:
`LOG-VALREQ-008` is covered only by three partial runs, `LOG-VALREQ-013` has
never been executed, and `LOG-VALREQ-014` has never been executed.

## LOG-VALEVID-20260809-001

**Requirement IDs:** `LOG-VALREQ-008`

**Version:** pre-1.0.0 scenario source delivered 2026-08-09

**Commit:** not recorded in the scenario verification table; each run stamps its own commit and dirty flag into `environment.json`

**Tree state:** committed scenario source

**Environment:** Windows console host; no container, service, credential, or hardware; the run writes only inside its own artifact directory

**Targets:** `net481` and `net9.0`

**Command or scenario:** `NekoLib.Observability.RuntimeTests.LongRunningRecovery.exe --smoke` over the specified 15-minute window on each target, Logging phase and checks only

**Execution:** automated

**Evidence level:** automated-runtime

**Result:** PARTIAL

**Artifacts:** `runtime_tests/Observability/LongRunningRecovery/README.md` verification record; 4951 checks on `net9.0` and 4591 on `net481` with 0 failed and 0 skipped; 164 and 152 full-matrix cycles under concurrent steady traffic; every bounded structure ended exactly at capacity; the working directory deleted cleanly, which is how no-retained-handle is proved rather than asserted

**Gaps:** This mode injects no faults, so it covers the sustained-ordering, capacity, and resource half of `LOG-VALREQ-008` and none of its failure-isolation half. Rotation and retention were exercised at a deliberately small maximum file size, which establishes the mechanics and nothing about a production file size. Ordering was asserted on delivery order; the timestamp-inversion count is a recorded note, not a claim. No second writer process was started. Resource movement was asserted against a bounded allowance for threads and handles and on shape only for memory, because these runs are the baseline a later threshold would have to be derived from.

**Supersedes:** none

## LOG-VALEVID-20260809-002

**Requirement IDs:** `LOG-VALREQ-008`

**Version:** pre-1.0.0 scenario source

**Commit:** not recorded in the scenario verification table

**Tree state:** committed scenario source

**Environment:** Windows console host with the scenario's own seeded fault schedule

**Targets:** `net9.0`

**Command or scenario:** `--soak 15m`, in which the seeded fault schedule and the capability cycles run together through the scenario's exclusivity gate

**Execution:** automated

**Evidence level:** automated-runtime

**Result:** PARTIAL

**Artifacts:** `runtime_tests/Observability/LongRunningRecovery/README.md` verification record; exit 0, 3848 checks, 0 failed, 0 skipped, 128 cycles, all seven fault kinds fired while the cycles were running and all seven passed; schedule `fnv1a64:173b5243f59382ef`; 1413 files removed at cleanup

**Gaps:** This is the only recorded run in which Logging faults and Logging workload overlapped, which is what `LOG-VALREQ-008` actually asks for — but it ran on one target for 15 minutes. It is not long-duration soak evidence, and no equivalent `net481` overlap run exists. Four of the seven fault kinds belong to Telemetry and Inspection and are not Logging evidence.

**Supersedes:** none

## LOG-VALEVID-20260810-001

**Requirement IDs:** `LOG-VALREQ-008`

**Version:** pre-1.0.0 scenario source

**Commit:** not recorded in the scenario verification table

**Tree state:** committed scenario source

**Environment:** Windows console host with the scenario's own seeded fault schedule

**Targets:** `net481` and `net9.0`

**Command or scenario:** `--recovery-rehearsal --rehearsal-duration 70m` on each target; the Logging fault kinds `log-sink-throws`, `log-file-locked`, and `log-flush-blocked` with their documented terminals and post-recovery probes

**Execution:** automated

**Evidence level:** automated-runtime

**Result:** PARTIAL

**Artifacts:** `runtime_tests/Observability/LongRunningRecovery/README.md` verification record; both targets elapsed 62.3 minutes inside the specified 60-to-90-minute window with 68 checks, 0 failed and 0 skipped, and produced the same schedule hash and byte-identical workload counters, so the rehearsal is deterministic in workload and not only in plan

**Gaps:** Covers the failure-isolation and recovery half of `LOG-VALREQ-008` and none of its sustained-resource half, because this mode runs no cycle loop and therefore takes no periodic samples. Every injected failure is scenario-owned; no fault-injection surface exists in `NekoLib.Logging`. The blocked-flush kind proves the budget is honored, not the width of the window a real slow sink would open.

**Supersedes:** the 2026-08-09 `net9.0` rehearsal that elapsed 52.9 minutes, below the specified lower bound

## LOG-VALEVID-20260817-001

**Requirement IDs:** `LOG-VALREQ-001`, `LOG-VALREQ-002`, `LOG-VALREQ-003`, `LOG-VALREQ-004`, `LOG-VALREQ-005`, `LOG-VALREQ-006`, `LOG-VALREQ-007`, `LOG-VALREQ-009`

**Version:** pre-1.0.0

**Commit:** reviewed at `c7967e784914b56863a1b2da97cfafecb32ea494`; final accepted implementation at `dd0cfb8d9c0b69f234cb8cbe802ed8cac4b14213`

**Tree state:** clean tracked worktree at the recorded implementation commit

**Environment:** Windows focused build and test environment, Release configuration

**Targets:** library `net481`/`net9.0`; focused tests `net481`/`net9.0`

**Command or scenario:** project builds, the focused Logging suite, and the scoped public-API comparison recorded in the F1-LOG audit and its two reconciliations

**Execution:** automated

**Evidence level:** automated-runtime

**Result:** PASS

**Artifacts:** `docs/modules/Logging/audits/public-api-review-2026-08-17.md`; 30/30 focused tests on each target, up from 9; both accepted API manifests verified unchanged with no baseline update; full solution 1,352/1,352 tests with no failures or skips

**Gaps:** The `DebugLogSink` correction was verified by IL inspection of the packaged `net481` assembly and by a Release regression, not under a real attached debugger. No cross-process rolling-file test ran, so the single-writer rule remains source-derived. The path-gate table growth is a code fact, not a measured leak. The `TaskScheduler.UnobservedTaskException` observation required forced collections, so the crash-record consequence it prevents is possible rather than guaranteed on every timeout. No long-running, package, or consumer claim is included.

**Supersedes:** the proposal-only portions of the F1-LOG audit snapshot

## LOG-VALEVID-20260817-002

**Requirement IDs:** `LOG-VALREQ-010`, `LOG-VALREQ-011`, `LOG-VALREQ-015`

**Version:** `1.0.0-local.17`

**Commit:** `dd0cfb8d9c0b69f234cb8cbe802ed8cac4b14213`

**Tree state:** clean

**Environment:** Windows canonical local package flow with isolated package-reference probes

**Targets:** the `NekoLib.Logging` package within the coordinated family, both target assets

**Command or scenario:** the canonical `eng/pack-local.ps1` campaign recorded in the F1-LOG package reconciliation

**Execution:** automated

**Evidence level:** automated-runtime

**Result:** PASS

**Artifacts:** immutable `NekoLib.Logging.1.0.0-local.17.nupkg` with both target assemblies, an aligned `NekoLib.Core` dependency, recorded source-commit provenance, and SHA-256 `8378290431CA1036BD8E70E254C6995415DC755B6A91D82D7CAEDD7802AF3991`; WinForms, WPF, multitarget, structure, deployment, stale-payload, publish, and clean probes

**Gaps:** The consumer probes restored, built, and ran, but none of them wrote a log file, rotated an archive, or exercised flush or disposal through the package boundary. XML package content was not a gate at this version, so this record does not satisfy `LOG-VALREQ-012`.

**Supersedes:** pre-package F1-LOG qualification

## LOG-VALEVID-20260821-001

**Requirement IDs:** `LOG-VALREQ-001`, `LOG-VALREQ-002`, `LOG-VALREQ-003`, `LOG-VALREQ-010`, `LOG-VALREQ-011`, `LOG-VALREQ-015`

**Version:** `1.0.0` qualified by `1.0.0-local.22`

**Commit:** `7090e40eed7c6b888ce8da732f21cbe10f1a936c`

**Tree state:** clean qualifying candidate and separately materialized stable packages

**Environment:** Windows canonical package flow and approved release-asset publication

**Targets:** the `NekoLib.Logging` package and both target assets within the stable coordinated family

**Command or scenario:** `eng/pack-local.ps1 -PackageVersion 1.0.0-local.22` plus the recorded stable materialization and publication flow

**Execution:** automated

**Evidence level:** automated-runtime

**Result:** PASS

**Artifacts:** `docs/stable-release-1.0.0.md`; stable `NekoLib.Logging.1.0.0.nupkg` SHA-256 `8183024F0130A0917DDBC4936F742F4246B22B62D383461BD2439797655E48BC`; qualifying `1.0.0-local.22` SHA-256 `463BC9708E7E6CDEDCE74EBCF339699545DF023B5058E9E41A21B483E0F20C23`

**Gaps:** The release campaign executed no long-running Logging scenario, no cross-process file test, and no XML package-content gate.

**Supersedes:** `1.0.0-local.17` as coordinated stable qualification, while preserving its Logging-specific package evidence

## LOG-VALEVID-20260828-001

**Requirement IDs:** `LOG-VALREQ-001`, `LOG-VALREQ-002`, `LOG-VALREQ-003`, `LOG-VALREQ-004`, `LOG-VALREQ-010`, `LOG-VALREQ-011`, `LOG-VALREQ-012`, `LOG-VALREQ-015`

**Version:** `1.1.0-local.8`

**Commit:** `d6f2efdbe99f4a827293cdf4e8ed27c4096d134a`

**Tree state:** clean

**Environment:** Windows documentation-enabled Release builds and the canonical local package flow with isolated consumers

**Targets:** both `NekoLib.Logging` target assemblies and both test targets

**Command or scenario:** the focused flush regression repeated 30 consecutive times on `net481`, the complete dual-target Logging suite, and `eng/pack-local.ps1 -PackageVersion 1.1.0-local.8`

**Execution:** automated

**Evidence level:** automated-runtime

**Result:** PASS

**Artifacts:** `docs/audit/public-api-documentation-extensibility-review-2026-08-27.md`; all 1,787 solution tests passed across their targets with zero failures and zero skips; every managed package carried both target assemblies and both matching XML files; immutable local-feed packages with aggregate SHA-256 `b56451a1ee8eb7ef4de0d32de143f9488b09f00b25fc300572bcbeb2ee34e9f2`

**Gaps:** This is a local candidate, not a stable release. The 30-run repetition establishes that the corrected exhaustion-stops-admission rule is not flaky at sub-millisecond boundaries on this host; it does not bound the timing on other hardware. Immutable `1.1.0-local.7` is retained as negative evidence because its managed packages omitted the XML files. This record predates the `RollingFileLogSink.Write` comment added on 2026-08-29.

**Supersedes:** `1.1.0-local.7`, retained as negative immutable evidence
## LOG-VALEVID-20260829-001

**Requirement IDs:** `LOG-VALREQ-001`, `LOG-VALREQ-002`, `LOG-VALREQ-003`, `LOG-VALREQ-004`, `LOG-VALREQ-005`, `LOG-VALREQ-006`, `LOG-VALREQ-007`, `LOG-VALREQ-012`

**Version:** unreleased documentation source

**Commit:** working tree based on `32ce261d3188e7e10ac2dbc46cd7e3df5fbb58bc`

**Tree state:** authorized module-first documentation working tree before its final commit, carrying one XML comment change in `RollingFileLogSink.Write`

**Environment:** Windows local checkout, Release configuration

**Targets:** library `net481`/`net9.0`; focused tests `net481`/`net9.0`

**Command or scenario:** two Release target builds; `eng/verify-public-api.ps1 -PackageId NekoLib.Logging -NoBuild`; focused Release tests per target with `-m:1`; `eng/verify-docs.ps1`; `git diff --check`

**Execution:** automated

**Evidence level:** automated-runtime

**Result:** PASS

**Artifacts:** 0 warnings and 0 errors on each target build; both generated `NekoLib.Logging.xml` assets byte-identical across targets at 12,318 bytes and both carrying the new member documentation; 30/30 tests passed with 0 failed and 0 skipped on each test target; both accepted API manifests verified unchanged; documentation verification and diff check passed

**Gaps:** No package candidate, PackageReference consumer, runtime scenario, cross-process writer test, disposal-versus-snapshot characterization, or full-solution regression was run in this pass, so `LOG-VALREQ-008` through `LOG-VALREQ-011` and `LOG-VALREQ-013` through `LOG-VALREQ-015` are untouched by it. The comment added here postdates every recorded package candidate, so no package evidence covers it.

**Supersedes:** none
