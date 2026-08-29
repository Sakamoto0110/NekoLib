# NekoLib.Watchdog Validation Evidence

**Document ID:** WDG-VALIDATION-EVIDENCE

**Schema version:** 1

**Kind:** reference

**Lifecycle:** current

**Subject:** executed validation evidence for the NekoLib.Watchdog boundary

**Surface:** validation-evidence

**Boundary:** watchdog

**Authority role:** evidence

**Mutation:** authored

**Indexing:** include

These records curate preserved evidence; they do not re-run it. The source
audit, scenario record, or release record remains the detailed owner of each
result. Build, test, protocol, process-runtime, interactive, package, and
release claims are kept separate, and a run that did not exercise a
requirement's boundary or evidence level does not satisfy it.

Evidence produced for the `NekoLib.Watchdog.Host` deployment boundary is not
reused here except where a run actually exercised this library's code. Where it
did, the record says exactly which part.

Requirements with no executed evidence at this baseline:

| Requirement | Status | Why |
|---|---|---|
| `WDG-VALREQ-006` | NOT_RUN | No run has observed an actual forced termination. The focused suite asserts that the resolved `taskkill` path ignores the process search path; it does not execute a kill, and no evidence covers the `/T` tree flag or an unresponsive target. |
| `WDG-VALREQ-009` | NOT_RUN | No cross-user or cross-elevation denial probe has ever been attempted for the Watchdog endpoints. Present evidence is same-user success plus source inspection of the Pipes per-target protection. |
| `WDG-VALREQ-011` | NOT_RUN | Every executed run used the developer account. No run has started a runtime under a restricted standard-user token, which is what `WDG-FINDING-001` needs to resolve. |
| `WDG-VALREQ-012` | NOT_RUN | No interactive hotkey registration, conflict, or window-activation probe has been executed. The F1-WDOG review and the crash-recovery scenario both record this exclusion explicitly. |
| `WDG-VALREQ-013` | NOT_RUN | No `net481`/`net9.0-windows` runtime-to-controller pairing has been run. Every executed run used one target family in both roles. |
| `WDG-VALREQ-014` | NOT_RUN | No nominal-window smoke, rehearsal, soak, or campaign run exists. The best available scenario run records `belowSpecifiedWindow: true`, and the four-hour soak has never executed. |
| `WDG-VALREQ-010` | NOT_APPLICABLE | Outside the accepted security boundary by decision, not by omission. |

The executed records follow.

## WDG-VALEVID-20260801-001

**Requirement IDs:** none

**Version:** pre-1.0.0

**Commit:** scenario source `32fc67e`

**Tree state:** not recorded

**Environment:** Windows with .NET Framework 4.8.1 developer support

**Targets:** `net481`

**Command or scenario:** `runtime_tests/Watchdog/Supervisor481` project build

**Execution:** manual

**Evidence level:** build-only

**Result:** PARTIAL

**Artifacts:** `runtime_tests/Watchdog/Supervisor481/README.md`

**Gaps:** The interactive in-process supervisor scenario compiled; its documented procedure — start, ping, status, child close and replacement, pause and resume, restart, stop — has never been rerun. A compiled scenario is build-only evidence until it is launched and observed, so this record satisfies no requirement. It remains the only evidence that the advanced `WatchdogRuntime` composition is exercised outside the focused suite, and even that is compilation only.

**Supersedes:** none

## WDG-VALEVID-20260811-001

**Requirement IDs:** `WDG-VALREQ-005`, `WDG-VALREQ-007`

**Version:** pre-1.0.0

**Commit:** not recorded for the final probes; the preceding attach fix and the corrected scenario code were in the working tree

**Tree state:** working tree during E3-WDOG remediation

**Environment:** Windows; a controller process, a scenario application child, and the separately deployed Host, all as real processes

**Targets:** `net481` and `net9.0-windows`, one complete run each

**Command or scenario:** `runtime_tests/Watchdog/CrashRecovery` `--smoke --smoke-duration 2m --layout source --seed 20260810`

**Execution:** automated

**Evidence level:** automated-runtime

**Result:** PASS

