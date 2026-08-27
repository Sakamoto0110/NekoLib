# NekoMarketplace External Consumer Evidence Intake — 2026-08-26

**Document ID:** GLOBAL-AUDIT-NEKOMARKETPLACE-100-EVIDENCE-20260826

**Schema version:** 1

**Kind:** audit

**Lifecycle:** historical

**Subject:** NekoMarketplace E2E evidence intake for the published NekoLib 1.0.0 package family

**Surface:** audit

**Boundary:** global

**Authority role:** evidence

**Mutation:** snapshot

**Indexing:** include

**Status:** intake complete; P-001 and QueryBuilder correction reconciled, remaining records pending

**Reference date:** 2026-08-26

**Reference commit:** `0fa1a321c85c541cc3e32c39e5607de881032b5a`

**Last reconciliation:** 2026-08-27

**Current state:** P-001 and F-003/NOTE-012/NOTE-013 are implemented and reconciled through the completed Data decision; every other record remains historical and unpromoted

## Purpose and authority

This audit preserves the semantic intake of an external NekoMarketplace E2E
experiment that consumed published NekoLib `1.0.0` packages. It records what
the external corpus observed, inferred, corrected, executed, and worked around
without treating any of those records as the current NekoLib contract.

The corpus is valuable because it exercised the framework as a real consumer,
including both supported target families, multiple database providers, native
UI, device emulators, observability composition, crash evidence, process
supervision, deployment payloads, and a live HTTP provider. It is not a current
source review, an accepted design decision, a module finding registry, an issue
registry, or a roadmap.

Authority remains with the repository owners defined by the
[documentation policy](../governance/documentation-policy.md): current source
for implemented behavior, compiled assemblies for actual public API, accepted
manifests for reviewed API, current module references for documented contracts,
and [`TODO.md`](../../TODO.md) for accepted scheduled work. This audit may supply
leads and bounded historical evidence to later module campaigns only.

The promotion boundary is:

```text
external evidence intake
    -> current source/API/documentation/package reconciliation
    -> accepted decision
    -> TODO.md
    -> implementation
    -> current technical documentation
```

No step after intake occurred here.

## Repository and corpus baselines

### NekoLib intake baseline

- Branch: `master`.
- Reference commit: `0fa1a321c85c541cc3e32c39e5607de881032b5a`.
- Upstream divergence at entry: 0 behind, 1 ahead.
- Index and worktree at entry: clean.
- Coverage: committed `HEAD` only as the repository location receiving this
  audit. Product source, tests, compiled API, packages, and current technical
  references were not reconciled against the external claims.

The reference commit identifies the NekoLib state at intake. It does not imply
that the external experiment ran against source built from that commit.

### External corpus baseline

