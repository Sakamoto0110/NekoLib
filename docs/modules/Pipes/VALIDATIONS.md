# NekoLib.Pipes Validation Evidence

**Document ID:** PIPE-VALIDATION-EVIDENCE

**Schema version:** 1

**Kind:** reference

**Lifecycle:** current

**Subject:** executed validation evidence for the NekoLib.Pipes boundary

**Surface:** validation-evidence

**Boundary:** pipes

**Authority role:** evidence

**Mutation:** authored

**Indexing:** include

These records curate preserved evidence; they do not re-run it. The source
audit, scenario record, or release record remains the detailed owner of each
result. Build, test, protocol, runtime, package, and release claims are kept
separate, and a run that did not exercise a requirement's boundary or evidence
level does not satisfy it.

The module review that produced this registry on 2026-08-28 executed nothing
itself. Every record below predates it.

Requirements with no executed evidence at this baseline:

| Requirement | Status | Why |
|---|---|---|
| `PIPE-VALREQ-006` | NOT_RUN | No cross-user or cross-elevation denial probe has ever been attempted. Present evidence is same-user success plus source inspection of the `net481` ACL and the `net9.0` pipe option, which cannot demonstrate denial. |
| `PIPE-VALREQ-007` | NOT_RUN | No `net481`/`net9.0` separate-process pairing has been run. The [F1-PIPE migration](migrations/f1.md) recorded this as a gate before the first stable release; `1.0.0` shipped on 2026-08-21 without it, so the gap outlived its stated gate and is carried here rather than being treated as closed. |
| `PIPE-VALREQ-008` | NOT_RUN | Admission saturation is listed as uncovered by the runtime scenario and has no focused test. |
| `PIPE-VALREQ-010` | NOT_RUN | No nominal-window smoke, rehearsal, soak, or campaign run exists. The best available run records itself as below the specified window. |
| `PIPE-VALREQ-011` | NOT_RUN | No Linux or macOS execution has been performed, and no non-Windows claim is currently made. |
| `PIPE-VALREQ-012` | NOT_APPLICABLE | Outside the accepted transport boundary by decision, not by omission. |

The executed records follow.

## PIPE-VALEVID-20260818-001

**Requirement IDs:** `PIPE-VALREQ-002`, `PIPE-VALREQ-003`

**Version:** pre-1.0.0

**Commit:** `e608dc873c78f5bcecbd79dc5931c59f5d461dcc`

**Tree state:** implementation worktree for the accepted F1-PIPE dispositions

**Environment:** Windows with the .NET SDK; no container, service, or hardware

**Targets:** `net481` and `net9.0-windows` test targets over the `net481` and `net9.0` library assets

**Command or scenario:** `dotnet test tests/NekoLib.Pipes.Tests/Unit/NekoLib.Pipes.Tests.Unit.csproj -f <target> -m:1`; `eng\verify-public-api.ps1 -PackageId NekoLib.Pipes`

**Execution:** automated

**Evidence level:** automated-runtime

**Result:** PASS

**Artifacts:** `docs/modules/Pipes/audits/public-api-review-2026-08-18.md`

**Gaps:** This run predates XML-documentation generation, so it does not satisfy `PIPE-VALREQ-001`. Same-target only, so it establishes nothing about `net481`/`net9.0` interoperability. The `CurrentUserOnly` coverage is same-user success plus inspection of the target-specific server protection, not a denial observation. No saturation, long-duration, throughput, or package evidence.

**Supersedes:** none

## PIPE-VALEVID-20260811-001

**Requirement IDs:** `PIPE-VALREQ-004`

**Version:** pre-1.0.0

**Commit:** not recorded; the run followed the event-hub liveness fix and the scenario endpoint fix of the same day

**Tree state:** not recorded

**Environment:** Windows; a controller process plus server and client child processes on one machine-wide named pipe allocated per run

**Targets:** `net9.0` and `net481`, one complete run each

**Command or scenario:** `runtime_tests/Pipes/LongRunningRecovery` `--smoke`, four cycles

**Execution:** automated

**Evidence level:** automated-runtime

**Result:** PASS

**Artifacts:** `runtime_tests/Pipes/LongRunningRecovery/README.md`; run directories `artifacts/validation/phase-e/e3pipe-smoke-net9.0-s20260808-20260811T025942081Z` and `artifacts/validation/phase-e/e3pipe-smoke-net481-s20260808-20260811T030208668Z`

**Gaps:** 75 checks per run, 0 failed, 0 skipped, endpoint released and no process left behind — but smoke generates no faults, so it is protocol and lifecycle evidence only. Each run uses one binary in all three roles, so both targets were covered separately and never paired. The scenario deliberately uses a 64 KiB frame limit and a subscriber queue of 8, which proves the mechanics rather than any capacity figure.

**Supersedes:** none

## PIPE-VALEVID-20260812-001

**Requirement IDs:** `PIPE-VALREQ-004`, `PIPE-VALREQ-005`

**Version:** pre-1.0.0

**Commit:** not recorded; the run executed the scenario code that was committed afterwards as `698960a`

**Tree state:** dirty — the artifacts record `repository.dirty: true`

