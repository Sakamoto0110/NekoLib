# NekoLib.Http Validation Evidence

**Document ID:** HTTP-VALIDATION-EVIDENCE

**Schema version:** 1

**Kind:** reference

**Lifecycle:** current

**Subject:** executed validation evidence for the NekoLib.Http boundary

**Surface:** validation-evidence

**Boundary:** http

**Authority role:** evidence

**Mutation:** authored

**Indexing:** include

These records curate preserved evidence; they do not re-run it. The source
audit, scenario record, or package record remains the detailed owner.

Deterministic and provider evidence are separate layers. A deterministic record
never establishes provider behavior, and a provider run never establishes a
deterministic contract. No record here contains a key, credential header, or
request or response body.

Requirements with no complete PASS, as of the current baseline:
`HTTP-VALREQ-012` has never been executed, and `HTTP-VALREQ-011` is satisfied
only by two bounded runs against one provider on 2026-08-16 rather than by any
current run.

## HTTP-VALEVID-20260816-001

**Requirement IDs:** `HTTP-VALREQ-001`, `HTTP-VALREQ-003`, `HTTP-VALREQ-004`, `HTTP-VALREQ-005`, `HTTP-VALREQ-013`, `HTTP-VALREQ-014`, `HTTP-VALREQ-016`

**Version:** `1.0.0-local.11`

**Commit:** `ae711fb51d27af29701d332a453912ad1f87a029`

**Tree state:** clean at the recorded implementation commit

**Environment:** Windows deterministic build and test environment plus the canonical local package flow

**Targets:** library `net481`/`net9.0`; deterministic tests `net481`/`net9.0`

**Command or scenario:** Phase G1 closure — deterministic suite with a controlled `HttpMessageHandler`, full solution tests, and the clean package campaign

**Execution:** automated

**Evidence level:** automated-runtime

**Result:** PASS

**Artifacts:** `docs/history/phase-g1-http-integration-2026-08-16.md`; 16/16 deterministic tests on each target with no public internet; 1,281/1,281 full-solution tests serially across both target families; `NekoLib.Http.1.0.0-local.11.nupkg` with `lib/net481` and `lib/net9.0`, recorded commit provenance, and SHA-256 `30464eca19e909a993d6e02e84d20b2cf3cb44b909cde3980ffc03cc44b81c1e`

**Gaps:** This predates the F1 corrections, so its charset, size-bound evidence, and option-validation shapes are not the current contract. No provider request was sent. XML package content was not a gate at this version.

**Supersedes:** none

## HTTP-VALEVID-20260816-002

**Requirement IDs:** `HTTP-VALREQ-011`

**Version:** scenario at implementation commit `ae711fb`

**Commit:** `ae711fb51d27af29701d332a453912ad1f87a029`

**Tree state:** committed scenario source

**Environment:** Windows, no `NEKOLIB_THECATAPI_KEY` present, writable repository artifacts directory

**Targets:** `net481` and `net9.0`

**Command or scenario:** the TheCatAPI scenario invoked with the credential absent

**Execution:** manual

**Evidence level:** automated-runtime

**Result:** PARTIAL

**Artifacts:** `runtime_tests/Http/TheCatApi/README.md` verification record; both invocations exited `3`, recorded that no key was present, sent no provider request, reported zero cleanup problems, and produced sanitized artifacts

**Gaps:** This proves prerequisite handling, artifact finalization, and the no-request boundary. It is explicitly **not** provider evidence and satisfies none of `HTTP-VALREQ-011`'s provider criteria.

**Supersedes:** none

## HTTP-VALEVID-20260816-003

**Requirement IDs:** `HTTP-VALREQ-011`

**Version:** scenario at implementation commit `ae711fb`

**Commit:** `ae711fb51d27af29701d332a453912ad1f87a029`

**Tree state:** committed scenario source

**Environment:** Windows with internet access and a maintainer-owned TheCatAPI key supplied through the environment; the key value is not recorded anywhere

**Targets:** `net481` and `net9.0`

**Command or scenario:** the TheCatAPI scenario executed against the live provider on each target — search, lookup, favourite creation, query by run-owned identifier, deletion, absence confirmation, and reconciled cleanup

**Execution:** manual

**Evidence level:** automated-runtime

**Result:** PASS

**Artifacts:** `runtime_tests/Http/TheCatApi/README.md` verification record; both targets exited `0` with 10/10 checks passed; search and lookup returned HTTP 200 and favourite creation returned HTTP 201; zero cleanup problems with no run-owned favourite remaining; artifacts under `artifacts/validation/http/` were inspected and contain no key, credential header, or body

