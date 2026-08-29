# NekoLib.Watchdog.Host Validation Evidence

**Document ID:** WDGHOST-VALIDATION-EVIDENCE

**Schema version:** 1

**Kind:** reference

**Lifecycle:** current

**Subject:** executed validation evidence for the NekoLib.Watchdog.Host boundary

**Surface:** validation-evidence

**Boundary:** watchdog.host

**Authority role:** evidence

**Mutation:** authored

**Indexing:** include

These records curate preserved evidence; they do not re-run it. The source
audit, scenario record, or release record remains the detailed owner of each
result. Build, package, deployment, protocol, process-runtime, and release
claims are kept separate, and a run that did not exercise a requirement's
boundary or evidence level does not satisfy it.

Package evidence is valid only for the exact version, repository commit, and
hashes it records. An immutable version is never rebuilt or overwritten to
refresh a claim; a new claim requires a new version.

Requirements with no executed evidence at this baseline:

| Requirement | Status | Why |
|---|---|---|
| `WDGHOST-VALREQ-009` | NOT_RUN | No run asserts the Host's own exit code. The package probes drive the packaged Host over its control pipe and check their own exit codes, not the Host's, and no test invokes `Program.Main`. Post-exit cleanup is observed by the scenario controller for the supervised processes, not for the Host's return value. |
| `WDGHOST-VALREQ-010` | NOT_RUN | Every consumer probe targets `net481` or `net9.0-windows`. No build has ever paired a different framework version with a payload. |
| `WDGHOST-VALREQ-011` | NOT_APPLICABLE | Outside the accepted deployment and security boundary by decision, not by omission. No cross-user, elevation, service-account, installer-ACL, signing, tamper, ARM64, self-contained, absent-runtime, or read-only-installation probe has been run, and none is required. |

The executed records follow.

## WDGHOST-VALEVID-20260811-001

**Requirement IDs:** `WDGHOST-VALREQ-003`

**Version:** `1.0.0-local.10`

**Commit:** clean `46befc6`

**Tree state:** clean

**Environment:** Windows; a disposable `PackageReference` consumer output built from an immutable local package, driven by the crash-recovery scenario controller

**Targets:** `net9.0-windows`

**Command or scenario:** `runtime_tests/Watchdog/CrashRecovery` `--smoke --layout disposable-package --application-root <consumer output> --package-file <feed>\NekoLib.Watchdog.Host.1.0.0-local.10.nupkg --package-version 1.0.0-local.10`

**Execution:** automated

**Evidence level:** automated-runtime

**Result:** PASS

**Artifacts:** `runtime_tests/Watchdog/CrashRecovery/README.md`; run directory `artifacts/validation/phase-e/e3wdog-smoke-net9.0-s20260810-20260811T235447397Z`; package SHA-256 `acc31d9f2450cc14d36ba6e723357a706dcf0b90d2ed1116f11201787b574710`

**Gaps:** The controller verified the package ID and version from its nuspec, hashed the package, recorded the PE machine and file version of the deployed Host, and required the deployed bytes to match one exact payload entry — `tools/net9.0-windows7.0/win-x64/NekoLib.Watchdog.Host.exe`. 20 of 20 checks passed with `supportsPackageClaim: true`. This covers the default x64 selection and byte identity for one target only: no `net481` package-backed repeat, no explicit x86 selection, and no unsupported-RID case. The run records `belowSpecifiedWindow: true`, so it is deployment-topology evidence rather than duration evidence.

**Supersedes:** none

## WDGHOST-VALEVID-20260820-001

**Requirement IDs:** `WDGHOST-VALREQ-002`, `WDGHOST-VALREQ-003`, `WDGHOST-VALREQ-004`, `WDGHOST-VALREQ-005`, `WDGHOST-VALREQ-006`, `WDGHOST-VALREQ-007`, `WDGHOST-VALREQ-008`

**Version:** `1.0.0-local.21`

**Commit:** clean `5c4aa621bee039a9a3c616212aba07a3e444c696`

**Tree state:** clean; packed without `-AllowDirty`

**Environment:** Windows; canonical `eng\pack-local.ps1` flow followed by the expanded `PackageReference`-only consumer campaign with an isolated NuGet cache

