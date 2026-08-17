# Inspection Public API Review - 2026-08-17

**Kind:** audit

**Lifecycle:** current

**Subject:** F1-INSP compiled public surface, passive recording, provider and
snapshot behavior, owner diagnostics, opt-in lifecycle, experimental action
boundary, and compatibility impact

**Status:** review complete; dispositions pending explicit decision

**Reference date:** 2026-08-17

**Reference commit:** `7c4d449ec3a6854b0561c8514701a1ec31fe3c35`

**Last reconciliation:** none

**Current state:** [`TODO.md`](../../TODO.md) F1-INSP

## Baseline and authority

This review covers committed `HEAD` on branch
`phase-e/sqlserver-and-orchestration`. Before this audit and its index entries
were added, the worktree and index were clean, `HEAD` was
`7c4d449ec3a6854b0561c8514701a1ec31fe3c35`, and the branch was 20 commits
ahead of its matching remote branch. The review covers that committed product
state plus the documentation-only working-tree additions that register this
artifact. Nothing was pushed.

The reviewed authority is the Inspection project and all of its source, project
file, direct tests, Release-built `net481`/`net9.0` assemblies, and both approved
API manifests; the frozen Core Inspection contracts; Navigation's recorder use;
Diagnostics' snapshot-source use; the Observability scenario source; the
PackageReference-only consumers; the public API release policy; and current
repository documentation. Historical audits and handoffs supplied leads and
decision context only. Source, tests, project files, compiled manifests, and
`TODO.md` override them.

This review changes no product source, test, roadmap item, changelog, migration
guide, API baseline, runtime scenario, package, or package consumer. Phase 1 is
limited to this artifact and its two documentation-index entries.

## Package baseline

The latest coordinated immutable baseline is
`NekoLib.Inspection.1.0.0-local.18.nupkg`, produced from
`518c078abc9bd9b340fbb7200470de47cde93452`. Local read-only inspection
reverified:

- package ID/version `NekoLib.Inspection` / `1.0.0-local.18`;
- SHA-256
  `990B950EED1E59F428F204EAE0BB65ADC1951F2D3D95240ECFF8A460025346F0`;
- `lib/net481/NekoLib.Inspection.dll` and
  `lib/net9.0/NekoLib.Inspection.dll`;
- aligned `NekoLib.Core` `1.0.0-local.18` dependencies for both targets;
- NuGet repository commit `518c078abc9bd9b340fbb7200470de47cde93452`.

The sole later commit is the Telemetry closing-documentation commit `7c4d449`.
A Git comparison found no change between the package commit and reviewed `HEAD`
under the Inspection project, frozen Core Inspection source, or Inspection API
manifests. The package remains truthful evidence for the pre-F1-INSP bits. It is
not evidence for any implementation proposed here.

The package description says "bounded operations, state snapshots and
constrained actions" in
[`NekoLib.Inspection.csproj:13-17`](../../src/Inspection/NekoLib.Inspection/NekoLib.Inspection.csproj),
and the root [`README.md:89`](../../README.md) repeats it. "Constrained" is not
an authorization or security property and does not match the passive-first
boundary. It should be corrected if INSP-01 is accepted.

## Scope

Included:

- all 3 compiled public types and all 27 public members on both targets;
- options, local/global construction, ownership, disposal, and provider-slot
  lifecycle;
- recording validation, lazy payloads, timestamps, ordering, retention,
  counters, and overflow assumptions;
- state/action identity, duplicate handling, handle ownership, ordering, and
  races;
- bounded snapshots, provider markers, shared budgets, outstanding work,
  `CaptureState`, shallow values, and partial evidence;
- post-disposal behavior, diagnostics consistency, and concurrent mutation;
- downstream contracts, target parity, package boundary, documentation
  ownership, compatibility, and validation gaps.

Excluded:

- implementing any disposition or modifying frozen Core;
- broad Inspection instrumentation or a Navigation action;
- changes to frozen Navigation lifecycle components;
- Instrumentation/TestControl, privileged hosts, plugin/reflection activation,
  IPC, debugger agents, permanent listeners, fault injection, or bypasses;
- F1-DIAG or any later F1 module;
- launching a runtime scenario or producing a package.

## Project, ownership, and lifecycle boundary

[`NekoLib.Inspection.csproj:3-29`](../../src/Inspection/NekoLib.Inspection/NekoLib.Inspection.csproj)
targets `net481;net9.0`, enables nullable annotations, disables implicit usings,
declares `NEKOLIB` plus `NETFRAMEWORK` / `NET_9`, and references only Core. No
Inspection source uses conditional compilation.

The ownership model is coherent:

- direct construction creates an immediately enabled local runtime owned by
  the composition root;
- `EnableGlobal` is the ordinary combined runtime/provider ownership path;
- direct `InspectionProvider.Install` is an advanced split-ownership path: its
  handle unregisters but does not dispose, so the caller owns both objects and
  should unregister before disposing the runtime;
- producers receive `IInspectionRecorder`; read-only consumers receive
  `IInspectionSnapshotSource`;
- the provider slot is process-wide for the loaded Core assembly context, not
  machine-wide or cross-process.

No facade, second provider, registry, listener, service, or project dependency
is required.

## Compiled public-surface inventory

`eng/verify-public-api.ps1 -PackageId NekoLib.Inspection` rebuilt and verified
both manifests with 0 warnings and 0 errors. After excluding the required
target-framework assembly attribute, the surfaces have no diff. Each target
exposes 3 public sealed types and 27 public members, with no protected or
target-specific member.

The manifests have no marker on the concrete action family. This differs from
the frozen Core interface, where `IInspectionRecorder.RegisterAction` already
carries `NEKOEXP0001`.

## Member-by-member proposed classification

No member is a legacy compatibility shim. No member is recommended for removal,
movement, or internalization. The four action members should be explicitly
experimental; all others are stable candidates, including legitimate concrete
owner conveniences beyond the Core interfaces.

