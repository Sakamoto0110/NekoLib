# Navigation long-running and recovery (E3-NAV)

**Kind:** guide

**Lifecycle:** current

**Owner:** `NekoLib.Navigation`, `NekoLib.Navigation.WinForms`, and
`NekoLib.Navigation.Wpf`

**OS / targets:** Windows; WinForms on `net481` and `net9.0-windows`, WPF on
`net9.0-windows`

**Prerequisites:** an interactive Windows desktop for runtime modes. Builds,
isolated contract checks, and `--print-schedule` do not start a scenario window.

**Last verification:** 2026-08-11 — **passing short development probes on all
three combinations.** WinForms `net481`, WinForms `net9.0-windows`, and WPF
`net9.0-windows` each passed 11/11 checks with zero skipped, exit 0, awaited
shutdown, and zero owned page, surface, provider, action, or native-child state.
The runs lasted about two minutes and are below the 15-minute smoke minimum.
Qualifying smoke, recovery rehearsal, soak, automated UI, and interactive
procedures remain open.

## Purpose and architecture

E3-NAV is the unattended, exit-code-decided companion to the existing
interactive WinForms and WPF smoke applications. It drives the same public
Navigation composition and native adapters on all three supported combinations:

| Host | Target | Evidence identity |
|---|---|---|
| WinForms | `net481` | `platform=winforms`, target recorded by the harness |
| WinForms | `net9.0-windows` | `platform=winforms`, target recorded by the harness |
| WPF | `net9.0-windows` | `platform=wpf`, target recorded by the harness |

`NekoLib.Navigation.RuntimeTests.LongRunningRecovery.Core` owns the deterministic
plan, probes, workload, recovery dispatcher, sampling, assertions, cleanup, and
exit code. The two smallest possible native hosts contain only concrete pages,
surfaces, adapter composition, and the UI message loop. A standalone run gets a
platform- and target-specific campaign directory. An orchestrated run must give
each combination a distinct `--worker-id`, so evidence cannot collide.

The existing shared harness is reused for schedule, artifact, check, sampling,
and summary contracts. It was not changed for E3-NAV. All workload switches and
fault controls are scenario-owned. There is no product `TestControl`,
Instrumentation module, reflection hook, Inspection action, or test API.

No file under `src/Navigation/` changed. The scenario composes only public
Navigation APIs and the public adapter contracts; the frozen lifecycle,
registry, factory, context, and runtime remain untouched.

## Implemented workload

Every mode begins with the same deterministic matrices:

- transient disposal, strong identity reuse, weak recreation after collection,
  keep-attached hiding, and reset release;
- history state capture and restore-before-enter ordering;
- authentication, role, permission, denial, and redirect behavior;
- `LoadBeforeShow`, `ShowImmediately`, and `LoadInBackground`, including
  successful, failed, discarded, and post-reset late completion;
- repeated toast, dialog, prompt, and popover use, overlapping modal ownership,
  and pending surface completion during reset or shutdown;
- idle denial and rearm, native interaction rearm, sign-out on admitted idle,
  stopped timers, and shutdown near a scenario-triggered tick;
- repeated `Start` / awaited `NavigationService.Shutdown()` cycles, static
  subscriber collection, an admitted pre-cutoff request, and rejection after
  shutdown;
- sustained batches of page switches, guard/session changes, surfaces, reset,
  and fresh mounts.

Assertions use public events, passive Inspection operations, scenario page
probes, weak references, native host membership, and scenario adapter wrappers.
They do not inspect pixels. Memory, handles, threads, and managed heap are
sampled and reported; the build-first implementation does not invent a hard
memory threshold before a real baseline exists. Owned pages, backgrounds,
surfaces, native children, static handlers, timers, and Inspection providers
must return to zero at their defined cleanup boundaries.

Two requested measurements are deliberately represented at the public
boundary. `StopIdle` is an internal bootstrap-lifetime operation, not an
application API; the scenario reaches it through awaited public Shutdown and
asserts that the wrapped native timer rejects later ticks. Passive Inspection
publishes cache, queue, active-attempt, overlay, background, and idle state as
opaque `object` projections. E3-NAV asserts those providers exist but records
`-1` for their opaque numeric fields and for gate depth instead of using
reflection against the frozen runtime. Scenario-owned page, background,
surface, request, and history counts remain directly sampled.

## Modes and deterministic recovery

- `--smoke` runs the complete matrix, then sustained cycles for a default of 20
  minutes. It injects no recovery faults.