**Targets:** `net481` and `net9.0-windows` for both the focused suite and the package consumers

**Command or scenario:** focused Watchdog suite per target; `eng\pack-local.ps1 -PackageVersion 1.0.0-local.21`; the expanded package-only campaign including the wrapper, transitive, and `WatchdogHostProtocol` consumers

**Execution:** manual

**Evidence level:** automated-runtime

**Result:** PASS

**Artifacts:** `docs/modules/WatchdogHost/audits/contract-review-2026-08-20.md`; `NekoLib.Watchdog.Host.1.0.0-local.21.nupkg` SHA-256 `F0D8572B261AEE65811CDE4F30921BA3A2EA417735C424AA0C7CB63A738DFBE5`

**Gaps:** This is the record that closed the accepted F1-WDOG-HOST dispositions. It verified exact required and forbidden package layout, the absence of any `lib/` asset, direct-only ownership through a wrapper consumer that received no sidecar, byte identity between package and deployed payloads, AnyCPU `net481` plus explicit x86 and default x64 selection, unsupported-RID failure, stale-directory replacement, build, publish, deployment opt-out and re-enable, clean, a clear protocol mismatch, and real package-backed startup and cooperative shutdown on both target families. The focused suite passed 106 tests per target, which is what carries the parser and fatal-log requirements. It did not assert the Host's exit code, did not exercise a mismatched consumer framework version, and produced no cross-user, elevation, ARM64, self-contained, absent-runtime, or read-only-installation evidence. The package was produced and consumed on one Windows machine; it is strong package and protocol evidence, not a claim about every deployment image or installer policy.

**Supersedes:** none

## WDGHOST-VALEVID-20260820-002

**Requirement IDs:** `WDGHOST-VALREQ-001`

**Version:** pre-1.0.0

**Commit:** `3ec2c63e2d60d96a8462c1a91483dea863015c01`

**Tree state:** clean; this is the review-entry baseline, which precedes the implementation recorded in `WDGHOST-VALEVID-20260820-001`

**Environment:** Windows with the .NET SDK

**Targets:** `net481` and `net9.0-windows` Host assemblies

**Command or scenario:** `dotnet build src\Watchdog\NekoLib.Watchdog.Host\NekoLib.Watchdog.Host.csproj -f <target> --no-restore`; reflection over each built assembly; a diagnostic `net481` build inspected with `GetPEKind`

**Execution:** automated

**Evidence level:** build-only

**Result:** PASS

**Artifacts:** `docs/modules/WatchdogHost/audits/contract-review-2026-08-20.md`

**Gaps:** Both targets built with zero warnings and zero errors, reflection reported **zero exported public types on each target**, and the `net481` output was confirmed as `ILOnly` with an I386 managed PE header and no `Required32Bit` — which is what supports the AnyCPU classification rather than the evaluated `PlatformTarget` property alone. It establishes the zero-exported-types property only at the pre-implementation baseline; `WDGHOST-VALEVID-20260829-001` re-asserts it at the current source. The `GetPEKind` observation is not repeated by that later record and remains this record's own contribution. No package was produced in this run.

**Supersedes:** none

## WDGHOST-VALEVID-20260821-001

**Requirement IDs:** none

**Version:** 1.0.0

**Commit:** qualifying source `7090e40eed7c6b888ce8da732f21cbe10f1a936c`; materialized package source `db63529cafce11690a18a595e4abc6c0610b9b8e`

**Tree state:** clean at both recorded commits

**Environment:** Windows; local manual coordinated pack flow, later published through the manually dispatched trusted-publication workflow

**Targets:** the three published payload roots

**Command or scenario:** coordinated family release flow recorded in the stable release baseline; qualifying candidate `1.0.0-local.22`

**Execution:** manual

**Evidence level:** automated-runtime

**Result:** PASS

**Artifacts:** `docs/stable-release-1.0.0.md`; `NekoLib.Watchdog.Host.1.0.0.nupkg` SHA-256 `914DD04CC5808BE8E314EE64D4004551F1C9AF26A26E6BAEF69983361BBE7230`

