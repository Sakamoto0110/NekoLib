# NekoLib 1.1.0 Stable Family Release

**Kind:** reference

**Lifecycle:** current

**Subject:** first compatible minor of the coordinated stable family

**Declaration date:** 2026-08-31

**Qualifying implementation commit:**
`d1bf43a89232bb2b2626009248f1a511ed5ae854`

**Qualifying package candidate:** `1.1.0-local.9`

**Materialized release source commit:** pending qualification of the clean
release-boundary commit that introduces this record

**Materialized package version:** `1.1.0`

## Declaration boundary

NekoLib `1.1.0` is the approved first compatible minor after the coordinated
`1.0.0` stable baseline. The SemVer-minor justification is the additive stable
`NekoLib.Data` surface: structured QueryBuilder operations, warning-only legacy
overloads, explicit write-side promotion and provider decay, and exact DTO
temporal materialization rules.

The release also carries the compatible Logging flush-budget correction and
package-owned managed XML documentation across all 15 library packages. It
does not remove a stable API, change the warning-only QueryBuilder compatibility
window, open `2.0.0`, promote `NEKOEXP0001`, or change the Watchdog Host protocol
or deployment topology.

The qualifying implementation commit contains the intended product, public API,
XML documentation, module-first documentation, indexing, and packaging inputs.
The administrative release-boundary commit reconciles the changelogs and this
record before any immutable candidate or stable artifact is produced. Its exact
commit is recorded here only after the clean package flow proves that source.

## Compatibility and migration

| Area | Classification | Consumer action |
|---|---|---|
| Structured QueryBuilder | additive stable API plus warning-only deprecations | Move new code to `Value`, `Set`, structured `Where`, `JoinOn`, and the explicitly trusted variants; existing overloads continue to compile in `1.1.0` |
| Data type adaptation | additive stable API and explicit behavioral policy | Configure exact promotion, decay, loss, schema, and DTO-property rules where adaptation is required |
| Logging flush admission | compatible behavioral correction | none |
| Managed XML assets | documentation and packaging | none; IDEs receive the package-owned XML beside each target assembly |
| Watchdog Host | coordinated release only | Upgrade `NekoLib.Watchdog` and `NekoLib.Watchdog.Host` together |

Migration guidance:

- [Structured QueryBuilder migration](modules/Data/migrations/querybuilder-structured-api.md)
- [Data type-adaptation migration](modules/Data/migrations/data-type-adaptation.md)

The 30 accepted compiled API baselines are not updated by the release operation.
They already contain the accepted additive Data surface and warning-only
`Obsolete` markers. Qualification must fail on any unexplained difference.

## Qualification plan and evidence boundary

The complete gate is executed on Windows against one clean source commit:

1. complete Release build and solution tests;
2. warning-identity comparison through `eng/verify-docs.ps1` with the captured
   rebuild log;
3. all 30 accepted public API manifests;
4. documentation, skills, and diff verification;
5. immutable `1.1.0-local.9` through the complete canonical package flow;
6. exact stable `1.1.0` through the same flow;
7. isolated PackageReference consumers and Watchdog Host package/runtime probes;
8. individual and aggregate SHA-256 recording before any remote publication.

| Evidence layer | Current state before qualification |
|---|---|
| Source and documentation | frozen at qualifying implementation commit; administrative release commit pending |
| Build and warnings | pending on the release source commit |
| Solution tests | pending on the release source commit |
| Compiled public API | pending no-diff verification on the release source commit |
| Package and managed XML | pending `1.1.0-local.9` and exact `1.1.0` |
| PackageReference consumers | pending canonical package probes |
| Watchdog Host deployment/runtime | pending package-owned topology, negative, protocol, startup, and stop probes |
| Interactive/native hardware/provider/soak | not added by this release gate; prior evidence remains scoped to its recorded source |
| Git, NuGet.org, and GitHub Release | pending explicit publication stages |

No pending row is release evidence. This record is updated with commands,
counts, hashes, source commits, workflow run, public-feed verification, and
unexecuted layers only after each gate completes.

## Publication boundary

Remote publication uses the manual-only
`.github/workflows/publish-nuget.yml` trusted-publication transport. The workflow
must be rebound to the immutable `v1.1.0` draft release ID and aggregate hash,
must reject any asset-set mismatch before requesting an OIDC credential, and
must be dispatched explicitly from `master` with publication confirmed.

The annotated `v1.1.0` tag, branch push, draft release, workflow dispatch,
NuGet.org publication, independent public download, and final GitHub Release are
distinct evidence stages. Completion of an earlier stage does not imply a later
one.
