# NekoLib.Inspection Validation Requirements

**Document ID:** INSP-VALIDATION-REQUIREMENTS

**Schema version:** 1

**Kind:** reference

**Lifecycle:** current

**Subject:** evidence contract for qualifying the NekoLib.Inspection boundary

**Surface:** validation-requirements

**Boundary:** inspection

**Authority role:** normative

**Mutation:** authored

**Indexing:** include

The [module manifest](MANIFEST.md) owns the inherited profile list. The
requirements below specialize those profiles for an opt-in runtime with a
process-wide ownership slot, passive bounded recording, application delegates
invoked under a shared completion budget that never cancels, one-way disposal,
and a marked experimental sub-surface inside an otherwise stable package.

Evidence produced by the shared Observability scenario qualifies an Inspection
requirement only through that scenario's Inspection checks. Its Logging and
Telemetry checks are not Inspection evidence.

## INSP-VALREQ-001

**Classification:** REQUIRED

**Trigger:** every release candidate and every change to source, project, target, or package settings

**Category:** build

**Boundary:** in-process

**Targets:** `net481` and `net9.0`

**Acceptance criteria:** Both target assemblies build with zero errors and no new normalized warning identity.

**Required evidence level:** build-only

**Rationale:** The project carries no conditional compilation, so a target-specific break would come from the SDK or Core rather than from a visible branch in Inspection source.

## INSP-VALREQ-002

**Classification:** REQUIRED

**Trigger:** every release candidate and every public declaration, attribute, target, nullable, default-value, or friend-assembly change

**Category:** api-compatibility

**Boundary:** in-process

**Targets:** both accepted `NekoLib.Inspection` manifests

**Acceptance criteria:** Release assemblies match both accepted manifests exactly, including the four experimental attributes, the `InternalsVisibleTo` friend declaration, and the per-target framework attribute that this package emits and its sibling capability packages do not.

**Required evidence level:** build-only

**Rationale:** This is the one capability package whose accepted manifests carry attribute-level content beyond signatures, so a comparison is the only thing that protects it.

## INSP-VALREQ-003

**Classification:** REQUIRED

**Trigger:** every change to recording, identity validation, payload handling, or retention

**Category:** focused-regression

**Boundary:** in-process

**Targets:** focused suite on `net481` and `net9.0`

**Acceptance criteria:** The suite proves that null and blank identities throw with their own parameter names, that a module containing the reserved delimiter is rejected while an operation name is not treated as an identity component, that a valid call after disposal is inert and does not evaluate the payload delegate, that a null payload result stays null and a throwing one becomes a type-only marker, that a slow payload does not hold the queue lock, that commit order follows payload completion rather than call order, and that capacity eviction removes the oldest record while preserving lifetime counters.

**Required evidence level:** automated-runtime

**Rationale:** Recording is the only path every producer touches, and each of these failures is silent: a wrongly evaluated payload, a dropped record, or a mis-ordered sequence produces evidence that looks valid and is not.

## INSP-VALREQ-004

**Classification:** REQUIRED

**Trigger:** every change to provider registration, identity composition, ordering, or unregistration

**Category:** focused-regression

**Boundary:** in-process

**Targets:** both focused test targets

**Acceptance criteria:** The suite proves ordinal case-sensitive `module::key` identity, that a duplicate registration is rejected without replacing the current owner, that the returned handle is idempotent and conditionally removes only its own registration so a stale handle cannot remove a later owner reusing the identity, and that provider invocation and key enumeration follow registration order.

**Required evidence level:** automated-runtime

**Rationale:** Registration order is a documented composition tool for putting essential evidence ahead of optional evidence under a shared budget, and the stale-handle case silently deletes another component's provider.

## INSP-VALREQ-005

**Classification:** REQUIRED

**Trigger:** every change to snapshot capture, budgeting, single-flight invocation, or outcome markers

**Category:** focused-regression

**Boundary:** in-process

**Targets:** both focused test targets

**Acceptance criteria:** The suite proves that negative limits or timeouts throw with their parameter names while zero operations and a zero timeout are valid, that a null provider result, a throwing provider, and a provider exceeding or skipped after the shared budget each yield their documented marker, that the shared budget skips later providers once exhausted, that repeated or overlapping captures share one outstanding invocation per registration, that a failure arriving after a caller timed out is still observed rather than lost, that a later capture can start fresh work once the task completes, and that operations are copied before providers run so the capture is not one atomic instant.

**Required evidence level:** automated-runtime