**Gaps:** This record establishes that the Host deployment contract, rather than a compiled API manifest, became a stable baseline within a qualified family release, and that all three payload roots were present and validated. It is release and provenance evidence and satisfies no requirement on its own; the behavioral results it depends on are the campaign records above.

**Supersedes:** none

## WDGHOST-VALEVID-20260828-001

**Requirement IDs:** `WDGHOST-VALREQ-002`, `WDGHOST-VALREQ-004`, `WDGHOST-VALREQ-005`, `WDGHOST-VALREQ-006`

**Version:** `1.1.0-local.8`

**Commit:** `d6f2efdbe99f4a827293cdf4e8ed27c4096d134a`

**Tree state:** clean

**Environment:** Windows; canonical `eng\pack-local.ps1` flow with an isolated NuGet global-packages cache for the consumer probes

**Targets:** `net481` and `net9.0-windows` consumers

**Command or scenario:** `eng\pack-local.ps1 -PackageVersion 1.1.0-local.8`, including the wrapper/transitive, deployment, cleanup, unsupported-RID, and Watchdog Host protocol probes

**Execution:** manual

**Evidence level:** automated-runtime

**Result:** PASS

**Artifacts:** `docs/audit/public-api-documentation-extensibility-review-2026-08-27.md`

**Gaps:** The run published 16 `.nupkg` and 15 `.snupkg` artifacts — the Host being the one main package with no symbol package, which is its documented `IncludeSymbols=false` behavior — and the wrapper/transitive, deployment, cleanup, unsupported-RID, and Host protocol probes all passed. This re-exercises the campaign on a newer candidate at a newer commit, which is why it carries the same layout, ownership, lifecycle, and protocol requirements. It adds nothing about parser rejections, fatal evidence, exit codes, or consumer framework versions, and this candidate was produced for the managed XML-documentation gate, which does not apply to a package with no `lib/` asset.

**Supersedes:** `WDGHOST-VALEVID-20260820-001` for the layout, direct-ownership, deployment-lifecycle, and protocol requirements at this newer commit; that record's focused-regression and byte-identity results stand.

## WDGHOST-VALEVID-20260829-001

**Requirement IDs:** `WDGHOST-VALREQ-001`

**Version:** post-1.0.0 source

**Commit:** `16c217e7daa406ee3992b56ff907604e82313d1c`

**Tree state:** working tree containing this module review's staged documentation changes; no Host source, project, or target file is modified

**Environment:** Windows with the .NET SDK; no package feed, consumer probe, or child-process campaign

**Targets:** `net481` and `net9.0-windows` Host assemblies

**Command or scenario:** `dotnet build src\Watchdog\NekoLib.Watchdog.Host\NekoLib.Watchdog.Host.csproj -c Release -t:Rebuild -m:1`; `src/Tools/NekoLib.PublicApiTool` run against each built managed assembly; `eng\verify-docs.ps1`; `git diff --check` and `git diff --cached --check`

**Execution:** automated

**Evidence level:** build-only

**Result:** PASS

**Artifacts:** this review's staged diff; the generated public-surface files are session-local and are not repository evidence

**Gaps:** Both targets rebuilt with zero warnings and zero errors, and the repository's own public-API generator reported **zero type declarations on each target** — only three assembly-level attributes on `net481` and five on `net9.0-windows`. That re-asserts at the current baseline the property `WDGHOST-VALEVID-20260820-002` established before implementation, and it is what justifies this boundary having no accepted API manifest; `eng/public-api/` was confirmed to contain no `NekoLib.Watchdog.Host` directory. The run also incidentally confirmed the payload-form claim: on `net481` the built `.exe` is itself the loadable managed assembly, while on `net9.0-windows` the `.exe` is a native apphost and the managed code is the sibling `.dll`. This review executed no package, package-consumer, deployment, protocol, runtime, or release run of its own and created no package version, so it satisfies no other requirement; the standing package and protocol evidence remains `WDGHOST-VALEVID-20260828-001` and `WDGHOST-VALEVID-20260820-001`.

**Supersedes:** `WDGHOST-VALEVID-20260820-002` for the zero-exported-types requirement at this newer baseline; that record's `GetPEKind` observation stands.