- Exact path at intake:
  `C:\Users\Sakamoto\dev\experiments\NekoMarketplace\Project_scope\`.
- Physical shape: 15 files in one flat directory, 14 Markdown and one HTML.
- Published framework baseline exercised by the experiment: NekoLib `1.0.0`.
- Latest observed experiment checkpoint: `CP-048`.
- Reported phase state: P0-P9 complete, P10 complete, P11 in progress after
  slice 1 and a between-slices regression correction.
- Exact NekoMarketplace commit corresponding to `CP-048`: not recorded in the
  corpus.
- External tree state and upstream divergence: not established by this intake.

The corpus therefore has a usable package/version and checkpoint baseline, but
not a commit-grade external source baseline.

## Gate authorization and exclusions

Gate A authorized a physical inventory without content access. Gate B
authorized read-only semantic intake of the exact external corpus. Gate C
authorized creation of this historical audit and its indispensable index
entries.

Gate C did not authorize:

- product, test, project, package, API-manifest, skill, or runtime-scenario
  changes;
- edits to `TODO.md`, a current technical reference, a module `FINDINGS.md`, a
  module `ISSUES.md`, or a module `BACKLOG.md`;
- confirmation, rejection, scheduling, or implementation of an external
  finding;
- reopening the external experiment or changing its records;
- reading ignored or external material beyond the already authorized corpus;
- package creation, build, test, runtime, provider, device, UI, deployment,
  commit, or push operations.

`PAGE_MAP.html` was not opened during semantic intake because `PAGES.md` is the
corpus-declared canonical textual design and no classification required visual
inspection of the generated map.

## Corpus authority model

The corpus defines its own authority order. This audit preserves that order
only for understanding the experiment; it does not place any corpus file above
the NekoLib repository authorities.

| Corpus surface | Corpus role | Intake treatment |
|---|---|---|
| `PROMPT.md` | Original E2E assignment plus owner-approved amendments | Authority for what NekoMarketplace was supposed to build and validate. |
| `SCOPE.md` | Accepted business scope | Authority for the consumer experiment boundary. |
| `STATE.md` | Resume pointer | Operational summary; known to lag `CP-048`. |
| `ROADMAP.md` | Consumer decisions and phases | Decision evidence for NekoMarketplace, not NekoLib product authority. |
| `CHECKPOINTS.md` | Append-only execution ledger | Primary chronology and correction evidence. |
| `DATABASES.md`, `PAGES.md`, `EMULATORS.md` | Consumer technical designs | Authority for the consumer's own design only. |
| `VALIDATION.md` | Required, executed, missing, and superseded evidence | Bounded evidence; it never creates a NekoLib contract. |
| `FINDINGS.md` | Curated framework DX report | External leads requiring current reconciliation. |
| `PROPOSALS.md` | Speculative design ideas | Unpromoted and non-authoritative. |
| `CLAUDE_THOUGHTS.md` | Append-only notebook | Provenance and interpretation history, explicitly non-authoritative. |
| `REFERENCES.md` | External and internal reference ledger | Provenance aid; some entries lag later execution. |
| `HANDOFF.md` | Rolling continuity summary | Useful routing surface; several enumerations are stale. |
| `PAGE_MAP.html` | Generated visual page map | Secondary to `PAGES.md`; not inspected in Gate B. |

## Inspection coverage

Read in full during Gate B:

- `PROMPT.md`;
- `SCOPE.md`;
- `FINDINGS.md`;
- `VALIDATION.md`;
- `PROPOSALS.md`; and
- `STATE.md`.

Read selectively for provenance, corrections, supersession, and routing:

- `CLAUDE_THOUGHTS.md`;
- `CHECKPOINTS.md`;
- `ROADMAP.md`;
- `HANDOFF.md`;
- `REFERENCES.md`;
- `DATABASES.md`;
- `PAGES.md`; and
- `EMULATORS.md`.

The selective pass covered every note, checkpoint, decision, and reference
needed to classify the 28 external findings, the one proposal, the 17
validation entries, the source-inspection boundary, the owner corrections, and
the material supersession chains. It was not a complete editorial review of
every secondary paragraph.

## Epistemic model

The intake keeps seven kinds of statement separate:

| Layer | Meaning |
|---|---|
| Observation | A public API, package, process, probe, error, rendered UI, or file produced the stated result. |
| Claude interpretation | Claude assigned intent, mechanism, severity, or design meaning to an observation. |
| Owner correction | The framework or experiment owner corrected that interpretation or the consumer premise. |
| Executed evidence | A recorded command, test, restore, provider probe, runtime, interactive run, or crash artifact produced a result. |
| Inference | A plausible explanation not directly demonstrated by the recorded evidence. |
| Consumer workaround | NekoMarketplace changed its own code, deployment, ACLs, or protocol to proceed. |
| Source-inspection effect | Knowledge was obtained from NekoLib source rather than the published consumer contract. |

A workaround does not resolve a possible NekoLib problem. A later correction
does not erase the earlier observation. An executed scenario proves only the
boundary and conditions it actually exercised.

## Experiment chronology

- P0-P9 closed at `CP-037` and tag `p9-complete` with 25 external findings,
  325 tests on each target family, and two NekoLib source reads.
- P10 owner-polish work added `F-026` and `F-027`.
- P11 crash evidence added `F-028`.
- `CP-045` records the owner's ACL mitigation for the minidump boundary and the
  discovery of `F-028`.
- `CP-046` corrects an earlier explanation that Docker had failed; the machine
  had been turned off.
- `CP-047` replaces the consumer premise that board LEDs mirror occupancy with
  the owner's correction that they actuate physical hardware.
- `CP-048`, later than `STATE.md`, records a noisy startup-clear regression
  found through a real application run.

The corpus currently contains 28 findings. Statements that say there are 25
belong to the valid P9 snapshot and must not be used as the final count.

## NekoLib source-inspection boundary

The experiment was black-box-first, but not pure black box. The corpus records
two NekoLib source reads.

### First source read — translator obligations

- Provenance: `NOTE-008`.
- Pre-inspection boundary: the public package and available documentation
  exposed the translator seam but did not establish defensive-copy, null,
  command-policy, or `Top` semantics sufficiently for a consumer translator.
- Source opened: working-tree `Translators.cs`.
- Reason: the consumer needed to implement the PostgreSQL translator without
  guessing shipped behavior.
- Effect: informed external findings `F-002` and `F-008` and influenced the
  consumer translator.
- Limitation: the source read was not established as source for the published
  `1.0.0` package.

### Second source read — QueryBuilder call shapes

- Provenance: `NOTE-012` and `NOTE-013`.
- Pre-inspection boundary: a consumer call compiled into the wrong semantic
  shape, and the framework owner asked whether QueryBuilder was consistent.
- Source opened: working-tree `QueryBuilder.cs`.
- Observation: different QueryBuilder operations exposed inconsistent call
  shapes.
- Initial Claude interpretation: the differences appeared deliberate and
  represented separate usage tiers.
- Owner correction: consistency was a design premise, so the inconsistency was
  a defect rather than intentional design.
- Effect: informed `F-003`.
- Limitation: the source read was not tied to the package source baseline.

Later source reads of NekoPcbEmulator are external-emulator evidence, not
NekoLib source inspection. The corpus phrase "one source read by request" means
one of the two reads was explicitly requested; it does not mean only one read
occurred.

## QueryBuilder correction chain

The QueryBuilder chain is retained verbatim in semantic order because it is a
representative governance case:

1. The consumer observed inconsistent call shapes capable of binding valid
   arguments to the wrong semantic slots.
2. Claude inspected the source and initially interpreted the shapes as
   deliberate.
3. The framework owner corrected the intent claim: consistency was required,
   and the observed inconsistency was a defect.
4. `NOTE-015` records Claude independently introducing the same class of
   overload/`params` trap in NekoMarketplace. One call failed loudly; another
   silently dropped the first migration statement until a clean database
   exposed it.
5. The consumer incident is corroborating ergonomics evidence, not proof of
   the current NekoLib implementation.

The technical observation survives. The "deliberate" interpretation is
historical and superseded. The owner's correction is an accepted statement
about original intent, but `F-003` still requires current source/API/test/package
reconciliation before it can become a NekoLib issue.

## External findings intake

The IDs below are preserved as external aliases. They are not NekoLib module
record IDs, are not inserted into module registries, and have not been promoted
to issues or `TODO.md`.

| External ID | Corpus classification | Primary route | Evidence character | Gate C disposition |
|---|---|---|---|---|
| F-001 | positive | Data | Public API plus successful custom-translator use | Preserve as validation/reference lead; confirm the current extension contract. |
| F-002 | gap | Data | Documentation blockage plus first source read | Reconcile translator obligations before deciding whether this is documentation, reference, or API work. |
| F-003 | defect; owner acknowledged | Data | Source observation, consumer trap, and owner correction | Priority issue candidate after current reconciliation; preserve the superseded intent interpretation. |
| F-004 | positive | Navigation; Navigation.WinForms related | Reflection, registration, and runtime use | Preserve as behavior-to-retain evidence; verify current attribute contract. |
| F-005 | context | Global compatibility; Data related | `net481` provider ecosystem | Compatibility context, not a framework defect. |
| F-006 | positive | Watchdog.Host | Package/deployment execution and fail-loud behavior | Preserve as deployment-package evidence. |
| F-007 | informational | Global package validation | Local-feed provenance observation | Candidate input to package/cold-restore requirements, not a product issue. |
| F-008 | latent | Data | Source-inspected `Top`/`Limit` construction concern | Reproduce against current code and decide between validation, normalization, or documentation. |
| F-009 | defect; corpus says owner acknowledged | Global documentation/package distribution | Package inspection found XML docs authored but absent from packages | Reconcile current package outputs. The inspected provenance contains the acknowledgement claim in `HANDOFF.md`, but not the originating owner statement. |
| F-010 | positive | Http | Public API and live external provider | Preserve as external-provider evidence for catalog and raw-response behavior. |
| F-011 | positive | Data | Real Access gateway probe correcting an earlier diagnosis | Preserve as provider evidence; retain the sub-second correction. |
| F-012 | defect | Data | Executed OleDb binding-mode matrix | Priority issue candidate after current reproduction; consumer workaround was `Named` with `?`. |
| F-013 | defect | Navigation | Real bootstrap exception after registry-level tests passed | Reconcile attribute naming, registry/bootstrap validation, global idle semantics, and current documentation. |
| F-014 | gap | Navigation; Navigation.WinForms related | Public API reflection and real bootstrap composition | Reconcile supported service-resolution/factory path; consumer retained `NavigationContext` in its shell. |
| F-015 | positive | Data | Reflected API and real SQL Server transaction | Preserve as `DbSession` transaction-seam evidence. |
| F-016 | latent | Data | Live `RecordItem` probe; conversion mechanism only inferred | High-priority finding candidate covering silent Guid fallback and the related raw-read overload gap. |
| F-017 | positive | Diagnostics; Core, Logging, Telemetry, and Inspection related | Public API reflection and crash-evidence composition | Preserve once as cross-boundary evidence; do not duplicate it into five authorities. |
| F-018 | positive | Inspection | Compiler diagnostic on experimental actions | Preserve as evidence that experimental status reaches consumers. |
| F-019 | context | Mvvm; WinForms related | Public API and actual consumer use | Compatibility context: `ViewModelBase` composed naturally; `ICommand` lacked a native WinForms host. |
| F-020 | latent compound record | Devices | Protocol probes, owner clarification, and live encoding correction | Split during reconciliation: `ERR`/`Success` ambiguity is owner acknowledged; lossy ASCII default is separate and remains open. |
| F-021 | positive | Devices | Two real wire formats through one engine | Preserve as transport/protocol evidence; exclude superseded occupancy semantics. |
| F-022 | gap | Diagnostics.Windows; Diagnostics related | A secret was observed in a minidump | Security/documentation boundary candidate. Consumer ACL restriction is mitigation, not framework resolution. |
| F-023 | positive | Watchdog; Diagnostics related | Real watchdog bundle | Preserve evidence that the application controls supplied evidence; reconcile current default bundle expectations. |
| F-024 | gap | Watchdog and Watchdog.Host | Public API plus real startup/handshake | Reconcile timing, caller, handoff, and configuration boundaries. |
| F-025 | gap | Watchdog and Watchdog.Host | API and runtime control probe | Reconcile whether external deployment control belongs in the supported contract; consumer owns its deliberate-shutdown policy. |
| F-026 | gap | Data | Compiled call against the declared Join signature | Documentation/API-ergonomics candidate, not a defect claim: implementation followed the published SQL-inverted argument order. |
| F-027 | gap | Navigation.WinForms; Navigation related | Screenshot and real native-control surface | Design/backlog candidate requiring a decision about modal affordance, theming, focus, and accessibility ownership. |
| F-028 | defect | Devices | Crash bundle and focused reproduction | Priority issue candidate: engine timeout and caller cancellation were indistinguishable; consumer token guard is only a workaround. |

### Priority is not confirmation

For later reconciliation ordering only, the intake identifies these higher-risk
records: `F-003`, `F-009`, `F-012`, `F-016`, the two separable halves of
`F-020`, `F-022`, `F-024`, `F-025`, and `F-028`.

This ordering reflects external impact and failure opacity. It does not assert
that the records remain present at the NekoLib reference commit.

## Proposal P-001 — author intent disputed

`P-001`, "Decay in policies", is preserved as an unpromoted external proposal
with disputed author fidelity.

The only safe owner-origin statement asks for policies that can have decay,
including for text, with an example resembling `DD/MM hh:mm:ss`. Claude then
formalized the idea into a separation between decay and scheduling, proposed a
new structured `Decay` type, preferred ISO 8601, and rejected alternatives.
The owner has since stated that this formalization did not fully capture the
intended idea.

Consequently:

- the original owner statement and provenance are retained;
- Claude's detailed model remains attributed interpretation, not owner intent;
- no NekoLib boundary is assigned authoritatively;
- no backlog, finding, issue, roadmap, API, or documentation record is created;
  and
- no technical investigation should begin until intent is clarified.

Required owner clarification:

1. whether decay means relative duration, absolute expiry, recurring schedule,
   or more than one concept;
2. whether the intended attachment surface is `DbCommandPolicy`, a generic
   policy, a cache policy, Data, or Core;
3. whether the textual date/time format was a requirement, an input example,
   or only an illustration;
4. whether a structured type, text input, or both are intended; and
5. which alternatives and rejections, if any, actually belong to the owner's
   proposal.

## Material note chains

| Notes | Material effect |
|---|---|
| NOTE-008 | First NekoLib source read; translator contract blockage; affects F-002/F-008. |
| NOTE-012 to NOTE-013 | QueryBuilder observation, superseded Claude intent interpretation, and owner correction; affects F-003. |
| NOTE-014 | Package inspection underlying F-009. |
| NOTE-016 to NOTE-023 | Replaces the DateTime-binding diagnosis with the sub-second boundary; produces positive F-011 and separates F-012. |
| NOTE-020 to NOTE-021 | P-001 origin and proposal-governance history; detailed semantics now disputed. |
| NOTE-022 | Records that an `.md`-only check missed `PAGE_MAP.html`; `PAGES.md` remains canonical. |
| NOTE-024 | Executed binding-mode matrix underlying F-012. |
| NOTE-025, NOTE-026, NOTE-029 | Bootstrap/idle/service-locator evidence underlying F-013/F-014. |
| NOTE-030 | Transaction-seam evidence underlying F-015. |
| NOTE-031 to NOTE-032 | Dynamic-record conversion and overload evidence underlying F-016. |
| NOTE-036 | Cross-cutting composition evidence underlying F-017/F-018/F-019. |
| NOTE-038 | Emulator release-provenance concern; external to NekoLib. |
| NOTE-039 to NOTE-048 | Devices transport/protocol/encoding chain; UTF-8 assumption later disproved by Latin-1 execution. |
| NOTE-052 and NOTE-054 | Watchdog, minidump, bootstrap, and control evidence underlying F-022 to F-025. |
| NOTE-060 | Corrects P9 classification counts; predates F-026 to F-028. |
| NOTE-061, NOTE-064, NOTE-068 | Evidence underlying F-026, F-027, and F-028. |
| NOTE-069 to NOTE-070 | Owner correction and command evidence for physical LED actuation. |
| NOTE-071 | Separates confirmed F-028 from an unconfirmed hung-connection hypothesis. |
| NOTE-072 | Consumer startup/logging regression; not a NekoLib finding. |

Other notes about consumer migrations, environment isolation, image races,
Access contention, UI layout, container availability, and consumer composition
remain useful method history. They are external-only and are not routed to
NekoLib product records.

## Validation evidence intake

The external `V-###` IDs remain aliases. They are not converted to module
`*-VALEVID-*` records because their required commit, tree-state, artifact, and
current-boundary reconciliation is incomplete.

