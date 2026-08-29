# NekoLib.Core Validation Evidence

**Document ID:** CORE-VALIDATIONS

**Schema version:** 1

**Kind:** reference

**Lifecycle:** current

**Subject:** executed evidence for the NekoLib.Core boundary

**Surface:** validation-evidence

**Boundary:** core

**Authority role:** evidence

**Mutation:** authored

**Indexing:** include

Evidence records what actually ran. Requirements are owned by
[`VALIDATION_REQUIREMENTS.md`](VALIDATION_REQUIREMENTS.md), and historical
package or release evidence is cited with its original provenance rather than
presented as a result of the current documentation campaign.

## CORE-VALEVID-20260817-001

**Requirement IDs:** `CORE-VALREQ-001`, `CORE-VALREQ-002`, `CORE-VALREQ-003`, `CORE-VALREQ-008`, `CORE-VALREQ-009`, `CORE-VALREQ-010`, `CORE-VALREQ-011`, `CORE-VALREQ-012`, `CORE-VALREQ-013`

**Version:** pre-1.0.0 implementation; package candidate recorded separately

**Commit:** `7ae62a23db4c8933f7db2cf783b227df21a59abe` implementation source, reconciled by `c7967e784914b56863a1b2da97cfafecb32ea494`

**Tree state:** clean implementation source for the later immutable package; the audit separately records the review and reconciliation states

**Environment:** Windows; exact host version not recorded in the historical audit

**Targets:** Core `net481`, Core `net9.0`, and the then-current solution target matrix

**Command or scenario:** focused Core tests; post-test incremental Release solution build; complete solution tests; `eng/verify-public-api.ps1 -PackageId NekoLib.Core`; documentation and diff validation

**Execution:** automated

**Evidence level:** automated-runtime

**Result:** PASS

**Artifacts:** [`audits/public-api-review-2026-08-17.md`](audits/public-api-review-2026-08-17.md)

**Gaps:** no standalone runtime scenario ran; package and PackageReference evidence is recorded separately; the historical audit does not preserve the exact OS build or every command-line option

**Supersedes:** none

The focused suite passed 13 tests on each target; both accepted manifests
matched; the solution build reported zero warnings and errors; and 1,310
solution tests passed. Those counts are historical evidence at the cited
baseline, not current repository counts.

## CORE-VALEVID-20260817-002

**Requirement IDs:** `CORE-VALREQ-006`, `CORE-VALREQ-007`

**Version:** `1.0.0-local.16`

**Commit:** `7ae62a23db4c8933f7db2cf783b227df21a59abe`

**Tree state:** clean canonical package source

**Environment:** Windows; canonical local package flow and isolated PackageReference consumers

**Targets:** `net481`, `net9.0`, PackageReference WinForms and WPF consumer target families

**Command or scenario:** canonical `eng/pack-local.ps1` flow for immutable family version `1.0.0-local.16`

**Execution:** automated

**Evidence level:** automated-runtime

**Result:** PARTIAL

**Artifacts:** [`audits/public-api-review-2026-08-17.md`](audits/public-api-review-2026-08-17.md)

**Gaps:** package layout, provenance, marker presence, and consumer restore/build/run passed, but the consumers did not implement every supported Core interface and this candidate predates package-owned XML delivery

**Supersedes:** none

## CORE-VALEVID-20260821-001

**Requirement IDs:** `CORE-VALREQ-003`, `CORE-VALREQ-006`, `CORE-VALREQ-007`, `CORE-VALREQ-012`

**Version:** `1.0.0`

**Commit:** `db63529cafce11690a18a595e4abc6c0610b9b8e`

**Tree state:** clean coordinated release source

**Environment:** Windows package-production host, isolated local consumers, GitHub Release, and NuGet.org trusted publication as recorded by the release owner

**Targets:** `net481`, `net9.0`, and the coordinated family target matrix

**Command or scenario:** canonical package flow and release materialization recorded in the stable release record

**Execution:** automated

**Evidence level:** automated-runtime

**Result:** PARTIAL

**Artifacts:** [`../../stable-release-1.0.0.md`](../../stable-release-1.0.0.md)

**Gaps:** stable package provenance and the Core package hash are recorded, but the release predates package-owned XML documentation and the consumer probes establish representative reachability rather than independent implementations of all eleven interfaces

**Supersedes:** `CORE-VALEVID-20260817-002` for stable release provenance, not for its F1 implementation detail

## CORE-VALEVID-20260828-001

**Requirement IDs:** `CORE-VALREQ-001`, `CORE-VALREQ-002`, `CORE-VALREQ-003`, `CORE-VALREQ-005`, `CORE-VALREQ-013`

**Version:** unreleased XML documentation implementation

