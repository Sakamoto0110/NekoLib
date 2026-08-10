# Runtime scenario harness

**Kind:** guide

**Lifecycle:** current

**Owner:** Phase E runtime scenarios

**OS / target:** `net481` and `net9.0`

**Prerequisites:** none. This project references nothing but the BCL.

**Last verification:** 2026-08-10 — both harness targets and both consumer
target matrices build with no warnings. An orchestrated `E3-OBS` regression
verified artifact layout v2, and a direct run verified that standalone layout
v1 remains unchanged.

## Status: validated by a second consumer

The boundary is no longer a guess. `E3-OBS` was written against it on
2026-08-09, and the split survived with **one thing moved out, one moved in, and
one deliberately refused**.

That is the outcome worth recording: the harness was right about *what* belongs
to it and wrong about the *shape* of one piece, which is exactly the kind of
error only a second consumer can expose.

### Moved out — the sampler's scenario columns

`ResourceSample` carried `ConnectionsCreated` and `ServerSessions`, and
`ResourceSampler` took a `Func<long>` for the first. Both are SQL Server
concepts. `E3-OBS` has neither; it has retained log entries, rolled files,
completed operations and registered providers.

The suite's own wording is what settles it: a sample must carry "active/retained
item counts for bounded components" and "queue/gate depth", which is a real
requirement with **no shared answer**. So the harness now owns the columns every
scenario has and asks the scenario for the rest, through `IScenarioSamples`.

A detail found on the way out: **`ServerSessions` was never populated.** No
caller ever passed it, so every row of every E4-SQL run recorded `-1`. A column
that exists because one consumer might need it one day is precisely what rule 2
is against, and it was already dead before the second consumer arrived.

### Moved in — the result and summary writer

`E4-SQL`'s `Program.cs` held about 200 lines that `E3-OBS` needed
byte-identically: the exit-code precedence, the `result.json` document, the
duplicate `summary.json`, and the `summary.md` table. That is not one scenario's
invention — the suite *specifies* the artifact layout — so it qualifies under
rule 2's second clause, and two divergent `summary.json` documents would have
defeated the point of having specified one.

`RunSummary` owns it now, with the same split as everywhere else: the harness
writes the document, and `IScenarioSummary` supplies the half only the scenario
can name (E4-SQL's provider and image digest; E3-OBS's three module versions).

One thing was added rather than merely moved: a **per-phase breakdown**, derived
from `CheckResult.Phase`, which the harness already owned. `E3-OBS` needs each
capability's totals visible so that one failing is legible at a glance, and it
costs nothing for a scenario whose phases are workload classes instead.

### Moved in — the run's cancellation token, in `CheckRunner`

This one was a defect rather than a boundary question, and it was in the harness
already, affecting both consumers.

`CheckRunner.RunAsync` caught everything that was not a `CheckFailure` and
recorded it as a failed check. During a Ctrl+C that is exactly wrong: every
check in flight throws `OperationCanceledException` and is reported as a
failure. `E3-OBS`'s first interrupt test produced **"145 passed, 5 failed"** for
a run that had simply been stopped — the exit code was a correct `8`, but the
summary said five things were broken when nothing was.

`CheckRunner` now takes the run's token. A cancellation that escapes while the
token is cancelled is recorded as **skipped** with "interrupted before the check
reached a verdict"; one that escapes while the run is *not* cancelling is still
a failure, because that is a scenario bug. The same interrupt now reports
"144 passed, 0 failed, 6 skipped".

`E4-SQL` had the identical flaw and now passes its token too. Nothing about this
was visible with one consumer, because nobody had interrupted a soak and read
the resulting summary.

### Refused — the Ctrl+C handler

Both scenarios open with the same fifteen lines wiring `Console.CancelKeyPress`
to a `CancellationTokenSource`, and the suite mandates the behaviour for every
scenario. It was still left duplicated.

Moving it would mean `ScenarioHost.Run(options, run)`, and the harness would
start **driving** — which the section below promises it does not. Fifteen
duplicated lines are cheaper than that promise. Recording the refusal matters as
much as recording the moves: this is the boundary being held on purpose, not
overlooked.