| External ID | Evidence classification | Intake disposition |
|---|---|---|
| V-001 | Partial package/restore evidence | Historical and superseded by V-017. |
| V-002 | Partial build evidence | Historical only; not a current build baseline. |
| V-003 | Remote infrastructure readiness | External-only; does not prove framework behavior. |
| V-004 | Real PostgreSQL translator and schema integration | Strong external-provider evidence for Data, bounded to the recorded package and environment. |
| V-005 | SQL Server schema and idempotency | Primarily consumer behavior; retain exact gateway integration evidence only. |
| V-006 | Access schema, geometry, and authentication | Primarily consumer behavior; retain exact provider integration evidence only. |
| V-007 | Real Cat API client/provider execution | Strong external-provider evidence for Http. |
| V-008 | Pull ledger, acceptance, and cache | Consumer-domain evidence; do not infer framework contracts. |
| V-009 | Outbox drain | Consumer sync evidence; Data transaction use is separately represented by F-015. |
| V-010 | Sync worker | Consumer runtime evidence. |
| V-011 | Nine required failure modes | Strong E2E consumer coverage; not a NekoLib requirement set. |
| V-012 | Logging, Telemetry, Inspection, Diagnostics, and Mvvm composition | Relevant cross-module external evidence for F-017 to F-019. |
| V-013 | Devices against both emulator modes | Real transport/protocol evidence, partially superseded by D-017/CP-047 on occupancy meaning. |
| V-014 | Watchdog/Host on both targets | Strong supervisor/deployment evidence; no forced kill occurred during a live drain. |
| V-015 | P9 evidence map | Its interactive marketplace-page gap is superseded by V-016. |
| V-016 | Real marketplace page plus one authorized live Cat API run | Closes the V-015 interaction gap; predominantly external E2E evidence. |
| V-017 | Cold NuGet.org restore | Strong package-consumer evidence: 109 of 109 packages restored; supersedes V-001. |