**Commit:** `8eea9440391c96a4537edb569d46ef350dc83c37`

**Tree state:** committed documentation source; package delivery recorded separately

**Environment:** Windows; documentation-enabled Release builds

**Targets:** `net481`, `net9.0`

**Command or scenario:** Core Release rebuild with XML output; focused Core suite; Core public API verifier; documentation verifier; source-diff and diff-hygiene checks

**Execution:** automated

**Evidence level:** automated-runtime

**Result:** PASS

**Artifacts:** [`../../audit/public-api-documentation-extensibility-review-2026-08-27.md`](../../audit/public-api-documentation-extensibility-review-2026-08-27.md)

**Gaps:** no package was created by this Core-only gate; runtime and package claims were explicitly deferred

**Supersedes:** none

The build reported zero warnings and errors, both XML files contained the same
109 non-blank documented member entries, the focused suite passed 13 tests on
each target, and both API baselines matched without update.

## CORE-VALEVID-20260828-002

**Requirement IDs:** `CORE-VALREQ-006`, `CORE-VALREQ-007`, `CORE-VALREQ-012`

**Version:** `1.1.0-local.8`

**Commit:** `d6f2efdbe99f4a827293cdf4e8ed27c4096d134a`

**Tree state:** clean canonical package source

**Environment:** Windows canonical package host, isolated PackageReference consumers, and local NuGet feed

**Targets:** Core `net481`, Core `net9.0`, and all coordinated managed package targets

**Command or scenario:** `eng/pack-local.ps1 -PackageVersion 1.1.0-local.8`

**Execution:** automated

**Evidence level:** automated-runtime

**Result:** PASS

**Artifacts:** [`../../audit/public-api-documentation-extensibility-review-2026-08-27.md`](../../audit/public-api-documentation-extensibility-review-2026-08-27.md)

**Gaps:** no interactive UI, long-running soak, external provider, or new public NuGet.org release was claimed; package consumers compile representative Core surfaces but do not implement every interface

**Supersedes:** `CORE-VALEVID-20260817-002` for package-owned XML delivery and current integrated package evidence

The package campaign verified both Core DLL/XML pairs inside the managed
package and after PackageReference extraction. Across the family it verified 30
DLL/XML pairs, all solution tests, all package consumers, and all accepted API
baselines. Those integrated counts remain evidence of the cited package source,
not of the current documentation tree.

## CORE-VALEVID-20260829-001

**Requirement IDs:** `CORE-VALREQ-001`, `CORE-VALREQ-002`, `CORE-VALREQ-003`, `CORE-VALREQ-004`, `CORE-VALREQ-005`, `CORE-VALREQ-008`, `CORE-VALREQ-009`

**Version:** unreleased module-first documentation campaign

**Commit:** starting commit `9e47ff6e4ded1b69b6c20765a7469b107c416e99`; validated working tree before the campaign commit

**Tree state:** dirty only with this Core documentation campaign, its live link updates, and one XML-summary correction; no unrelated staged, unstaged, or untracked input was present at baseline

**Environment:** Windows NT 10.0.26200.0; PowerShell 7.6.4; .NET SDK 10.0.400

**Targets:** `net481`, `net9.0`

**Command or scenario:** `dotnet build src/Core/NekoLib.Core/NekoLib.Core.csproj -f net481`; the same command with `-f net9.0`; `dotnet test tests/NekoLib.Core.Tests/Unit/NekoLib.Core.Tests.Unit.csproj`; `eng/verify-public-api.ps1 -PackageId NekoLib.Core`; XML output inspection

**Execution:** automated

**Evidence level:** automated-runtime

**Result:** PASS

**Artifacts:** generated target assemblies and XML files under ignored `src/Core/NekoLib.Core/bin/`; console results from the current campaign; no durable package artifact created

**Gaps:** no package, PackageReference consumer, full-solution, runtime, interactive, soak, external-provider, hardware, or release gate ran; the focused suite does not independently implement all eleven interfaces or stress concurrent `InspectionProvider.Install` admission and disable-during-install rollback

**Supersedes:** none

Both requested direct builds completed with zero warnings and zero errors. The
focused suite passed 13 tests on `net481` and 13 on `net9.0`, with no failures or
skips. The public API verifier rebuilt Core with zero warnings and errors and
matched both accepted manifests without updating them. Both generated Release
XML files contained 109 member entries and the corrected
`ITelemetrySink` summary.

### Current evidence boundary

Core has no standalone runtime, interactive, provider, transport, hardware, or
soak evidence because it owns none of those implementations. The current
module-first documentation campaign adds source/build/test/API/documentation
evidence after its commands run; it does not create package, package-consumer,
runtime, or release evidence.