| Type or member | Classification | Proposed disposition |
|---|---|---|
| `InspectionOptions` | Stable candidate | Retain sealed. |
| `InspectionOptions()` | Stable candidate | Retain; freeze the default capacity at `1024`. |
| `InspectionOptions.Capacity` | Stable candidate | Retain; mutable for composition, read once, minimum `1`. |
| `InspectionRuntime` | Stable candidate | Retain sealed as the supplied recorder/snapshot/disposal implementation. |
| `InspectionRuntime(InspectionOptions?)` | Stable candidate | Retain local construction; correct invalid-capacity `ParamName` (INSP-05). |
| `InspectionRuntime.IsEnabled` | Stable candidate | Retain as producer fast path and one-way lifecycle state. |
| `InspectionRuntime.EnableGlobal` | Stable candidate | Retain as ordinary combined provider ownership. |
| `InspectionRuntime.Record` | Stable candidate | Retain passive recording; correct identifier validation (INSP-02). |
| `InspectionRuntime.RegisterStateProvider` | Stable candidate | Retain passive pull state; correct identity/order (INSP-02/03). |
| `InspectionRuntime.RegisterAction` | Explicitly experimental | Retain public and add `NEKOEXP0001` on both targets (INSP-01). |
| `InspectionRuntime.CaptureSnapshot` | Stable candidate | Retain bounded read; correct repeated timeout work (INSP-04). |
| `InspectionRuntime.CaptureState` | Legitimate owner convenience; stable candidate | Retain as explicitly unbudgeted synchronous local-owner read. |
| `InspectionRuntime.GetOperations` | Legitimate owner diagnostic; stable candidate | Retain detached chronological copy. |
| `InspectionRuntime.ClearOperations` | Legitimate owner diagnostic; stable candidate | Retain lifetime-counter semantics; become inert after disposal (INSP-06). |
| `InspectionRuntime.GetDiagnostics` | Legitimate owner diagnostic; stable candidate | Retain and document best-effort cross-domain consistency. |
| `InspectionRuntime.TryInvokeAction` | Explicitly experimental | Retain with `NEKOEXP0001`; no stable authorization/async/timeout/UI contract. |
| `InspectionRuntime.StateKeys` | Legitimate owner diagnostic; stable candidate | Retain in registration order (INSP-03). |
| `InspectionRuntime.ActionKeys` | Explicitly experimental | Retain with `NEKOEXP0001`; current order becomes deterministic without stabilizing actions. |
| `InspectionRuntime.Dispose` | Stable candidate | Retain idempotent cleanup and global unregistration. |
| `InspectionRuntimeDiagnostics` | Legitimate owner diagnostic type; stable candidate | Retain sealed; constructor remains internal. |
| `InspectionRuntimeDiagnostics.IsEnabled` | Stable candidate | Retain lifecycle reading. |
| `InspectionRuntimeDiagnostics.Capacity` | Stable candidate | Retain read-once capacity. |
| `InspectionRuntimeDiagnostics.RetainedCount` | Stable candidate | Retain current retained count. |
| `InspectionRuntimeDiagnostics.TotalRecorded` | Stable candidate | Retain lifetime accepted-record count. |
| `InspectionRuntimeDiagnostics.EvictedCount` | Stable candidate | Retain lifetime capacity-eviction count. |
| `InspectionRuntimeDiagnostics.ClearCount` | Stable candidate | Retain enabled explicit-clear count, including empty clears. |
| `InspectionRuntimeDiagnostics.OldestSequence` | Stable candidate | Retain; null when empty. |
| `InspectionRuntimeDiagnostics.NewestSequence` | Stable candidate | Retain; null when empty. |
| `InspectionRuntimeDiagnostics.ProviderCount` | Stable candidate | Retain passive provider count. |
| `InspectionRuntimeDiagnostics.ActionCount` | Explicitly experimental | Retain with `NEKOEXP0001`; do not stabilize actions through diagnostics. |

Proposed result: **3 stable public sealed types, 23 stable members, and 4
explicitly experimental members** on both targets. There are no proposed
removals or internalizations.

## Observed behavior

### Options and construction

[`InspectionRuntime.cs:38-46`](../../src/Inspection/NekoLib.Inspection/InspectionRuntime.cs)
reads capacity once into readonly `_capacity`, defaults to `1024`, rejects below
`1`, and sizes the queue from it. Later options mutation cannot affect the
runtime. The exception currently reports `ParamName == "options"`, although
`Capacity` is invalid. Direct construction is enabled immediately through the
one-way disposed flag
([`InspectionRuntime.cs:82`](../../src/Inspection/NekoLib.Inspection/InspectionRuntime.cs));
that is the correct local opt-in contract.

### Record, retention, and time

[`InspectionRuntime.cs:84-120`](../../src/Inspection/NekoLib.Inspection/InspectionRuntime.cs)
shows:

- null module/operation throws; blank values are accepted today;
- valid post-disposal calls return without evaluating payload;
- payload executes before the operation lock, with a second enabled check under
  the lock;
- a payload exception becomes `<payload threw: TypeName>`; null remains null;
- sequence, totals, enqueue, and eviction are one locked order;
- timestamp is read at locked commit after payload work. `Sequence`, not the
  wall clock, is the concurrency ordering authority;
- a slow earlier payload may commit after a faster later call without holding a
  lock across application code;
- payloads are shallow. Inspection does not clone, serialize, redact, or
  truncate them.

Sequence, totals, evictions, clears, and registration IDs are `long` unchecked
counters. Wraparound is outside the supported operational envelope, not a
rollover protocol; saturation/reset behavior is not proposed.

### Registration identity, ordering, and races

State/action identity is
[`module + "::" + key`](../../src/Inspection/NekoLib.Inspection/InspectionRuntime.cs)
at line 374, while only null is rejected at lines 122-180. Therefore blank
components are ambiguous, and `("a::b", "c")` collides with
`("a", "b::c")`. A second registration is rejected, and an action lookup
through the other pair can reach the first registration.