The recorded residual gaps include:

- no forced process kill in the middle of an active outbox drain;
- an intermittent timing-sensitive test history that required trace-based
  diagnosis; and
- a direct Pipes reference present in the consumer graph without meaningful
  direct consumer usage.

Build, test, provider, protocol, interactive UI, crash, process, deployment,
and package-feed observations remain separate evidence claims.

## Material checkpoints and decisions

### Checkpoints

| Checkpoint | Intake significance |
|---|---|
| CP-037 | P9 closure: 25 findings at that time, two NekoLib source reads, and no framework modification. |
| CP-045 | Owner chose consumer ACL mitigation for F-022 and real crash evidence revealed F-028. |
| CP-046 | Corrects the Docker-failure narrative and records a full-suite rerun after infrastructure returned. |
| CP-047 | Owner replaces occupancy-display semantics with physical board actuation; V-013 needs correction rather than deletion. |
| CP-048 | Latest checkpoint; a real app run exposed noisy startup clear after CP-047. |

### Decisions

| Decision | Intake significance |
|---|---|
| D-004 | Hybrid database topology deliberately exercised the PostgreSQL translator. |
| D-011 | Consumer-owned protocols use shipped NekoLib transports and engine. |
| D-012 | UTF-8 assumption was temporary and later disproved by live Latin-1 evidence. |
| D-013 | Occupancy-mirroring model; retained as a superseded consumer decision. |
| D-014 | Supervise consumer processes, prefer graceful shutdown, and accept the Host's bounded behavior. |
| D-015 | Deliberate shutdown releases supervision; other process exits restart. |
| D-016 | Board-first actuation model. |
| D-017 | Owner correction: LEDs actuate hardware rather than represent occupancy. |
| D-018 | Locker interaction/state-machine direction during P11. |