### Refused — `--smoke-duration`

`E3-OBS` needs a smoke window; the harness already carries `--rehearsal-duration`
for the same reason. It would have been natural to put its sibling beside it.

It stays in `E3-OBS` because rule 2 has no exception for symmetry: `E4-SQL` has
no smoke-duration concept, so the option has one consumer. It moves when a
second scenario needs it.

## The rules, still in force

- **Do not expand the harness preemptively.** Nothing enters in anticipation of
  a consumer that does not exist yet.
- **Move anything back out** that a later consumer shows is not common after
  all. A wrong boundary defended is worse than one corrected.
- A **third** consumer is the next test. `E3-NAV` and `E3-PIPE` are the
  candidates, and both are likely to press on `WorkloadCounters`, whose
  vocabulary — successes, expected failures, cancellations — has so far only had
  to describe two scenarios that both happen to think in operations.

## What this is

The plumbing every Phase E scenario needs and none of them should own a private
copy of:

| Piece | Why it is shared |
|---|---|
| `ExitCodes` | `E3-ORCH` aggregates exit codes across scenarios; they have to mean the same thing |
| `CheckRunner`, `Check`, `CheckResult` | one check mechanism, one result shape in `result.json` |
| `RunArtifacts`, `ResourceSampler`, `WorkloadCounters` | the suite specifies one artifact layout |
| `RunSummary` | the suite specifies one `result.json`, `summary.json` and `summary.md`, and one exit-code precedence |
| `RuntimeFacts`, `Native` | the environment record the suite requires |
| `FaultSchedule`, `DeterministicRandom` | deterministic seeded scheduling, reproducible across targets |
| `ScenarioOptionsBase` | the common command line: modes, seed, artifacts, schedule |
| `Json`, `ProcessRunner` | `net481` has no `System.Text.Json`, and every scenario shells out |

Three of these are split down the middle, and the split is the same each time:
the harness owns the common half and asks the scenario for the rest.
`ScenarioOptionsBase` parses the modes and hands `TryParseScenarioOption` the
scenario's own flags; `IScenarioSamples` supplies the sample columns only the
scenario can name; `IScenarioSummary` supplies the versions only the scenario
can name. A base class accumulating every scenario's private fields would be the
wrong kind of sharing.

## What this is not

The suite says it is *"deliberately a set of small executables and scripts, not
a new test framework"*, and that rule is respected here. The prohibition is
against a mandatory, extensible architecture that all scenarios must conform to
and that ends up driving them. This drives nothing, defines no concepts a
scenario has to adopt, and can be ignored piecemeal — a scenario may use the
check runner and none of the rest.

## The two rules that keep it that way

Both were agreed when the project was created, and breaking either is how it
would become the framework the suite forbids.

**1. It never references a product module.** No `NekoLib.Data`, no
`NekoLib.Pipes`, nothing from `src/`. A scenario that needs to name its provider
or the module under test passes a `Type` to
`RuntimeFacts.DescribeAssembly`.

**2. Nothing enters it without two real consumers**, or without being part of an
explicitly uniform suite contract — exit codes and the artifact format being the
clearest examples. Speculative generality is how shared code turns into a
framework nobody chose.

A third, smaller rule follows from the first two: keep the *common* half of a
concept and leave the specific half behind. `ScenarioOptionsBase` parses the
modes and the seed; a scenario's own `--container` or `--port` is parsed by
overriding `TryParseScenarioOption`. A base class accumulating every scenario's
private flags would be the wrong kind of sharing.

Like every scenario, this project stays outside `NekoLib.sln`.

## Using it

Reference the project, then:

```csharp
internal sealed class MyOptions : ScenarioOptionsBase
{
    public override string ScenarioId => "E3-OBS";
    protected override string CampaignPrefix => "e3obs";
}
```

`FaultSchedule.Generate` takes an `IFaultVocabulary`: the harness places faults
in time and hashes the plan, and the scenario says what each kind targets and
what recovery from it looks like.