**Artifacts:** `runtime_tests/Watchdog/CrashRecovery/README.md`; run directories `artifacts/validation/phase-e/e3wdog-smoke-net481-s20260810-20260811T164300191Z` and `artifacts/validation/phase-e/e3wdog-smoke-net9.0-s20260810-20260811T164537338Z`

**Gaps:** 20 checks passed per run with zero failed or skipped, all six scheduled faults reaching their expected terminal, seven healthy generations, both ordinary exit codes observed as 0, valid bundle integrity and retention, released endpoints, zero cleanup problems, and exit 0 in about 123 seconds. This is source-layout evidence: it exercises the deployed path and the bootstrap shape but proves nothing about package deployment. It is deliberately far below the nominal smoke window, so it is not duration evidence. Forced termination, hotkeys, cross-user access, and the restricted-token case were not exercised. The run that reached this state also found and fixed the first-generation attach exit-code defect described in [`HISTORY.md`](HISTORY.md).

**Supersedes:** none

## WDG-VALEVID-20260811-002

**Requirement IDs:** `WDG-VALREQ-005`

**Version:** `1.0.0-local.10`

**Commit:** clean `46befc6`

**Tree state:** clean

**Environment:** Windows; a disposable `PackageReference` consumer output built from an immutable local package, driven by the scenario controller

**Targets:** `net9.0-windows`

**Command or scenario:** `runtime_tests/Watchdog/CrashRecovery` `--smoke --layout disposable-package` against `NekoLib.Watchdog.Host` `1.0.0-local.10`

**Execution:** automated

**Evidence level:** automated-runtime

**Result:** PASS

**Artifacts:** `runtime_tests/Watchdog/CrashRecovery/README.md`; run directory `artifacts/validation/phase-e/e3wdog-smoke-net9.0-s20260810-20260811T235447397Z`; package SHA-256 `acc31d9f2450cc14d36ba6e723357a706dcf0b90d2ed1116f11201787b574710`

**Gaps:** 20 of 20 checks passed with all six faults, seven healthy generations, one complete retained bundle, both endpoints released, no leaked process or window, and exit 0 in about 123 seconds. The result records `supportsPackageClaim: true` and an exact byte match between the deployed Host and one package payload entry. It also records `belowSpecifiedWindow: true`, so it is topology and recovery evidence rather than duration evidence. `net481` package parity was never repeated, and the four-hour soak never ran. Because the payload is a Host artifact, this record establishes that this library's bootstrap and supervision worked against a package-deployed Host; it is not a claim about the `NekoLib.Watchdog` library package itself.

**Supersedes:** none

## WDG-VALEVID-20260818-001

**Requirement IDs:** `WDG-VALREQ-002`, `WDG-VALREQ-003`

**Version:** pre-1.0.0

**Commit:** `6580b6f6ece4bd7c90f4cc80cd7e1f01d47eace6`

**Tree state:** implementation worktree for the accepted F1-WDOG dispositions

**Environment:** Windows with the .NET SDK; no container, service, or hardware

**Targets:** `net481` and `net9.0-windows` test targets over both library assets

**Command or scenario:** focused Watchdog suite per target; full solution `dotnet test`; `dotnet build NekoLib.sln -t:Rebuild`; `eng\verify-public-api.ps1 -PackageId NekoLib.Watchdog`; `eng\verify-docs.ps1` with the captured rebuild log

**Execution:** automated

**Evidence level:** automated-runtime

**Result:** PASS

**Artifacts:** `docs/modules/Watchdog/audits/public-api-review-2026-08-18.md`

**Gaps:** The focused suite passed 92 tests per target, the full solution passed 1,614, the rebuild emitted 340 existing warning identities and zero errors with no new identity, and both reviewed manifests verified after the accepted baseline update. The new regressions closed the original lifecycle, option-mutation, counter-split, metadata-parity, callback-isolation, system-`taskkill`-path, JSON, and crash-outcome gaps. Four runtime-scenario projects were built and deliberately **not** launched. No interactive hotkey or window-activation probe, no actual `taskkill` termination, no cross-user or elevation probe, no packaged-sidecar run, no long-running campaign, and no package, publish, or push. This run predates XML documentation generation, so it does not satisfy `WDG-VALREQ-001`.

**Supersedes:** none

## WDG-VALEVID-20260820-001