Duplicates otherwise preserve the original owner. Handles are idempotent, and
registration IDs prevent a stale handle from removing a later owner
([`InspectionRuntime.cs:386-448`](../../src/Inspection/NekoLib.Inspection/InspectionRuntime.cs)).
State/action registries are separate. Comparison is currently case-sensitive
through default string equality; the accepted contract should make
`StringComparer.Ordinal` explicit.

Registration and disposal recheck lifecycle under the registry lock. A racing
registration is either admitted then cleared or returned as `Disposable.Empty`.
Delegates execute outside that lock. A copied provider/action may still execute
after unregistration or runtime disposal; no contract can cancel it, so this
must be documented instead of holding locks across application code.

Provider enumeration and `StateKeys`/`ActionKeys` use dictionary enumeration
([`InspectionRuntime.cs:202-208`](../../src/Inspection/NekoLib.Inspection/InspectionRuntime.cs),
[`InspectionRuntime.cs:346-356`](../../src/Inspection/NekoLib.Inspection/InspectionRuntime.cs)).
That order is not an accepted cross-target contract, yet it determines which
providers receive the shared budget first.

### Snapshot and provider budgets

[`InspectionRuntime.cs:182-241`](../../src/Inspection/NekoLib.Inspection/InspectionRuntime.cs)
implements these semantics:

- negative `maxOperations`/`timeout` throw with their parameter names; zero
  operations is valid;
- the newest bounded operation window is captured first in chronological
  sequence order;
- providers are copied afterwards and share one `Stopwatch` timeout budget;
- null, thrown, and over-budget results become `<null>`,
  `<snapshot threw: RootType>`, and `<snapshot timed out>`;
- providers reached after exhaustion are not invoked and get the timeout marker;
- `Task.Run` plus `Wait(remaining)` bounds caller completion only; it never
  cancels provider code;
- `CapturedUtc` is read after provider processing. Operations and state span an
  interval, not an atomic instant.

Frozen Core copies/wraps the outer snapshot collections but retains shallow
payload/state values
([`InspectionSnapshot.cs:17-24`](../../src/Core/NekoLib.Core/Inspection/InspectionSnapshot.cs),
[`InspectionSnapshot.cs:34-51`](../../src/Core/NekoLib.Core/Inspection/InspectionSnapshot.cs)).
No deep cloning is proposed.

`CaptureState` invokes all copied providers synchronously without a timeout
([`InspectionRuntime.cs:243-265`](../../src/Inspection/NekoLib.Inspection/InspectionRuntime.cs)).
It remains an owner convenience only; Diagnostics must use the budgeted
interface.

The budgeted path has a resource defect: after timeout, the task is dropped,
late exceptions are not observed, and a new capture can start another task for
the same stuck provider. Repeated captures can grow outstanding work without
bound.

### Clear, diagnostics, and post-disposal

`ClearOperations` clears retained operations and increments `ClearCount` even
when already empty, while preserving totals, evictions, and next sequence
([`InspectionRuntime.cs:273-279`](../../src/Inspection/NekoLib.Inspection/InspectionRuntime.cs)).
Oldest/newest become null; later records resume the lifetime sequence.

`GetDiagnostics` reads the operation domain under one lock, registry counts
under another, and lifecycle when creating the result
([`InspectionRuntime.cs:282-319`](../../src/Inspection/NekoLib.Inspection/InspectionRuntime.cs)).
Each group is coherent, but the result is best-effort across domains under
concurrency. That is acceptable and avoids one global application-code lock.

`Dispose` is idempotent, flips state before cleanup, unregisters an
`EnableGlobal` installation, and clears registries/operations
([`InspectionRuntime.cs:358-372`](../../src/Inspection/NekoLib.Inspection/InspectionRuntime.cs)).
After completed disposal and valid arguments:

- record is inert and registration returns `Disposable.Empty`;
- operations, state, and key lists are empty;
- snapshot is empty but retains capacity and lifetime total/eviction counters;
- diagnostics is disabled with zero retained/providers/actions and preserved
  lifetime counters;
- action lookup returns false and null.

Argument validation still runs before disabled fast paths and should remain so.
One mutation is inconsistent: `ClearOperations` still increments after disposal.
Reads/captures racing with disposal may return their pre-cleanup copy or the
empty post-cleanup state; already copied delegates are not cancelled.

### Global provider

`EnableGlobal` installs a new runtime, stores the Core installation handle, and
rolls both back on failure
([`InspectionRuntime.cs:48-80`](../../src/Inspection/NekoLib.Inspection/InspectionRuntime.cs)).
A second owner is refused without disturbing the first.

The internal `afterProviderInstall` hook has no production caller. It is
justified only as a deterministic seam for the publication/handle-storage race.
The accepted implementation should use it in a regression that disposes the
just-published runtime and verifies rollback; otherwise remove it. It is not a
general test-control surface and has no manifest impact.

Frozen Core remains authoritative: `Current` is non-null, `Install` admits at
most one enabled recorder, and its idempotent conditional handle unregisters but
does not dispose
([`InspectionProvider.cs:8-38`](../../src/Core/NekoLib.Core/Inspection/InspectionProvider.cs),
[`InspectionProvider.cs:41-55`](../../src/Core/NekoLib.Core/Inspection/InspectionProvider.cs)).

## Downstream evidence

| Consumer | Current use | Consequence |
|---|---|---|
| Navigation | Resolves/provides `IInspectionRecorder`, then uses only `RegisterStateProvider` and `Record` ([`PageNavBootstrap.cs:120-141`](../../src/Navigation/NekoLib.Navigation/Bootstrap/PageNavBootstrap.cs), [`InspectionNavigationObserver.cs:226-246`](../../src/Navigation/NekoLib.Navigation/Diagnostics/InspectionNavigationObserver.cs), [`InspectionNavigationObserver.cs:1509-1517`](../../src/Navigation/NekoLib.Navigation/Diagnostics/InspectionNavigationObserver.cs)). | Only feature-library producer; no action and no edit required. |
| Diagnostics | Accepts only `IInspectionSnapshotSource`, supplies operation/time bounds, then safely formats, redacts, and truncates ([`CrashHandler.cs:59-74`](../../src/Diagnostics/NekoLib.Diagnostics/CrashHandler.cs), [`CrashHandler.cs:394-410`](../../src/Diagnostics/NekoLib.Diagnostics/CrashHandler.cs), [`CrashHandler.cs:694-725`](../../src/Diagnostics/NekoLib.Diagnostics/CrashHandler.cs)). | Cannot discover/invoke actions; F1-DIAG remains untouched. |
| Other feature modules | Source-wide search found no recorder/provider/registration use outside Core, Inspection, Navigation, and Diagnostics' snapshot-only use. | B4/B5 remains frozen; application calls are not library instrumentation. |
| Observability scenario | Exercises retention, provider shapes/budgets, lifecycle, concurrency, and global ownership; explicitly registers no action ([`InspectionMatrix.cs:13-38`](../../runtime_tests/Observability/LongRunningRecovery/NekoLib.Observability.RuntimeTests.LongRunningRecovery/Workload/InspectionMatrix.cs), [`InspectionMatrix.cs:534-570`](../../runtime_tests/Observability/LongRunningRecovery/NekoLib.Observability.RuntimeTests.LongRunningRecovery/Workload/InspectionMatrix.cs)). | Strong prior runtime source/evidence, but not run here; its records are application-owned. |
| Package consumers | WinForms projects directly reference Inspection and compile/load `InspectionRuntime`; shared code compiles the Core snapshot-source call. WPF projects do not directly reference Inspection ([`WinFormsSmokeProgram.cs:10-60`](../../tests/NekoLib.PackageConsumers/WinFormsSmokeProgram.cs)). | Package reachability/graph evidence only, not behavior or action adoption. |