**Gaps:** This is provider interoperability evidence for the declared flow on 2026-08-16 and nothing more. It does not establish provider uptime, provider policy, behavior beyond these bounded runs, or any transport concern the scenario does not exercise. It also predates the F1 corrections, so it is not evidence for the current charset or size-bound contract. No later provider run has been recorded.

**Supersedes:** none

## HTTP-VALEVID-20260817-001

**Requirement IDs:** `HTTP-VALREQ-001`, `HTTP-VALREQ-002`, `HTTP-VALREQ-003`, `HTTP-VALREQ-004`, `HTTP-VALREQ-005`, `HTTP-VALREQ-006`, `HTTP-VALREQ-007`, `HTTP-VALREQ-008`, `HTTP-VALREQ-009`, `HTTP-VALREQ-010`

**Version:** pre-1.0.0

**Commit:** reviewed at `e845165252c60c9ecff2e90221eac739a1631c68`; accepted implementation at `ea7c47623daa97a28e31e5c0e2825ef385305f2e`

**Tree state:** clean tracked worktree at the recorded implementation commit

**Environment:** Windows deterministic build and test environment

**Targets:** library `net481`/`net9.0`; deterministic tests `net481`/`net9.0`

**Command or scenario:** project builds, the deterministic suite, and the scoped public-API comparison recorded in the F1-HTTP audit and its reconciliation

**Execution:** automated

**Evidence level:** automated-runtime

**Result:** PASS

**Artifacts:** `docs/modules/Http/audits/public-api-review-2026-08-17.md`; both targets built with 0 warnings and 0 errors; 29/29 deterministic tests on each target, up from 16; the API delta was exactly the accepted one — the `protected` endpoint constructor removed and three additive exception properties added — after which the baselines were updated and re-verified; documentation verification and diff hygiene passed

**Gaps:** No external request was sent, no credential was configured, and the TheCatAPI scenario was neither built nor launched — nothing here is provider evidence. All probes used an in-process `HttpMessageHandler`, so no real socket, TLS, proxy, redirect, compression, or HTTP/2 behavior was exercised. No full-solution build or test run and no package or consumer probe was performed in this block. The `net9.0` charset behavior was measured through an unresolvable declared charset rather than a real provider returning `windows-1252`.

**Supersedes:** the proposal-only portions of the F1-HTTP audit snapshot

## HTTP-VALEVID-20260818-001

**Requirement IDs:** `HTTP-VALREQ-013`, `HTTP-VALREQ-014`, `HTTP-VALREQ-016`

**Version:** `1.0.0-local.20`

**Commit:** implementation `ea7c47623daa97a28e31e5c0e2825ef385305f2e`; package source `63785cc8bb801f1d4a90ade6cffb7f0b42c6bc1b`

**Tree state:** clean

**Environment:** Windows canonical local package flow with isolated package-reference probes

**Targets:** the `NekoLib.Http` package within the coordinated family, both target assets

**Command or scenario:** the coordinated clean `1.0.0-local.20` campaign recorded in the F1-HTTP package reconciliation

**Execution:** automated

**Evidence level:** automated-runtime

**Result:** PASS

**Artifacts:** 1,538/1,538 full-solution tests; rebuild with 464 warnings, zero errors, and no new warning identity; `NekoLib.Http.1.0.0-local.20.nupkg` containing `lib/net481/NekoLib.Http.dll` and `lib/net9.0/NekoLib.Http.dll`, declaring `Newtonsoft.Json 13.0.3` in both dependency groups, recording its source commit, with SHA-256 `545833DC1303B32ABF6C4A25FE753B9D8B19CA7555896C426D28F3133A1423D5`; all PackageReference-only consumer, multitarget, package, deployment, publish, and clean probes passed

**Gaps:** The consumer probes restored, built, and ran, but none of them constructed a catalog, sent an endpoint, or materialized a response through the package boundary. No external request was sent and the scenario was not run. XML package content was not a gate at this version.

**Supersedes:** the `1.0.0-local.11` package record previously carried by the module reference

## HTTP-VALEVID-20260821-001

**Requirement IDs:** `HTTP-VALREQ-001`, `HTTP-VALREQ-002`, `HTTP-VALREQ-013`, `HTTP-VALREQ-014`, `HTTP-VALREQ-016`

**Version:** `1.0.0` qualified by `1.0.0-local.22`

**Commit:** `7090e40eed7c6b888ce8da732f21cbe10f1a936c`

**Tree state:** clean qualifying candidate and separately materialized stable packages

**Environment:** Windows canonical package flow and approved release-asset publication

**Targets:** the `NekoLib.Http` package and both target assets within the stable coordinated family

**Command or scenario:** `eng/pack-local.ps1 -PackageVersion 1.0.0-local.22` plus the recorded stable materialization and publication flow

**Execution:** automated

**Evidence level:** automated-runtime