- `--recovery-rehearsal` runs the matrix and dispatches all 14 fault kinds over
  the shared 60-minute schedule window.
- `--soak <duration>` runs sustained workload and the same deterministic fault
  vocabulary concurrently. Four hours is the Phase E gate; 16 hours is optional.

The fault vocabulary covers registry lookup, page creation, page load, enter,
leave, background load, guard timeout, guard throw, redirect cycle, redirect
depth, surface binding, surface show, surface cleanup, and dispatcher
unavailability. Each control is implemented in a scenario page, guard, surface,
or adapter wrapper. After every planned fault, ordinary reset and page switching
must recover.

`schedule.json` is written with UTF-8 without BOM before Navigation is mounted
or any page, timer, surface, or workload exists. Dispatch uses persisted
monotonic offsets from the scenario start. The generation timestamp is
provenance only and is excluded from the normalized hash.

## Build and isolated checks

Build the exact required combinations:

```powershell
dotnet build runtime_tests\Navigation\LongRunningRecovery\NekoLib.Navigation.RuntimeTests.LongRunningRecovery.WinForms\NekoLib.Navigation.RuntimeTests.LongRunningRecovery.WinForms.csproj -f net481
dotnet build runtime_tests\Navigation\LongRunningRecovery\NekoLib.Navigation.RuntimeTests.LongRunningRecovery.WinForms\NekoLib.Navigation.RuntimeTests.LongRunningRecovery.WinForms.csproj -f net9.0-windows
dotnet build runtime_tests\Navigation\LongRunningRecovery\NekoLib.Navigation.RuntimeTests.LongRunningRecovery.Wpf\NekoLib.Navigation.RuntimeTests.LongRunningRecovery.Wpf.csproj -f net9.0-windows
```

The isolated console checks never initialize WinForms, WPF, or Navigation. They
cover same-seed stability, seed sensitivity, platform independence, complete
fault vocabulary, smoke's empty fault plan, exact UTF-8 plan persistence, and
scenario option boundaries:

```powershell
dotnet run --project runtime_tests\Navigation\LongRunningRecovery\NekoLib.Navigation.RuntimeTests.LongRunningRecovery.Tests\NekoLib.Navigation.RuntimeTests.LongRunningRecovery.Tests.csproj -f net481
dotnet run --project runtime_tests\Navigation\LongRunningRecovery\NekoLib.Navigation.RuntimeTests.LongRunningRecovery.Tests\NekoLib.Navigation.RuntimeTests.LongRunningRecovery.Tests.csproj -f net9.0
```

Schedule preview exits before a native application object or window is created:

```powershell
dotnet run --project runtime_tests\Navigation\LongRunningRecovery\NekoLib.Navigation.RuntimeTests.LongRunningRecovery.WinForms\NekoLib.Navigation.RuntimeTests.LongRunningRecovery.WinForms.csproj -f net481 -- --recovery-rehearsal --seed 20260810 --print-schedule
dotnet run --project runtime_tests\Navigation\LongRunningRecovery\NekoLib.Navigation.RuntimeTests.LongRunningRecovery.WinForms\NekoLib.Navigation.RuntimeTests.LongRunningRecovery.WinForms.csproj -f net9.0-windows -- --recovery-rehearsal --seed 20260810 --print-schedule
dotnet run --project runtime_tests\Navigation\LongRunningRecovery\NekoLib.Navigation.RuntimeTests.LongRunningRecovery.Wpf\NekoLib.Navigation.RuntimeTests.LongRunningRecovery.Wpf.csproj -f net9.0-windows -- --recovery-rehearsal --seed 20260810 --print-schedule
```

## Runtime commands

The first source-tree development gate used the smoke matrix with a two-minute
override. It passed standalone on all three combinations, but the override
means none of those runs is smoke-gate evidence:

```powershell
dotnet run --project runtime_tests\Navigation\LongRunningRecovery\NekoLib.Navigation.RuntimeTests.LongRunningRecovery.WinForms\NekoLib.Navigation.RuntimeTests.LongRunningRecovery.WinForms.csproj -f net481 -- --smoke --smoke-duration 2m --seed 20260810
```

The next runtime gate is the normal standalone smoke on all three combinations
without `--smoke-duration`. Only after those smokes pass should the recovery
rehearsals run, followed by one four-hour full native combination and
smoke/rehearsal parity on the other two. Do not infer WPF behavior from WinForms
evidence or one target from another.