**Rationale:** This is the surface `NekoLib.Diagnostics` calls on the crash path, where a provider is most likely to be slow or broken and where an unbounded wait or a lost failure would cost the whole bundle.

## INSP-VALREQ-006

**Classification:** REQUIRED

**Trigger:** every change to global activation, the Core provider slot, or ownership rollback

**Category:** focused-regression

**Boundary:** process

**Targets:** both focused test targets

**Acceptance criteria:** The suite proves that only one enabled global owner is admitted, that a second installation throws without replacing the current owner, that disposing the owner restores the Core null recorder so the slot stays non-null, and that a runtime disposed while activation is completing rolls the installation back rather than leaving a disabled recorder installed.

**Required evidence level:** automated-runtime

**Rationale:** The slot is process-wide state shared by every module that opts in; a stale or disabled recorder left installed would silently disable inspection for the whole process with no error.

## INSP-VALREQ-007

**Classification:** REQUIRED

**Trigger:** every change to disposal or post-disposal behavior

**Category:** focused-regression

**Boundary:** in-process

**Targets:** both focused test targets

**Acceptance criteria:** The suite proves that disposal is idempotent and one-way, that it disables first and then removes a global installation, clears registrations, and clears retained operations, and that afterwards valid writes and registrations are inert, operation and key lists and budgeted snapshots are empty, diagnostics report disabled state with zero live counts, and experimental action lookup returns false.

**Required evidence level:** automated-runtime

**Rationale:** Disposal is the only teardown path and it releases process-wide state; a partially disposed runtime would accept work that nothing can ever read.

## INSP-VALREQ-008

**Classification:** REQUIRED

**Trigger:** every change to diagnostic counters or clearing

**Category:** focused-regression

**Boundary:** in-process

**Targets:** both focused test targets

**Acceptance criteria:** The suite proves that the operation counters are internally coherent and the registry counts are internally coherent, that an enabled clear of an already empty queue still increments the clear count, that clearing preserves lifetime recorded and eviction counts and the next sequence, and that clearing after disposal is inert.

**Required evidence level:** automated-runtime

**Rationale:** These counters are how an owner distinguishes "nothing happened" from "evidence was dropped or cleared", which is the difference between an empty snapshot being reassuring and being a warning.

## INSP-VALREQ-009

**Classification:** REQUIRED

**Trigger:** every change to the experimental members, their marker text, or the read-only consumer boundary

**Category:** api-compatibility

**Boundary:** in-process

**Targets:** both accepted manifests and both focused test targets

**Acceptance criteria:** Evidence proves that exactly the four documented members carry the exact marker text on both targets, that the read-only snapshot surface exposes no action registration or invocation, and that documentation states the marker is release signaling rather than authentication, authorization, or a remote-execution boundary.

**Required evidence level:** build-only

**Rationale:** A stable package containing a pre-stable sub-surface is only honest while the marker is exact and the read-only consumer genuinely cannot reach it; treating the compiler warning as an access control would be a false assurance.

## INSP-VALREQ-010

**Classification:** REQUIRED

**Trigger:** every change to recording, retention, provider invocation, or global lifecycle

**Category:** soak

**Boundary:** in-process

**Targets:** a sustained scenario run on each supported target

**Acceptance criteria:** A sustained run holds bounded retention exactly at capacity under continuous recording, isolates a throwing provider into its marker while healthy providers stay in the same snapshot, marks a slow provider as timed out with the capture still returning inside its budget, restores the process-wide slot to the null recorder on teardown and admits a fresh installation afterwards, and ends with provider and registration counts back to baseline and with threads and handles inside a bounded allowance. Duration, workload, concurrency, operations, faults, expected and actual recovery, resource measurements, acceptance criteria, and cleanup are recorded, and no unmeasured threshold is invented.

**Required evidence level:** automated-runtime

**Rationale:** The runtime holds application objects by shallow reference in a bounded queue, starts pool work for providers, and mutates process-wide state, so retention, thread, and slot behavior over time cannot be inferred from short tests.

## INSP-VALREQ-011

**Classification:** REQUIRED

**Trigger:** every change to payload capture, provider results, snapshot exposure, or the action boundary

**Category:** security

**Boundary:** in-process

**Targets:** both focused test targets and the current technical reference

**Acceptance criteria:** Evidence and documentation state that payload and provider values are retained as shallow references, that the runtime performs no deep clone, serialization, redaction, truncation, persistence, or transport, that a consumer persisting or transmitting a snapshot owns access control and retention, and that in-process action invocation provides no authentication or authorization.

