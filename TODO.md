# NekoLib Promoted Work

**Document ID:** GLOBAL-TODO

**Schema version:** 1

**Kind:** roadmap/status

**Lifecycle:** current

**Subject:** formally promoted work, execution gates, dependencies, and completion criteria

**Surface:** work-scheduler

**Boundary:** global

**Authority role:** scheduler

**Mutation:** authored

**Indexing:** include

**Last promotion decision:** 2026-11-28

## Purpose and admission rule

This file owns only work that has been formally promoted. It may describe an
accepted implementation scope in enough detail to execute and close it, but it
does not store unpromoted ideas, historical completion narratives, package
hashes, or general product direction.

Use [`ROADMAP.md`](ROADMAP.md) for direction, intentions, guardrails, and
planning horizons. Use [`docs/proposals/`](docs/proposals/README.md) for concise
unpromoted ideas. Completed work moves to [`docs/history/`](docs/history/README.md)
or another appropriate evidence owner.

Promotion does not have to originate in `docs/proposals/`. A proposal, finding,
confirmed issue, audit, external-consumer record, or direct owner decision may
enter this file when all of the following are true:

1. the relevant current authority and evidence have been inspected;
2. the direction and boundary are formalized;
3. the owner has accepted the work intentionally;
4. dependencies, gates, and completion criteria are explicit; and
5. the source decision or evidence is linked without being copied here in full.

Unchecked items are not automatically authorized to start. An entry may retain
an explicit implementation gate after promotion.

## Current promoted work

### 2.0.0 release documentation

For the paired demos, compare `UseIdleTimeout(300_000)` in 1.x with the planned `UseIdleTimeout(TimeSpan.FromMinutes(5))` in 2.0. Show the separate optional session limit with `UseSessionTimeout(TimeSpan.FromMinutes(30))` and verify the syntax against the qualified package.

Prepare the 2.0.0 handoff for three readers: a developer installing NekoLib for the first time, a 1.x maintainer planning an upgrade, and an external reviewer evaluating the project for Codex for Open Source. The publishing decision remains open.

**Two runnable migration demos.** Demo 1 is the baseline application and uses only NekoLib 1.x packages. Demo 2 is that same application migrated to NekoLib 2.0.0. Keep the 1.x version available alongside the migrated version so a reader can run the same scenario with the same inputs in both. Document the exact package versions, restore and launch commands, and expected result for each demo. Link the pair from the root README once available. In the migration walkthrough, show the actual diff and distinguish required upgrade edits from any optional adoption of 2.0 features.

The [Navigation idle fixes](docs/modules/Navigation/ISSUES.md#nav-issue-002) and the [maximum session lifetime](docs/modules/Navigation/VALIDATIONS.md#nav-valevid-20261128-001) passed focused and package-only checks. Include the corrected packages in the next coordinated release qualification.

1. **Prepare onboarding now.** Review the [root README](README.md) from a fresh consumer's perspective. Within the first few sections a reader should be able to identify a supported target, install the appropriate package and follow a small Navigation example to its expected result. Verify the example in a package-only consumer once the final candidate is available. Give existing users a direct route to the [four QueryBuilder migrations](docs/modules/Data/migrations/querybuilder-structured-api.md). Use the [family changelog](CHANGELOG.md#unreleased) for the short version summary.
2. **Release notes.** Keep [the 2.0.0 Markdown file](docs/release-notes-2.0.0.md) as the versioned account of migration, impact and limitations at release time. On publication, use a short GitHub Release summary with a tag-pinned link to that file. Keep the 2.0.0 account intact when a later 2.x release fixes an issue.
3. **Reconcile the last open failure.** The owner reports one failure remaining. The current notes still list five observations from candidate `b418aa6`. Match those observations to the subsequent fixes and evidence, identify the remaining failure by its record and capture its consumer impact. Keep the earlier candidate's judgment at its original baseline. Update the release summary when each disposition is traceable. For the session timeout, use the API, defaults and results supplied by the implementation work above.
4. **Verify the candidate documentation.** Check migration signatures against the accepted Data API changes, links and anchors against the release tree, and validate with `eng/verify-docs.ps1`. The root README is embedded in each main NuGet package. After the documentation baseline is approved and packed, compare the embedded README bytes in all 16 main archives with that baseline and record the resulting package hashes.
5. **Close on actual distribution.** Keep the candidate status and independent qualification result visible until the release decision. At publication, update the README, notes and changelog together to reflect the actual version and date. Record the approved source commit, package hashes, release decision and subsequent public-download checks in the global release record.

**External review handoff.** Use the versioned notes and the global release record as the starting points for a short project submission. Explain what NekoLib does for an unattended terminal, why 2.0 requires a migration, which regressions the delivered packages cover, and what work remains in 2.x. Link to repository-visible tests, validation records and the [roadmap](ROADMAP.md). An external reader must be able to follow those links without access to a local artifacts directory. Prepare the narrative now and insert final tag, distribution links and verified figures after qualification.

### Other promoted work

`WDG-UIHANG-001` completed with the coordinated 1.4.0 release. Its
[completion history](docs/history/watchdog-ui-hang-2026-11-24.md) routes the
delivered policy, public evidence and remaining boundaries.

`WDG-AUTOBOOT-001` completed with the coordinated 1.3.0 release. Its
[completion record](docs/history/watchdog-auto-bootstrap-2026-11-24.md) owns
the integrated scope, published evidence and remaining boundaries.

`NEKOMKT-F026` implementation and local validation completed on 2026-11-27
for the unreleased `2.0.0` family. The owner extended the original Join scope
to all four QueryBuilder overloads deprecated since `1.1.0`. The
[completion history](docs/modules/Data/HISTORY.md)
preserves the decision and scope. The
[validation record](docs/modules/Data/VALIDATIONS.md#data-valevid-20261128-001)
records solution tests, API review, disposable packages and four package-only
desktop consumers. Expanded regression qualification completed on 2026-11-28
with all eight legacy-call compiler checks and the complete 168-case
recovery/bootstrap matrix. Public release qualification and publication remain
pending.

`NEKOMKT-F009` completed on 2026-08-28. Its five-family documentation campaign,
package-delivery correction, immutable package evidence, consumer validation,
and closure are preserved in the historical
[`public API documentation and extensibility review`](docs/audit/public-api-documentation-extensibility-review-2026-08-27.md).

`DATA-ADAPT-QB-001` completed on 2026-08-27. Its accepted decisions,
implementation reconciliation, provider/package evidence, and closure are
preserved in the historical
[`Data type-adaptation and QueryBuilder review`](docs/modules/Data/audits/type-adaptation-querybuilder-api-review-2026-08-26.md).

Items in [`docs/proposals/`](docs/proposals/README.md), module findings or
issues, audit recommendations, and [`ROADMAP.md`](ROADMAP.md) intentions remain
outside this scheduler until their own formal promotion.