The existing interactive procedures remain authoritative for pixel placement,
focus, touch/click reachability, and human UI behavior:

- [`../WinFormsSmoke/README.md`](../WinFormsSmoke/README.md)
- [`../WpfSmoke/README.md`](../WpfSmoke/README.md)

E3-NAV does not replace or automate those visual steps.

## Artifacts, exit codes, and cleanup

Each combination writes the shared Phase E artifact contract under
`artifacts/validation/phase-e/`, including `schedule.json`, `environment.json`,
`samples.csv`, `checks.ndjson` where applicable, `events.jsonl`, `stdout.log`,
`stderr.log`, `result.json`, and `summary.md`. Environment evidence records the
platform, target, runtime, adapter assembly, Navigation and Inspection versions,
repository state, seed, workload controls, and claim boundaries.

The process exit code is the verdict. Exit `0` means every selected check and
cleanup assertion passed. Nonzero shared exit codes represent assertion,
prerequisite, interruption, cleanup, unexpected, or evidence-write failure;
free-form console output is never the verdict.

Normal completion, window close, and the first Ctrl+C request cancellation and
then await the single cleanup path. Cleanup releases blocked scenario loads,
disables scenario faults, awaits `NavigationService.Shutdown()`, verifies zero
owned page/surface/native/provider/action state, clears scenario-only global
slots, and only then writes the final result. E3-NAV starts no child process,
service, endpoint, package operation, or external resource.

## What a passing run does not establish

- Pixel layout, focus order, input reachability, accessibility, DPI behavior,
  first paint, and human-perceived responsiveness remain interactive claims.
- A source-tree scenario cannot prove package-consumer behavior.
- Passive Inspection evidence applies only to Navigation's existing hooks. It
  does not activate Inspection actions or imply instrumentation in other modules.
- Scenario-triggered failures and races do not prove power loss, process crash,
  operating-system failure, or physical input hardware behavior.
- Resource samples need executed baselines before slow drift can be classified;
  build and preview results are not leak evidence.

## Verification record

- 2026-08-10 / uncommitted working tree based on `d515137`: build-first on
  Windows. Explicit builds passed for WinForms `net481`, WinForms
  `net9.0-windows`, and WPF `net9.0-windows`, with 123, 155, and 121 existing
  dependency warnings respectively and no warning originating under this
  scenario. Six isolated contract checks passed on `net481` and six on `net9.0`.
  Recovery schedule previews repeated twice in all three host/target builds at
  `fnv1a64:2af9145c9ebf63ec`; seed `99` changed it to
  `fnv1a64:838221b8dec2baa0`. The previews created no application object or
  window. No smoke, workload matrix, UI automation, recovery rehearsal, soak,
  interactive procedure, package operation, or campaign registration ran.
- 2026-08-11 / clean `e7c86a4`: the final two-minute standalone development
  probe passed on every required combination. WinForms `net481` artifact
  `e3nav-winforms-smoke-net481-s20260810-20260811T171950042Z` completed 50
  cycles in 121.7 seconds; WinForms `net9.0-windows` artifact
  `e3nav-winforms-smoke-net9.0-s20260810-20260811T172241152Z` completed 51
  cycles in 121.8 seconds; WPF `net9.0-windows` artifact
  `e3nav-wpf-smoke-net9.0-s20260810-20260811T172453517Z` completed 552 cycles
  in 120.1 seconds. Each recorded `dirty=false`, the exact commit, smoke hash
  `fnv1a64:8e55e6d894226c65`, 11 passed checks, zero failed/skipped, exit 0,
  awaited `NavigationService.Shutdown()`, and zero cleanup problems. No process
  or window remained. These are development probes below the smoke minimum,
  not qualifying smoke or resource-drift evidence.
- The first committed-source `net481` attempt at `5781e0b` exited 4 with 7/11
  checks passing. It exposed three scenario defects: the redirect check measured
  an ordinary redirect from the permission page while claiming the target was
  already current; reset and shutdown released scenario-owned blocked loads
  before the public cutoff had completed. The latter failure aborted its check
  before remount and caused the following idle/shutdown check to fail as a
  cascade. E3-NAV now establishes the intended current-page history state and
  awaits reset or shutdown before releasing late page-owned work. A dirty
  confirmation reduced the result to the redirect assertion alone, then passed
  11/11 after that assertion was aligned with the public history contract. No
  file under `src/Navigation`, shared harness source, or `campaign.json` changed.