These decisions own NekoMarketplace behavior only. They may explain how a
framework API was exercised, but they are not NekoLib design decisions.

## Corrections and supersession map

| Earlier record or statement | Later controlling corpus state |
|---|---|
| P9 summaries say 25 findings | Correct at CP-037; the current corpus contains 28 after P10/P11. |
| `STATE.md` ends at CP-047 | CP-048 exists and is later. |
| `VALIDATION.md` header says P0-P8 | The body includes P9/P10/P11-era V-015 to V-017 and corrections. |
| `FINDINGS.md` footer says P9 is still to come | P9 and P10 are complete and P11 is in progress. |
| `REFERENCES.md` says the emulator release has not been fetched | V-013 and later checkpoints record actual release execution. |
| NOTE-012 says QueryBuilder inconsistency may be deliberate | NOTE-013 records the owner's defect correction. |
| NOTE-016 attributes Access DateTime failure to binding | NOTE-023 demonstrates that the sub-second fraction was causal. |
| D-012/NOTE-045 temporarily assume UTF-8 | NOTE-048 records live Latin-1 behavior. |
| D-013 and early V-013 treat LEDs as occupancy display | D-017 and CP-047 replace that premise with physical actuation. |
| V-015 records a real-page interaction gap | V-016 closes it. |
| V-001 is partial restore evidence | V-017 supersedes it with a cold restore. |
| A narrative says Docker fell during the session | CP-046 records that the machine had been off. |
| One source read is described as owner-requested | Two source reads occurred; one was owner-requested. |