**Result:** PASS

**Artifacts:** `docs/stable-release-1.0.0.md`; stable `NekoLib.Http.1.0.0.nupkg` SHA-256 `06824B22FD43EBF68C225F6B89C16903E4572AE654304D3FC4FCFA8DCB603601`; qualifying `1.0.0-local.22` SHA-256 `2B505F4B426FE0C719256C71AE4FA6F59D8877F99E9447275AD21A9C6A4C208E`

**Gaps:** The release campaign sent no external request, ran no provider scenario, and applied no XML package-content gate.

**Supersedes:** `1.0.0-local.20` as coordinated stable qualification, while preserving its HTTP-specific package evidence

## HTTP-VALEVID-20260828-001

**Requirement IDs:** `HTTP-VALREQ-001`, `HTTP-VALREQ-002`, `HTTP-VALREQ-013`, `HTTP-VALREQ-014`, `HTTP-VALREQ-015`, `HTTP-VALREQ-016`

**Version:** `1.1.0-local.8`

**Commit:** `d6f2efdbe99f4a827293cdf4e8ed27c4096d134a`

**Tree state:** clean

**Environment:** Windows documentation-enabled Release builds and the canonical local package flow with isolated consumers

**Targets:** both `NekoLib.Http` target assemblies

**Command or scenario:** the family documentation campaign builds and `eng/pack-local.ps1 -PackageVersion 1.1.0-local.8`

**Execution:** automated

**Evidence level:** automated-runtime

**Result:** PASS

**Artifacts:** `docs/audit/public-api-documentation-extensibility-review-2026-08-27.md`; the campaign identified `IHttpBodySerializer` as this boundary's supported extension seam and documented its contract; all 1,787 solution tests passed with zero failures and zero skips; every managed package carried both target assemblies and both matching XML files; immutable local-feed packages with aggregate SHA-256 `b56451a1ee8eb7ef4de0d32de143f9488b09f00b25fc300572bcbeb2ee34e9f2`

**Gaps:** This is a local candidate, not a stable release. The campaign's mid-flight scan recorded 53 residual `CS1591` diagnostics for this assembly; the closing state was not separately measured at that time, so this record proves package XML delivery rather than complete member coverage. No external request was sent and the scenario was not run.

**Supersedes:** `1.1.0-local.7`, retained as negative immutable evidence because its managed packages omitted the XML files
## HTTP-VALEVID-20260829-001

**Requirement IDs:** `HTTP-VALREQ-001`, `HTTP-VALREQ-002`, `HTTP-VALREQ-003`, `HTTP-VALREQ-004`, `HTTP-VALREQ-005`, `HTTP-VALREQ-006`, `HTTP-VALREQ-007`, `HTTP-VALREQ-008`, `HTTP-VALREQ-009`, `HTTP-VALREQ-015`

**Version:** unreleased documentation source

**Commit:** working tree based on `2229c36e83bb1ecc5f47aa587903b27a308fcec9`

**Tree state:** authorized module-first documentation working tree before its final commit; no HTTP source change is carried

**Environment:** Windows local checkout, Release configuration, tests run with `-m:1`; no network access was used and no credential was read

**Targets:** library `net481`/`net9.0`; deterministic tests `net481`/`net9.0`

**Command or scenario:** forced `-t:Rebuild` on each target; `eng/verify-public-api.ps1 -PackageId NekoLib.Http -NoBuild`; the deterministic suite in Release on each target; `eng/verify-docs.ps1`; `git diff --check`

**Execution:** automated

**Evidence level:** automated-runtime

**Result:** PASS

**Artifacts:** 0 warnings and 0 errors on both forced rebuilds — and because this project sets no `NoWarn`, `CS1591` was live on both targets, so the assembly carries no undocumented public member; both generated `NekoLib.Http.xml` assets byte-identical across targets at 31,667 bytes; 29/29 deterministic tests passed with 0 failed and 0 skipped on each target, confirming by measurement the executed count previously carried only as an audit figure; both accepted API manifests verified unchanged; documentation verification and diff check passed

**Gaps:** **The TheCatAPI provider scenario was NOT_RUN.** It was neither built nor launched, no external request was sent, and no credential was read, so this record is deterministic evidence only and satisfies nothing in `HTTP-VALREQ-011` or `HTTP-VALREQ-012`. No package candidate, PackageReference consumer, or full-solution regression was run either, leaving `HTTP-VALREQ-013`, `HTTP-VALREQ-014`, and `HTTP-VALREQ-016` untouched by this pass. Member coverage was measured in the working tree; this is not package-delivery evidence.

**Supersedes:** the mid-campaign residual-`CS1591` count recorded in `HTTP-VALEVID-20260828-001`, which remains valid as package-delivery evidence
