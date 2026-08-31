# Work Campaign Policy

**Document ID:** GLOBAL-WORK-CAMPAIGN-POLICY

**Schema version:** 1

**Kind:** reference

**Lifecycle:** current

**Subject:** bounded multi-stage work coordination, deferred finalizers, and local campaign state

**Surface:** policy

**Boundary:** global

**Authority role:** normative

**Mutation:** authored

**Indexing:** include

This policy defines how NekoLib groups already-authorized work into a bounded
campaign so shared closing commands run once, at the right tree state, without
turning execution coordination into another source of product truth or work
authorization.

The machine-readable contract is the
[work-campaign schema](../schemas/work-campaign-schema.json). The versioned
[example](../templates/work-campaign.example.json) demonstrates its shape, and
[`eng/invoke-work-campaign.ps1`](../../eng/invoke-work-campaign.ps1) validates,
plans, deduplicates, and executes declared finalizers.

## Definition and admission

A **work campaign** is an explicit execution envelope over two or more bounded
stages or subtasks that share at least one baseline, scope rule, finalizer, or
evidence boundary. A single ordinary edit or one independently complete task
does not need a campaign.

A campaign exists only when its manifest is deliberately created. Agents must
not silently infer or start one merely because work is large. Documentation,
implementation, validation, release, repository-maintenance, and mixed
campaigns use the same coordination semantics; the required technical evidence
still comes from each affected boundary.

The existing Phase E runtime `campaign.json` is a specialized scenario
orchestrator, not a work-campaign manifest. Its runtime schedule and process
ownership contract remain unchanged.

## Authority boundary

A work campaign consumes authority; it never creates it.

- `TODO.md` remains the only scheduler for formally promoted work.
- An explicit owner decision may authorize a bounded campaign when repository
  governance already permits direct authorization of that exact work.
- `ROADMAP.md`, proposals, findings, issues, audits, tests, and campaign state
  do not authorize implementation by themselves.
- A campaign must name its authority source and any repository references. It
  must not widen their scope, cross a freeze, promote findings, update accepted
  API baselines, or convert unexecuted validation into evidence.
- Declaring a finalizer does not authorize an otherwise unauthorized commit,
  push, publication, destructive command, runtime scenario, package build, or
  external action. Those actions still require their normal authority.

When the authority or scope is ambiguous, the campaign stops before mutation.

## Versioned contract and local state

The repository owns:

- this normative policy;
- `docs/schemas/work-campaign-schema.json`;
- `docs/templates/work-campaign.example.json`; and
- `eng/invoke-work-campaign.ps1`.

An active manifest and its mutable execution state belong under:

```text
.local/work-campaigns/<campaign-id>/
|-- campaign.json
`-- state.json
```

`.local/` is ignored, machine-specific, and non-authoritative. The manifest is
coordination input; `state.json` is a deduplication ledger. Neither is clean-
clone evidence, a task scheduler, or a validation record. A campaign that must
cross machines or worktrees is transported explicitly through the authorized
handoff mechanism and is re-baselined at the destination.

Durable outcomes go to their existing owners: source and current references for
implemented behavior, `VALIDATIONS.md` for curated module evidence, audits for
dated review evidence, release records for package provenance, and history for
completed roadmap state. Do not create a parallel campaign evidence authority.

## Baseline and scope

Every manifest records:

- the exact branch and full baseline commit;
- whether the campaign started from a clean or dirty working tree;
- every pre-existing tracked or untracked path that is outside campaign
  ownership;
- included and excluded repository-relative path patterns;
- the authorized task IDs, when they exist; and
- at least two ordered stages with explicit completion criteria.

A clean working-tree baseline is preferred. Starting dirty is permitted only when
every pre-existing path is listed and the campaign will not modify it. The
runner removes declared pre-existing paths from trigger selection and rejects
new campaign changes outside the declared scope. Overlap with a pre-existing
path is a stop condition, not an implicit adoption of that work.

The recorded baseline commit must remain an ancestor of actual `HEAD`, and the
campaign runs only on its recorded branch. Rebase, reset, branch substitution,
or history replacement requires a new manifest or an explicitly reviewed
re-baseline; local state must not conceal the change.

## Stage validation and campaign finalizers

Each stage performs the narrow feedback needed to establish that its own work
is coherent. Expensive or shared commands may be deferred to finalizers only
when that delay does not hide a high-risk failure or violate a module, public
API, package, runtime, or release gate.

Finalizers have stable unique IDs and declare:

- `pre-commit` or `post-commit` phase;
- an executable plus an argument array, never a shell command string;
- repository-relative working directory;
- path patterns that trigger the command;
- required local paths or prerequisites;
- whether missing prerequisites block the campaign;
- whether a clean tree is required; and
- accepted exit codes.

`pre-commit` finalizers validate the complete campaign change set before its
commit gate. `post-commit` finalizers require the intended committed baseline
and are appropriate for clean-tree packaging, local index refresh, or another
explicitly authorized closure operation. The runner never creates a commit or
pushes.

Finalizer selection is change-driven. If several stages declare the same
campaign finalizer, the manifest contains one finalizer ID and the runner
executes it once for the matching change-set fingerprint. Focused stage checks
remain independent and are not deduplicated into a weaker aggregate command.

## Planning, execution, and deduplication

Planning is the default:

```powershell
.\eng\invoke-work-campaign.ps1 `
    -Campaign .local\work-campaigns\<campaign-id>\campaign.json `
    -Phase pre-commit
```

Execution requires the explicit switch:

```powershell
.\eng\invoke-work-campaign.ps1 `
    -Campaign .local\work-campaigns\<campaign-id>\campaign.json `
    -Phase pre-commit `
    -Execute
```

The runner validates the manifest, branch, baseline ancestry, scope, command
shape, required paths, and clean-tree gates before execution. It supports only
direct repository maintenance scripts and a small non-shell executable set; it
does not evaluate command strings.

The change-set fingerprint includes baseline, `HEAD`, Git status, index object
identity, and current file content for every campaign-owned changed path. A
successful finalizer with the same ID, phase, and fingerprint is skipped.
`-Force` makes a deliberate repeat visible without deleting the prior run.

A failed required finalizer is recorded and stops the phase. Missing optional
local prerequisites are reported as skipped and never become PASS evidence.
Changing content, stage scope, baseline, or finalizer identity prevents a stale
success from satisfying the new state.

## Evidence and completion

Command success proves only what that command actually exercises. Work-campaign
state must keep source, build, test, API, runtime, interactive, package, and
release claims separate under the existing validation policy.

The runner does not mark a campaign, `TODO.md` item, issue, or release complete.
Closure still requires a human or authorized agent to:

1. inspect the complete change and finalizer results;
2. confirm every required evidence layer and gap;
3. perform the separately authorized commit, package, release, or push gates;
4. write durable evidence to its existing owner; and
5. report final Git state and deliberately unrun layers.

Interrupted campaigns retain their local state. Resumption rechecks branch,
baseline ancestry, current changes, scope, and fingerprints before using any
prior success. Abandoned local state may be removed only as an explicit local
cleanup; durable evidence already recorded elsewhere is never rewritten.

## Explicit non-goals

Work campaigns do not provide a general job scheduler, CI/CD service, remote
agent coordinator, test framework, package publisher, hidden background worker,
or product runtime feature. They do not automatically start from repository
changes and do not replace module-specific skills, tests, manifests, validation
requirements, or release policy.

Scoped-premise definitions remain outside work-campaign schema version 1. An
accepted record governed by the
[scoped premise policy](premise-policy.md) may name applicable campaign IDs and
reduce only its permitted redundant investigation. It never suppresses campaign
finalizers, validation requirements, evidence, or authorization gates.