Historical rows must remain readable through these explicit links. A later
reconciliation must append outcomes rather than rewrite this intake as though
the external corpus had always been internally consistent.

## Positive evidence to preserve

Ten external findings describe behavior that worked and may identify useful
contracts to preserve if current reconciliation confirms them:

- `F-001`: custom Data translator seam;
- `F-004`: Navigation attribute support;
- `F-006`: Watchdog.Host fail-loud deployment behavior;
- `F-010`: Http provider composition and raw response evidence;
- `F-011`: Access through `DatabaseGateway`;
- `F-015`: transaction composition through `DbSession`;
- `F-017`: direct observability-source composition into crash evidence;
- `F-018`: compiler-visible experimental Inspection actions;
- `F-021`: two wire formats through one hardware engine; and
- `F-023`: application-controlled evidence supplied to Watchdog.

Positive findings are not automatically current reference text. They remain
package-baseline evidence until reconciled.

## Cross-cutting patterns

### Documentation distribution amplified ambiguity

`F-009` is a potential root contributor rather than proof that every related
record is a documentation defect. When XML documentation was unavailable in
the package, consumers relied on type names, enum names, signatures, exception
messages, reflection, and eventually source inspection. That amplified the
discoverability impact of `F-002`, `F-003`, `F-012`, `F-013`, `F-014`, `F-019`,
`F-020`, `F-022`, `F-024`, `F-025`, `F-026`, and `F-027`.