**Requirement IDs:** `WDG-VALREQ-003`, `WDG-VALREQ-004`, `WDG-VALREQ-008`

**Version:** `1.0.0-local.21`

**Commit:** clean `5c4aa621bee039a9a3c616212aba07a3e444c696`

**Tree state:** clean; packed without `-AllowDirty`

**Environment:** Windows; canonical `eng\pack-local.ps1` flow followed by the PackageReference-only consumer campaign

**Targets:** `net481` and `net9.0-windows` for both the focused suite and the package consumers

**Command or scenario:** focused Watchdog suite per target; `eng\verify-public-api.ps1 -PackageId NekoLib.Watchdog`; `eng\pack-local.ps1 -PackageVersion 1.0.0-local.21`; the expanded package-only campaign including the `WatchdogHostProtocol` consumer

**Execution:** manual

**Evidence level:** automated-runtime

**Result:** PASS

**Artifacts:** `docs/audit/watchdog-host-contract-review-2026-08-20.md`; `NekoLib.Watchdog.1.0.0-local.21.nupkg` SHA-256 `BB7A68F7CD056E7EB3EAE94FC51D36AF91F301FD9AE1C76F94B84E4B77712D2D`

**Gaps:** The focused suite passed 106 tests per target, both compiled baselines verified unchanged, and the full serial solution test gate passed inside the pack flow. The package-only campaign is the one run in which this library's `WatchdogBootstrap`, `WatchdogController`, and protocol handling executed from a package rather than from repository build output: a coordinated pair started, answered `ping` and `status`, and stopped cleanly on both target families, and a deliberately mismatched protocol produced the version-specific failure instead of a timeout. That closes the protocol requirement for the shipped same-target pairing only. The campaign did not exercise crash loops, forced termination, hotkeys, cross-user access, or long-running supervision, and it predates XML documentation delivery, so it does not satisfy `WDG-VALREQ-001` and satisfies `WDG-VALREQ-008` only for assets and consumption, not for the XML pairing.

**Supersedes:** none

## WDG-VALEVID-20260821-001

**Requirement IDs:** none

**Version:** 1.0.0

**Commit:** qualifying source `7090e40eed7c6b888ce8da732f21cbe10f1a936c`; materialized package source `db63529cafce11690a18a595e4abc6c0610b9b8e`

**Tree state:** clean at both recorded commits

**Environment:** Windows; local manual coordinated pack flow, later published through the manually dispatched trusted-publication workflow

**Targets:** `NekoLib.Watchdog` `lib/net481` and `lib/net9.0-windows7.0` package assets

**Command or scenario:** coordinated family release flow recorded in the stable release baseline; qualifying candidate `1.0.0-local.22`

**Execution:** manual

**Evidence level:** automated-runtime

**Result:** PASS

**Artifacts:** `docs/stable-release-1.0.0.md`; `NekoLib.Watchdog.1.0.0.nupkg` SHA-256 `4880C6AFDBC8D4D54A7D3040E35F9DFADD35F88EA3E8B44080A3D86A56BA8FE0`

**Gaps:** This record establishes that the two accepted Watchdog manifests became stable baselines within a qualified family release. It is release and provenance evidence and satisfies no requirement on its own: it carries no separate baseline-verification result, and the packages of that version predate XML documentation delivery.

**Supersedes:** none

## WDG-VALEVID-20260827-001

**Requirement IDs:** `WDG-VALREQ-001`, `WDG-VALREQ-002`, `WDG-VALREQ-003`

**Version:** post-1.0.0 source

**Commit:** not separately recorded for this run; the owning audit records reference commit `78d8ce0061b9e8cfab87ab88db5c8ed1832eb4bd`

**Tree state:** working tree containing the Tail-family documentation changes under review

**Environment:** Windows with the .NET SDK

**Targets:** `net481` and `net9.0-windows` library assets and test targets

**Command or scenario:** documentation-enabled Release rebuild across the Tail family; focused Watchdog suite per target; accepted-manifest verification; `eng\verify-docs.ps1`; `git diff --check`

**Execution:** automated

**Evidence level:** automated-runtime

**Result:** PASS

**Artifacts:** `docs/audit/public-api-documentation-extensibility-review-2026-08-27.md`

