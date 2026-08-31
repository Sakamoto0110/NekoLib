# Scoped Premise Policy

**Document ID:** GLOBAL-SCOPED-PREMISE-POLICY

**Schema version:** 1

**Kind:** reference

**Lifecycle:** current

**Subject:** bounded confidence premises, eligibility, contradiction handling, and automatic suspension

**Surface:** policy

**Boundary:** global

**Authority role:** normative

**Mutation:** authored

**Indexing:** include

This policy defines how NekoLib may reuse a deliberately accepted, falsifiable
premise to avoid redundant investigation without confusing confidence with
reality. A premise is a reasoning optimization over an explicit scope. It is
never implementation, product, public API, validation, release, or work
authority.

The machine-readable record contract is the
[premise schema](../schemas/premise-schema.json). Shared records live under
[`docs/premises/`](../premises/README.md), the
[example](../templates/premise.example.json) demonstrates their shape, and
[`eng/verify-premises.ps1`](../../eng/verify-premises.ps1) validates records and
derives their effective status.

## Definition and admission

A **scoped premise** is an owner-accepted statement that agents may reuse
without repeating its full investigation while all of the following remain
true:

1. the statement is precise enough to be contradicted;
2. current source and relevant evidence establish unusually high confidence;
3. included paths, exclusions, boundaries, campaigns, and permitted shortcuts
   are explicit;
4. the evidence anchor, expiry rule, change-invalidation paths, and failure
   threshold are recorded; and
5. no higher authority or current evidence conflicts with it.

Agents must not infer or activate a premise merely because code appears stable,
tests are numerous, or a previous task described the area as trusted. Activation
requires an explicit owner or accepted-review decision. A premise may be
temporary, campaign-scoped, repository-scoped, or retired when its optimization
is no longer useful.

## Authority and permitted effect

An eligible premise may only:

- avoid repeating a source inspection already covered by its basis;
- reuse an established contract classification within its declared scope; or
- narrow an investigation to the edges around the trusted statement.

It must never:

- override source, compiled API, tests, runtime observations, or normative
  documentation;
- authorize implementation, promotion, baseline updates, commits, pushes,
  packages, releases, destructive actions, or external effects;
- suppress required build, test, API, package, runtime, security, or release
  evidence;
- turn absence of failure into proof of behavior outside its scope;
- propagate transitively to dependencies, consumers, targets, or campaigns not
  named by the record; or
- be cited as durable evidence that the statement is true.

When a premise and current evidence disagree, the evidence wins immediately.
The premise changes investigation priority; it does not dictate reality.

## Record ownership and lifecycle

Shared premise records are one JSON file per premise under `docs/premises/`,
named `<premiseId>.json`. They are versioned because another agent must be able
to inspect the exact statement, scope, basis, contradictions, and history
without private memory. Machine-derived evaluation state may be cached under
ignored `.local/premises/`, but that cache is neither authority nor evidence.

The declared statuses are:

- `draft` — being designed; never eligible for use;
- `active` — reviewed and potentially eligible after effective-status checks;
- `challenged` — suspended by at least one qualifying contradiction;
- `broken` — deactivated by the configured distinct-failure threshold, a
  critical contradiction, or an explicit accepted break decision;
- `stale` — suspended because relevant paths or the evidence anchor changed;
- `expired` — suspended after its declared expiry;
- `retired` — deliberately no longer used; and
- `superseded` — replaced by a new stable premise ID.

Records are never deleted merely because they break, expire, or are superseded.
Their status history and contradiction records preserve why confidence changed.
A broken premise is not edited back to `active`; a newly supported statement
uses a new premise ID and links the previous record through `supersedes`.

## Declared and effective status

`status` is the last reviewed durable state. Consumers use the stricter
**effective status** derived at the current checkout:

1. `broken`, `retired`, and `superseded` remain terminal;
2. a qualifying critical contradiction derives `broken` immediately;
3. qualifying contradictions with at least the configured number of distinct
   `identity` values derive `broken`;
4. any smaller non-zero qualifying set derives `challenged`;
5. a passed expiry derives `expired`;
6. a non-ancestor evidence commit or a change matching
   `freshness.invalidateWhenChanged` derives `stale`; and
7. only a declared `active` record with none of those conditions is effectively
   `active`.

Only effective `active` is eligible for a shortcut. The evaluator reports and
fails on a declared/effective mismatch, so the premise is automatically
suspended even before its durable status is reconciled. Updating the durable
record remains an evidence-preserving review action, not a silent rewrite.

## Contradictions and distinct failures

Every contrary observation encountered while relying on a premise must be
recorded before further use. A contradiction records its stable ID, independent
identity, time, category, severity, boundary, target, environment, command or
scenario, observation, evidence references, and classification.

Classifications mean:

- `qualifying` — current evidence genuinely conflicts with the premise;
- `duplicate` — repeats an already represented identity and does not increase
  the threshold count; and
- `rejected` — investigation showed that the observation did not contradict the
  premise; the record remains for auditability.

Distinctness is based on `identity`, normally a fully-qualified test, scenario,
source/API check, or independently observable failure contract. Reruns,
parameter variations, and multiple logs from the same root check count once.
The threshold applies only to ordinary qualifying contradictions and must be at
least two. `critical` is reserved for confirmed security-boundary violations,
data loss or corruption, accepted-public-API mismatches, or another explicitly
reviewed condition where continued reliance would be unsafe.

One ordinary qualifying contradiction suspends use as `challenged`; it does not
wait for the break threshold. Reaching the threshold changes the effective
status to `broken` and should trigger a wider investigation around the formerly
trusted boundary, because failure in a high-confidence area is evidence that
adjacent assumptions, fixtures, environments, or ownership boundaries may also
be wrong.

## Freshness, scope, and campaigns

Every active premise anchors its basis to a full Git commit. The
`freshness.invalidateWhenChanged` patterns identify changes that make the basis
stale. Staleness is not a contradiction: it says the previous confidence has
not yet been re-established for the changed code.

`scope.mode` is either `repository` or `campaign`. A campaign-scoped premise
lists every permitted work-campaign ID. A work campaign may consume that premise
only when its own ID is listed, its changed path and boundary are inside the
premise scope, the requested optimization is listed in `permittedUses`, and the
effective status is `active`.

Premise definitions remain outside the work-campaign schema. The premise record
owns the cross-reference; the campaign manifest continues to coordinate work
without becoming truth or premise authority. A campaign fingerprint PASS does
not activate a premise, and a premise does not suppress campaign finalizers.

## Consumption protocol

Before relying on a premise, an agent must:

1. read the complete record and this policy;
2. run `eng/verify-premises.ps1` for the current checkout;
3. confirm effective `active`, path/boundary applicability, campaign
   applicability when relevant, and the exact permitted shortcut;
4. state which investigation is being omitted and which validation remains;
5. stop using the premise immediately on contrary evidence; and
6. record the contradiction and report its effect before continuing.

If the evaluator is unavailable, the record is malformed, or scope cannot be
established, the premise is ineligible. The fallback is normal source-first
investigation, not optimistic reliance.

## Restoration and closure

`challenged`, `stale`, or `expired` records may be superseded after the
statement, scope, anchor, and evidence are re-established. `broken` records must
also retain the root-cause disposition and every qualifying contradiction.
Restoration requires a new premise ID, a fresh owner or accepted-review
decision, current evidence, and an explicit `supersedes` link.

Premise verification proves structural consistency and checkout eligibility
only. It does not prove the premise statement, test adequacy, semantic truth, or
the correctness of its original acceptance decision.
