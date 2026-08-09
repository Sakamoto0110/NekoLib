# Runtime scenario harness

**Kind:** guide

**Lifecycle:** current

**Owner:** Phase E runtime scenarios

**OS / target:** `net481` and `net9.0`

**Prerequisites:** none. This project references nothing but the BCL.

**Last verification:** 2026-08-08 — extracted from `E4-SQL`, which now consumes
it. Both targets build with no warnings, the E4-SQL smoke passes with exit 0,
and the recorded schedule determinism hash `fnv1a64:49a3ab65b5f249e9` is
unchanged on both targets.

## Status: accepted, implemented, and provisional

The boundary drawn here is **accepted and in use, but not yet validated**. It
has one consumer, and a boundary with one consumer is a guess that happens to
compile.

`E3-OBS` is the second real consumer, and implementing it is the test. Until
then:

- **Do not expand the harness preemptively.** Nothing enters it in anticipation
  of a consumer that does not exist yet.
- While implementing `E3-OBS`, move in only mechanics that turn out to be
  genuinely common.
- If the second consumer shows that something here is not common after all,
  **move it back out.** A wrong boundary defended is worse than a wrong boundary
  corrected.
- Record the outcome of that validation in this README's verification record and
  in `TODO.md`, whichever way it goes.

## What this is

The plumbing every Phase E scenario needs and none of them should own a private
copy of:

| Piece | Why it is shared |
|---|---|
| `ExitCodes` | `E3-ORCH` aggregates exit codes across scenarios; they have to mean the same thing |
| `CheckRunner`, `Check`, `CheckResult` | one check mechanism, one result shape in `result.json` |
| `RunArtifacts`, `ResourceSampler`, `WorkloadCounters` | the suite specifies one artifact layout |
| `RuntimeFacts`, `Native` | the environment record the suite requires |
| `FaultSchedule`, `DeterministicRandom` | deterministic seeded scheduling, reproducible across targets |
| `ScenarioOptionsBase` | the common command line: modes, seed, artifacts, schedule |
| `Json`, `ProcessRunner` | `net481` has no `System.Text.Json`, and every scenario shells out |

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

## Verification record

| Date | Result |
|---|---|
| 2026-08-08 | **Extraction verified.** Moved out of `E4-SQL` with `git mv` so history follows. Both targets build clean; E4-SQL smoke exits 0 with the same data digest as before; the schedule hash is byte-identical on `net481` and `net9.0`, which is what proves the move did not disturb determinism. **The extraction changed no check count and no assertion** — E4-SQL's smoke went from 28 checks to 29 earlier the same day, from the `state-baseline` check added while fixing the soak, and that is a separate change from this one. |
| — | **Second-consumer validation: pending.** `E3-OBS` has not been written. Until it has, this boundary is provisional and no claim should be made that the split is correct. |