**Environment:** Windows; controller, server child, and client children as separate processes on a real named pipe

**Targets:** `net9.0`

**Command or scenario:** `runtime_tests/Pipes/LongRunningRecovery` `--recovery-rehearsal --rehearsal-duration 10m --seed 20260808`

**Execution:** automated

**Evidence level:** automated-runtime

**Result:** PASS

**Artifacts:** `runtime_tests/Pipes/LongRunningRecovery/README.md`; run directory `artifacts/validation/phase-e/e3pipe-recovery-net9.0-s20260808-20260812T135339773Z`

**Gaps:** 37 checks passed with the recovery phase at 6/6 in 530.6 s, all six faults reaching their expected terminal and post-recovery probe, cleanup complete and the endpoint released. The run records `belowSpecifiedWindow: true` and is therefore not nominal-window evidence, it ran on `net9.0` only, and the tree was dirty. `MaxClients` saturation and event delivery across a server restart through `AutoReconnect` were not exercised. Counter trends were sampled into `samples.csv` without being asserted.

**Supersedes:** none

## PIPE-VALEVID-20260821-001

**Requirement IDs:** none

**Version:** 1.0.0

**Commit:** qualifying source `7090e40eed7c6b888ce8da732f21cbe10f1a936c`; materialized package source `db63529cafce11690a18a595e4abc6c0610b9b8e`

**Tree state:** clean at both recorded commits

**Environment:** Windows; local manual coordinated pack flow, later published through the manually dispatched trusted-publication workflow

**Targets:** `NekoLib.Pipes` `lib/net481` and `lib/net9.0` package assets

**Command or scenario:** coordinated family release flow recorded in the stable release baseline; qualifying candidate `1.0.0-local.22`

**Execution:** manual

**Evidence level:** automated-runtime

**Result:** PASS

**Artifacts:** `docs/stable-release-1.0.0.md`; `NekoLib.Pipes.1.0.0.nupkg` SHA-256 `D147D5142515BAA821512F294F5C6763D0D9494A4FB14AA60DC2AAB12D386B89`

**Gaps:** This record establishes that the two accepted Pipes manifests became stable baselines within a qualified family release. It is release and provenance evidence and satisfies no requirement on its own: it carries no separate baseline-verification result, and the packages of that version predate XML documentation delivery, so `PIPE-VALREQ-002` and `PIPE-VALREQ-009` are not met by it.

**Supersedes:** none

## PIPE-VALEVID-20260827-001

**Requirement IDs:** `PIPE-VALREQ-001`, `PIPE-VALREQ-002`, `PIPE-VALREQ-003`

**Version:** post-1.0.0 source

**Commit:** not separately recorded for this run; the owning audit records reference commit `78d8ce0061b9e8cfab87ab88db5c8ed1832eb4bd`

**Tree state:** working tree containing the documentation changes under review; the audit candidate itself was untracked at the time

**Environment:** Windows with the .NET SDK

**Targets:** `net481` and `net9.0` library assets; `net481` and `net9.0-windows` test targets

**Command or scenario:** documentation-enabled Release rebuild; `dotnet test` for the focused suite on both targets with `-m:1`; `eng\verify-public-api.ps1 -PackageId NekoLib.Pipes`; `eng\verify-docs.ps1`; `git diff --check`

**Execution:** automated

**Evidence level:** automated-runtime

**Result:** PASS

**Artifacts:** `docs/audit/public-api-documentation-extensibility-review-2026-08-27.md`

**Gaps:** The 94 planning-baseline `CS1591` diagnostics went to zero with no malformed or unresolved XML-comment warning, both generated XML files were produced, both accepted manifests matched without a baseline update, and the focused suite passed 74 tests per target. No package was built in this run, so XML delivery through a package was not claimed here. No runtime scenario, saturation, security-denial, or cross-target run.

**Supersedes:** none

## PIPE-VALEVID-20260828-001

**Requirement IDs:** `PIPE-VALREQ-009`

**Version:** `1.1.0-local.8`

**Commit:** `d6f2efdbe99f4a827293cdf4e8ed27c4096d134a`

**Tree state:** clean

**Environment:** Windows; canonical `eng\pack-local.ps1` flow with an isolated NuGet global-packages cache for the consumer probes

**Targets:** `NekoLib.Pipes` `lib/net481` and `lib/net9.0` package assets

**Command or scenario:** `eng\pack-local.ps1 -PackageVersion 1.1.0-local.8`, including the permanent package-content guard and the WinForms and WPF `PackageReference` consumer probes

**Execution:** manual

**Evidence level:** automated-runtime

**Result:** PASS

**Artifacts:** `docs/audit/public-api-documentation-extensibility-review-2026-08-27.md`

**Gaps:** Pipes was validated as one of the 15 managed packages: each contained two package-owned target assemblies with both matching XML files, and isolated `PackageReference` restores extracted the same pairs from the package rather than from repository build output. The record is a family-level result; it carries no Pipes-specific behavioral, protocol, or security claim, and it is a local immutable candidate rather than a public release.

**Supersedes:** `PIPE-VALEVID-20260821-001` for XML-documentation delivery only; the 1.0.0 release provenance record stands.