## Findings and proposed dispositions

### INSP-01 - Concrete actions are unmarked and package wording overstates them

**Priority:** high release-classification risk.

Core says action registration alone is experimental and that authorization,
discovery/invocation, async, cancellation, timeout, UI marshalling, and adoption
are unstable
([`NekoLib.Core/README.md:115-136`](../../src/Core/NekoLib.Core/README.md)).
No feature module registers an action, and the roadmap rejects rollout
([`TODO.md:175-188`](../../TODO.md)). Concrete `RegisterAction`,
`TryInvokeAction`, `ActionKeys`, and `ActionCount` currently appear stable.

**Disposition:** retain all four for coherent source/binary compatibility, but
apply the exact `NEKOEXP0001` marker to each on both targets. Keep them
in-process only. Document current synchronous behavior without promising it.
Replace "constrained actions" with passive-first wording that says experimental
actions are not authorization.

This creates intentional attribute manifest diffs and new warnings for concrete
callers, but no signature removal. Deliberate tests/scenarios must opt in
narrowly; passive consumers should not touch action diagnostics merely to assert
zero if they do not need the experiment.

### INSP-02 - Blank and delimiter-bearing identities are ambiguous

**Priority:** high correctness risk.

Distinct pairs can flatten to one ID, and action lookup can logically misroute.

**Disposition:** preserve null exceptions; reject blank/whitespace module,
operation, provider key, and action name; reject `::` in the module and in
provider/action components; preserve `module::key` for every valid ID; make
ordinal case-sensitive identity explicit. This is narrower than escaping and
requires invalid callers to rename; no repository caller uses such input.

### INSP-03 - Provider and key order is unspecified

**Priority:** medium partial-evidence risk.

Dictionary order decides provider admission under a shared budget.

**Disposition:** freeze provider invocation, `StateKeys`, and `ActionKeys` in
registration order. This lets owners put essential passive state first and uses
the existing registration IDs. Accidental dictionary order may change; no
supported consumer declares a dependency.

### INSP-04 - Timed-out providers can accumulate work and late unobserved faults

**Priority:** high unattended-resource risk.

**Disposition:** allow at most one outstanding budgeted invocation per provider
registration and explicitly observe any exception arriving after timeout.
Repeated captures share in-flight work; after completion, a later capture may
start fresh work. Timeout still bounds only the caller, never cancels code, and
still returns the same marker. `CaptureState` remains unbudgeted.

### INSP-05 - Invalid capacity reports the wrong parameter

**Priority:** low diagnostics/consistency risk.

**Disposition:** change `ParamName` from `options` to
`nameof(InspectionOptions.Capacity)`. Freeze default `1024`, minimum `1`,
read-once options, and immediate enabled local construction.

### INSP-06 - Clear mutates diagnostics after disposal

**Priority:** low lifecycle-consistency risk.

**Disposition:** make post-disposal clear inert, including under a clear/dispose
race. While enabled, every explicit clear still counts, including an empty one,
and lifetime totals/evictions/sequence remain preserved.

### INSP-07 - No current technical owner document exists

**Priority:** medium governance risk.

No `src/Inspection/NekoLib.Inspection/README.md` exists. Core owns interfaces
but nothing current owns the concrete ordering, time, budget, disposal,
diagnostic, privacy, experimental, and non-goal contracts.

**Disposition:** after acceptance, add and index that technical reference. It
should own composition/local-global lifecycle; passive retention/counters;
registration identity/order; shared budgets/partial evidence/outstanding work;
unbudgeted `CaptureState`; disposal/concurrency; `NEKOEXP0001`; shallow privacy
and Diagnostics-owned persistence safeguards; and explicit non-goals. The audit
must remain a snapshot, not the live reference.

### INSP-08 - Direct coverage leaves most reviewed edges unfrozen