### One signal represented more than one semantic layer

Several external records concern a name or result that collapsed distinct
meanings:

- OleDb binds positionally, while `DbParameterBindingMode` validation appeared
  to reason about names;
- `[PageTimeout]` appeared per-page while the runtime enforced global idle
  behavior;
- `HardwareResponse.Success` represented transport lifecycle rather than
  device-protocol success;
- crash-text redaction did not define minidump content;
- bootstrap success did not expose all Host configuration or timing; and
- timeout and caller cancellation produced indistinguishable cancellation.

This is a reconciliation hypothesis, not a cross-module design verdict.

### Green component evidence did not prove composition

The corpus repeatedly found a later-layer problem after lower-layer tests were
green: registry tests did not prove bootstrap validity, navigation assertions
did not prove native layout, protocol mechanics did not prove the physical
meaning of LEDs, and component tests did not prove the complete marketplace
page. This pattern should inform later risk-derived validation requirements,
but the NekoMarketplace requirement set must not be copied wholesale into
module validation documents.

### Consumer problems remain external

The experiment also found genuine consumer defects in migrations, environment
isolation, cache/image delivery, Access contention, native layout, service
composition, startup logging, and domain semantics. Their presence strengthens
the credibility of the investigation because the corpus did not attribute
every failure to NekoLib. They do not belong in NekoLib module registries.

## Module routing and reconciliation campaigns

The following routes avoid duplicating one external record across multiple
module authorities.

| Campaign | Primary external records | Related boundaries | Required current authorities |
|---|---|---|---|
| Global package and documentation | F-005, F-007, F-009, V-017 | Every shipped package | Pack projects/scripts, package contents, documentation indexes, package-consumer probes, release record. |
| Data | F-001, F-002, F-003, F-008, F-011, F-012, F-015, F-016, F-026 | Provider scenarios and package evidence | Data source/project, compiled API/manifests, tests, current reference, package, real-provider evidence. |
| Navigation family | F-004, F-013, F-014, F-027 | Navigation.WinForms and interactive evidence | Core/adapter source and projects, manifests, tests, current reference, package, runtime/UI evidence. |
| Devices | F-020, F-021, F-028, V-013 | Emulator evidence and P11 corrections | Devices source/project, manifests, tests, reference, package, protocol/timeout/cancellation/runtime evidence. |
| Diagnostics and observability | F-017, F-018, F-022, F-023, V-012 | Core, Logging, Telemetry, Inspection, Diagnostics.Windows, Watchdog | Source, projects, manifests, tests, current references, security/native/runtime evidence. |
| Watchdog and Host | F-006, F-023, F-024, F-025, V-014 | Diagnostics and deployment | Library/Host source and projects, protocol/package contract, manifests, tests, package-only consumers, process/deployment evidence. |
| Http | F-010, V-007 | External provider | Http source/project, manifests, tests, reference, package, deterministic and authorized provider evidence. |
| Mvvm | F-019, relevant V-012 rows | Navigation.WinForms consumer context | Mvvm source/project, manifests, tests, module reference, package, native WinForms composition evidence. |
| Proposal clarification | P-001 | Boundary unknown | Owner intent only; no technical authority traversal before clarification. |

Each campaign must begin from its current boundary manifest when one exists and
must separate source, build, test, compiled API, runtime, interactive, package,
and release evidence. No campaign is authorized by this audit.

## Questions requiring owner disposition