**Gaps:** Watchdog's planning-baseline `CS1591` diagnostics went to zero with no malformed or unresolved XML-comment warning, both generated XML files were produced beside their assemblies, both accepted manifests matched without a baseline update, and the focused suite passed 106 tests per target. One modern-target process-marker failure occurred while all eight family suites ran concurrently; the failed test and the complete modern suite passed when rerun sequentially, and no Watchdog change was made. That intermittency is unexplained and is a residual limitation of this record. No package was built in this run, so XML delivery through a package is not claimed here, and no runtime, scenario, security, or interactive evidence was produced.

**Supersedes:** none

## WDG-VALEVID-20260828-001

**Requirement IDs:** `WDG-VALREQ-008`

**Version:** `1.1.0-local.8`

**Commit:** `d6f2efdbe99f4a827293cdf4e8ed27c4096d134a`

**Tree state:** clean

**Environment:** Windows; canonical `eng\pack-local.ps1` flow with an isolated NuGet global-packages cache for the consumer probes

**Targets:** `NekoLib.Watchdog` `lib/net481` and `lib/net9.0-windows7.0` package assets

**Command or scenario:** `eng\pack-local.ps1 -PackageVersion 1.1.0-local.8`, including the permanent package-content guard and the `PackageReference` consumer probes

**Execution:** manual

**Evidence level:** automated-runtime

**Result:** PASS

**Artifacts:** `docs/audit/public-api-documentation-extensibility-review-2026-08-27.md`

**Gaps:** Watchdog was validated as one of the 15 managed packages: each contained two package-owned target assemblies with both matching XML files, and isolated `PackageReference` restores extracted the same pairs from the package rather than from repository build output. The record is a family-level result; it carries no Watchdog-specific behavioral, protocol, or supervision claim, and it is a local immutable candidate rather than a public release. Together with `WDG-VALEVID-20260820-001` it covers `WDG-VALREQ-008`: that record supplied package-backed consumption and this one supplied the XML pairing, and no single run has yet supplied both.

**Supersedes:** `WDG-VALEVID-20260821-001` for XML-documentation delivery only; the 1.0.0 release provenance record stands.

## WDG-VALEVID-20260829-001

**Requirement IDs:** `WDG-VALREQ-001`, `WDG-VALREQ-002`

**Version:** post-1.0.0 source

**Commit:** `c1d6aadcea773529220b7850f0e18d1dd0e3c1f0`

**Tree state:** working tree containing this module review's staged documentation and comment-only source changes; nothing committed

**Environment:** Windows with the .NET SDK; no container, service, child-process campaign, or package feed

**Targets:** `net481` and `net9.0-windows` library assets

**Command or scenario:** `dotnet build src\Watchdog\NekoLib.Watchdog\NekoLib.Watchdog.csproj -c Release -t:Rebuild -m:1`; `eng\verify-docs.ps1 -BuildLogPath <captured rebuild log>`; `eng\verify-public-api.ps1 -PackageId NekoLib.Watchdog -NoBuild`; `git diff --check` and `git diff --cached --check`

**Execution:** automated

**Evidence level:** build-only

**Result:** PASS

**Artifacts:** this review's staged diff; the captured rebuild log is session-local and is not repository evidence

**Gaps:** Both targets rebuilt with zero warnings and zero errors, both `NekoLib.Watchdog.xml` files were produced beside their assemblies and carry every corrected member, and both accepted manifests verified without a baseline update — which is what proves the six XML-comment corrections changed no compiled metadata. A mechanical check confirmed that every changed source line is an XML comment line and that no project file, target, dependency, or baseline was touched. The warning comparison is scoped to this one project, so it establishes that this project emits no new identity and reports 175 unrelated baseline identities as not emitted; it is not a solution-wide warning result. This review executed no test, runtime scenario, package, package-consumer, security, interactive, or release run of its own, so it satisfies no other requirement: the standing focused-regression evidence remains `WDG-VALEVID-20260827-001`, and the seven requirements listed above as `NOT_RUN` are unchanged by it.

**Supersedes:** `WDG-VALEVID-20260827-001` for the build and API-compatibility requirements at this newer baseline only; that record's focused-regression result stands.