**Required evidence level:** build-only

**Rationale:** Inspection exists to expose live application state, so the only thing standing between it and a leak is an explicit, correct statement of who owns the boundary.

## INSP-VALREQ-012

**Classification:** REQUIRED

**Trigger:** every publishable release candidate and every package, dependency, target, or XML-delivery change

**Category:** package

**Boundary:** package-feed

**Targets:** the `NekoLib.Inspection` package and both target assets

**Acceptance criteria:** The immutable package contains both target assemblies, a package-owned matching XML file per assembly, correct target groups, the `NekoLib.Core` dependency at the aligned version, provenance metadata, and a recorded hash, with no unexpected repository assets.

**Required evidence level:** build-only

**Rationale:** Project output proves neither package contents nor dependency groups; a managed package once shipped without its XML files and only direct inspection caught it.

## INSP-VALREQ-013

**Classification:** REQUIRED

**Trigger:** every publishable release candidate and every package, dependency, target, or experimental-marker change

**Category:** package-consumer

**Boundary:** package-feed

**Targets:** isolated package-reference-only consumers on both supported targets

**Acceptance criteria:** Clean isolated consumers restore and build without repository project references, extract the expected assembly and XML pairs from the package cache, and observe the experimental diagnostic when they reference a marked member.

**Required evidence level:** build-only

**Rationale:** The experimental marker only protects consumers if it survives packaging and reaches their build; a project-reference build cannot prove that.

## INSP-VALREQ-014

**Classification:** REQUIRED

**Trigger:** every publishable release candidate and every public XML comment or documentation-generation change

**Category:** build

**Boundary:** in-process

**Targets:** both target assemblies

**Acceptance criteria:** Documentation-enabled Release builds generate matching XML assets with zero missing-member, malformed, unresolved, or ambiguous XML-comment diagnostics. Because `CS1591` is suppressed on `net481` by the project's `NoWarn`, a qualifying run either measures the `net9.0` target where it is live or unsuppresses it explicitly.

**Required evidence level:** build-only

**Rationale:** The accepted manifests prove compiled shape but say nothing about IDE guidance, and an ordinary `net481` build cannot detect a missing member at all.

## INSP-VALREQ-015

**Classification:** CONDITIONAL

**Trigger:** an accepted, explicitly recorded unfreeze of module instrumentation or of the concrete action channel

**Category:** focused-regression

**Boundary:** in-process

**Targets:** the one bounded module admitted by that unfreeze, on both of its supported targets

**Acceptance criteria:** The unfrozen scope names the operational question its data answers, validates the smallest real producer, proves that disabled and NO-OP behavior is preserved on both targets, keeps the read-only consumer unable to invoke actions, and records the restoration of the broad freeze after the scope closes.

**Required evidence level:** automated-runtime

**Rationale:** This requirement exists to hold the shape of a future unfreeze, not to enable one. The freeze, its guardrails, and its unfreeze conditions are owned by [`ROADMAP.md`](../../../ROADMAP.md); nothing here authorizes instrumentation, an action producer, or a consumer.

## INSP-VALREQ-016

**Classification:** RECOMMENDED

**Trigger:** a change to single-flight provider invocation, or an application that registers a provider capable of blocking indefinitely

**Category:** focused-regression

**Boundary:** in-process

**Targets:** both focused test targets

**Acceptance criteria:** A bounded characterization of a permanently blocked provider: that its slot reports the timeout marker on every later capture, that no fresh invocation is attempted while the task is outstanding, that healthy providers are unaffected, and that the retained thread cost is recorded rather than assumed.

**Required evidence level:** automated-runtime

**Rationale:** The existing timeout evidence uses a provider that eventually returns. A provider that never returns converts a transient timeout into a permanent evidence hole and a leaked pool thread that diagnostics do not surface. See [`INSP-FINDING-003`](FINDINGS.md).

## INSP-VALREQ-017

**Classification:** REQUIRED

**Trigger:** every coordinated family release candidate

**Category:** full-regression

**Boundary:** in-process

**Targets:** `NekoLib.sln`

**Acceptance criteria:** The canonical full build, test, and package campaign passes and preserves the repository warning baseline, dependency graph, public APIs, packages, consumers, and unrelated module regressions.

**Required evidence level:** automated-runtime

**Rationale:** Navigation records Inspection operations and installs the process-wide slot in its own tests, and Diagnostics consumes the read-only snapshot source, so an Inspection change can fail outside its own suite.