In addition to P-001, later campaigns need explicit decisions where evidence
alone cannot establish the intended contract:

- which custom-translator obligations are intentionally supported for F-002;
- whether F-008 should be documented, rejected, guarded, or normalized;
- whether the corpus's F-009 acknowledgement has a stronger original
  provenance record;
- which service-resolution/factory boundary is intended for F-014;
- whether a WinForms command binder belongs in Mvvm or remains consumer glue;
- whether Devices should change its default text encoding independently from
  protocol-success semantics in F-020;
- whether the current Diagnostics documentation already states the
  redaction/minidump boundary completely for F-022;
- whether external deployment control belongs in the supported Watchdog
  contract for F-024/F-025;
- whether modal affordance is framework-owned or consumer-themed for F-027;
  and
- what distinct timeout representation is intended for F-028.

These are decision points, not accepted work.

## Validation of this intake artifact

Gate C validation is limited to the changed documentation surface:

- documentation structure, metadata, links, and registry coverage through
  `eng/verify-docs.ps1`;
- whitespace and patch integrity through `git diff --check`; and
- manual review of the final diff and changed-file scope.

Not run and not claimed:

- source compilation;
- unit, integration, or full regression tests;
- compiled public API verification;
- package build or package-consumer execution;
- provider, database, protocol, device, process, deployment, security,
  minidump, runtime, or interactive UI scenarios; and
- release validation.

Those layers would be disproportionate for a historical evidence-intake
document and, more importantly, would cross the authorized stop point into
current module reconciliation.

## Stop point and future reconciliation

This audit stops after preserving and routing the external evidence. It does
not determine which external findings still exist at the reference commit.

A future authorized campaign may append a dated reconciliation section that
records, for each external ID, whether current evidence confirms, disproves,
supersedes, splits, or reclassifies it. Any confirmed defect belongs in the
appropriate module `ISSUES.md`; any uncertain observation belongs in module
`FINDINGS.md`; any unpromoted idea belongs in `BACKLOG.md`; and scheduling still
requires explicit promotion to [`TODO.md`](../../TODO.md).

Until that work occurs, this document is the sole repository intake snapshot
for the NekoMarketplace corpus and every external record remains historical,
baseline-bound evidence.

## Reconciliation — 2026-08-26 — P-001 and QueryBuilder correction

This dated reconciliation preserves the original intake body above. It covers
only `P-001` and the `F-003` / `NOTE-012` / `NOTE-013` QueryBuilder chain against
current NekoLib.Data source at
`fc10319a439edc4943a1226fc66d0cf4ee2d2e2a`.

### P-001 disposition

The owner clarified that **decay** means representation fallback when the
preferred semantic/provider representation is incompatible. It does not mean
expiration or scheduling, and `dd/MM` was only one formatter illustration.
The accepted boundary is NekoLib.Data, initially covering `DateTime` and
`DateTimeOffset` on read and write while allowing the architecture to support
other registered type families.

Promotion permission, provider adaptation/decay, loss authorization, schema
discovery, provider profiles, sanitized failure evidence, and an observational
adaptation hook are now separated explicitly in the
[`Data type-adaptation and QueryBuilder API review`](data-type-adaptation-querybuilder-api-review-2026-08-26.md).
The accepted implementation work is promoted to [`TODO.md`](../../TODO.md)
Phase G3. This resolves the owner-intent dispute recorded in the original
`P-001` section without rewriting that historical section.

### QueryBuilder disposition

Current source confirms the material shape difference described by `F-003`:
condition-template `Where`, dictionary-at-start `InsertInto`/`Update`, and raw
expression `Join` coexist with field/value-oriented fluent methods. The owner
accepted one canonical structured fluent convention centered on `Value`, `Set`,
structured predicates, and a structured join path.

The replaced stable overloads will remain temporarily as compatibility shims,
delegate to the same logical model, and carry warning-only
`Obsolete(..., error: false)` markers whose messages name the replacement and
state next-major removal. They must remain through at least one released minor
version and may be removed only in `2.0.0` or later. This disposition is also
owned by Phase G3.

### Remaining boundary

No other external finding, validation record, or proposal in this intake is
reconciled or promoted by this section. No product source, compiled API
baseline, test, package, provider, runtime, or release evidence was changed or
executed as part of this design decision.