**Priority:** medium regression risk.

The 13 tests cover basic eviction/sequences, payload/provider failures,
provider timeout, duplicates, handle removal, current action behavior, clear
counting, disposal cleanup, and global ownership. They do not cover options
defaults/minimum/read-once/parameter name; blank/delimiter identity; ordering;
disabled/null/shallow payloads; concurrent record/clear/dispose; snapshot
boundaries/shared skipping/null providers/captured time; late faults/repeated
stuck captures; unbounded/post-disposal `CaptureState`; full post-disposal
behavior; post-clear bounds; global post-install disposal; or concrete markers.

**Disposition:** add deterministic dual-target regressions for every accepted
correction and frozen behavior. Use gates/events for interleavings, not arbitrary
`Thread.Sleep`; do not block async tests with `.Wait()` or `.Result`.

## Stable behavior proposed for documentation

- direct construction is immediately enabled and caller-owned; options are read
  once;
- valid post-disposal writes/registrations are inert, while validation remains;
- payload executes outside the operation lock, exceptions are type-only, null
  remains null, and values are shallow;
- sequence is the concurrent order; timestamps are wall-clock annotations;
- retention is newest-bounded/chronological; clear preserves lifetime counters
  and counts enabled empty clears;
- duplicates never replace; handles are idempotent and conditional;
- copied application delegates can finish after unregistration/disposal;
- one shared snapshot completion budget returns partial evidence and never
  cancels provider code; operations are captured before provider state;
- `CapturedUtc` is snapshot-construction completion, not an atomic instant;
- `CaptureState` is synchronous, unbudgeted, and owner-only;
- diagnostics is coherent per lock domain and best-effort across domains;
- Core protects outer collections while payload/state objects stay shallow;
- Inspection does no persistence redaction/truncation; Diagnostics owns it;
- counter overflow is outside the operational contract.

## Compatibility and migration

| Disposition | Source impact | Binary / behavioral impact |
|---|---|---|
| Four `NEKOEXP0001` markers | Concrete calls gain `CS0618` and must opt in/migrate. | Attribute-only manifest diffs; no symbol/signature removal. |
| Identifier validation | Invalid callers rename IDs. | Pre-stable behavioral break; valid output unchanged. |
| Registration order | None. | Tight-budget/key ordering becomes deterministic. |
| Single-flight providers / late-fault observation | None. | Repeated captures stop starting overlapping work; no cancellation added. |
| Capacity `ParamName` | Only callers asserting old metadata. | `options` becomes `Capacity`. |
| Inert post-disposal clear | None. | `ClearCount` stops changing after disposal. |
| Documentation/tests | None. | No runtime/manifest effect alone. |

Accepted work requires `CHANGELOG.md` and
`docs/migrations/f1-inspection.md`. Manifests should change only by the four
attributes. Any member/signature/target diff is a stop signal.

## Rejected alternatives

- remove/internalize actions: Core still exposes experimental registration and
  a larger source/binary break has no demonstrated benefit;
- promote actions stable: in-process is neither authorization nor a complete
  execution contract;
- keep `ActionCount` stable: it stabilizes actions through diagnostics;
- treat `Obsolete` as security: it is compiler/release signaling only;
- create Instrumentation/TestControl/plugin/IPC/reflection/remote control:
  rejected by roadmap and scope;
- escape/encode `::`: changes public keys; rejecting ambiguity preserves valid
  output;
- use tuple identity but retain flat collisions: snapshots still cannot
  represent both pairs;
- deep clone/serialize/redact/truncate in Inspection: producers/persistence
  consumers own those boundaries;
- add provider cancellation to Core: Core is frozen and timeout is a completion
  budget;
- create a task per timed-out capture: permits unbounded growth;
- throw from every post-disposal member: breaks opt-in/NO-OP lifecycle;
- globally lock diagnostics/application code: best-effort domains suffice;
- add a facade/provider/registry or edit downstream modules to manufacture use:
  no ownership need exists.