**The generator version is a parameter, not a constant.** It is covered by the
schedule hash, so a scenario that changes it invalidates its own recorded
determinism evidence. `E4-SQL` keeps `e4sql-schedule-1` for exactly that reason.

## Artifact layout versions

`RunArtifacts` has two deliberate modes:

- **v1 standalone:** without orchestration arguments, the scenario generates
  its own campaign id and writes `<campaign-id>/<scenario-id>/result.json`;
- **v2 orchestrated:** `--campaign-id` and `--worker-id` must be supplied
  together, and the scenario writes
  `<campaign-id>/workers/<worker-id>/<scenario-id>/result.json`.

The pair is validated as safe single path segments before any run starts.
`environment.json`, `result.json` and `summary.md` identify the chosen layout;
v2 also records the worker id. The harness does not move, rewrite or infer old
artifact directories. This keeps existing evidence stable while giving a
multi-process campaign one collision-free contract.

## Verification record

| Date | Result |
|---|---|
| 2026-08-08 | **Extraction verified.** Moved out of `E4-SQL` with `git mv` so history follows. Both targets build clean; E4-SQL smoke exits 0 with the same data digest as before; the schedule hash is byte-identical on `net481` and `net9.0`, which is what proves the move did not disturb determinism. **The extraction changed no check count and no assertion** — E4-SQL's smoke went from 28 checks to 29 earlier the same day, from the `state-baseline` check added while fixing the soak, and that is a separate change from this one. |
| 2026-08-09 | **Second-consumer validation complete.** `E3-OBS` was written against this boundary and consumes `ExitCodes`, `CheckRunner`, `RunArtifacts`, `ResourceSampler`, `WorkloadCounters`, `RunSummary`, `RuntimeFacts`, `FaultSchedule`, `DeterministicRandom`, `IFaultVocabulary`, `ScenarioOptionsBase` and `JsonWriter` by name, plus `Native` and `ProcessRunner` indirectly through `RuntimeFacts`. Nothing in the harness went unused by the second consumer, which is the weaker but still useful half of the result: no piece turned out to be there for E4-SQL alone. One piece moved out (the sampler's SQL-specific columns, one of which was dead), one moved in (`RunSummary`), and two candidates were refused on the rules (the Ctrl+C handler and `--smoke-duration`). **Regression evidence:** both targets build with no warnings; E4-SQL's determinism hash is still `fnv1a64:49a3ab65b5f249e9` on `net481` and `net9.0` after the changes; E3-OBS's own hash `fnv1a64:af14ff69cf61b022` matches across both targets. |
| 2026-08-10 | **Artifact layout v2 validated without breaking v1.** Harness, E3-OBS and E4-SQL build clean on both declared targets. A 12-second E3-ORCH/E3-OBS regression exited 0 with 91 checks, no failures, matching worker/aggregate schedule hash `fnv1a64:683eb00b749a22bb`, and the indexed result at `workers/E3-OBS-net9.0/E3-OBS/result.json`. A direct 8-second E3-OBS run exited 0 with 61 checks and wrote the original v1 shape with no `workerId`. These short runs validate the contract only; the earlier duration evidence remains authoritative. |

### What the changes cost the existing consumer

**Nothing measurable.** `E4-SQL`'s smoke was re-run against the modified harness
on 2026-08-09: exit 0, **29 checks, 0 failed, 0 skipped**, against the same
pinned container, with the adopted container restored to the stopped state the
run found it in. That is the same check count it had before, so the refactor is
regression-free at runtime and not merely at build.

Two artifact details changed and neither is an assertion:

- `samples.csv` lost the `server_sessions` column, which had only ever held
  `-1`, and `connections_created` moved to the end as a scenario column.
- `result.json` groups the scenario's own properties where `IScenarioSummary`
  writes them, gained a `checksByPhase` array, and split the old `scheduleHash`
  into a bare hash plus a `scheduleFaultCount` integer. The bare hash is what
  makes two runs comparable by equality, which a "7 fault(s), fnv1a64:…"
  sentence would not be.

The schedule hash — the one recorded value that would matter — is unchanged.