## Core-contract assessment

**No conflict found.** Recorder/snapshot separation, action-free Diagnostics,
provider ownership, null default, conditional handles, completion-budget
timeouts, and shallow protected snapshots remain unchanged. Concrete actions
reuse the accepted experiment instead of changing Core. If implementation
requires a Core source/manifest change, F1-INSP must stop and report it.

## Package gate

Accepted attributes/runtime corrections change Inspection bits. `local.18`
would become prior evidence only. Codex must later package a clean committed
build through the canonical family flow. `1.0.0-local.19` is expected but must
be checked immediately before packing. Record exact ID/version/source/hash,
both assets, aligned Core, full tests/warnings, PackageReference consumers, and
all package/deployment/publish/clean probes. No package is created here.

## Proposed implementation block after acceptance

1. Promote only accepted decisions to `TODO.md`.
2. Add four markers and only their intentional manifest attributes.
3. Implement accepted identity/order/provider/capacity/clear corrections.
4. Use the internal post-install hook only for a deterministic rollback test.
5. Add focused dual-target regressions for changed/frozen behavior.
6. Add/index the Inspection README and correct package/root wording.
7. Update changelog/migration and append audit reconciliation.
8. Run focused tests, scoped API verification, full Release build with warning
   identities, full tests, docs verification, and diff hygiene.
9. Build Observability LongRunningRecovery on both targets if affected; do not
   launch it.
10. Commit cleanly and execute the Codex-owned immutable package gate before
    completion.

## Review validation

| Evidence class | Result | Claim and limit |
|---|---|---|
| Baseline | Branch, full `HEAD`, tracking, ahead/behind, and clean status matched the supplied baseline. | Exact reviewed scope. |
| Focused tests | Release `net481` 13/13 and `net9.0` 13/13 passed; no failures/skips. | Existing baseline only; see INSP-08. |
| Build/API | Scoped verifier built both targets with 0 warnings/errors and verified 2/2 baselines. | No baseline update. |
| Inventory | 3 types and 27 members per target; zero normalized surface differences. | Target-framework metadata intentionally differs. |
| Source review | Inspection/Core/downstream/scenario/package-consumer source inspected. | Code facts; no scratch probe counted as a test. |
| Prior package | Local.18 hash/assets/dependencies/repository metadata reverified; Git confirms unchanged Inspection inputs. | Prior immutable evidence only; no package produced/consumed. |
| Documentation | `eng/verify-docs.ps1` passed after the intended audit was staged so linked-path tracking could be verified. `git diff --check`, staged diff checking, and the complete `HEAD` diff check passed. No build log was supplied, so warning identities were not compared. | Documentation links, metadata, topology, tracked clean-clone availability, and diff hygiene only. |
| Runtime/full/package | No runtime scenario was built or launched; no full solution build/test or package flow ran. | No new runtime, full-solution, or package claim. |

## Residual validation gaps

- Proposed behavior is not implemented; current tests omit INSP-08 edges.
- Ordering and outstanding-work risks are source-confirmed, not newly stressed.
- The current timeout test releases its delegate and does not prove bounded
  repeated work or late-fault observation.
- No runtime scenario was built/launched; prior records remain historical.
- No full solution, warning comparison, package flow, or external campaign ran.
- WinForms consumers prove broad reachability but do not exercise a runtime;
  WPF consumers do not directly reference Inspection.
- No external consumer inventory proves absence of use.

## Decision gate

Nothing here authorizes product, test, roadmap, changelog, migration, API
baseline, scenario, or package changes. F1-INSP stops until the user explicitly
accepts, modifies, or rejects:

- 23 stable and 4 `NEKOEXP0001` members, with no removals;
- validated non-ambiguous IDs preserving `module::key`;
- registration-order provider/key enumeration;
- one outstanding budgeted provider invocation plus late-fault observation,
  without cancellation;
- capacity parameter-name and post-disposal-clear corrections;
- the current Inspection technical reference and expanded regressions.
